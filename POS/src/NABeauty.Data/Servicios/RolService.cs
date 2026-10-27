using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;

namespace NABeauty.Data.Servicios;

/// <summary>
/// Roles editables. El Administrador es de sistema (tiene todo, no se toca). "Gestionar roles"
/// nunca se puede asignar a otro rol: así nadie se da más permisos de los que tiene.
/// Los cambios aplican a cada usuario en su próximo ingreso (POS) o en su próxima petición (web).
/// </summary>
public sealed class RolService(Contexto ctx)
{
    public List<Rol> Listar()
    {
        ctx.Exigir(Permiso.GestionarRoles);
        using var u = ctx.Bd.Abrir();
        return new RolRepo(u).Todos();
    }

    /// <summary>Crea (id 0) o actualiza un rol con sus permisos. Devuelve el id.</summary>
    public long Guardar(long id, string? nombre, string? descripcion, IEnumerable<Permiso> permisos)
    {
        ctx.Exigir(Permiso.GestionarRoles);
        var n = Validar.Texto(nombre, 40) ?? throw new ReglaNegocioException("Escribe el nombre del rol.");
        if (n.Length < 3) throw new ReglaNegocioException("El nombre del rol es muy corto.");
        var lista = permisos.Distinct().ToList();
        if (lista.Any(p => !Permisos.EsAsignable(p))) throw new ReglaNegocioException("«Gestionar roles» es exclusivo del Administrador.");
        if (lista.Count == 0) throw new ReglaNegocioException("Marca al menos un permiso.");

        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new RolRepo(u);
        if (repo.NombreExiste(n, id)) throw new ReglaNegocioException($"Ya existe el rol «{n}».");
        string clave;
        if (id == 0)
        {
            var base_ = Slug.Crear(n, 30).Replace('-', '_');
            clave = base_;
            for (var i = 2; repo.Existe(clave); i++) clave = $"{base_}_{i}";
            id = repo.Crear(clave, n, Validar.Texto(descripcion, 200));
        }
        else
        {
            var r = repo.PorId(id) ?? throw new ReglaNegocioException("El rol ya no existe.");
            if (r.Sistema) throw new ReglaNegocioException("El rol Administrador no se modifica: siempre tiene todos los permisos.");
            clave = r.Clave;
            repo.Actualizar(id, n, Validar.Texto(descripcion, 200));
        }
        repo.ReemplazarPermisos(id, lista);
        new SyncRepo(u).Encolar("rol", clave, "actualizar");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "rol.guardar", "rol", id, new { n, permisos = lista.Select(p => p.ToString()) });
        u.Confirmar();
        return id;
    }

    /// <summary>Los triggers impiden borrar el Administrador o un rol con usuarios (con mensaje claro).</summary>
    public void Eliminar(long id)
    {
        ctx.Exigir(Permiso.GestionarRoles);
        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new RolRepo(u);
        var r = repo.PorId(id) ?? throw new ReglaNegocioException("El rol ya no existe.");
        repo.Eliminar(id);
        new SyncRepo(u).Encolar("rol", r.Clave, "anular");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "rol.eliminar", "rol", id, new { r.Nombre });
        u.Confirmar();
    }
}
