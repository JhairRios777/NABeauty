using System.Net.Mail;
using System.Text.RegularExpressions;

namespace NABeauty.Core.Dominio;

/// <summary>Validaciones de datos de entrada que comparten formularios y servicios.</summary>
public static partial class Validar
{
    /// <summary>Solo dígitos; 8 dígitos locales → se antepone 504. null si no es válido.</summary>
    public static string? Telefono(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var d = SoloDigitos().Replace(texto, "");
        if (d.Length == 8) d = "504" + d;
        return d.Length is >= 8 and <= 15 ? d : null;
    }

    /// <summary>"50493429641" → "+504 9342-9641"</summary>
    public static string TelefonoLegible(string? numero)
    {
        if (string.IsNullOrEmpty(numero)) return "";
        var m = TelHn().Match(numero);
        return m.Success ? $"+504 {m.Groups[1].Value}-{m.Groups[2].Value}" : numero;
    }

    public static bool Correo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > 150) return false;
        return MailAddress.TryCreate(texto.Trim(), out var dir) && dir.Address == texto.Trim();
    }

    /// <summary>Recorta y convierte vacío en null.</summary>
    public static string? Texto(string? texto, int max)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        return t.Length > max ? t[..max] : t;
    }

    [GeneratedRegex(@"\D")] private static partial Regex SoloDigitos();
    [GeneratedRegex(@"^504(\d{4})(\d{4})$")] private static partial Regex TelHn();
}
