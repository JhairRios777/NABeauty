namespace NABeauty.Core.Entidades;

/// <summary>
/// Datos del tablero de Inicio. Dinero en CENTAVOS (long), como en toda la app.
/// Las ventas ANULADAS nunca suman: se informan aparte.
/// </summary>
public sealed class Metricas
{
    public long VentasHoy { get; set; }
    public long NumVentasHoy { get; set; }
    public long VentasAyer { get; set; }
    public long VentasSemana { get; set; }
    public long NumVentasSemana { get; set; }
    public long VentasSemanaAnterior { get; set; }
    public long VentasMes { get; set; }
    public long NumVentasMes { get; set; }
    public long VentasMesAnterior { get; set; }
    public long UnidadesMes { get; set; }
    public long CostoMes { get; set; }
    public long AnuladasMes { get; set; }

    public long ProductosActivos { get; set; }
    public long PorAgotarse { get; set; }
    public long Agotados { get; set; }
    /// <summary>Opciones a la venta con al menos 1 unidad.</summary>
    public long ConStock { get; set; }
    public long Categorias { get; set; }

    public long TicketPromedioMes => NumVentasMes == 0 ? 0 : VentasMes / NumVentasMes;
    public long GananciaMes => VentasMes - CostoMes;
    public bool SinCatalogo => ProductosActivos == 0;
}

/// <summary>Total vendido en un día (para el gráfico de barras).</summary>
public sealed class VentaDia
{
    public string Dia { get; set; } = "";     // yyyy-MM-dd
    public long Total { get; set; }
    public long Ventas { get; set; }
}

/// <summary>Producto más vendido en el período.</summary>
public sealed class TopProducto
{
    public string Producto { get; set; } = "";
    public string? Marca { get; set; }
    public long Unidades { get; set; }
    public long Total { get; set; }
}

/// <summary>Variante con pocas existencias (o agotada).</summary>
public sealed class StockBajo
{
    public string Producto { get; set; } = "";
    public string? Marca { get; set; }
    public string Variante { get; set; } = "";
    public string Tipo { get; set; } = "unica";
    public long Stock { get; set; }

    public string Detalle => Tipo == "unica" ? Marca ?? "" : $"{Marca} · {Variante}";
    public string Estado => Stock <= 0 ? "agotado" : "bajo";
    public string EstadoTexto => Stock <= 0 ? "Agotado" : Stock == 1 ? "1 en stock" : $"{Stock} en stock";
}

/// <summary>Venta reciente (lista del tablero).</summary>
public sealed class VentaReciente
{
    public string Numero { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string MetodoPago { get; set; } = "efectivo";
    public long Total { get; set; }
    public string Estado { get; set; } = "completada";
    public string? Vendedor { get; set; }
    public long Unidades { get; set; }
}
