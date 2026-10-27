using System.Collections.ObjectModel;
using NABeauty.Core.Seguridad;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>Elemento del menú lateral. Icono = glifo de Segoe Fluent Icons / MDL2 Assets.</summary>
public sealed class ItemMenu(string clave, string texto, string icono, string grupo, bool proximamente) : Observable
{
    private bool _activo;
    public string Clave { get; } = clave;
    public string Texto { get; } = texto;
    public string Icono { get; } = icono;
    public string Grupo { get; } = grupo;
    /// <summary>Módulo todavía en construcción: se muestra con la etiqueta "Pronto".</summary>
    public bool Proximamente { get; } = proximamente;
    public bool Activo { get => _activo; set => Set(ref _activo, value); }
}

/// <summary>Grupo de opciones del menú lateral (GENERAL, VENTAS, INVENTARIO…).</summary>
public sealed record GrupoMenu(string Nombre, List<ItemMenu> Items);

/// <summary>
/// Ventana principal: menú según permisos, navegación con historial y título de la sección.
/// Solo Inicio está construido; el resto del menú abre la pantalla "En construcción" que
/// describe lo que tendrá el módulo (así el recorrido del sistema ya se puede mostrar).
/// </summary>
public sealed class ShellViewModel : Observable, INavegador
{
    private readonly Sesion _s;
    private readonly Stack<(object Vista, string Titulo)> _historial = new();
    private object? _vista;
    private string _titulo = "Inicio";
    private CajaViewModel? _caja;

    public ShellViewModel(Sesion s)
    {
        _s = s;
        _s.Navegador = this;

        void Agregar(string clave, string texto, string icono, string grupo, params Permiso[] alguno)
        {
            if (alguno.Any(_s.Puede)) Menu.Add(new ItemMenu(clave, texto, icono, grupo, proximamente: false));
        }
        Agregar("inicio", "Inicio", "", "GENERAL", Permiso.VerInicio);
        Agregar("caja", "Caja", "", "VENTAS", Permiso.Vender);
        Agregar("ventas", "Ventas", "", "VENTAS", Permiso.VerVentas);
        Agregar("productos", "Productos", "", "INVENTARIO", Permiso.VerProductos);
        Agregar("categorias", "Categorías", "", "INVENTARIO", Permiso.GestionarProductos);
        Agregar("entradas", "Entradas", "", "INVENTARIO", Permiso.RegistrarEntradas, Permiso.AjustarInventario);
        Agregar("reportes", "Reportes", "", "REPORTES", Permiso.VerReportes);
        Agregar("usuarios", "Usuarios", "", "SISTEMA", Permiso.GestionarUsuarios);
        Agregar("roles", "Roles y permisos", "", "SISTEMA", Permiso.GestionarRoles);
        Agregar("ajustes", "Ajustes", "", "SISTEMA", Permiso.Configurar);

        IrACommand = new Comando(p => IrA((string)p!));
        VolverCommand = new Comando(Volver, () => _historial.Count > 0);

        IrA("inicio");
    }

    public ObservableCollection<ItemMenu> Menu { get; } = [];
    public List<GrupoMenu> Grupos => [.. Menu.GroupBy(m => m.Grupo).Select(g => new GrupoMenu(g.Key, [.. g]))];

    public object? Vista { get => _vista; private set => Set(ref _vista, value); }
    public string Titulo { get => _titulo; private set => Set(ref _titulo, value); }
    public bool PuedeVolver => _historial.Count > 0;
    public string NombreUsuario => _s.Usuario?.Nombre ?? "";
    public string RolUsuario => _s.Usuario?.RolNombre ?? _s.Usuario?.Rol ?? "";
    public string Iniciales => _s.Usuario?.Iniciales ?? "";

    public Comando IrACommand { get; }
    public Comando VolverCommand { get; }

    // ── INavegador ──────────────────────────────────────────────────
    public void Ir(object vista, string? titulo = null)
    {
        if (Vista is not null) _historial.Push((Vista, Titulo));
        Mostrar(vista, titulo);
    }

    public void Volver()
    {
        if (_historial.Count == 0) return;
        var (vista, titulo) = _historial.Pop();
        Mostrar(vista, titulo);
    }

    /// <summary>Cambia de sección del menú (borra el historial). Una sección sin permiso vuelve a Inicio.</summary>
    public void IrA(string seccion)
    {
        _historial.Clear();
        if (Menu.All(m => m.Clave != seccion)) seccion = "inicio";

        object vista = seccion switch
        {
            "caja" => _caja ??= new CajaViewModel(_s),   // la misma caja: la venta en curso no se pierde al ir a otra sección
            "ventas" => new VentasViewModel(_s),
            "productos" => new ProductosViewModel(_s),
            "categorias" => new CategoriasViewModel(_s),
            "entradas" => new EntradasViewModel(_s),
            "reportes" => new ReportesViewModel(_s),
            "usuarios" => new UsuariosViewModel(_s),
            "roles" => new RolesViewModel(_s),
            "ajustes" => new AjustesViewModel(_s),
            _ => new InicioViewModel(_s),
        };

        foreach (var m in Menu) m.Activo = m.Clave == seccion;
        Mostrar(vista, null);
    }

    private void Mostrar(object vista, string? titulo)
    {
        if (vista is VistaBase vb)
        {
            vb.Cargar();      // siempre datos frescos al entrar (o volver) a una pantalla
            titulo ??= vb.Titulo;
        }
        Vista = vista;
        Titulo = titulo ?? "";
        Avisar(nameof(PuedeVolver));
        VolverCommand.Refrescar();
    }

    /// <summary>Recarga la pantalla actual (botón actualizar de la barra superior).</summary>
    public void Refrescar()
    {
        if (Vista is VistaBase vb) vb.Cargar();
    }
}
