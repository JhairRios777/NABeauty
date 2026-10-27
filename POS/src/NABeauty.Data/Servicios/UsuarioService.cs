using System.Text.RegularExpressions;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;

namespace NABeauty.Data.Servicios;

/// <summary>
/// Usuarios del POS (que viajan a la web con sus permisos web). Reglas:
///  * Al crear o restablecer se genera una contraseña TEMPORAL: el usuario la cambia al entrar.
///  * Nadie se desactiva ni se quita el rol de administrador a sí mismo.
///  * Siempre queda al menos un administrador activo.
/// </summary>
public sealed partial class UsuarioService(Contexto ctx)
{
    public List<Usuario> Listar()
    {
        ctx.Exigir(Permiso.GestionarUsuarios);
        using var u = ctx.Bd.Abrir();
        var repo = new UsuarioRepo(u);
        var (max, min) = LoginService.Limites(new ConfigRepo(u));
        var bloqueados = repo.Bloqueados(max, min);
        var lista = repo.Todos();
        foreach (var x in lista)
        {
            x.PasswordHash = "";
            x.Bloqueado = bloqueados.Contains(x.UsuarioLogin.ToLowerInvariant());
        }
        return lista;
    }

    /// <summary>Roles para elegir. Solo un administrador puede dar el rol de administrador.</summary>
    public List<Rol> Roles()
    {
        ctx.Exigir(Permiso.GestionarUsuarios);
        using var u = ctx.Bd.Abrir();
        var roles = new RolRepo(u).Todos();
        return ctx.Usuario.EsAdmin ? roles : [.. roles.Where(r => !r.Sistema)];
    }

    /// <returns>La contraseña temporal (se muestra UNA vez para entregarla).</returns>
    public string Crear(string? nombre, string? login, string? rol)
    {
        ctx.Exigir(Permiso.GestionarUsuarios);
        var (n, l) = Validar(nombre, login);
        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new UsuarioRepo(u);
        ValidarRol(u, rol);
        if (repo.LoginExiste(l, 0)) throw new ReglaNegocioException($"Ya existe el usuario «{l}».");
        var temporal = PasswordHasher.Temporal();
        var id = repo.Crear(n, l, PasswordHasher.Hash(temporal), rol!);
        var uuid = repo.PorId(id)!.Uuid;
        new SyncRepo(u).Encolar("usuario", uuid, "crear");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "usuario.crear", "usuario", id, new { login = l, rol });
        u.Confirmar();
        return temporal;
    }

    public void Actualizar(long id, string? nombre, string? login, string? rol, bool activo)
    {
        ctx.Exigir(Permiso.GestionarUsuarios);
        var (n, l) = Validar(nombre, login);
        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new UsuarioRepo(u);
        var actual = repo.PorId(id) ?? throw new ReglaNegocioException("El usuario ya no existe.");
        ValidarRol(u, rol);
        if (actual.EsAdmin && rol != Permisos.RolAdmin && !ctx.Usuario.EsAdmin)
            throw new ReglaNegocioException("Solo un administrador puede cambiar el rol de otro administrador.");
        if (repo.LoginExiste(l, id)) throw new ReglaNegocioException($"Ya existe el usuario «{l}».");
        if (id == ctx.UsuarioId && !activo) throw new ReglaNegocioException("No puedes desactivar tu propio usuario.");
        if (id == ctx.UsuarioId && actual.EsAdmin && rol != Permisos.RolAdmin)
            throw new ReglaNegocioException("No puedes quitarte el rol de administrador a ti mismo.");
        var dejaDeSerAdminActivo = actual.EsAdmin && actual.Activo && (rol != Permisos.RolAdmin || !activo);
        if (dejaDeSerAdminActivo && repo.AdminsActivos(id) == 0)
            throw new ReglaNegocioException("Debe quedar al menos un administrador activo.");

        repo.Actualizar(id, n, l, rol!, activo);
        new SyncRepo(u).Encolar("usuario", actual.Uuid, "actualizar");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "usuario.editar", "usuario", id, new { login = l, rol, activo });
        u.Confirmar();
    }

    /// <returns>La nueva contraseña temporal.</returns>
    public string RestablecerPassword(long id)
    {
        ctx.Exigir(Permiso.GestionarUsuarios);
        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new UsuarioRepo(u);
        var x = repo.PorId(id) ?? throw new ReglaNegocioException("El usuario ya no existe.");
        if (x.EsAdmin && !ctx.Usuario.EsAdmin) throw new ReglaNegocioException("Solo un administrador puede restablecer la contraseña de otro administrador.");
        var temporal = PasswordHasher.Temporal();
        repo.CambiarPassword(id, PasswordHasher.Hash(temporal), temporal: true);
        repo.LimpiarFallos(x.UsuarioLogin);
        new SyncRepo(u).Encolar("usuario", x.Uuid, "actualizar");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "usuario.restablecer", "usuario", id);
        u.Confirmar();
        return temporal;
    }

    public void Desbloquear(long id)
    {
        ctx.Exigir(Permiso.GestionarUsuarios);
        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new UsuarioRepo(u);
        var x = repo.PorId(id) ?? throw new ReglaNegocioException("El usuario ya no existe.");
        repo.LimpiarFallos(x.UsuarioLogin);
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "usuario.desbloquear", "usuario", id);
        u.Confirmar();
    }

    private static (string Nombre, string Login) Validar(string? nombre, string? login)
    {
        var n = Core.Dominio.Validar.Texto(nombre, 80) ?? throw new ReglaNegocioException("Escribe el nombre de la persona.");
        if (n.Length < 3) throw new ReglaNegocioException("El nombre es muy corto.");
        var l = (login ?? "").Trim().ToLowerInvariant();
        if (!LoginValido().IsMatch(l))
            throw new ReglaNegocioException("El usuario debe tener de 3 a 30 caracteres: letras sin tilde, números, punto, guion o guion bajo.");
        return (n, l);
    }

    private void ValidarRol(UnidadTrabajo u, string? rol)
    {
        if (string.IsNullOrEmpty(rol) || !new RolRepo(u).Existe(rol)) throw new ReglaNegocioException("Elige un rol válido.");
        if (rol == Permisos.RolAdmin && !ctx.Usuario.EsAdmin) throw new ReglaNegocioException("Solo un administrador puede dar el rol de administrador.");
    }

    [GeneratedRegex("^[a-z0-9._-]{3,30}$")] private static partial Regex LoginValido();
}
