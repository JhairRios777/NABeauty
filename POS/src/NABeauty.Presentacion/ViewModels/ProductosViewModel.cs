using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Repositorios;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>Opción de un ComboBox (clave + texto visible).</summary>
public sealed record Opcion(string? Clave, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>
/// Listado del catálogo con búsqueda y filtros. Cada fila muestra la miniatura, precio,
/// opciones y estado de stock. Doble clic (o Enter) abre la ficha del producto.
/// </summary>
public sealed class ProductosViewModel : VistaBase
{
    public static readonly IReadOnlyList<Opcion> Estados =
        [new("activo", "Activos"), new("archivado", "Archivados"), new(null, "Todos")];

    private string _texto = "";
    private Opcion _categoria = new(null, "Todas las categorías");
    private Opcion _estado = Estados[0];
    private bool _soloPorReponer;
    private bool _cargando;

    public ProductosViewModel(Sesion s) : base(s)
    {
        NuevoCommand = new Comando(Nuevo, () => PuedeEditar);
        AbrirCommand = new Comando(p => { if (p is ProductoFila f) Abrir(f.Uuid); });
        LimpiarCommand = new Comando(() => { _texto = ""; _soloPorReponer = false; _estado = Estados[0]; _categoria = Categorias[0]; Avisar(nameof(Texto), nameof(SoloPorReponer), nameof(Estado), nameof(Categoria)); Buscar(); });
        IrCategoriasCommand = new Comando(() => Nav.IrA("categorias"));
    }

    public override string Titulo => "Productos";
    public bool PuedeEditar => S.Puede(Permiso.GestionarProductos);
    public bool PuedeCategorias => S.Puede(Permiso.GestionarProductos);

    public List<Opcion> Categorias { get; private set; } = [];
    public IReadOnlyList<Opcion> OpcionesEstado => Estados;

    public string Texto { get => _texto; set { if (Set(ref _texto, value)) Buscar(); } }
    public Opcion Categoria { get => _categoria; set { if (value is not null && Set(ref _categoria, value)) Buscar(); } }
    public Opcion Estado { get => _estado; set { if (value is not null && Set(ref _estado, value)) Buscar(); } }
    public bool SoloPorReponer { get => _soloPorReponer; set { if (Set(ref _soloPorReponer, value)) Buscar(); } }

    public List<ProductoFila> Lista { get; private set; } = [];
    public bool HayProductos => Lista.Count > 0;
    public bool SinCategorias { get; private set; }
    public bool HayFiltros => _texto.Length > 0 || _categoria.Clave is not null || _estado != Estados[0] || _soloPorReponer;
    public string Conteo => Lista.Count == 1 ? "1 producto" : $"{Lista.Count} productos";
    /// <summary>Mensaje del estado vacío según el motivo.</summary>
    public string MensajeVacio => SinCategorias
        ? "Primero crea una categoría (Maquillaje, Skincare, Accesorios…). Luego vuelve aquí para cargar los productos."
        : HayFiltros ? "Ningún producto coincide con la búsqueda o los filtros."
        : "Todavía no hay productos. Crea el primero con «Nuevo producto».";

    public Comando NuevoCommand { get; }
    public Comando AbrirCommand { get; }
    public Comando LimpiarCommand { get; }
    public Comando IrCategoriasCommand { get; }

    public override void Cargar()
    {
        _cargando = true;
        var cats = new CatalogoService(Ctx).Categorias();
        SinCategorias = cats.Count == 0;
        Categorias = [new(null, "Todas las categorías"), .. cats.Select(c => new Opcion(c.Uuid, c.Nombre))];
        _categoria = Categorias.FirstOrDefault(c => c.Clave == _categoria.Clave) ?? Categorias[0];
        Avisar(nameof(Categorias), nameof(Categoria), nameof(SinCategorias));
        _cargando = false;
        Buscar();
    }

    public void Buscar()
    {
        if (_cargando) return;
        Lista = new CatalogoService(Ctx).Productos(new FiltroProductos(_texto, _categoria.Clave, _estado.Clave, _soloPorReponer));
        Avisar(nameof(Lista), nameof(HayProductos), nameof(Conteo), nameof(HayFiltros), nameof(MensajeVacio));
    }

    private void Nuevo()
    {
        if (SinCategorias) { Dlg.Error("Primero crea al menos una categoría."); return; }
        Nav.Ir(new ProductoEditorViewModel(S, null, _categoria.Clave), "Nuevo producto");
    }

    public void Abrir(string uuid) => Nav.Ir(new ProductoEditorViewModel(S, uuid, null));
}
