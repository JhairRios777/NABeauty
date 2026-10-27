using System.Globalization;

namespace NABeauty.Core.Dominio;

/// <summary>Formatos de fecha para mostrar. En la BD las fechas son TEXT ISO.</summary>
public static class Formato
{
    public const string Iso = "yyyy-MM-dd";

    /// <summary>"2026-10-23" o "2026-10-23 13:20:00" → "23/10/2026"</summary>
    public static string Fecha(string? iso, bool conHora = false)
    {
        if (string.IsNullOrEmpty(iso)) return "—";
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
            return f.ToString(conHora ? "dd/MM/yyyy HH:mm" : "dd/MM/yyyy", CultureInfo.InvariantCulture);
        return iso;
    }

    public static string ATexto(DateOnly f) => f.ToString(Iso, CultureInfo.InvariantCulture);
    public static DateOnly DeTexto(string iso) => DateOnly.ParseExact(iso[..10], Iso, CultureInfo.InvariantCulture);
    public static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);
}
