using System.Globalization;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>
/// Usuarios: lista + formulario en la misma pantalla. Al crear (o restablecer) se muestra la
/// contraseña temporal UNA vez para entregarla; el usuario la cambia al entrar.
/// </summary>
public sealed class UsuariosViewModel : VistaBase
{
    private Usuario? _sel;
    private string _nombre = "", _login = "";
    private Opcion? _rol;
    private bool _activo = true;
    private string _temporal = "";

    public UsuariosViewModel(Sesion s) : base(s)
    {
        NuevoCommand = new Comando(Nuevo);
        GuardarCommand = new Comando(() => Guardar());
        RestablecerCommand = new Comando(() => Restablecer(), () => _sel is not null);
        DesbloquearCommand = new Comando(() => Desbloquear(), () => _sel is { Bloqueado: true });
        CerrarTemporalCommand = new Comando(() => ClaveTemporal = "");
    }

    public override string Titulo => "Usuarios";

    public List<Usuario> Lista { get; private set; } = [];
    public string Conteo => $"{Lista.Count(u => u.Activo)} activo(s) de {Lista.Count}";
    public List<Opcion> Roles { get; private set; } = [];

    public Usuario? Seleccionado
    {
        get => _sel;
        set
        {
            if (!Set(ref _sel, value) || value is null) return;
            _nombre = value.Nombre; _login = value.UsuarioLogin; _activo = value.Activo;
            _rol = Roles.FirstOrDefault(r => r.Clave == value.Rol);
            AvisarFormulario();
        }
    }

    public string Nombre { get => _nombre; set => Set(ref _nombre, value); }
    public string Login { get => _login; set => Set(ref _login, value); }
    public Opcion? Rol { get => _rol; set => Set(ref _rol, value); }
    public bool Activo { get => _activo; set => Set(ref _activo, value); }
    public bool EsNuevo => _sel is null;
    public bool EsYo => _sel?.Id == S.Usuario?.Id;
    public string TituloFormulario => EsNuevo ? "Nuevo usuario" : $"Editar: {_sel!.Nombre}";
    public string TextoBoton => EsNuevo ? "Crear usuario" : "Guardar cambios";
    public string UltimoAcceso => _sel?.UltimoAcceso is { } f ? $"Último ingreso: {Core.Dominio.Formato.Fecha(f, conHora: true)}" : EsNuevo ? "" : "Nunca ha ingresado";
    public bool Bloqueado => _sel?.Bloqueado == true;

    /// <summary>Contraseña temporal recién generada (se muestra en un recuadro hasta cerrarlo).</summary>
    public string ClaveTemporal { get => _temporal; private set { if (Set(ref _temporal, value)) Avisar(nameof(HayClaveTemporal)); } }
    public bool HayClaveTemporal => _temporal.Length > 0;
    public string ClaveTemporalPara { get; private set; } = "";

    public Comando NuevoCommand { get; }
    public Comando GuardarCommand { get; }
    public Comando RestablecerCommand { get; }
    public Comando DesbloquearCommand { get; }
    public Comando CerrarTemporalCommand { get; }

    public override void Cargar() => Recargar(_sel?.Id);

    private void Recargar(long? seleccionar)
    {
        var svc = new UsuarioService(Ctx);
        Roles = [.. svc.Roles().Select(r => new Opcion(r.Clave, r.Nombre))];
        Lista = svc.Listar();
        Avisar(nameof(Roles), nameof(Lista), nameof(Conteo));
        var sel = Lista.FirstOrDefault(u => u.Id == seleccionar);
        if (sel is null) Nuevo(); else { _sel = null; Seleccionado = sel; }
    }

    public void Nuevo()
    {
        _sel = null;
        _nombre = ""; _login = ""; _activo = true;
        _rol = Roles.FirstOrDefault(r => r.Clave == "cajera") ?? Roles.FirstOrDefault();
        AvisarFormulario();
    }

    private void AvisarFormulario()
    {
        Avisar(nameof(Seleccionado), nameof(Nombre), nameof(Login), nameof(Rol), nameof(Activo), nameof(EsNuevo), nameof(EsYo),
               nameof(TituloFormulario), nameof(TextoBoton), nameof(UltimoAcceso), nameof(Bloqueado));
        RestablecerCommand.Refrescar();
        DesbloquearCommand.Refrescar();
    }

    public bool Guardar()
    {
        var svc = new UsuarioService(Ctx);
        if (EsNuevo)
        {
            string? temporal = null;
            var login = _login;
            if (!Intentar(() => temporal = svc.Crear(_nombre, _login, _rol?.Clave))) return false;
            ClaveTemporalPara = $"Usuario «{login.Trim().ToLowerInvariant()}» creado. Entrégale esta contraseña temporal; la cambiará al entrar:";
            Avisar(nameof(ClaveTemporalPara));
            ClaveTemporal = temporal!;
            Recargar(svc.Listar().FirstOrDefault(u => u.UsuarioLogin == login.Trim().ToLowerInvariant())?.Id);
            return true;
        }
        var id = _sel!.Id;
        if (!Intentar(() => svc.Actualizar(id, _nombre, _login, _rol?.Clave, _activo))) return false;
        Recargar(id);
        Dlg.Info("Cambios guardados. Los permisos nuevos aplican en el próximo ingreso de la persona.");
        return true;
    }

    public bool Restablecer()
    {
        if (_sel is not { } u) return false;
        if (!Dlg.Confirmar($"¿Generar una contraseña temporal nueva para {u.Nombre}? La actual deja de funcionar.")) return false;
        string? temporal = null;
        if (!Intentar(() => temporal = new UsuarioService(Ctx).RestablecerPassword(u.Id))) return false;
        ClaveTemporalPara = $"Nueva contraseña temporal de «{u.UsuarioLogin}» (la cambiará al entrar):";
        Avisar(nameof(ClaveTemporalPara));
        ClaveTemporal = temporal!;
        Recargar(u.Id);
        return true;
    }

    public bool Desbloquear()
    {
        if (_sel is not { } u) return false;
        if (!Intentar(() => new UsuarioService(Ctx).Desbloquear(u.Id))) return false;
        Recargar(u.Id);
        return true;
    }
}

/// <summary>Casilla de un permiso en la pantalla Roles.</summary>
public sealed class PermisoCheck(PermisoInfo info, bool marcado) : Observable
{
    private bool _marcado = marcado;
    public Permiso Permiso => info.Permiso;
    public string Nombre => info.Nombre;
    public string Detalle => info.Detalle;
    public bool Marcado { get => _marcado; set => Set(ref _marcado, value); }
}

public sealed record GrupoPermisos(string Nombre, List<PermisoCheck> Permisos);

/// <summary>
/// Roles y permisos (solo Administrador). El rol Administrador se ve pero no se edita.
/// Los permisos "Panel web" deciden qué puede hacer el usuario en la tienda en línea.
/// </summary>
public sealed class RolesViewModel : VistaBase
{
    private Rol? _sel;
    private string _nombre = "", _descripcion = "";

    public RolesViewModel(Sesion s) : base(s)
    {
        NuevoCommand = new Comando(Nuevo);
        GuardarCommand = new Comando(() => Guardar(), () => !EsSistema);
        EliminarCommand = new Comando(() => Eliminar(), () => _sel is { Sistema: false });
        TodosCommand = new Comando(p => { if (p is GrupoPermisos g) { var todos = g.Permisos.All(x => x.Marcado); g.Permisos.ForEach(x => x.Marcado = !todos); } });
    }

    public override string Titulo => "Roles y permisos";

    public List<Rol> Lista { get; private set; } = [];
    public List<GrupoPermisos> Grupos { get; private set; } = [];

    public Rol? Seleccionado
    {
        get => _sel;
        set
        {
            if (!Set(ref _sel, value) || value is null) return;
            _nombre = value.Nombre; _descripcion = value.Descripcion ?? "";
            ArmarGrupos(value.Sistema ? [.. Permisos.Catalogo.Select(c => c.Permiso)] : value.Permisos);
        }
    }
    public string Nombre { get => _nombre; set => Set(ref _nombre, value); }
    public string Descripcion { get => _descripcion; set => Set(ref _descripcion, value); }
    public bool EsSistema => _sel?.Sistema == true;
    public bool Editable => !EsSistema;
    public bool EsNuevo => _sel is null;
    public string TituloFormulario => EsNuevo ? "Nuevo rol" : EsSistema ? "Administrador (rol del sistema)" : $"Editar: {_sel!.Nombre}";
    public string Marcados => $"{Grupos.Sum(g => g.Permisos.Count(p => p.Marcado))} de {Permisos.Catalogo.Count} permisos";

    public Comando NuevoCommand { get; }
    public Comando GuardarCommand { get; }
    public Comando EliminarCommand { get; }
    public Comando TodosCommand { get; }

    public override void Cargar() => Recargar(_sel?.Id);

    private void Recargar(long? seleccionar)
    {
        Lista = new RolService(Ctx).Listar();
        Avisar(nameof(Lista));
        var sel = Lista.FirstOrDefault(r => r.Id == seleccionar);
        if (sel is null) Nuevo(); else { _sel = null; Seleccionado = sel; }
    }

    public void Nuevo()
    {
        _sel = null;
        _nombre = ""; _descripcion = "";
        ArmarGrupos([Permiso.VerInicio]);
    }

    private void ArmarGrupos(IEnumerable<Permiso> marcados)
    {
        var set = marcados.ToHashSet();
        Grupos = [.. Permisos.Catalogo.GroupBy(c => c.Grupo)
            .Select(g => new GrupoPermisos(g.Key, [.. g.Select(c => new PermisoCheck(c, set.Contains(c.Permiso)))]))];
        foreach (var p in Grupos.SelectMany(g => g.Permisos)) p.PropertyChanged += (_, _) => Avisar(nameof(Marcados));
        Avisar(nameof(Seleccionado), nameof(Nombre), nameof(Descripcion), nameof(EsSistema), nameof(Editable), nameof(EsNuevo),
               nameof(TituloFormulario), nameof(Grupos), nameof(Marcados));
        GuardarCommand.Refrescar();
        EliminarCommand.Refrescar();
    }

    public bool Guardar()
    {
        var permisos = Grupos.SelectMany(g => g.Permisos).Where(p => p.Marcado).Select(p => p.Permiso).ToList();
        long id = 0;
        if (!Intentar(() => id = new RolService(Ctx).Guardar(_sel?.Id ?? 0, _nombre, _descripcion, permisos))) return false;
        Recargar(id);
        Dlg.Info("Rol guardado. Los cambios aplican a cada usuario en su próximo ingreso.");
        return true;
    }

    public bool Eliminar()
    {
        if (_sel is not { } r) return false;
        if (!Dlg.Confirmar($"¿Eliminar el rol «{r.Nombre}»?")) return false;
        if (!Intentar(() => new RolService(Ctx).Eliminar(r.Id))) return false;
        Recargar(null);
        return true;
    }
}

/// <summary>Ajustes: negocio y recibo, seguridad del ingreso, tienda en línea y respaldos.</summary>
public sealed class AjustesViewModel : VistaBase
{
    private Negocio _n = new();
    private string _intentos = "", _minutos = "", _url = "", _token = "";

    public AjustesViewModel(Sesion s) : base(s)
    {
        GuardarNegocioCommand = new Comando(() => GuardarNegocio());
        GuardarSeguridadCommand = new Comando(() => GuardarSeguridad());
        GuardarWebCommand = new Comando(() => GuardarWeb());
        RespaldarCommand = new Comando(() => Respaldar(), () => S.CarpetaRespaldos is not null);
        AbrirRespaldosCommand = new Comando(() => { if (S.CarpetaRespaldos is { } c) Dlg.Abrir(c); }, () => S.CarpetaRespaldos is not null);
        PruebaReciboCommand = new Comando(() => PruebaRecibo(), () => S.Impresion is not null);
    }

    public override string Titulo => "Ajustes";

    // Negocio y recibo
    public string NegocioNombre { get => _n.Nombre; set { _n.Nombre = value; Avisar(); } }
    public string Rtn { get => _n.Rtn ?? ""; set { _n.Rtn = value; Avisar(); } }
    public string Telefono { get => _n.Telefono ?? ""; set { _n.Telefono = value; Avisar(); } }
    public string Direccion { get => _n.Direccion ?? ""; set { _n.Direccion = value; Avisar(); } }
    public string MensajeRecibo { get => _n.MensajeRecibo ?? ""; set { _n.MensajeRecibo = value; Avisar(); } }
    public bool Papel58 { get => _n.AnchoRecibo == 58; set { if (value) { _n.AnchoRecibo = 58; Avisar(nameof(Papel58), nameof(Papel80)); } } }
    public bool Papel80 { get => _n.AnchoRecibo == 80; set { if (value) { _n.AnchoRecibo = 80; Avisar(nameof(Papel58), nameof(Papel80)); } } }
    public bool ImprimirAlCobrar { get => _n.ImprimirAlCobrar; set { _n.ImprimirAlCobrar = value; Avisar(); } }

    // Seguridad
    public string Intentos { get => _intentos; set => Set(ref _intentos, value); }
    public string Minutos { get => _minutos; set => Set(ref _minutos, value); }

    // Tienda en línea
    public string Url { get => _url; set => Set(ref _url, value); }
    public string Token { get => _token; set => Set(ref _token, value); }
    public string EstadoWeb { get; private set; } = "";
    public string TokenTexto { get; private set; } = "";

    // Respaldos
    public string CarpetaRespaldos => S.CarpetaRespaldos ?? "—";
    public List<string> Respaldos { get; private set; } = [];
    public string RespaldosTexto { get; private set; } = "";

    public Comando GuardarNegocioCommand { get; }
    public Comando GuardarSeguridadCommand { get; }
    public Comando GuardarWebCommand { get; }
    public Comando RespaldarCommand { get; }
    public Comando AbrirRespaldosCommand { get; }
    public Comando PruebaReciboCommand { get; }

    public override void Cargar()
    {
        var svc = new AjustesService(Ctx);
        _n = svc.Negocio();
        var seg = svc.Seguridad();
        _intentos = seg.MaxIntentos.ToString(CultureInfo.InvariantCulture);
        _minutos = seg.MinutosBloqueo.ToString(CultureInfo.InvariantCulture);
        var web = svc.Web();
        _url = web.Url ?? "";
        _token = "";
        TokenTexto = web.TieneToken ? "Hay un token guardado. Déjalo vacío para conservarlo." : "Todavía no hay token.";
        EstadoWeb = string.IsNullOrEmpty(web.Url)
            ? "Sin conectar. El POS funciona igual sin internet; los cambios esperan en la cola."
            : $"{web.Pendientes} cambio(s) esperando para enviarse" + (web.UltimaSync is null ? " · aún no se ha sincronizado" : $" · última vez {Core.Dominio.Formato.Fecha(web.UltimaSync, conHora: true)}");
        CargarRespaldos();
        Avisar(string.Empty);
    }

    private void CargarRespaldos()
    {
        Respaldos = S.CarpetaRespaldos is { } c && Directory.Exists(c)
            ? [.. Directory.GetFiles(c, "nabeauty-*.db").OrderByDescending(f => f).Take(8).Select(f =>
                $"{Path.GetFileName(f)} · {new FileInfo(f).Length / 1024.0:N0} KB")]
            : [];
        RespaldosTexto = Respaldos.Count == 0 ? "Todavía no hay respaldos." : "Se crea uno automático cada día al abrir el POS y se guardan los últimos 30.";
        Avisar(nameof(Respaldos), nameof(RespaldosTexto));
    }

    public bool GuardarNegocio()
    {
        if (!Intentar(() => new AjustesService(Ctx).GuardarNegocio(_n))) return false;
        Dlg.Info("Datos del negocio guardados.");
        return true;
    }

    public bool GuardarSeguridad()
    {
        if (!int.TryParse(_intentos.Trim(), out var i) || !int.TryParse(_minutos.Trim(), out var m))
        {
            Dlg.Error("Escribe números enteros en intentos y minutos.");
            return false;
        }
        if (!Intentar(() => new AjustesService(Ctx).GuardarSeguridad(i, m))) return false;
        Dlg.Info("Seguridad del ingreso guardada.");
        return true;
    }

    public bool GuardarWeb()
    {
        if (!Intentar(() => new AjustesService(Ctx).GuardarWeb(_url, _token))) return false;
        Cargar();
        Dlg.Info("Conexión guardada. La sincronización automática se activa en la próxima etapa.");
        return true;
    }

    public bool Respaldar()
    {
        if (S.CarpetaRespaldos is not { } c) return false;
        string? ruta = null;
        if (!Intentar(() => ruta = new AjustesService(Ctx).Respaldar(c))) return false;
        CargarRespaldos();
        Dlg.Info($"Respaldo creado: {Path.GetFileName(ruta)}");
        return true;
    }

    private void PruebaRecibo()
    {
        var lineas = new List<VentaLinea>
        {
            new() { ProductoNombre = "Labial Mate", VarianteNombre = "Rojo Pasión", Cantidad = 2, PrecioUnitario = 25000, Subtotal = 50000 },
            new() { ProductoNombre = "Sérum Vitamina C", VarianteNombre = "Única", Cantidad = 1, PrecioUnitario = 42000, Subtotal = 42000 },
        };
        S.Impresion?.ImprimirRecibo(new Recibo(_n, "PRUEBA", DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            S.Usuario?.Nombre ?? "", lineas, 92000, 2000, 90000, "efectivo", 100000, Anulada: false));
    }
}
