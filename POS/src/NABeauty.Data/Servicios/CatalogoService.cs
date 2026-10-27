using System.Text.RegularExpressions;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;

namespace NABeauty.Data.Servicios;

/// <summary>
/// Categorías, productos, variantes y fotos. Aquí viven TODAS las reglas del catálogo;
/// la pantalla solo junta los datos y muestra el mensaje si algo no cumple.
/// Cada cambio deja una fila en sync_cola para que la tienda en línea se entere.
/// </summary>
public sealed partial class CatalogoService(Contexto ctx, IAlmacenFotos? fotos = null)
{
    public const int MaxFotos = 8;

    // ═════════════════════ Categorías ═════════════════════
    public List<Categoria> Categorias(bool soloActivas = false)
    {
        ctx.Exigir(Permiso.VerProductos);
        using var u = ctx.Bd.Abrir();
        return new CategoriaRepo(u).Listar(soloActivas);
    }

    /// <summary>Crea (Uuid vacío) o actualiza una categoría. Devuelve su uuid.</summary>
    public string GuardarCategoria(Categoria c)
    {
        ctx.Exigir(Permiso.GestionarProductos);
        var nombre = Validar.Texto(c.Nombre, 60) ?? throw new ReglaNegocioException("Escribe el nombre de la categoría.");
        if (nombre.Length < 2) throw new ReglaNegocioException("El nombre de la categoría es muy corto.");
        var color = (c.Color ?? "").Trim().ToUpperInvariant();
        if (!ColorHex().IsMatch(color)) throw new ReglaNegocioException("El color debe tener el formato #RRGGBB.");

        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new CategoriaRepo(u);
        var nueva = string.IsNullOrEmpty(c.Uuid);
        if (repo.NombreExiste(nombre, nueva ? null : c.Uuid)) throw new ReglaNegocioException($"Ya existe la categoría «{nombre}».");

        var guardar = new Categoria
        {
            Uuid = nueva ? Guid.NewGuid().ToString() : c.Uuid,
            Nombre = nombre,
            Color = color,
            Activa = c.Activa,
        };
        if (nueva)
        {
            guardar.Slug = SlugLibre(Slug.Crear(nombre, 70), s => repo.SlugExiste(s, null));
            guardar.Orden = repo.SiguienteOrden();
            repo.Crear(guardar);
        }
        else
        {
            var actual = repo.Obtener(c.Uuid) ?? throw new ReglaNegocioException("La categoría ya no existe.");
            guardar.Slug = actual.Slug;          // el slug no cambia: es la URL en la tienda en línea
            guardar.Orden = c.Orden > 0 ? c.Orden : actual.Orden;
            repo.Actualizar(guardar);
        }
        new SyncRepo(u).Encolar("categoria", guardar.Uuid, nueva ? "crear" : "actualizar");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, nueva ? "categoria.crear" : "categoria.editar", "categoria", null,
            new { guardar.Uuid, guardar.Nombre, guardar.Activa });
        u.Confirmar();
        return guardar.Uuid;
    }

    /// <summary>Solo se elimina si nunca tuvo productos; si los tuvo, se oculta.</summary>
    public void EliminarCategoria(string uuid)
    {
        ctx.Exigir(Permiso.GestionarProductos);
        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new CategoriaRepo(u);
        var c = repo.Obtener(uuid) ?? throw new ReglaNegocioException("La categoría ya no existe.");
        if (repo.ProductosTotales(uuid) > 0)
            throw new ReglaNegocioException($"«{c.Nombre}» tiene productos. Muévelos a otra categoría u oculta la categoría.");
        repo.Eliminar(uuid);
        new SyncRepo(u).Encolar("categoria", uuid, "anular");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "categoria.eliminar", "categoria", null, new { c.Uuid, c.Nombre });
        u.Confirmar();
    }

    /// <summary>Sube o baja una categoría en el orden (intercambia con la vecina).</summary>
    public void MoverCategoria(string uuid, int direccion)
    {
        ctx.Exigir(Permiso.GestionarProductos);
        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new CategoriaRepo(u);
        var lista = repo.Listar();
        var i = lista.FindIndex(c => c.Uuid == uuid);
        var j = i + Math.Sign(direccion);
        if (i < 0 || j < 0 || j >= lista.Count) return;
        (lista[i], lista[j]) = (lista[j], lista[i]);
        for (var k = 0; k < lista.Count; k++)
        {
            if (lista[k].Orden == k + 1) continue;
            lista[k].Orden = k + 1;
            repo.Actualizar(lista[k]);
            new SyncRepo(u).Encolar("categoria", lista[k].Uuid, "actualizar");
        }
        u.Confirmar();
    }

    // ═════════════════════ Productos ═════════════════════
    public List<ProductoFila> Productos(FiltroProductos filtro)
    {
        ctx.Exigir(Permiso.VerProductos);
        using var u = ctx.Bd.Abrir();
        return new ProductoRepo(u).Listar(filtro);
    }

    /// <summary>Producto completo. Sin permiso de ver costos, el costo llega vacío.</summary>
    public Producto Producto(string uuid)
    {
        ctx.Exigir(Permiso.VerProductos);
        using var u = ctx.Bd.Abrir();
        var p = new ProductoRepo(u).Obtener(uuid) ?? throw new ReglaNegocioException("El producto ya no existe.");
        if (!ctx.Usuario.Puede(Permiso.VerCostos)) p.Variantes.ForEach(v => v.Costo = null);
        return p;
    }

    /// <summary>
    /// Crea (Uuid vacío) o actualiza un producto con sus variantes, en una sola transacción.
    /// Variantes sin Uuid = nuevas (con stock inicial opcional). Variantes de la base que ya no
    /// vienen en la lista se quitan, salvo que tengan historial (entonces hay que desactivarlas).
    /// </summary>
    public string GuardarProducto(Producto p)
    {
        ctx.Exigir(Permiso.GestionarProductos);
        var verCostos = ctx.Usuario.Puede(Permiso.VerCostos);

        var nombre = Validar.Texto(p.Nombre, 120) ?? throw new ReglaNegocioException("Escribe el nombre del producto.");
        if (nombre.Length < 2) throw new ReglaNegocioException("El nombre del producto es muy corto.");
        var marca = Validar.Texto(p.Marca, 80) ?? throw new ReglaNegocioException("Escribe la marca (la tienda en línea la muestra en cada producto).");
        if (p.Ciclo is not ("permanente" or "temporada")) throw new ReglaNegocioException("Elige si el producto es permanente o de temporada.");
        if (string.IsNullOrEmpty(p.CategoriaUuid)) throw new ReglaNegocioException("Elige la categoría.");
        var descripcion = Validar.Texto(p.Descripcion, 2000);
        var variantes = ValidarVariantes(p.Variantes, verCostos);
        if (variantes.Any(v => string.IsNullOrEmpty(v.Uuid) && v.StockInicial > 0))
            ctx.Exigir(Permiso.AjustarInventario);

        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new ProductoRepo(u);
        if (new CategoriaRepo(u).Obtener(p.CategoriaUuid) is null) throw new ReglaNegocioException("La categoría elegida ya no existe.");

        var nuevo = string.IsNullOrEmpty(p.Uuid);
        Producto? actual = null;
        var guardar = new Producto
        {
            Uuid = nuevo ? Guid.NewGuid().ToString() : p.Uuid,
            CategoriaUuid = p.CategoriaUuid,
            Nombre = nombre,
            Marca = marca,
            Descripcion = descripcion,
            Ciclo = p.Ciclo,
            Destacado = p.Destacado,
        };
        if (nuevo)
        {
            guardar.Slug = SlugLibre(Slug.Crear($"{nombre} {marca}"), s => repo.SlugExiste(s, null));
            repo.Crear(guardar);
        }
        else
        {
            actual = repo.Obtener(p.Uuid) ?? throw new ReglaNegocioException("El producto ya no existe.");
            guardar.Slug = actual.Slug;          // la URL en la tienda en línea no cambia
            repo.Actualizar(guardar);
        }

        // Unicidad de SKU y código de barras contra el resto de la base
        foreach (var v in variantes)
        {
            if (v.Sku is not null && repo.VarianteConCodigo("sku", v.Sku, NuloSiVacio(v.Uuid)) is { } otra1)
                throw new ReglaNegocioException($"El SKU {v.Sku} ya lo usa «{repo.NombreDeVariante(otra1)}».");
            if (v.CodigoBarras is not null && repo.VarianteConCodigo("codigo_barras", v.CodigoBarras, NuloSiVacio(v.Uuid)) is { } otra2)
                throw new ReglaNegocioException($"El código de barras {v.CodigoBarras} ya lo usa «{repo.NombreDeVariante(otra2)}».");
        }

        // Variantes quitadas
        var inventario = new InventarioRepo(u);
        foreach (var vieja in actual?.Variantes ?? [])
        {
            if (variantes.Any(v => v.Uuid == vieja.Uuid)) continue;
            if (vieja.ConHistorial)
                throw new ReglaNegocioException($"«{vieja.Nombre}» ya tiene movimientos de inventario: desactívala en lugar de quitarla.");
            repo.EliminarVariante(vieja.Uuid);
        }

        var orden = 0;
        foreach (var v in variantes)
        {
            v.ProductoUuid = guardar.Uuid;
            v.Orden = orden++;
            if (string.IsNullOrEmpty(v.Uuid))
            {
                v.Uuid = Guid.NewGuid().ToString();
                repo.CrearVariante(v);
                if (v.StockInicial > 0)
                    inventario.Movimiento(v.Uuid, "ajuste", v.StockInicial, ctx.UsuarioId, nota: "Stock inicial");
            }
            else
            {
                var antes = actual?.Variantes.FirstOrDefault(x => x.Uuid == v.Uuid)
                            ?? throw new ReglaNegocioException("Una de las opciones ya no pertenece a este producto. Vuelve a abrirlo.");
                if (!verCostos) v.Costo = antes.Costo;   // sin permiso no se ve ni se pisa el costo
                repo.ActualizarVariante(v);
            }
        }

        new SyncRepo(u).Encolar("producto", guardar.Uuid, nuevo ? "crear" : "actualizar");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, nuevo ? "producto.crear" : "producto.editar", "producto", null,
            new { guardar.Uuid, guardar.Nombre, Variantes = variantes.Count });
        u.Confirmar();
        return guardar.Uuid;
    }

    /// <summary>Archivar = sale de la caja y de la tienda en línea, pero las ventas viejas lo siguen nombrando.</summary>
    public void CambiarEstadoProducto(string uuid, bool archivar)
    {
        ctx.Exigir(Permiso.GestionarProductos);
        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new ProductoRepo(u);
        var p = repo.Obtener(uuid) ?? throw new ReglaNegocioException("El producto ya no existe.");
        repo.CambiarEstado(uuid, archivar ? "archivado" : "activo");
        new SyncRepo(u).Encolar("producto", uuid, "actualizar");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, archivar ? "producto.archivar" : "producto.reactivar", "producto", null, new { p.Uuid, p.Nombre });
        u.Confirmar();
    }

    // ═════════════════════ Fotos ═════════════════════
    /// <summary>Procesa la imagen (reduce y crea miniatura) y la agrega al final. La primera queda como principal.</summary>
    public FotoProducto AgregarFoto(string productoUuid, string rutaOrigen)
    {
        ctx.Exigir(Permiso.GestionarProductos);
        var almacen = fotos ?? throw new InvalidOperationException("No hay almacén de fotos configurado.");
        if (!File.Exists(rutaOrigen)) throw new ReglaNegocioException("No se encontró el archivo de la foto.");
        var ext = Path.GetExtension(rutaOrigen).ToLowerInvariant();
        if (!IAlmacenFotos.Extensiones.Contains(ext))
            throw new ReglaNegocioException("Formato no admitido. Usa fotos JPG o PNG.");
        if (new FileInfo(rutaOrigen).Length > IAlmacenFotos.MaxBytes)
            throw new ReglaNegocioException("La foto pesa más de 20 MB.");

        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new ProductoRepo(u);
        if (repo.Obtener(productoUuid) is null) throw new ReglaNegocioException("El producto ya no existe.");
        var actuales = repo.Fotos(productoUuid);
        if (actuales.Count >= MaxFotos) throw new ReglaNegocioException($"Cada producto admite hasta {MaxFotos} fotos.");

        var archivo = almacen.Guardar(rutaOrigen);
        try
        {
            var f = new FotoProducto
            {
                Uuid = Guid.NewGuid().ToString(),
                ProductoUuid = productoUuid,
                Archivo = archivo,
                Orden = actuales.Count == 0 ? 0 : actuales.Max(x => x.Orden) + 1,
            };
            repo.AgregarFoto(f);
            new SyncRepo(u).Encolar("producto", productoUuid, "actualizar");
            u.Confirmar();
            return f;
        }
        catch
        {
            almacen.Eliminar(archivo);   // sin fila en la base, el archivo sobra
            throw;
        }
    }

    public void EliminarFoto(string fotoUuid)
    {
        ctx.Exigir(Permiso.GestionarProductos);
        string archivo;
        using (var u = ctx.Bd.Abrir(transaccional: true))
        {
            var repo = new ProductoRepo(u);
            var foto = BuscarFoto(u, fotoUuid);
            archivo = foto.Archivo;
            repo.EliminarFoto(fotoUuid);
            Renumerar(repo, foto.ProductoUuid, null);
            new SyncRepo(u).Encolar("producto", foto.ProductoUuid, "actualizar");
            u.Confirmar();
        }
        fotos?.Eliminar(archivo);   // el archivo se borra solo si la base ya confirmó
    }

    /// <summary>La foto elegida pasa a ser la principal (orden 0); las demás conservan su orden.</summary>
    public void HacerPrincipal(string fotoUuid)
    {
        ctx.Exigir(Permiso.GestionarProductos);
        using var u = ctx.Bd.Abrir(transaccional: true);
        var foto = BuscarFoto(u, fotoUuid);
        Renumerar(new ProductoRepo(u), foto.ProductoUuid, fotoUuid);
        new SyncRepo(u).Encolar("producto", foto.ProductoUuid, "actualizar");
        u.Confirmar();
    }

    // ═════════════════════ Auxiliares ═════════════════════
    private static FotoProducto BuscarFoto(UnidadTrabajo u, string fotoUuid) =>
        u.Uno<FotoProducto>("SELECT uuid, producto_uuid, archivo, orden FROM producto_imagenes WHERE uuid = @fotoUuid", new { fotoUuid })
        ?? throw new ReglaNegocioException("La foto ya no existe.");

    private static void Renumerar(ProductoRepo repo, string productoUuid, string? primera)
    {
        var lista = repo.Fotos(productoUuid);
        if (primera is not null) lista = [.. lista.Where(f => f.Uuid == primera), .. lista.Where(f => f.Uuid != primera)];
        for (var i = 0; i < lista.Count; i++)
            if (lista[i].Orden != i) repo.OrdenFoto(lista[i].Uuid, i);
    }

    /// <summary>Normaliza y valida las variantes. Devuelve la lista lista para guardar.</summary>
    private static List<Variante> ValidarVariantes(List<Variante> entrada, bool verCostos)
    {
        if (entrada.Count == 0) throw new ReglaNegocioException("El producto necesita al menos una opción con precio.");
        var tipo = entrada[0].Tipo;
        if (!TiposVariante.Valido(tipo) || entrada.Any(v => v.Tipo != tipo))
            throw new ReglaNegocioException("Todas las opciones del producto deben ser del mismo tipo.");
        if (tipo == "unica" && entrada.Count > 1)
            throw new ReglaNegocioException("Un producto sin opciones lleva un solo precio. Elige tonos, tallas, colores o aromas para agregar más.");

        var lista = new List<Variante>();
        foreach (var v in entrada)
        {
            var etiqueta = TiposVariante.Etiqueta(tipo).ToLowerInvariant();
            var nombre = tipo == "unica" ? "Única" : Validar.Texto(v.Nombre, 60);
            if (nombre is null) throw new ReglaNegocioException($"Falta el nombre de un {etiqueta}.");
            var cual = tipo == "unica" ? "" : $" ({nombre})";
            if (v.Precio <= 0) throw new ReglaNegocioException($"El precio{cual} debe ser mayor a cero.");
            if (v.PrecioOferta is { } o && (o <= 0 || o >= v.Precio))
                throw new ReglaNegocioException($"El precio de oferta{cual} debe ser menor que el precio normal.");
            if (verCostos && v.Costo is < 0) throw new ReglaNegocioException($"El costo{cual} no puede ser negativo.");
            if (v.StockInicial < 0) throw new ReglaNegocioException($"El stock inicial{cual} no puede ser negativo.");
            var color = string.IsNullOrWhiteSpace(v.ColorHex) ? null : v.ColorHex.Trim().ToUpperInvariant();
            if (color is not null && !ColorHex().IsMatch(color)) throw new ReglaNegocioException($"El color{cual} debe tener el formato #RRGGBB.");

            lista.Add(new Variante
            {
                Uuid = v.Uuid,
                Tipo = tipo,
                Nombre = nombre,
                Sku = Validar.Texto(v.Sku, 40)?.ToUpperInvariant(),
                CodigoBarras = Validar.Texto(v.CodigoBarras, 40),
                ColorHex = tipo is "tono" or "color" ? color : null,
                Precio = v.Precio,
                PrecioOferta = v.PrecioOferta,
                Costo = v.Costo,
                Activa = v.Activa,
                ConHistorial = v.ConHistorial,
                StockInicial = string.IsNullOrEmpty(v.Uuid) ? v.StockInicial : 0,
            });
        }

        if (!lista.Any(v => v.Activa)) throw new ReglaNegocioException("Deja al menos una opción activa.");
        var repetido = lista.GroupBy(v => v.Nombre, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null) throw new ReglaNegocioException($"La opción «{repetido.Key}» está repetida.");
        var skuRep = lista.Where(v => v.Sku is not null).GroupBy(v => v.Sku).FirstOrDefault(g => g.Count() > 1);
        if (skuRep is not null) throw new ReglaNegocioException($"El SKU {skuRep.Key} está repetido en este producto.");
        var cbRep = lista.Where(v => v.CodigoBarras is not null).GroupBy(v => v.CodigoBarras).FirstOrDefault(g => g.Count() > 1);
        if (cbRep is not null) throw new ReglaNegocioException($"El código de barras {cbRep.Key} está repetido en este producto.");
        return lista;
    }

    /// <summary>"labial-rojo" ya existe → "labial-rojo-2", "labial-rojo-3"…</summary>
    private static string SlugLibre(string base_, Func<string, bool> existe)
    {
        var slug = base_;
        for (var i = 2; existe(slug); i++) slug = $"{base_}-{i}";
        return slug;
    }

    private static string? NuloSiVacio(string? s) => string.IsNullOrEmpty(s) ? null : s;

    [GeneratedRegex("^#[0-9A-F]{6}$")] private static partial Regex ColorHex();
}
