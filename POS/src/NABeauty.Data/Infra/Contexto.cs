using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;

namespace NABeauty.Data.Infra;

/// <summary>
/// Quién está operando. Los servicios lo reciben para la bitácora y para
/// volver a validar permisos (la UI esconde botones, pero la regla vive aquí).
/// </summary>
public sealed class Contexto(BaseDatos bd, Usuario usuario)
{
    public BaseDatos Bd { get; } = bd;
    public Usuario Usuario { get; } = usuario;
    public long UsuarioId => Usuario.Id;

    public void Exigir(Permiso permiso)
    {
        if (!Usuario.Puede(permiso))
            throw new ReglaNegocioException("No tienes permiso para esta acción.");
    }
}
