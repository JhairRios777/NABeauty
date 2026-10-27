using System.Globalization;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;

namespace NABeauty.Data.Servicios;

/// <summary>Seguridad del ingreso (Ajustes).</summary>
public sealed record AjustesSeguridad(int MaxIntentos, int MinutosBloqueo);

/// <summary>Conexión con la tienda en línea (la sincronización se activa en una etapa posterior).</summary>
public sealed record AjustesWeb(string? Url, bool TieneToken, string? UltimaSync, long Pendientes);

/// <summary>
/// Ajustes del sistema, guardados en la tabla config (clave/valor).
/// Claves: negocio_*, recibo_*, login_max_intentos, login_bloqueo_min, web_url, web_token.
/// </summary>
public sealed class AjustesService(Contexto ctx)
{
    /// <summary>Datos del negocio para el recibo (lo usa también VentaService sin exigir Configurar).</summary>
    public static Negocio LeerNegocio(ConfigRepo cfg) => new()
    {
        Nombre = cfg.Get("negocio_nombre") ?? "N&A Beauty",
        Rtn = cfg.Get("negocio_rtn"),
        Telefono = cfg.Get("negocio_telefono"),
        Direccion = cfg.Get("negocio_direccion"),
        MensajeRecibo = cfg.Get("recibo_mensaje") ?? "¡Gracias por tu compra!",
        AnchoRecibo = cfg.Get("recibo_ancho") == "58" ? 58 : 80,
        ImprimirAlCobrar = cfg.Get("recibo_auto") == "1",
    };

    /// <summary>Lo puede leer cualquier usuario con sesión (la caja decide si imprime sola).</summary>
    public Negocio Negocio()
    {
        using var u = ctx.Bd.Abrir();
        return LeerNegocio(new ConfigRepo(u));
    }

    public void GuardarNegocio(Negocio n)
    {
        ctx.Exigir(Permiso.Configurar);
        var nombre = Validar.Texto(n.Nombre, 80) ?? throw new ReglaNegocioException("Escribe el nombre del negocio.");
        var rtn = Validar.Texto(n.Rtn, 20);
        if (rtn is not null && (rtn.Replace("-", "").Length != 14 || !rtn.Replace("-", "").All(char.IsDigit)))
            throw new ReglaNegocioException("El RTN debe tener 14 dígitos.");
        var tel = Validar.Texto(n.Telefono, 20);
        if (tel is not null && Core.Dominio.Validar.Telefono(tel) is null) throw new ReglaNegocioException("El teléfono no es válido.");
        if (n.AnchoRecibo is not (58 or 80)) throw new ReglaNegocioException("El ancho del recibo debe ser 58 u 80 mm.");

        using var u = ctx.Bd.Abrir(transaccional: true);
        var cfg = new ConfigRepo(u);
        cfg.Set("negocio_nombre", nombre);
        cfg.Set("negocio_rtn", rtn);
        cfg.Set("negocio_telefono", tel);
        cfg.Set("negocio_direccion", Validar.Texto(n.Direccion, 200));
        cfg.Set("recibo_mensaje", Validar.Texto(n.MensajeRecibo, 200));
        cfg.Set("recibo_ancho", n.AnchoRecibo.ToString(CultureInfo.InvariantCulture));
        cfg.Set("recibo_auto", n.ImprimirAlCobrar ? "1" : "0");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "ajustes.negocio", "config", null);
        u.Confirmar();
    }

    public AjustesSeguridad Seguridad()
    {
        ctx.Exigir(Permiso.Configurar);
        using var u = ctx.Bd.Abrir();
        var (max, min) = LoginService.Limites(new ConfigRepo(u));
        return new AjustesSeguridad(max, min);
    }

    public void GuardarSeguridad(int maxIntentos, int minutos)
    {
        ctx.Exigir(Permiso.Configurar);
        if (maxIntentos is < 3 or > 20) throw new ReglaNegocioException("Los intentos permitidos deben estar entre 3 y 20.");
        if (minutos is < 1 or > 1440) throw new ReglaNegocioException("El bloqueo debe durar entre 1 minuto y 24 horas (1440 minutos).");
        using var u = ctx.Bd.Abrir(transaccional: true);
        var cfg = new ConfigRepo(u);
        cfg.Set("login_max_intentos", maxIntentos.ToString(CultureInfo.InvariantCulture));
        cfg.Set("login_bloqueo_min", minutos.ToString(CultureInfo.InvariantCulture));
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "ajustes.seguridad", "config", null, new { maxIntentos, minutos });
        u.Confirmar();
    }

    public AjustesWeb Web()
    {
        ctx.Exigir(Permiso.Configurar);
        using var u = ctx.Bd.Abrir();
        var cfg = new ConfigRepo(u);
        return new AjustesWeb(cfg.Get("web_url"), !string.IsNullOrEmpty(cfg.Get("web_token")), cfg.Get("web_ultima_sync"),
            u.Escalar<long>("SELECT COUNT(*) FROM sync_cola WHERE enviado IS NULL"));
    }

    /// <param name="token">null o vacío = conservar el token actual.</param>
    public void GuardarWeb(string? url, string? token)
    {
        ctx.Exigir(Permiso.Configurar);
        var limpia = Validar.Texto(url, 200)?.TrimEnd('/');
        if (limpia is not null)
        {
            if (!Uri.TryCreate(limpia, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
                throw new ReglaNegocioException("Escribe la dirección completa de la tienda, por ejemplo https://nyabeauty.com");
            // Fuera de la red local solo HTTPS: el token viaja en cada petición
            if (uri.Scheme == "http" && !uri.IsLoopback && !uri.Host.EndsWith(".test") && !uri.Host.StartsWith("192.168."))
                throw new ReglaNegocioException("Usa https:// para la tienda en línea (el token no debe viajar sin cifrar).");
        }
        var t = Validar.Texto(token, 200);
        if (t is not null && t.Length < 20) throw new ReglaNegocioException("El token debe tener al menos 20 caracteres.");

        using var u = ctx.Bd.Abrir(transaccional: true);
        var cfg = new ConfigRepo(u);
        cfg.Set("web_url", limpia);
        if (t is not null) cfg.Set("web_token", t);
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "ajustes.web", "config", null, new { url = limpia, token = t is not null ? "cambiado" : "sin cambio" });
        u.Confirmar();
    }

    /// <summary>Respaldo manual (botón "Respaldar ahora").</summary>
    public string Respaldar(string carpeta)
    {
        ctx.Exigir(Permiso.Configurar);
        return ctx.Bd.Respaldar(carpeta, manual: true) ?? throw new ReglaNegocioException("Ya se creó un respaldo hace un momento.");
    }
}
