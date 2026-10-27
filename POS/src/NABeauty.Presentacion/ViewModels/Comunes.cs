namespace NABeauty.Presentacion.ViewModels;

/// <summary>
/// Tarjeta de indicador del tablero.
/// Tendencia: "sube", "baja" o "" (sin comparación). La vista elige color e ícono con triggers,
/// nunca solo el color: la flecha y el texto dicen lo mismo.
/// </summary>
public sealed record Kpi(string Titulo, string Valor, string Detalle, string Icono,
                         bool Destacado = false, string Variacion = "", string Tendencia = "")
{
    public bool HayVariacion => Variacion.Length > 0;
}

/// <summary>Barra del gráfico de ventas por día (altura en píxeles, 0–150).</summary>
public sealed record BarraDia(string Etiqueta, string Valor, double Altura, string Detalle, bool EsHoy)
{
    public bool Vacia => Altura <= 0;
    /// <summary>Texto del globo al pasar el mouse.</summary>
    public string Globo => $"{Etiqueta}: {Valor}{(Detalle.Length > 0 ? " · " + Detalle : "")}";
}

/// <summary>Fila de una lista corta del tablero (más vendidos, ventas recientes, stock).</summary>
public sealed record FilaPanel(string Titulo, string Subtitulo, string Valor, string ValorDetalle, string Estado = "", string EstadoTexto = "")
{
    public bool HayEstado => EstadoTexto.Length > 0;
}

/// <summary>Paso de la guía de primeros pasos.</summary>
public sealed record Paso(string Numero, string Titulo, string Detalle, bool Hecho)
{
    public string Marca => Hecho ? "" : Numero;
}
