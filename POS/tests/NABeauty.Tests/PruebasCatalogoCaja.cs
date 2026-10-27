using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.Tests;

/// <summary>
/// Almacén de fotos falso: copia el archivo a una carpeta temporal (la app real además reduce
/// y crea la miniatura). Rechaza lo que no empieza como PNG o JPEG, igual que el real rechaza
/// lo que no puede decodificar.
/// </summary>
internal sealed class AlmacenFalso : IAlmacenFotos
{
    public readonly string Carpeta = Directory.CreateTempSubdirectory("nabeauty_fotos_").FullName;

    public string Guardar(string rutaOrigen)
    {
        var b = File.ReadAllBytes(rutaOrigen);
        var esPng = b.Length > 4 && b[0] == 0x89 && b[1] == 0x50;
        var esJpg = b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8;
        if (!esPng && !esJpg) throw new ReglaNegocioException("El archivo no es una imagen válida.");
        var nombre = $"{Guid.NewGuid():N}.jpg";
        File.WriteAllBytes(Path.Combine(Carpeta, nombre), b);
        File.WriteAllBytes(Path.Combine(Carpeta, "mini_" + nombre), b);
        return nombre;
    }

    public void Eliminar(string archivo)
    {
        File.Delete(Path.Combine(Carpeta, archivo));
        File.Delete(Path.Combine(Carpeta, "mini_" + archivo));
    }

    public bool Existe(string archivo) => File.Exists(Path.Combine(Carpeta, archivo));

    /// <summary>Crea una "foto" PNG mínima en disco para las pruebas.</summary>
    public static string Png(string nombre = "foto.png")
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{nombre}");
        File.WriteAllBytes(ruta, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);
        return ruta;
    }
}

/// <summary>Categorías, productos, fotos, carrito y cobro: servicios y pantallas.</summary>
internal static class PruebasCatalogoCaja
{
    public static void Correr(Action<string, bool, string> check, Action<string, Action> lanza)
    {
        void Check(string n, bool c, string e = "") => check(n, c, e);
        var archivo = Path.Combine(Path.GetTempPath(), $"nabeauty_cat_{Guid.NewGuid():N}.db");
        var bd = new BaseDatos(() => new ConexionMinima(archivo));
        bd.Inicializar();
        long Esc(string sql) { using var u = bd.Abrir(); return u.Escalar<long>(sql); }
        string? Txt(string sql) { using var u = bd.Abrir(); return u.Escalar<string?>(sql); }

        var login = new LoginService(bd);
        var adm = login.Ingresar("admin", BaseDatos.ClaveInicial);
        using (var u = bd.Abrir(true))
        {
            new UsuarioRepo(u).Crear("Cajera", "caja1", PasswordHasher.Hash("Cajera2026x"), "cajera");
            u.Confirmar();
        }
        var caj = login.Ingresar("caja1", "Cajera2026x");
        var almacen = new AlmacenFalso();
        var cat = new CatalogoService(new Contexto(bd, adm), almacen);
        var catCajera = new CatalogoService(new Contexto(bd, caj), almacen);

        // ═══════════════ Categorías ═══════════════
        Console.WriteLine("== Catálogo: categorías ==");
        var maq = cat.GuardarCategoria(new Categoria { Nombre = "Maquillaje", Color = "#f6d5dc" });
        var piel = cat.GuardarCategoria(new Categoria { Nombre = "Cuidado de la piel", Color = "#C9E4DE" });
        var lista = cat.Categorias();
        Check("Crea categorías en orden", lista.Count == 2 && lista[0].Nombre == "Maquillaje" && lista[1].Orden == 2);
        Check("Slug sin acentos ni espacios", Txt($"SELECT slug FROM categorias WHERE uuid = '{piel}'") == "cuidado-de-la-piel");
        Check("Color normalizado a mayúsculas", lista[0].Color == "#F6D5DC");
        lanza("Nombre repetido (sin importar mayúsculas)", () => cat.GuardarCategoria(new Categoria { Nombre = "maquillaje", Color = "#FFFFFF" }));
        lanza("Color inválido", () => cat.GuardarCategoria(new Categoria { Nombre = "Uñas", Color = "rosa" }));
        lanza("Cajera no crea categorías", () => catCajera.GuardarCategoria(new Categoria { Nombre = "Uñas", Color = "#FFFFFF" }));
        cat.MoverCategoria(piel, -1);
        Check("Mover cambia el orden", cat.Categorias()[0].Uuid == piel);
        var unas = cat.GuardarCategoria(new Categoria { Nombre = "Uñas", Color = "#DDD3F0" });
        cat.EliminarCategoria(unas);
        Check("Eliminar una categoría sin productos", cat.Categorias().Count == 2);
        Check("Cada cambio queda en la cola de sincronización", Esc("SELECT COUNT(*) FROM sync_cola WHERE entidad = 'categoria'") >= 3);

        // ═══════════════ Productos ═══════════════
        Console.WriteLine("== Catálogo: productos y opciones ==");
        Variante Tono(string n, long precio, long stock = 0, string? sku = null, string? cb = null, long? costo = 9000) =>
            new() { Tipo = "tono", Nombre = n, ColorHex = "#9e1b2a", Precio = precio, Costo = costo, StockInicial = stock, Sku = sku, CodigoBarras = cb };

        var labial = cat.GuardarProducto(new Producto
        {
            CategoriaUuid = maq, Nombre = "Labial Mate", Marca = "Ruby Rose",
            Variantes = [Tono("Rojo Pasión", 25000, 5, "LAB-01", "7501234567890"), Tono("Nude", 25000, 2, "lab-02"), Tono("Vino", 25000, 0)],
        });
        var p = cat.Producto(labial);
        Check("Producto con 3 tonos", p.Variantes.Count == 3 && p.Variantes.All(v => v.Tipo == "tono"));
        Check("Slug = nombre + marca", p.Slug == "labial-mate-ruby-rose");
        Check("Stock inicial entra como ajuste", p.Variantes[0].Stock == 5 && p.Variantes[1].Stock == 2 && p.Variantes[2].Stock == 0
            && Esc("SELECT COUNT(*) FROM inventario_movimientos WHERE tipo = 'ajuste' AND nota = 'Stock inicial'") == 2);
        Check("SKU en mayúsculas y color normalizado", p.Variantes[1].Sku == "LAB-02" && p.Variantes[0].ColorHex == "#9E1B2A");
        Check("Tonos con historial no se pueden quitar", p.Variantes[0].ConHistorial && !p.Variantes[2].ConHistorial);

        var serum = cat.GuardarProducto(new Producto
        {
            CategoriaUuid = piel, Nombre = "Sérum Vitamina C", Marca = "The Ordinary", Ciclo = "temporada",
            Variantes = [new Variante { Tipo = "unica", Precio = 48000, PrecioOferta = 42000, Costo = 30000, StockInicial = 1 }],
        });
        Check("Producto sin opciones con oferta", cat.Producto(serum).Variantes.Single() is { Nombre: "Única", PrecioVenta: 42000 });

        Producto Base(params Variante[] vs) => new() { CategoriaUuid = maq, Nombre = "Rubor", Marca = "Marca", Variantes = [.. vs] };
        lanza("Sin marca", () => cat.GuardarProducto(new Producto { CategoriaUuid = maq, Nombre = "Rubor", Variantes = [Tono("A", 100)] }));
        lanza("Oferta mayor o igual al precio", () => cat.GuardarProducto(Base(new Variante { Precio = 100, PrecioOferta = 100 })));
        lanza("Tono repetido", () => cat.GuardarProducto(Base(Tono("Coral", 100), Tono("coral", 100))));
        lanza("Sin opciones con dos precios", () => cat.GuardarProducto(Base(new Variante { Precio = 100 }, new Variante { Precio = 200 })));
        lanza("Tipos mezclados", () => cat.GuardarProducto(Base(Tono("A", 100), new Variante { Tipo = "talla", Nombre = "M", Precio = 100 })));
        try { cat.GuardarProducto(Base(Tono("Coral", 100, sku: "LAB-01"))); Check("SKU ya usado por otro producto", false); }
        catch (ReglaNegocioException e) { Check("SKU ya usado: dice quién lo tiene", e.Message.Contains("Labial Mate · Rojo Pasión"), e.Message); }
        lanza("Código de barras ya usado", () => cat.GuardarProducto(Base(Tono("Coral", 100, cb: "7501234567890"))));
        lanza("Cajera no crea productos", () => catCajera.GuardarProducto(Base(Tono("Coral", 100))));
        Check("Ningún intento fallido dejó datos a medias", Esc("SELECT COUNT(*) FROM productos") == 2);

        // Editar: quitar un tono sin historial sí; con historial no
        p = cat.Producto(labial);
        p.Nombre = "Labial Mate Larga Duración";
        p.Variantes = [p.Variantes[0], p.Variantes[1]];   // se quita "Vino" (sin movimientos)
        cat.GuardarProducto(p);
        p = cat.Producto(labial);
        Check("Editar: quita el tono sin historial", p.Variantes.Count == 2 && p.Nombre == "Labial Mate Larga Duración");
        Check("Editar no cambia el slug (la URL de la web)", p.Slug == "labial-mate-ruby-rose");
        var sinRojo = cat.Producto(labial);
        sinRojo.Variantes = [sinRojo.Variantes[1]];
        lanza("Editar: no quita un tono con movimientos", () => cat.GuardarProducto(sinRojo));
        var pc = catCajera.Producto(labial);
        Check("Cajera no recibe el costo", pc.Variantes.All(v => v.Costo is null));

        // Listado
        var filas = cat.Productos(new FiltroProductos());
        var fl = filas.First(f => f.Uuid == labial);
        Check("Listado: stock total y opciones", fl.Stock == 7 && fl.Variantes == 2 && fl.Opciones == "2 tonos", $"{fl.Stock} {fl.Opciones}");
        Check("Listado: buscar por nombre de tono", cat.Productos(new FiltroProductos("nude")).Single().Uuid == labial);
        Check("Listado: buscar por código de barras", cat.Productos(new FiltroProductos("7501234567890")).Single().Uuid == labial);
        Check("Listado: filtro por categoría", cat.Productos(new FiltroProductos(CategoriaUuid: piel)).Single().Uuid == serum);
        Check("Listado: por reponer (stock ≤ 3)", cat.Productos(new FiltroProductos(SoloPorReponer: true)).Count == 2);
        Check("Categoría cuenta sus productos", cat.Categorias().First(c => c.Uuid == maq).Productos == 1);
        lanza("No se elimina una categoría con productos", () => cat.EliminarCategoria(maq));

        // ═══════════════ Fotos ═══════════════
        Console.WriteLine("== Catálogo: fotos ==");
        var f1 = cat.AgregarFoto(labial, AlmacenFalso.Png());
        var f2 = cat.AgregarFoto(labial, AlmacenFalso.Png());
        Check("La primera foto es la principal", f1.Principal && !f2.Principal && almacen.Existe(f1.Archivo));
        Check("El listado muestra la principal", cat.Productos(new FiltroProductos()).First(f => f.Uuid == labial).Foto == f1.Archivo);
        cat.HacerPrincipal(f2.Uuid);
        Check("Cambiar la principal", cat.Producto(labial).Fotos[0].Uuid == f2.Uuid);
        cat.EliminarFoto(f2.Uuid);
        var fotos = cat.Producto(labial).Fotos;
        Check("Quitar la principal: la siguiente pasa a principal y se borra el archivo",
            fotos.Count == 1 && fotos[0].Uuid == f1.Uuid && fotos[0].Orden == 0 && !almacen.Existe(f2.Archivo));
        var txt = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(txt, "hola");
        lanza("Rechaza un archivo que no es imagen", () => cat.AgregarFoto(labial, txt));
        var falsa = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
        File.WriteAllText(falsa, "no soy jpg");
        lanza("Rechaza un .jpg que no es imagen", () => cat.AgregarFoto(labial, falsa));
        for (var i = 0; i < CatalogoService.MaxFotos - 1; i++) cat.AgregarFoto(labial, AlmacenFalso.Png());
        lanza("Máximo 8 fotos", () => cat.AgregarFoto(labial, AlmacenFalso.Png()));
        Check("Ocho archivos guardados, ninguno huérfano", cat.Producto(labial).Fotos.Count == 8
            && Directory.GetFiles(almacen.Carpeta, "mini_*").Length == 8);
        lanza("Cajera no sube fotos", () => catCajera.AgregarFoto(labial, AlmacenFalso.Png()));

        // ═══════════════ Carrito (dominio) ═══════════════
        Console.WriteLine("== Caja: carrito ==");
        var caja = new CajaService(new Contexto(bd, caj));
        var rojo = caja.PorCodigo("7501234567890")!;
        var nude = caja.PorCodigo("lab-02")!;
        var vitC = caja.Buscar("sérum").Single();
        Check("Escáner: código de barras exacto", rojo.Variante == "Rojo Pasión" && rojo.Stock == 5);
        Check("SKU sin importar mayúsculas", nude.Variante == "Nude");
        Check("Buscar por nombre con tilde y oferta", vitC.PrecioVenta == 42000 && vitC.EnOferta && vitC.PrecioAntes == "L 480.00");
        Check("La caja ve la foto principal", rojo.Foto == f1.Archivo);
        Check("Código inexistente", caja.PorCodigo("000") is null);

        var carrito = new Carrito();
        carrito.Agregar(rojo);
        carrito.Agregar(rojo);
        Check("Agregar lo mismo suma cantidad", carrito.Lineas.Count == 1 && carrito.Lineas[0].Cantidad == 2 && carrito.Total == 50000);
        lanza("No deja agregar más que el stock", () => carrito.Agregar(rojo, 4));
        carrito.Agregar(vitC);
        lanza("Descuento mayor al subtotal", () => carrito.AplicarDescuento(1_000_000));
        carrito.AplicarDescuento(2000);
        Check("Total con descuento", carrito.Subtotal == 92000 && carrito.Total == 90000);
        carrito.Quitar(rojo.VarianteUuid);
        carrito.AplicarDescuento(42000);
        carrito.CambiarCantidad(vitC.VarianteUuid, 0);
        Check("Quitar todo deja el descuento en cero", carrito.Vacio && carrito.Descuento == 0);
        var agotado = new ArticuloCaja { VarianteUuid = "x", Producto = "X", Stock = 0 };
        lanza("No agrega un agotado", () => new Carrito().Agregar(agotado));

        // ═══════════════ Cobro ═══════════════
        Console.WriteLine("== Caja: cobro ==");
        carrito.Agregar(rojo, 2);
        carrito.Agregar(vitC);
        carrito.AplicarDescuento(2000);
        lanza("Efectivo que no alcanza", () => caja.Cobrar(carrito, "efectivo", 50000));
        lanza("Método de pago inválido", () => caja.Cobrar(carrito, "bitcoin", 0));
        Check("Un cobro rechazado no registra nada", Esc("SELECT COUNT(*) FROM ventas") == 0);
        var v1 = caja.Cobrar(carrito, "efectivo", 100000);
        Check("Venta V-000001 con cambio", v1.Numero == "V-000001" && v1.Total == 90000 && v1.Cambio == 10000 && v1.Unidades == 3);
        Check("Descuenta el stock", caja.PorCodigo("7501234567890")!.Stock == 3);
        Check("Guarda el costo del momento en el detalle",
            Esc($"SELECT SUM(costo_unitario * cantidad) FROM venta_detalle WHERE venta_uuid = '{v1.Uuid}'") == 9000 * 2 + 30000);
        Check("Totales cuadran en la base", Esc($"SELECT subtotal - descuento - total FROM ventas WHERE uuid = '{v1.Uuid}'") == 0
            && Esc($"SELECT descuento FROM ventas WHERE uuid = '{v1.Uuid}'") == 2000);
        Check("La venta queda para la tienda en línea", Esc($"SELECT COUNT(*) FROM sync_cola WHERE entidad = 'venta' AND uuid = '{v1.Uuid}'") == 1);
        Check("Venta a nombre de la cajera", Esc($"SELECT usuario_id FROM ventas WHERE uuid = '{v1.Uuid}'") == caj.Id);
        Check("Sérum agotado: va al final de la búsqueda", caja.Buscar(null).Last().VarianteUuid == vitC.VarianteUuid);

        var tarjeta = new Carrito();
        tarjeta.Agregar(caja.PorCodigo("lab-02")!);
        var v2 = caja.Cobrar(tarjeta, "tarjeta", 0);
        Check("Tarjeta: número correlativo y sin cambio", v2.Numero == "V-000002" && v2.Cambio == 0 && v2.Recibido == v2.Total);

        // El carrito quedó viejo: otra venta se llevó el stock / cambió el precio / se archivó
        var viejo = new Carrito();
        var rojoAhora = caja.PorCodigo("7501234567890")!;   // stock 3
        viejo.Agregar(rojoAhora, 3);
        var otra = new Carrito();
        otra.Agregar(rojoAhora, 2);
        caja.Cobrar(otra, "tarjeta", 0);                     // queda 1
        lanza("Stock vendido por otra venta mientras tanto", () => caja.Cobrar(viejo, "tarjeta", 0));
        var conPrecio = new Carrito();
        conPrecio.Agregar(caja.PorCodigo("7501234567890")!);
        var cambio = cat.Producto(labial);
        cambio.Variantes[0].Precio = 27000;
        cat.GuardarProducto(cambio);
        lanza("Precio cambiado desde que se agregó", () => caja.Cobrar(conPrecio, "tarjeta", 0));
        var archivado = new Carrito();
        archivado.Agregar(caja.PorCodigo("7501234567890")!);
        cat.CambiarEstadoProducto(labial, archivar: true);
        lanza("Producto archivado no se cobra", () => caja.Cobrar(archivado, "tarjeta", 0));
        Check("Archivado no aparece en la caja", caja.Buscar("labial").Count == 0 && caja.PorCodigo("7501234567890") is null);
        cat.CambiarEstadoProducto(labial, archivar: false);
        Check("Stock = suma del libro después de todo", Esc("""
            SELECT COUNT(*) FROM producto_variantes v
             WHERE v.stock <> (SELECT COALESCE(SUM(cantidad),0) FROM inventario_movimientos m WHERE m.variante_uuid = v.uuid)
            """) == 0);
        lanza("Socio no cobra", () =>
        {
            using (var u = bd.Abrir(true)) { new UsuarioRepo(u).Crear("Socio", "socio1", PasswordHasher.Hash("Socio2026xx"), "socio"); u.Confirmar(); }
            new CajaService(new Contexto(bd, login.Ingresar("socio1", "Socio2026xx"))).Buscar(null);
        });

        // ═══════════════ Pantallas ═══════════════
        Console.WriteLine("== Pantallas: categorías, productos y caja ==");
        var dlg = new DialogosFalsos();
        var s = new Sesion(bd, dlg) { Fotos = almacen };
        s.Iniciar(adm);
        var shell = new ShellViewModel(s);
        shell.IrA("categorias");
        var vc = (CategoriasViewModel)shell.Vista!;
        Check("Categorías abre con formulario de nueva", vc.EsNueva && vc.Lista.Count == 2 && vc.Color.StartsWith('#'));
        vc.Nombre = "Accesorios";
        Check("Crear desde la pantalla deja el formulario listo para otra", vc.Guardar() && vc.Lista.Count == 3 && vc.EsNueva && vc.Nombre == "");
        vc.Seleccionada = vc.Lista.First(c => c.Nombre == "Accesorios");
        Check("Elegir una fila la carga en el formulario", !vc.EsNueva && vc.Nombre == "Accesorios" && vc.TextoBoton == "Guardar cambios");
        vc.Nombre = "Accesorios y brochas";
        vc.Guardar();
        Check("Editar desde la pantalla mantiene la selección", vc.Lista.Any(c => c.Nombre == "Accesorios y brochas") && vc.Seleccionada?.Nombre == "Accesorios y brochas");
        vc.Nueva();
        vc.Nombre = "";
        Check("Error de validación se muestra", !vc.Guardar() && dlg.Errores.Count > 0);

        shell.IrA("productos");
        var vp = (ProductosViewModel)shell.Vista!;
        Check("Productos lista el catálogo", vp.Lista.Count == 2 && vp.HayProductos && vp.Conteo == "2 productos");
        vp.Texto = "sérum";
        Check("Buscar filtra al escribir", vp.Lista.Count == 1);
        vp.Texto = "zzz";
        Check("Sin coincidencias explica por qué", !vp.HayProductos && vp.MensajeVacio.Contains("coincide"));
        vp.Texto = "";
        vp.NuevoCommand.Execute(null);
        var ed = (ProductoEditorViewModel)shell.Vista!;
        Check("Nuevo producto abre la ficha con una opción vacía", ed.EsNuevo && ed.Variantes.Count == 1 && ed.EsUnica && shell.PuedeVolver);
        ed.Nombre = "Rubor Compacto";
        ed.Marca = "Bissú";
        ed.Tipo = ed.Tipos.First(t => t.Clave == "tono");
        ed.Variantes[0].Nombre = "Durazno";
        ed.Variantes[0].Precio = "185";
        ed.Variantes[0].StockInicial = "4";
        ed.AgregarVarianteCommand.Execute(null);
        Check("Agregar tono copia el precio", ed.Variantes.Count == 2 && ed.Variantes[1].Precio == "185");
        ed.Variantes[1].Nombre = "Rosa";
        ed.Variantes[1].Precio = "18x";
        dlg.Errores.Clear();
        Check("Precio mal escrito no guarda", !ed.Guardar() && dlg.Errores.Single().Contains("Rosa"), dlg.Errores.FirstOrDefault() ?? "");
        ed.Variantes[1].Precio = "185.50";
        dlg.Imagenes = [AlmacenFalso.Png(), AlmacenFalso.Png()];
        ed.AgregarFotosCommand.Execute(null);
        Check("Fotos de un producto nuevo quedan pendientes", ed.Fotos.Count == 2 && ed.Fotos.All(f => f.Pendiente) && ed.Fotos[0].Principal);
        Check("Guardar vuelve al listado", ed.Guardar() && shell.Vista is ProductosViewModel);
        vp = (ProductosViewModel)shell.Vista!;
        var rubor = vp.Lista.First(f => f.Nombre == "Rubor Compacto");
        Check("Producto nuevo con fotos subidas y stock", rubor.Foto is not null && rubor.Stock == 4 && rubor.PrecioMax == 18550);
        vp.Abrir(rubor.Uuid);
        ed = (ProductoEditorViewModel)shell.Vista!;
        Check("Abrir: carga tonos, fotos y stock", ed.Variantes.Count == 2 && ed.Fotos.Count == 2 && ed.Variantes[0].StockTexto == "4 en stock"
            && ed.Titulo == "Rubor Compacto" && !ed.EsUnica);
        dlg.Errores.Clear();
        ed.QuitarVarianteCommand.Execute(ed.Variantes[0]);
        Check("No quita un tono con stock (pide desactivarlo)", ed.Variantes.Count == 2 && dlg.Errores.Count == 1);
        ed.QuitarVarianteCommand.Execute(ed.Variantes[1]);
        Check("Quita un tono sin movimientos", ed.Variantes.Count == 1);
        dlg.Infos.Clear();
        Check("Guardar cambios se queda en la ficha", ed.Guardar() && dlg.Infos.Count == 1 && shell.Vista == ed);

        // Cajera: solo lectura
        var sc = new Sesion(bd, new DialogosFalsos()) { Fotos = almacen };
        sc.Iniciar(caj);
        var shellC = new ShellViewModel(sc);
        shellC.IrA("productos");
        var vpc = (ProductosViewModel)shellC.Vista!;
        Check("Cajera ve productos sin poder crear", vpc.HayProductos && !vpc.NuevoCommand.CanExecute(null));
        vpc.Abrir(rubor.Uuid);
        var edc = (ProductoEditorViewModel)shellC.Vista!;
        Check("Cajera abre la ficha en solo lectura y sin costos", edc.SoloLectura && !edc.VerCostos && !edc.GuardarCommand.CanExecute(null));

        shellC.IrA("caja");
        var vk = (CajaViewModel)shellC.Vista!;
        Check("Caja: pestañas por categoría con productos", vk.Filtros.Count == 3 && vk.Filtros[0].Activa && vk.HayResultados, vk.Filtros.Count.ToString());
        vk.FiltroCommand.Execute(vk.Filtros.First(f => f.Nombre == "Cuidado de la piel"));
        Check("Filtro por categoría", vk.Resultados.All(r => r.Categoria == "Cuidado de la piel"));
        vk.FiltroCommand.Execute(vk.Filtros[0]);
        vk.Texto = "7501234567890";
        vk.Enter();
        Check("Escanear agrega y limpia la búsqueda", vk.Lineas.Count == 1 && vk.Texto == "" && vk.Total == "L 270.00", vk.Total);
        vk.Texto = "7501234567890";
        vk.Enter();
        Check("Escanear sin stock avisa en pantalla", vk.Lineas[0].Cantidad == 1 && vk.HayAviso && vk.AvisoError, vk.Aviso);
        vk.Texto = "durazno";
        vk.Enter();
        Check("Enter con un solo resultado lo agrega", vk.Lineas.Count == 2);
        vk.MasCommand.Execute(vk.Lineas[1]);
        Check("Más cantidad", vk.Lineas[1].Cantidad == 2 && vk.Total == "L 640.00", vk.Total);
        vk.Descuento = "40";
        Check("Descuento al escribir", vk.Total == "L 600.00" && vk.HayDescuento);
        Check("Montos rápidos: exacto y billetes que alcanzan", vk.MontosRapidos[0].Centavos == 60000 && vk.MontosRapidos.Skip(1).All(m => m.Centavos > 60000),
            string.Join(",", vk.MontosRapidos.Select(m => m.Texto)));
        vk.Recibido = "500";
        Check("Efectivo corto: dice cuánto falta", vk.HayFalta && vk.Falta == "Faltan L 100.00" && vk.Cambio == "—");
        Check("No cobra si falta efectivo", !vk.Cobrar() && vk.AvisoError);
        vk.MontoCommand.Execute(vk.MontosRapidos.First(m => m.Centavos == 100000));
        Check("Cambio calculado", vk.Cambio == "L 400.00");
        Check("Cobrar (F12)", vk.Cobrar() && vk.HayUltimaVenta && vk.UltimaTitulo == "Venta V-000004 registrada" && vk.UltimaCambio == "Cambio: L 400.00", vk.UltimaTitulo);
        Check("Venta nueva lista: carrito vacío, efectivo por defecto", vk.CarritoVacio && vk.Descuento == "" && vk.Recibido == "" && vk.EsEfectivo && vk.Metodos[0].Activa);
        vk.MetodoCommand.Execute("transferencia");
        Check("Elegir método de pago", !vk.EsEfectivo && vk.Metodos.Single(m => m.Activa).Clave == "transferencia");
        vk.AgregarCommand.Execute(vk.Resultados.First(r => !r.Agotado));
        Check("Agregar quita el aviso de la venta anterior", !vk.HayUltimaVenta && vk.Lineas.Count == 1);
        Check("Cobrar por transferencia sin escribir efectivo", vk.Cobrar() && vk.UltimaCambio == "");

        vk.AgregarCommand.Execute(vk.Resultados.First(r => !r.Agotado));
        shellC.IrA("productos");
        shellC.IrA("caja");
        Check("Ir a otra sección no pierde la venta en curso", shellC.Vista == vk && vk.Lineas.Count == 1);
        vk.CancelarCommand.Execute(null);
        Check("Cancelar venta vacía el carrito", vk.CarritoVacio);

        shellC.IrA("inicio");
        var ini = (InicioViewModel)shellC.Vista!;
        Check("Inicio refleja las ventas de la caja", ini.KpisVentas[0].Valor != "L 0.00" && ini.Ultimas.Count >= 5);

        File.Delete(archivo);
    }
}
