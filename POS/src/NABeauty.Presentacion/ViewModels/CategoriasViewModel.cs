using NABeauty.Core.Entidades;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>
/// Categorías: lista a la izquierda y formulario a la derecha (crear y editar en la misma pantalla).
/// El orden de la lista es el orden de las pestañas de la caja y del menú de la tienda en línea.
/// </summary>
public sealed class CategoriasViewModel : VistaBase
{
    /// <summary>Colores sugeridos para la etiqueta: tonos de la marca y pasteles legibles con texto oscuro.</summary>
    public static readonly IReadOnlyList<string> Paleta =
        ["#F6D5DC", "#F2B8C6", "#E8A0B4", "#F9E0C7", "#F5D7A1", "#D8E8C8", "#C9E4DE", "#CFE0F5", "#DDD3F0", "#E7E2DF"];

    private Categoria? _seleccionada;
    private string _nombre = "";
    private string _color = Paleta[0];
    private bool _activa = true;

    public CategoriasViewModel(Sesion s) : base(s)
    {
        NuevaCommand = new Comando(Nueva);
        GuardarCommand = new Comando(() => Guardar());
        EliminarCommand = new Comando(Eliminar, () => !EsNueva);
        SubirCommand = new Comando(p => Mover(p, -1));
        BajarCommand = new Comando(p => Mover(p, +1));
        ColorCommand = new Comando(p => { if (p is string c) Color = c; });
    }

    public override string Titulo => "Categorías";

    public List<Categoria> Lista { get; private set; } = [];
    public bool HayCategorias => Lista.Count > 0;
    public string Conteo => Lista.Count == 1 ? "1 categoría" : $"{Lista.Count} categorías";
    public IReadOnlyList<string> Colores => Paleta;

    /// <summary>Al elegir una fila se carga en el formulario.</summary>
    public Categoria? Seleccionada
    {
        get => _seleccionada;
        set
        {
            if (!Set(ref _seleccionada, value) || value is null) return;
            _nombre = value.Nombre;
            _color = value.Color;
            _activa = value.Activa;
            Avisar(nameof(Nombre), nameof(Color), nameof(Activa), nameof(EsNueva), nameof(TituloFormulario), nameof(TextoBoton));
            EliminarCommand.Refrescar();
        }
    }

    public string Nombre { get => _nombre; set => Set(ref _nombre, value); }
    public string Color { get => _color; set => Set(ref _color, value); }
    public bool Activa { get => _activa; set => Set(ref _activa, value); }

    public bool EsNueva => _seleccionada is null;
    public string TituloFormulario => EsNueva ? "Nueva categoría" : "Editar categoría";
    public string TextoBoton => EsNueva ? "Crear categoría" : "Guardar cambios";

    public Comando NuevaCommand { get; }
    public Comando GuardarCommand { get; }
    public Comando EliminarCommand { get; }
    public Comando SubirCommand { get; }
    public Comando BajarCommand { get; }
    public Comando ColorCommand { get; }

    public override void Cargar() => Recargar(_seleccionada?.Uuid);

    private void Recargar(string? seleccionar)
    {
        Lista = new CatalogoService(Ctx).Categorias();
        Avisar(nameof(Lista), nameof(HayCategorias), nameof(Conteo));
        var sel = seleccionar is null ? null : Lista.FirstOrDefault(c => c.Uuid == seleccionar);
        if (sel is null) Nueva(); else { _seleccionada = null; Seleccionada = sel; }
    }

    public void Nueva()
    {
        _seleccionada = null;
        _nombre = "";
        _color = Paleta[Lista.Count % Paleta.Count];   // va rotando para que las etiquetas se distingan
        _activa = true;
        Avisar(nameof(Seleccionada), nameof(Nombre), nameof(Color), nameof(Activa), nameof(EsNueva), nameof(TituloFormulario), nameof(TextoBoton));
        EliminarCommand.Refrescar();
    }

    public bool Guardar()
    {
        string? uuid = null;
        var eraNueva = EsNueva;
        var ok = Intentar(() => uuid = new CatalogoService(Ctx).GuardarCategoria(new Categoria
        {
            Uuid = _seleccionada?.Uuid ?? "",
            Nombre = Nombre,
            Color = Color,
            Activa = Activa,
            Orden = _seleccionada?.Orden ?? 0,
        }));
        // Al crear, el formulario queda listo para la siguiente; al editar, sigue en la misma
        if (ok) Recargar(eraNueva ? null : uuid);
        return ok;
    }

    private void Eliminar()
    {
        if (_seleccionada is not { } c) return;
        if (!Dlg.Confirmar($"¿Eliminar la categoría «{c.Nombre}»?")) return;
        if (Intentar(() => new CatalogoService(Ctx).EliminarCategoria(c.Uuid))) Recargar(null);
    }

    private void Mover(object? p, int dir)
    {
        if (p is not Categoria c) return;
        if (Intentar(() => new CatalogoService(Ctx).MoverCategoria(c.Uuid, dir))) Recargar(_seleccionada?.Uuid);
    }
}
