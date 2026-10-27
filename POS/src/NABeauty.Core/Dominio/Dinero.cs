using System.Globalization;

namespace NABeauty.Core.Dominio;

/// <summary>
/// Montos en lempiras manejados como CENTAVOS (long).
/// Nunca se usa double para dinero: 0.1 + 0.2 != 0.3 en punto flotante.
/// </summary>
public static class Dinero
{
    private static readonly CultureInfo Formato = CultureInfo.InvariantCulture;

    /// <summary>125050 → "L 1,250.50"</summary>
    public static string Formatear(long centavos)
    {
        var signo = centavos < 0 ? "-" : "";
        var abs = Math.Abs(centavos);
        return $"{signo}L {(abs / 100).ToString("N0", Formato)}.{abs % 100:00}";
    }

    /// <summary>125050 → "1250.50" (para cajas de texto editables)</summary>
    public static string ParaEditar(long centavos) => (centavos / 100m).ToString("0.00", Formato);

    /// <summary>
    /// Convierte lo que escribe el usuario a centavos. Acepta "1500", "1,500.5", "L 1,500.50".
    /// Rechaza más de 2 decimales, negativos y texto.
    /// </summary>
    public static bool TryParse(string? texto, out long centavos)
    {
        centavos = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var limpio = texto.Replace("L", "", StringComparison.OrdinalIgnoreCase).Replace(",", "").Trim();
        if (limpio.Length == 0 || limpio.StartsWith('-')) return false;

        var punto = limpio.IndexOf('.');
        if (punto >= 0 && limpio.Length - punto - 1 > 2) return false;

        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, Formato, out var valor)) return false;
        if (valor > 999_999_999m) return false;

        centavos = (long)decimal.Round(valor * 100m, 0, MidpointRounding.AwayFromZero);
        return true;
    }
}
