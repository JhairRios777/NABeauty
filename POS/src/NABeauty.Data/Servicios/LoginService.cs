using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;

namespace NABeauty.Data.Servicios;

/// <summary>
/// Ingreso al sistema.
/// - Mismo mensaje para usuario inexistente y contraseña incorrecta (no delata qué usuarios existen):
///   los intentos se cuentan por el texto escrito en "usuario", exista o no.
/// - N fallos en M minutos bloquean ese usuario. N y M se configuran en Ajustes (por defecto 5 y 15).
/// - Hashes viejos se actualizan solos al entrar (rehash).
/// </summary>
public sealed class LoginService(BaseDatos bd)
{
    public const int MaxIntentosPorDefecto = 5;
    public const int MinutosPorDefecto = 15;
    private static readonly string HashFalso = PasswordHasher.Hash(Guid.NewGuid().ToString());

    /// <summary>Límite de intentos y minutos de bloqueo vigentes (Ajustes), siempre dentro de un rango seguro.</summary>
    public static (int MaxIntentos, int Minutos) Limites(ConfigRepo cfg)
    {
        static int Leer(string? v, int defecto, int min, int max) =>
            int.TryParse(v, out var n) ? Math.Clamp(n, min, max) : defecto;
        return (Leer(cfg.Get("login_max_intentos"), MaxIntentosPorDefecto, 3, 20),
                Leer(cfg.Get("login_bloqueo_min"), MinutosPorDefecto, 1, 1440));
    }

    public Usuario Ingresar(string login, string password)
    {
        login = (login ?? "").Trim();
        if (login.Length == 0 || string.IsNullOrEmpty(password))
            throw new ReglaNegocioException("Ingresa tu usuario y tu contraseña.");   // no cuenta como intento fallido

        using var uow = bd.Abrir(transaccional: true);
        var repo = new UsuarioRepo(uow);
        var (maxIntentos, minutos) = Limites(new ConfigRepo(uow));

        if (repo.FallosRecientes(login, minutos) >= maxIntentos)
            throw new ReglaNegocioException(MensajeBloqueo(repo, login, minutos));

        var usuario = login.Length > 0 ? repo.PorLogin(login) : null;
        // Se verifica siempre (con un hash falso si no existe) para que el tiempo de respuesta no delate usuarios
        var valida = PasswordHasher.Verificar(password ?? "", usuario?.PasswordHash ?? HashFalso);

        if (usuario is null || !valida || !usuario.Activo)
        {
            repo.RegistrarFallo(login);
            var restantes = maxIntentos - (int)repo.FallosRecientes(login, minutos);
            var mensaje = restantes <= 0
                ? $"Usuario o contraseña incorrectos. {MensajeBloqueo(repo, login, minutos)}"
                : $"Usuario o contraseña incorrectos. Te queda{(restantes == 1 ? "" : "n")} {restantes} intento{(restantes == 1 ? "" : "s")} antes del bloqueo.";
            uow.Confirmar();
            throw new ReglaNegocioException(mensaje);
        }

        repo.LimpiarFallos(login);
        if (PasswordHasher.NecesitaRehash(usuario.PasswordHash))
            repo.CambiarPassword(usuario.Id, PasswordHasher.Hash(password!), usuario.DebeCambiarPassword);
        repo.RegistrarAcceso(usuario.Id);
        new BitacoraRepo(uow).Registrar(usuario.Id, "sesion.entrar", "usuario", usuario.Id);
        uow.Confirmar();
        return repo.PorIdSinHash(usuario.Id);
    }

    private static string MensajeBloqueo(UsuarioRepo repo, string login, int minutos)
    {
        var primero = DateTime.Parse(repo.PrimerFalloReciente(login, minutos)!);
        var faltan = Math.Max(1, (int)Math.Ceiling((primero.AddMinutes(minutos) - DateTime.Now).TotalMinutes));
        return $"Acceso bloqueado por demasiados intentos. Espera {faltan} minuto{(faltan == 1 ? "" : "s")} o pide a un administrador que te desbloquee.";
    }

    public void CambiarPassword(Usuario usuario, string actual, string nueva, string repetida)
    {
        using var uow = bd.Abrir(transaccional: true);
        var repo = new UsuarioRepo(uow);
        var u = repo.PorId(usuario.Id) ?? throw new ReglaNegocioException("El usuario ya no existe.");

        if (!PasswordHasher.Verificar(actual, u.PasswordHash)) throw new ReglaNegocioException("La contraseña actual no es correcta.");
        if (PasswordHasher.ValidarFortaleza(nueva) is { } error) throw new ReglaNegocioException(error);
        if (nueva != repetida) throw new ReglaNegocioException("Las contraseñas nuevas no coinciden.");
        if (nueva == actual) throw new ReglaNegocioException("La nueva contraseña debe ser distinta de la actual.");

        repo.CambiarPassword(u.Id, PasswordHasher.Hash(nueva), temporal: false);
        new BitacoraRepo(uow).Registrar(u.Id, "usuario.password", "usuario", u.Id);
        uow.Confirmar();
        usuario.DebeCambiarPassword = false;
    }
}
