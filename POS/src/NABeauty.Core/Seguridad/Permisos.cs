namespace NABeauty.Core.Seguridad;

/// <summary>
/// Acciones protegidas del POS. Los nombres se guardan como texto en rol_permisos: no renombrar.
/// Los permisos Web* NO se usan dentro del POS: viajan a la tienda en línea con la sincronización
/// (web.acceso, web.productos, web.reportes, web.exportar) y deciden qué puede hacer el usuario en el panel web.
/// </summary>
public enum Permiso
{
    VerInicio,
    // Ventas
    Vender, AnularVentas, VerVentas,
    // Inventario
    VerProductos, GestionarProductos, RegistrarEntradas, AjustarInventario,
    // Reportes
    VerReportes, ExportarReportes, VerCostos,
    // Sistema
    GestionarUsuarios, Configurar,
    /// <summary>Crear/editar/eliminar roles. SOLO el rol Administrador: no se puede asignar a otro rol (evita escalar privilegios).</summary>
    GestionarRoles,
    // Panel web (se sincronizan con la tienda en línea)
    WebAcceso, WebProductos, WebReportes, WebExportar,
}

/// <summary>Descripción legible de un permiso, agrupada para la pantalla de Roles.</summary>
public sealed record PermisoInfo(Permiso Permiso, string Grupo, string Nombre, string Detalle);

/// <summary>
/// RBAC con roles editables. Los ROLES viven en la base de datos (tablas roles / rol_permisos);
/// aquí solo están el catálogo de permisos asignables, el rol de sistema "admin" (siempre tiene todo)
/// y los permisos con que se siembran Cajera y Socio en una base nueva.
/// </summary>
public static class Permisos
{
    public const string RolAdmin = "admin";

    public static readonly IReadOnlyList<PermisoInfo> Catalogo =
    [
        new(Permiso.VerInicio, "General", "Ver Inicio", "Panel con las cifras del negocio"),
        new(Permiso.Vender, "Ventas", "Vender (caja)", "Abrir caja y cobrar ventas"),
        new(Permiso.AnularVentas, "Ventas", "Anular ventas", "Anular una venta y devolver el stock"),
        new(Permiso.VerVentas, "Ventas", "Ver ventas e ingresos", "Historial de ventas y montos vendidos"),
        new(Permiso.VerProductos, "Inventario", "Ver productos", "Catálogo, precios y existencias"),
        new(Permiso.GestionarProductos, "Inventario", "Gestionar productos", "Crear y editar productos, precios y categorías"),
        new(Permiso.RegistrarEntradas, "Inventario", "Registrar entradas", "Mercadería que ingresa (suma stock)"),
        new(Permiso.AjustarInventario, "Inventario", "Ajustar inventario", "Corregir existencias con un motivo"),
        new(Permiso.VerReportes, "Reportes", "Ver reportes", "Ventas y entradas por día, semana y mes"),
        new(Permiso.ExportarReportes, "Reportes", "Exportar reportes", "Descargar en Excel o PDF"),
        new(Permiso.VerCostos, "Reportes", "Ver costos y ganancias", "Costo de lo vendido y margen"),
        new(Permiso.GestionarUsuarios, "Sistema", "Gestionar usuarios", "Crear usuarios y restablecer contraseñas"),
        new(Permiso.Configurar, "Sistema", "Ajustes", "Datos del negocio y conexión con la tienda en línea"),
        new(Permiso.WebAcceso, "Panel web", "Entrar al panel web", "Iniciar sesión en la tienda en línea (/admin)"),
        new(Permiso.WebProductos, "Panel web", "Editar productos en la web", "Productos, fotos y categorías desde el panel web"),
        new(Permiso.WebReportes, "Panel web", "Ver reportes en la web", "Ventas y entradas desde el panel web"),
        new(Permiso.WebExportar, "Panel web", "Descargar desde la web", "Excel y PDF desde el panel web"),
    ];

    /// <summary>Permisos que se pueden marcar en un rol (todo el catálogo; GestionarRoles nunca).</summary>
    public static bool EsAsignable(Permiso p) => Catalogo.Any(c => c.Permiso == p);

    /// <summary>Clave que entiende la tienda en línea para los permisos web (null si no es web).</summary>
    public static string? ClaveWeb(Permiso p) => p switch
    {
        Permiso.WebAcceso => "web.acceso",
        Permiso.WebProductos => "web.productos",
        Permiso.WebReportes => "web.reportes",
        Permiso.WebExportar => "web.exportar",
        _ => null,
    };

    /// <summary>Permisos iniciales de los roles sembrados en una base nueva (luego se editan en la pantalla Roles).</summary>
    public static readonly IReadOnlyDictionary<string, (string Nombre, string Descripcion, Permiso[] Permisos)> Predeterminados =
        new Dictionary<string, (string, string, Permiso[])>
        {
            ["cajera"] = ("Cajera", "Vende y consulta productos. No ve costos ni administra.",
            [
                Permiso.VerInicio, Permiso.Vender, Permiso.VerVentas, Permiso.VerProductos,
            ]),
            ["socio"] = ("Socio", "Consulta ventas y reportes, aquí y en el panel web. No vende ni edita.",
            [
                Permiso.VerInicio, Permiso.VerVentas, Permiso.VerProductos, Permiso.VerReportes, Permiso.ExportarReportes,
                Permiso.VerCostos, Permiso.WebAcceso, Permiso.WebReportes, Permiso.WebExportar,
            ]),
        };
}
