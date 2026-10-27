using NABeauty.Core.Dominio;

namespace NABeauty.Core.Entidades;

// ═══════════════════════ Ventas ═══════════════════════

/// <summary>Fila del historial de ventas.</summary>
public sealed class VentaFila
{
    public string Uuid { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Fecha { get; set; } = "";
    public long? UsuarioId { get; set; }
    public string? Vendedora { get; set; }
    public string MetodoPago { get; set; } = "efectivo";
    public long Subtotal { get; set; }
    public long Descuento { get; set; }
    public long Total { get; set; }
    public long? Recibido { get; set; }
    public string Estado { get; set; } = "completada";
    public string? MotivoAnulacion { get; set; }
    public string? AnuladaPor { get; set; }
    public string? AnuladaFecha { get; set; }
    public long Unidades { get; set; }
    public long Lineas { get; set; }

    public bool Anulada => Estado == "anulada";
    public string FechaTexto => Formato.Fecha(Fecha, conHora: true);
    public string TotalTexto => Dinero.Formatear(Total);
    public string Metodo => MetodosPago.Nombre(MetodoPago);
    public string EstadoTexto => Anulada ? "Anulada" : "Completada";
    public string UnidadesTexto => Unidades == 1 ? "1 artículo" : $"{Unidades} artículos";
    public long Cambio => MetodoPago == "efectivo" && Recibido is { } r ? Math.Max(0, r - Total) : 0;
}

/// <summary>Línea de una venta (nombres copiados al momento de vender).</summary>
public sealed class VentaLinea
{
    public string VarianteUuid { get; set; } = "";
    public string ProductoNombre { get; set; } = "";
    public string VarianteNombre { get; set; } = "";
    public long Cantidad { get; set; }
    public long PrecioUnitario { get; set; }
    public long? CostoUnitario { get; set; }
    public long Subtotal { get; set; }

    public string Nombre => VarianteNombre == "Única" ? ProductoNombre : $"{ProductoNombre} · {VarianteNombre}";
    public string PrecioTexto => Dinero.Formatear(PrecioUnitario);
    public string SubtotalTexto => Dinero.Formatear(Subtotal);
}

/// <summary>Filtro del historial de ventas.</summary>
public sealed record FiltroVentas(Periodo Periodo, string? MetodoPago = null, long? UsuarioId = null, string? Estado = null, string? Numero = null);

/// <summary>Totales del filtro actual (solo completadas suman).</summary>
public sealed class ResumenVentas
{
    public long Ventas { get; set; }
    public long Total { get; set; }
    public long Unidades { get; set; }
    public long Anuladas { get; set; }
    public long TotalAnulado { get; set; }
}

// ═══════════════════════ Entradas e inventario ═══════════════════════

public sealed class EntradaFila
{
    public string Uuid { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string? Proveedor { get; set; }
    public string? Usuario { get; set; }
    public long TotalCosto { get; set; }
    public string? Nota { get; set; }
    public long Unidades { get; set; }
    public long Lineas { get; set; }

    public string FechaTexto => Formato.Fecha(Fecha, conHora: true);
    public string ProveedorTexto => string.IsNullOrWhiteSpace(Proveedor) ? "Sin proveedor" : Proveedor;
    public string TotalTexto => Dinero.Formatear(TotalCosto);
    public string Resumen => $"{(Unidades == 1 ? "1 unidad" : $"{Unidades} unidades")} · {(Lineas == 1 ? "1 producto" : $"{Lineas} productos")}";
}

public sealed class EntradaLinea
{
    public string VarianteUuid { get; set; } = "";
    public string ProductoNombre { get; set; } = "";
    public string VarianteNombre { get; set; } = "";
    public long Cantidad { get; set; }
    public long CostoUnitario { get; set; }
    public long Subtotal { get; set; }

    public string Nombre => VarianteNombre == "Única" ? ProductoNombre : $"{ProductoNombre} · {VarianteNombre}";
    public string CostoTexto => Dinero.Formatear(CostoUnitario);
    public string SubtotalTexto => Dinero.Formatear(Subtotal);
}

/// <summary>Línea que se va a registrar en una entrada nueva.</summary>
public sealed record LineaEntrada(string VarianteUuid, long Cantidad, long CostoUnitario);

/// <summary>Ajuste de inventario (movimiento tipo "ajuste") para el historial.</summary>
public sealed class AjusteFila
{
    public long Id { get; set; }
    public string Fecha { get; set; } = "";
    public string Producto { get; set; } = "";
    public string Variante { get; set; } = "";
    public long Cantidad { get; set; }
    public string? Nota { get; set; }
    public string? Usuario { get; set; }

    public string FechaTexto => Formato.Fecha(Fecha, conHora: true);
    public string Nombre => Variante == "Única" ? Producto : $"{Producto} · {Variante}";
    public string CantidadTexto => Cantidad > 0 ? $"+{Cantidad}" : Cantidad.ToString();
    public string Signo => Cantidad > 0 ? "activo" : "vencido";
}

// ═══════════════════════ Reportes ═══════════════════════

public sealed class ResumenReporte
{
    public long Ventas { get; set; }
    public long Subtotal { get; set; }
    public long Descuentos { get; set; }
    public long Total { get; set; }
    public long Unidades { get; set; }
    public long Costo { get; set; }
    public long Anuladas { get; set; }
    public long TotalAnulado { get; set; }
    public long Entradas { get; set; }
    public long UnidadesEntrada { get; set; }
    public long CostoEntradas { get; set; }

    public long Ganancia => Total - Costo;
    public long Ticket => Ventas == 0 ? 0 : Total / Ventas;
    public double Margen => Total == 0 ? 0 : Ganancia * 100.0 / Total;
}

/// <summary>Cubeta del gráfico (un día o un mes).</summary>
public sealed class PuntoSerie
{
    public string Clave { get; set; } = "";
    public long Total { get; set; }
    public long Ventas { get; set; }
}

/// <summary>Fila agrupada (producto, categoría, método o vendedora).</summary>
public sealed class FilaReporte
{
    public string Nombre { get; set; } = "";
    public string? Detalle { get; set; }
    public long Ventas { get; set; }
    public long Unidades { get; set; }
    public long Total { get; set; }
    public long Costo { get; set; }
    /// <summary>% sobre el total del período (lo calcula el servicio).</summary>
    public double Porcentaje { get; set; }

    public long Ganancia => Total - Costo;
    public string TotalTexto => Dinero.Formatear(Total);
    public string PorcentajeTexto => $"{Porcentaje:0.#}%";
}

/// <summary>Todo el reporte de un período: una sola fuente para pantalla, Excel y PDF.</summary>
public sealed class Reporte
{
    public required Periodo Periodo { get; init; }
    public required ResumenReporte Resumen { get; init; }
    public required ResumenReporte Anterior { get; init; }
    public required List<PuntoSerie> Serie { get; init; }
    public required List<FilaReporte> Productos { get; init; }
    public required List<FilaReporte> Categorias { get; init; }
    public required List<FilaReporte> Metodos { get; init; }
    public required List<FilaReporte> Vendedoras { get; init; }
    public bool IncluyeCostos { get; init; }
    public string Negocio { get; init; } = "N&A Beauty";
    public string GeneradoPor { get; init; } = "";
    public DateTime Generado { get; init; } = DateTime.Now;

    /// <summary>% de cambio contra el período anterior; null si antes no hubo ventas.</summary>
    public double? Variacion => Anterior.Total == 0 ? null : (Resumen.Total - Anterior.Total) * 100.0 / Anterior.Total;
}

// ═══════════════════════ Negocio y recibo ═══════════════════════

/// <summary>Datos del negocio (Ajustes) que salen en el recibo y en los reportes.</summary>
public sealed class Negocio
{
    public string Nombre { get; set; } = "N&A Beauty";
    public string? Rtn { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? MensajeRecibo { get; set; } = "¡Gracias por tu compra!";
    /// <summary>Ancho del papel del recibo en mm (58 u 80).</summary>
    public int AnchoRecibo { get; set; } = 80;
    /// <summary>Imprimir el recibo al cobrar sin preguntar.</summary>
    public bool ImprimirAlCobrar { get; set; }
}

/// <summary>Recibo listo para imprimir (lo arma VentaService; lo dibuja la app).</summary>
public sealed record Recibo(
    Negocio Negocio, string Numero, string Fecha, string Vendedora, IReadOnlyList<VentaLinea> Lineas,
    long Subtotal, long Descuento, long Total, string MetodoPago, long? Recibido, bool Anulada)
{
    public long Cambio => MetodoPago == "efectivo" && Recibido is { } r ? Math.Max(0, r - Total) : 0;
    public int Unidades => (int)Lineas.Sum(l => l.Cantidad);
}
