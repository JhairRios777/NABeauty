using System.Globalization;

namespace NABeauty.Core.Dominio;

/// <summary>
/// Rango de fechas para Ventas, Entradas y Reportes. Desde inclusivo, Hasta EXCLUSIVO
/// (el día "hasta" ya no entra): así un día termina a las 23:59:59.999 sin trucos.
/// </summary>
public sealed record Periodo(DateOnly Desde, DateOnly Hasta)
{
    private static readonly CultureInfo Hn = CultureInfo.GetCultureInfo("es-HN");

    /// <summary>Presets que ofrecen las pantallas (clave → nombre).</summary>
    public static readonly IReadOnlyList<(string Clave, string Nombre)> Presets =
    [
        ("hoy", "Hoy"),
        ("ayer", "Ayer"),
        ("semana", "Esta semana"),
        ("7dias", "Últimos 7 días"),
        ("mes", "Este mes"),
        ("mes_anterior", "Mes anterior"),
        ("anio", "Este año"),
        ("rango", "Rango de fechas"),
    ];

    public static Periodo Dia(DateOnly d) => new(d, d.AddDays(1));

    /// <summary>Rango de días completos: del <paramref name="primerDia"/> al <paramref name="ultimoDia"/>, ambos incluidos.</summary>
    public static Periodo Rango(DateOnly primerDia, DateOnly ultimoDia)
    {
        if (ultimoDia < primerDia) (primerDia, ultimoDia) = (ultimoDia, primerDia);
        if (ultimoDia.DayNumber - primerDia.DayNumber > 3660) throw new ReglaNegocioException("El rango no puede pasar de 10 años.");
        return new(primerDia, ultimoDia.AddDays(1));
    }

    public static Periodo DePreset(string clave, DateOnly hoy)
    {
        var lunes = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
        var mes = new DateOnly(hoy.Year, hoy.Month, 1);
        return clave switch
        {
            "hoy" => Dia(hoy),
            "ayer" => Dia(hoy.AddDays(-1)),
            "semana" => new(lunes, lunes.AddDays(7)),
            "7dias" => new(hoy.AddDays(-6), hoy.AddDays(1)),
            "mes" => new(mes, mes.AddMonths(1)),
            "mes_anterior" => new(mes.AddMonths(-1), mes),
            "anio" => new(new DateOnly(hoy.Year, 1, 1), new DateOnly(hoy.Year + 1, 1, 1)),
            _ => Dia(hoy),
        };
    }

    public int Dias => Hasta.DayNumber - Desde.DayNumber;
    /// <summary>Último día incluido.</summary>
    public DateOnly UltimoDia => Hasta.AddDays(-1);

    /// <summary>Para la consulta SQL: 'AAAA-MM-DD'.</summary>
    public string DesdeSql => Formato.ATexto(Desde);
    public string HastaSql => Formato.ATexto(Hasta);

    /// <summary>Período inmediatamente anterior de la misma duración (para comparar).</summary>
    public Periodo Anterior()
    {
        // Mes calendario completo → mes anterior completo (no "los 31 días previos")
        if (Desde.Day == 1 && Hasta == Desde.AddMonths(1)) return new(Desde.AddMonths(-1), Desde);
        if (Desde.Day == 1 && Desde.Month == 1 && Hasta == Desde.AddYears(1)) return new(Desde.AddYears(-1), Desde);
        return new(Desde.AddDays(-Dias), Desde);
    }

    /// <summary>Con más de 62 días el gráfico agrupa por mes; si no, por día.</summary>
    public bool PorMes => Dias > 62;

    public string Etiqueta => Dias == 1
        ? Desde.ToString("dddd d 'de' MMMM yyyy", Hn)
        : Desde.Day == 1 && Hasta == Desde.AddMonths(1)
            ? Hn.TextInfo.ToTitleCase(Desde.ToString("MMMM yyyy", Hn))
            : $"{Desde:dd/MM/yyyy} – {UltimoDia:dd/MM/yyyy}";
}
