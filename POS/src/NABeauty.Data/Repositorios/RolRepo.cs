using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>Roles editables y sus permisos (tablas roles / rol_permisos).</summary>
public sealed class RolRepo(UnidadTrabajo u)
{
    private const string Select = """
        SELECT r.*, (SELECT COUNT(*) FROM usuarios us WHERE us.rol = r.clave) AS usuarios
          FROM roles r
        """;

    /// <summary>Todos los roles con su número de usuarios y sus permisos (dos consultas, sin N+1).</summary>
    public List<Rol> Todos()
    {
        var roles = u.Lista<Rol>(Select + " ORDER BY r.sistema DESC, r.nombre");
        var filas = u.Lista<RolPermisoFila>("SELECT rol_id, permiso FROM rol_permisos");
        foreach (var r in roles)
            r.Permisos = [.. filas.Where(f => f.RolId == r.Id).Select(f => Parse(f.Permiso)).OfType<Permiso>().Order()];
        return roles;
    }

    public Rol? PorId(long id)
    {
        var r = u.Uno<Rol>(Select + " WHERE r.id = @id", new { id });
        if (r is not null) r.Permisos = [.. PermisosDe(r.Clave).Order()];
        return r;
    }

    public bool Existe(string clave) => u.Escalar<long>("SELECT COUNT(*) FROM roles WHERE clave = @clave", new { clave }) > 0;

    public bool NombreExiste(string nombre, long excepto) =>
        u.Escalar<long>("SELECT COUNT(*) FROM roles WHERE nombre = @nombre AND id <> @excepto", new { nombre, excepto }) > 0;

    /// <summary>Permisos de un rol por su clave. Un valor desconocido en la tabla se ignora (nunca da acceso de más).</summary>
    public HashSet<Permiso> PermisosDe(string clave) =>
    [
        .. u.Columna<string>("""
            SELECT rp.permiso FROM rol_permisos rp JOIN roles r ON r.id = rp.rol_id WHERE r.clave = @clave
            """, new { clave }).Select(Parse).OfType<Permiso>()
    ];

    public long Crear(string clave, string nombre, string? descripcion) => u.Insertar(
        "INSERT INTO roles (clave, nombre, descripcion) VALUES (@clave, @nombre, @descripcion)", new { clave, nombre, descripcion });

    public void Actualizar(long id, string nombre, string? descripcion) => u.Ejecutar(
        "UPDATE roles SET nombre = @nombre, descripcion = @descripcion WHERE id = @id", new { id, nombre, descripcion });

    public void ReemplazarPermisos(long id, IEnumerable<Permiso> permisos)
    {
        u.Ejecutar("DELETE FROM rol_permisos WHERE rol_id = @id", new { id });
        foreach (var p in permisos)
            u.Ejecutar("INSERT INTO rol_permisos (rol_id, permiso) VALUES (@id, @permiso)", new { id, permiso = p.ToString() });
    }

    /// <summary>Los triggers impiden borrar el rol de sistema o un rol con usuarios.</summary>
    public void Eliminar(long id) => u.Ejecutar("DELETE FROM roles WHERE id = @id", new { id });

    private static Permiso? Parse(string texto) => Enum.TryParse<Permiso>(texto, out var p) ? p : null;

    private sealed class RolPermisoFila
    {
        public long RolId { get; set; }
        public string Permiso { get; set; } = "";
    }
}
