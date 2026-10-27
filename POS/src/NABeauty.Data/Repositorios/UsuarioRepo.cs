using NABeauty.Core.Entidades;
using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>Usuarios del sistema e intentos fallidos de ingreso.</summary>
public sealed class UsuarioRepo(UnidadTrabajo u)
{
    private const string Select = "SELECT us.*, r.nombre AS rol_nombre FROM usuarios us LEFT JOIN roles r ON r.clave = us.rol";

    public Usuario? PorLogin(string login) => u.Uno<Usuario>(Select + " WHERE us.usuario = @login", new { login });
    public Usuario? PorId(long id) => u.Uno<Usuario>(Select + " WHERE us.id = @id", new { id });
    /// <summary>Usuario para la sesión: con los permisos de su rol y sin el hash en memoria más tiempo del necesario.</summary>
    public Usuario PorIdSinHash(long id)
    {
        var x = PorId(id)!;
        x.PasswordHash = "";
        x.PermisosRol = new RolRepo(u).PermisosDe(x.Rol);
        return x;
    }

    public List<Usuario> Todos() => u.Lista<Usuario>(Select + " ORDER BY us.activo DESC, us.nombre");

    public bool LoginExiste(string login, long excepto) =>
        u.Escalar<long>("SELECT COUNT(*) FROM usuarios WHERE usuario = @login AND id <> @excepto", new { login, excepto }) > 0;

    /// <summary>Usuario nuevo con contraseña temporal. El uuid lo identifica al sincronizarlo con la web.</summary>
    public long Crear(string nombre, string login, string hash, string rol) => u.Insertar(
        "INSERT INTO usuarios (uuid, nombre, usuario, password_hash, rol, debe_cambiar_password) VALUES (@uuid, @nombre, @login, @hash, @rol, 1)",
        new { uuid = Guid.NewGuid().ToString(), nombre, login, hash, rol });

    public void Actualizar(long id, string nombre, string login, string rol, bool activo) => u.Ejecutar(
        "UPDATE usuarios SET nombre = @nombre, usuario = @login, rol = @rol, activo = @activo, actualizado = datetime('now','localtime') WHERE id = @id",
        new { id, nombre, login, rol, activo });

    public void CambiarPassword(long id, string hash, bool temporal) => u.Ejecutar(
        "UPDATE usuarios SET password_hash = @hash, debe_cambiar_password = @temporal, actualizado = datetime('now','localtime') WHERE id = @id", new { id, hash, temporal });

    public void RegistrarAcceso(long id) =>
        u.Ejecutar("UPDATE usuarios SET ultimo_acceso = datetime('now','localtime') WHERE id = @id", new { id });

    public long AdminsActivos(long excepto) =>
        u.Escalar<long>("SELECT COUNT(*) FROM usuarios WHERE rol = 'admin' AND activo = 1 AND id <> @excepto", new { excepto });

    // Bloqueo por intentos
    public long FallosRecientes(string login, int minutos) => u.Escalar<long>(
        "SELECT COUNT(*) FROM login_intentos WHERE usuario = @login AND creado >= datetime('now','localtime', @ventana)",
        new { login, ventana = $"-{minutos} minutes" });

    public string? PrimerFalloReciente(string login, int minutos) => u.Escalar<string?>(
        "SELECT MIN(creado) FROM login_intentos WHERE usuario = @login AND creado >= datetime('now','localtime', @ventana)",
        new { login, ventana = $"-{minutos} minutes" });

    public void RegistrarFallo(string login) => u.Ejecutar("INSERT INTO login_intentos (usuario) VALUES (@login)", new { login });
    /// <summary>Logins con el acceso bloqueado ahora mismo (para marcarlos en la pantalla Usuarios).</summary>
    public HashSet<string> Bloqueados(int maxIntentos, int minutos) =>
    [
        .. u.Columna<string>("""
            SELECT usuario FROM login_intentos WHERE creado >= datetime('now','localtime', @ventana)
             GROUP BY usuario COLLATE NOCASE HAVING COUNT(*) >= @maxIntentos
            """, new { ventana = $"-{minutos} minutes", maxIntentos }).Select(x => x.ToLowerInvariant())
    ];

    public void LimpiarFallos(string login) => u.Ejecutar("DELETE FROM login_intentos WHERE usuario = @login", new { login });
}
