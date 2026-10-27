using System.Collections.ObjectModel;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>
/// Fila editable de una opción (tono, talla…). Los montos se editan como texto y se
/// convierten a centavos solo al guardar (así se puede escribir "1,250.5" sin pelear con el campo).
/// </summary>
public sealed class VarianteFila : Observable
{
    private string _nombre = "", _precio = "", _oferta = "", _costo = "", _stockInicial = "", _sku = "", _codigo = "", _color = "";
    private bool _activa = true;

    public string Uuid { get; init; } = "";
    public long Stock { get; init; }
    public bool ConHistorial { get; init; }
    public bool EsNueva => Uuid.Length == 0;
    /// <summary>Se puede quitar de la lista (nueva o sin movimientos); si no, solo desactivar.</summary>
    public bool SePuedeQuitar => !ConHistorial;
    public string StockTexto => EsNueva ? "" : Stock == 1 ? "1 en stock" : $"{Stock} en stock";

    public string Nombre { get => _nombre; set => Set(ref _nombre, value); }
    public string Precio { get => _precio; set => Set(ref _precio, value); }
    public string Oferta { get => _oferta; set => Set(ref _oferta, value); }
    public string Costo { get => _costo; set => Set(ref _costo, value); }
    public string StockInicial { get => _stockInicial; set => Set(ref _stockInicial, value); }
    public string Sku { get => _sku; set => Set(ref _sku, value); }
    public string Codigo { get => _codigo; set => Set(ref _codigo, value); }
    public string ColorHex { get => _color; set { if (Set(ref _color, value)) Avisar(nameof(TieneColor)); } }
    public bool TieneColor => ColorHex.Length == 7 && ColorHex[0] == '#';
    public bool Activa { get => _activa; set => Set(ref _activa, value); }

    public static VarianteFila De(Variante v) => new()
    {
        Uuid = v.Uuid,
        Stock = v.Stock,
        ConHistorial = v.ConHistorial,
        _nombre = v.Tipo == "unica" ? "" : v.Nombre,
        _precio = Dinero.ParaEditar(v.Precio),
        _oferta = v.PrecioOferta is { } o ? Dinero.ParaEditar(o) : "",
        _costo = v.Costo is { } c ? Dinero.ParaEditar(c) : "",
        _sku = v.Sku ?? "",
        _codigo = v.CodigoBarras ?? "",
        _color = v.ColorHex ?? "",
        _activa = v.Activa,
    };
}

/// <summary>Foto en la ficha: guardada (Archivo) o elegida pero aún no subida (ruta en el disco).</summary>
public sealed class FotoFila(string? uuid, string ruta, bool principal)
{
    public string? Uuid { get; } = uuid;
    /// <summary>Nombre del archivo guardado, o la ruta completa si todavía está pendiente.</summary>
    public string Ruta { get; } = ruta;
    public bool Principal { get; } = principal;
    public bool Pendiente => Uuid is null;
}

/// <summary>
/// Ficha de producto: datos generales, opciones con precio/stock y fotos.
/// Producto nuevo: las fotos elegidas quedan pendientes y se suben al guardar.
/// Producto existente: agregar, quitar o cambiar la principal se aplica al momento.
/// Sin permiso de gestionar productos la ficha se abre en solo lectura.
/// </summary>
public sealed class ProductoEditorViewModel : VistaBase
{
    private readonly IAlmacenFotos? _almacen;
    private string? _uuid;
    private string _nombre = "", _marca = "", _descripcion = "", _tipo = "unica";
    private Opcion? _categoria;
    private bool _temporada, _destacado;
    private string _estado = "activo";
    private readonly string? _categoriaInicial;

    public ProductoEditorViewModel(Sesion s, string? uuid, string? categoriaInicial, IAlmacenFotos? almacen = null) : base(s)
    {
        _uuid = uuid;
        _categoriaInicial = categoriaInicial;
        _almacen = almacen ?? s.Fotos;
        GuardarCommand = new Comando(() => Guardar(), () => PuedeEditar);
        AgregarVarianteCommand = new Comando(AgregarVariante, () => PuedeEditar && !EsUnica);
        QuitarVarianteCommand = new Comando(p => { if (p is VarianteFila v) QuitarVariante(v); });
        AgregarFotosCommand = new Comando(AgregarFotos, () => PuedeEditar);
        QuitarFotoCommand = new Comando(p => { if (p is FotoFila f) QuitarFoto(f); });
        PrincipalCommand = new Comando(p => { if (p is FotoFila f) HacerPrincipal(f); });
        ArchivarCommand = new Comando(CambiarEstado, () => PuedeEditar && !EsNuevo);
        CancelarCommand = new Comando(() => Nav.Volver());
    }

    public override string Titulo => EsNuevo ? "Nuevo producto" : _nombre;

    // ── Permisos y estado ───────────────────────────────────────────
    public bool PuedeEditar => S.Puede(Permiso.GestionarProductos);
    public bool SoloLectura => !PuedeEditar;
    public bool VerCostos => S.Puede(Permiso.VerCostos);
    public bool PuedeStockInicial => S.Puede(Permiso.AjustarInventario);
    public bool EsNuevo => _uuid is null;
    public bool Archivado => _estado == "archivado";
    public string TextoArchivar => Archivado ? "Reactivar producto" : "Archivar producto";
    public string Encabezado => EsNuevo ? "Nuevo producto" : Archivado ? $"{_nombre} (archivado)" : _nombre;

    // ── Datos generales ─────────────────────────────────────────────
    public List<Opcion> Categorias { get; private set; } = [];
    public Opcion? Categoria { get => _categoria; set => Set(ref _categoria, value); }
    public string Nombre { get => _nombre; set => Set(ref _nombre, value); }
    public string Marca { get => _marca; set => Set(ref _marca, value); }
    public string Descripcion { get => _descripcion; set => Set(ref _descripcion, value); }
    /// <summary>De temporada: en la tienda en línea solo se ve mientras tenga stock.</summary>
    public bool Temporada { get => _temporada; set => Set(ref _temporada, value); }
    public bool Destacado { get => _destacado; set => Set(ref _destacado, value); }

    // ── Opciones ────────────────────────────────────────────────────
    public IReadOnlyList<Opcion> Tipos { get; } = [.. TiposVariante.Todos.Select(t => new Opcion(t.Clave, t.Nombre))];
    public Opcion Tipo
    {
        get => Tipos.First(t => t.Clave == _tipo);
        set
        {
            if (value?.Clave is not { } nuevo || nuevo == _tipo) return;
            if (nuevo == "unica" && Variantes.Count > 1)
            {
                Dlg.Error("Para dejar el producto sin opciones, primero quita las demás y deja solo una.");
                Avisar(nameof(Tipo));
                return;
            }
            _tipo = nuevo;
            Avisar(nameof(Tipo), nameof(EsUnica), nameof(EtiquetaOpcion), nameof(ConColor));
            AgregarVarianteCommand.Refrescar();
        }
    }
    public bool EsUnica => _tipo == "unica";
    public string EtiquetaOpcion => TiposVariante.Etiqueta(_tipo);
    public bool ConColor => _tipo is "tono" or "color";
    public ObservableCollection<VarianteFila> Variantes { get; } = [];

    // ── Fotos ───────────────────────────────────────────────────────
    public ObservableCollection<FotoFila> Fotos { get; } = [];
    private readonly List<string> _pendientes = [];
    private List<FotoProducto> _guardadas = [];
    public bool HayFotos => Fotos.Count > 0;
    public string TextoFotos => Fotos.Count == 0
        ? $"Hasta {CatalogoService.MaxFotos} fotos. La primera es la principal: es la que se ve en la caja y en la tienda en línea."
        : $"{Fotos.Count} de {CatalogoService.MaxFotos} fotos{(_pendientes.Count > 0 ? " · las nuevas se suben al guardar" : "")}";

    public Comando GuardarCommand { get; }
    public Comando AgregarVarianteCommand { get; }
    public Comando QuitarVarianteCommand { get; }
    public Comando AgregarFotosCommand { get; }
    public Comando QuitarFotoCommand { get; }
    public Comando PrincipalCommand { get; }
    public Comando ArchivarCommand { get; }
    public Comando CancelarCommand { get; }

    public override void Cargar()
    {
        var svc = new CatalogoService(Ctx, _almacen);
        var cats = svc.Categorias();
        Categorias = [.. cats.Select(c => new Opcion(c.Uuid, c.Activa ? c.Nombre : $"{c.Nombre} (oculta)"))];

        Variantes.Clear();
        if (_uuid is null)
        {
            _categoria = Categorias.FirstOrDefault(c => c.Clave == _categoriaInicial) ?? Categorias.FirstOrDefault();
            if (Variantes.Count == 0) Variantes.Add(new VarianteFila());
        }
        else
        {
            var p = svc.Producto(_uuid);
            _nombre = p.Nombre; _marca = p.Marca; _descripcion = p.Descripcion ?? "";
            _temporada = p.Ciclo == "temporada"; _destacado = p.Destacado; _estado = p.Estado;
            _categoria = Categorias.FirstOrDefault(c => c.Clave == p.CategoriaUuid);
            _tipo = p.Variantes.FirstOrDefault()?.Tipo ?? "unica";
            foreach (var v in p.Variantes) Variantes.Add(VarianteFila.De(v));
            if (Variantes.Count == 0) Variantes.Add(new VarianteFila());
            CargarFotos(p.Fotos);
        }
        Avisar(string.Empty);
        AgregarVarianteCommand.Refrescar();
        ArchivarCommand.Refrescar();
    }

    private void CargarFotos(List<FotoProducto> guardadas)
    {
        _guardadas = guardadas;
        Fotos.Clear();
        foreach (var f in guardadas) Fotos.Add(new FotoFila(f.Uuid, f.Archivo, f.Principal));
        foreach (var r in _pendientes) Fotos.Add(new FotoFila(null, r, Fotos.Count == 0));
        Avisar(nameof(HayFotos), nameof(TextoFotos));
    }

    // ── Opciones ────────────────────────────────────────────────────
    private void AgregarVariante()
    {
        // La nueva copia el precio de la anterior: casi siempre todos los tonos cuestan igual
        var ultima = Variantes.LastOrDefault();
        Variantes.Add(new VarianteFila { Precio = ultima?.Precio ?? "", Oferta = ultima?.Oferta ?? "", Costo = ultima?.Costo ?? "" });
    }

    private void QuitarVariante(VarianteFila v)
    {
        if (!PuedeEditar) return;
        if (v.ConHistorial) { Dlg.Error("Esta opción ya tiene movimientos de inventario: desactívala en lugar de quitarla."); return; }
        if (Variantes.Count == 1) { Dlg.Error("El producto necesita al menos una opción."); return; }
        Variantes.Remove(v);
    }

    // ── Fotos ───────────────────────────────────────────────────────
    private void AgregarFotos()
    {
        var rutas = Dlg.ElegirImagenes();
        if (rutas.Count == 0) return;
        var espacio = CatalogoService.MaxFotos - Fotos.Count;
        if (espacio <= 0) { Dlg.Error($"Cada producto admite hasta {CatalogoService.MaxFotos} fotos."); return; }
        if (rutas.Count > espacio) Dlg.Info($"Solo se agregarán {espacio} foto(s): el máximo es {CatalogoService.MaxFotos}.");

        if (EsNuevo)
        {
            _pendientes.AddRange(rutas.Take(espacio));
            CargarFotos(_guardadas);
            return;
        }
        var svc = new CatalogoService(Ctx, _almacen);
        foreach (var r in rutas.Take(espacio))
            if (!Intentar(() => svc.AgregarFoto(_uuid!, r))) break;
        CargarFotos(svc.Producto(_uuid!).Fotos);
    }

    private void QuitarFoto(FotoFila f)
    {
        if (!PuedeEditar) return;
        if (f.Pendiente)
        {
            _pendientes.Remove(f.Ruta);
            CargarFotos(_guardadas);
            return;
        }
        if (!Dlg.Confirmar("¿Quitar esta foto? También se quitará de la tienda en línea.")) return;
        var svc = new CatalogoService(Ctx, _almacen);
        if (Intentar(() => svc.EliminarFoto(f.Uuid!))) CargarFotos(svc.Producto(_uuid!).Fotos);
    }

    private void HacerPrincipal(FotoFila f)
    {
        if (!PuedeEditar || f.Principal) return;
        if (f.Pendiente)
        {
            _pendientes.Remove(f.Ruta);
            _pendientes.Insert(0, f.Ruta);
            CargarFotos(_guardadas);
            return;
        }
        var svc = new CatalogoService(Ctx, _almacen);
        if (Intentar(() => svc.HacerPrincipal(f.Uuid!))) CargarFotos(svc.Producto(_uuid!).Fotos);
    }

    // ── Guardar ─────────────────────────────────────────────────────
    public bool Guardar()
    {
        if (!PuedeEditar) return false;
        if (!ArmarVariantes(out var variantes)) return false;

        var svc = new CatalogoService(Ctx, _almacen);
        string? uuid = null;
        var ok = Intentar(() => uuid = svc.GuardarProducto(new Producto
        {
            Uuid = _uuid ?? "",
            CategoriaUuid = _categoria?.Clave ?? "",
            Nombre = _nombre,
            Marca = _marca,
            Descripcion = _descripcion,
            Ciclo = _temporada ? "temporada" : "permanente",
            Destacado = _destacado,
            Variantes = variantes,
        }));
        if (!ok) return false;

        // Fotos elegidas antes de que el producto existiera
        var fallidas = 0;
        foreach (var r in _pendientes)
            if (!Intentar(() => svc.AgregarFoto(uuid!, r))) fallidas++;
        _pendientes.Clear();

        var eraNuevo = EsNuevo;
        _uuid = uuid;
        if (eraNuevo && fallidas == 0)
        {
            Nav.Volver();   // vuelve al listado, que se recarga solo
            return true;
        }
        Cargar();
        Dlg.Info(fallidas > 0 ? "Producto guardado. Algunas fotos no se pudieron subir." : "Cambios guardados.");
        return true;
    }

    /// <summary>Convierte las filas (texto) a variantes (centavos). Muestra el primer error.</summary>
    private bool ArmarVariantes(out List<Variante> lista)
    {
        lista = [];
        foreach (var f in Variantes)
        {
            var cual = EsUnica ? "" : $" de «{(f.Nombre.Trim().Length > 0 ? f.Nombre.Trim() : "la opción sin nombre")}»";
            if (!LeerMonto(f.Precio, $"Precio{cual}", out var precio)) return false;
            long? oferta = null, costo = null;
            if (!string.IsNullOrWhiteSpace(f.Oferta))
            {
                if (!LeerMonto(f.Oferta, $"Precio de oferta{cual}", out var o)) return false;
                oferta = o;
            }
            if (VerCostos && !string.IsNullOrWhiteSpace(f.Costo))
            {
                if (!LeerMonto(f.Costo, $"Costo{cual}", out var c, permitirCero: true)) return false;
                costo = c;
            }
            long stockInicial = 0;
            if (f.EsNueva && PuedeStockInicial && !string.IsNullOrWhiteSpace(f.StockInicial)
                && (!long.TryParse(f.StockInicial.Trim(), out stockInicial) || stockInicial < 0 || stockInicial > 100_000))
            {
                Dlg.Error($"Stock inicial{cual}: escribe un número entero (0 o más).");
                return false;
            }
            lista.Add(new Variante
            {
                Uuid = f.Uuid,
                Tipo = _tipo,
                Nombre = EsUnica ? "Única" : f.Nombre,
                ColorHex = ConColor ? f.ColorHex : null,
                Sku = f.Sku,
                CodigoBarras = f.Codigo,
                Precio = precio,
                PrecioOferta = oferta,
                Costo = costo,
                Activa = f.Activa,
                ConHistorial = f.ConHistorial,
                StockInicial = stockInicial,
            });
        }
        return true;
    }

    private void CambiarEstado()
    {
        if (_uuid is null) return;
        var archivar = !Archivado;
        if (archivar && !Dlg.Confirmar($"¿Archivar «{_nombre}»? Deja de aparecer en la caja y en la tienda en línea. Las ventas anteriores no cambian y se puede reactivar cuando quieras.")) return;
        if (Intentar(() => new CatalogoService(Ctx, _almacen).CambiarEstadoProducto(_uuid, archivar))) Cargar();
    }
}
