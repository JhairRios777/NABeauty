using NABeauty.Core.Seguridad;

namespace NABeauty.Core.Entidades;

/// <summary>Rol de usuario editable. "admin" es de sistema: protegido.</summary>
public sealed class Rol
{
    public long Id { get; set; }
    public string Clave { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    /// <summary>Rol de sistema (Administrador): no se edita ni se elimina.</summary>
    public bool Sistema { get; set; }
    /// <summary>Usuarios que tienen este rol (calculado).</summary>
    public long Usuarios { get; set; }
    public List<Permiso> Permisos { get; set; } = [];

    public string UsuariosTexto => Usuarios == 1 ? "1 usuario" : $"{Usuarios} usuarios";
    public string PermisosTexto => Sistema ? "Todos los permisos" : $"{Permisos.Count} de {Seguridad.Permisos.Catalogo.Count} permisos";
}
