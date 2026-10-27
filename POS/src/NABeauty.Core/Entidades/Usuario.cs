using NABeauty.Core.Seguridad;

namespace NABeauty.Core.Entidades;

public sealed class Usuario
{
    public long Id { get; set; }
    /// <summary>Identificador para sincronizar con la tienda en línea (el usuario se crea aquí y viaja a la web).</summary>
    public string Uuid { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string UsuarioLogin { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    /// <summary>Clave del rol (roles.clave).</summary>
    public string Rol { get; set; } = "cajera";
    /// <summary>Nombre legible del rol (JOIN con roles).</summary>
    public string? RolNombre { get; set; }
    public bool Activo { get; set; } = true;
    public bool DebeCambiarPassword { get; set; }
    public string? UltimoAcceso { get; set; }

    /// <summary>Permisos del rol, cargados al iniciar sesión. Cambios en el rol aplican en el próximo ingreso.</summary>
    public HashSet<Permiso> PermisosRol { get; set; } = [];
    /// <summary>Solo en el listado: tiene el acceso bloqueado por intentos fallidos.</summary>
    public bool Bloqueado { get; set; }

    public bool EsAdmin => Rol == Permisos.RolAdmin;
    /// <summary>El administrador tiene todo (incluido GestionarRoles); el resto, lo que diga su rol.</summary>
    public bool Puede(Permiso p) => Activo && (EsAdmin || PermisosRol.Contains(p));
    public string Iniciales => string.Concat(Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
    public string EstadoTexto => !Activo ? "Inactivo" : Bloqueado ? "Bloqueado" : "Activo";
    public string Estado => !Activo ? "inactivo" : Bloqueado ? "vencido" : "activo";
}
