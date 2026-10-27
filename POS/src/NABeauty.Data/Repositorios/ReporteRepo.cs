using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>
/// Consultas de Reportes. Solo suman ventas COMPLETADAS; las anuladas se informan aparte.
/// Costo = costo guardado en cada línea al momento de vender (no el costo de hoy).
/// </summary>
public sealed class ReporteRepo(UnidadTrabajo u)
{
    private const string VentasDelPeriodo = "v.estado = 'completada' AND v.fecha >= @desde AND v.fecha < @hasta";

    private static object P(Periodo p) => new { desde = p.DesdeSql, hasta = p.HastaSql };

    public ResumenReporte Resumen(Periodo p) => u.Uno<ResumenReporte>($"""
        SELECT
          (SELECT COUNT(*) FROM ventas v WHERE {VentasDelPeriodo}) AS ventas,
          (SELECT COALESCE(SUM(subtotal),0) FROM ventas v WHERE {VentasDelPeriodo}) AS subtotal,
          (SELECT COALESCE(SUM(descuento),0) FROM ventas v WHERE {VentasDelPeriodo}) AS descuentos,
          (SELECT COALESCE(SUM(total),0) FROM ventas v WHERE {VentasDelPeriodo}) AS total,
          (SELECT COALESCE(SUM(d.cantidad),0) FROM venta_detalle d JOIN ventas v ON v.uuid = d.venta_uuid WHERE {VentasDelPeriodo}) AS unidades,
          (SELECT COALESCE(SUM(d.cantidad * COALESCE(d.costo_unitario,0)),0) FROM venta_detalle d JOIN ventas v ON v.uuid = d.venta_uuid WHERE {VentasDelPeriodo}) AS costo,
          (SELECT COUNT(*) FROM ventas v WHERE v.estado = 'anulada' AND v.fecha >= @desde AND v.fecha < @hasta) AS anuladas,
          (SELECT COALESCE(SUM(total),0) FROM ventas v WHERE v.estado = 'anulada' AND v.fecha >= @desde AND v.fecha < @hasta) AS total_anulado,
          (SELECT COUNT(*) FROM entradas e WHERE e.fecha >= @desde AND e.fecha < @hasta) AS entradas,
          (SELECT COALESCE(SUM(d.cantidad),0) FROM entrada_detalle d JOIN entradas e ON e.uuid = d.entrada_uuid
              WHERE e.fecha >= @desde AND e.fecha < @hasta) AS unidades_entrada,
          (SELECT COALESCE(SUM(total_costo),0) FROM entradas e WHERE e.fecha >= @desde AND e.fecha < @hasta) AS costo_entradas
        """, P(p)) ?? new ResumenReporte();

    /// <summary>Total por día ('AAAA-MM-DD') o por mes ('AAAA-MM').</summary>
    public List<PuntoSerie> Serie(Periodo p) => u.Lista<PuntoSerie>($"""
        SELECT {(p.PorMes ? "substr(v.fecha,1,7)" : "substr(v.fecha,1,10)")} AS clave, SUM(v.total) AS total, COUNT(*) AS ventas
          FROM ventas v WHERE {VentasDelPeriodo}
         GROUP BY clave ORDER BY clave
        """, P(p));

    /// <summary>Por producto y opción (como se llamaban al venderse).</summary>
    public List<FilaReporte> Productos(Periodo p, int? limite) => u.Lista<FilaReporte>($"""
        SELECT d.producto_nombre AS nombre, CASE WHEN d.variante_nombre = 'Única' THEN NULL ELSE d.variante_nombre END AS detalle,
               COUNT(DISTINCT v.uuid) AS ventas, SUM(d.cantidad) AS unidades, SUM(d.subtotal) AS total,
               SUM(d.cantidad * COALESCE(d.costo_unitario,0)) AS costo
          FROM venta_detalle d JOIN ventas v ON v.uuid = d.venta_uuid
         WHERE {VentasDelPeriodo}
         GROUP BY d.producto_nombre, d.variante_nombre
         ORDER BY unidades DESC, total DESC
         {(limite is { } l ? $"LIMIT {l}" : "")}
        """, P(p));

    /// <summary>Por categoría actual del producto. Montos por línea (antes del descuento general de la venta).</summary>
    public List<FilaReporte> Categorias(Periodo p) => u.Lista<FilaReporte>($"""
        SELECT COALESCE(c.nombre, 'Sin categoría') AS nombre, COUNT(DISTINCT v.uuid) AS ventas, SUM(d.cantidad) AS unidades,
               SUM(d.subtotal) AS total, SUM(d.cantidad * COALESCE(d.costo_unitario,0)) AS costo
          FROM venta_detalle d
          JOIN ventas v ON v.uuid = d.venta_uuid
          LEFT JOIN producto_variantes pv ON pv.uuid = d.variante_uuid
          LEFT JOIN productos pr ON pr.uuid = pv.producto_uuid
          LEFT JOIN categorias c ON c.uuid = pr.categoria_uuid
         WHERE {VentasDelPeriodo}
         GROUP BY COALESCE(c.nombre, 'Sin categoría') ORDER BY total DESC
        """, P(p));

    public List<FilaReporte> Metodos(Periodo p) => u.Lista<FilaReporte>($"""
        SELECT v.metodo_pago AS nombre, COUNT(*) AS ventas,
               (SELECT COALESCE(SUM(d.cantidad),0) FROM venta_detalle d JOIN ventas x ON x.uuid = d.venta_uuid
                 WHERE x.metodo_pago = v.metodo_pago AND x.estado = 'completada' AND x.fecha >= @desde AND x.fecha < @hasta) AS unidades,
               SUM(v.total) AS total
          FROM ventas v WHERE {VentasDelPeriodo}
         GROUP BY v.metodo_pago ORDER BY total DESC
        """, P(p));

    public List<FilaReporte> Vendedoras(Periodo p) => u.Lista<FilaReporte>($"""
        SELECT COALESCE(us.nombre, 'Sin usuario') AS nombre, COUNT(*) AS ventas,
               (SELECT COALESCE(SUM(d.cantidad),0) FROM venta_detalle d JOIN ventas x ON x.uuid = d.venta_uuid
                 WHERE x.usuario_id IS v.usuario_id AND x.estado = 'completada' AND x.fecha >= @desde AND x.fecha < @hasta) AS unidades,
               SUM(v.total) AS total
          FROM ventas v LEFT JOIN usuarios us ON us.id = v.usuario_id
         WHERE {VentasDelPeriodo}
         GROUP BY v.usuario_id ORDER BY total DESC
        """, P(p));

    /// <summary>Todas las líneas vendidas del período (hoja "Detalle" del Excel).</summary>
    public List<DetalleExport> Detalle(Periodo p) => u.Lista<DetalleExport>($"""
        SELECT v.numero, v.fecha, COALESCE(us.nombre,'') AS vendedora, v.metodo_pago, d.producto_nombre, d.variante_nombre,
               d.cantidad, d.precio_unitario, d.subtotal, COALESCE(d.costo_unitario,0) AS costo_unitario
          FROM venta_detalle d JOIN ventas v ON v.uuid = d.venta_uuid LEFT JOIN usuarios us ON us.id = v.usuario_id
         WHERE {VentasDelPeriodo}
         ORDER BY v.fecha, v.numero, d.id
        """, P(p));
}

/// <summary>Línea vendida para exportar.</summary>
public sealed class DetalleExport
{
    public string Numero { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string Vendedora { get; set; } = "";
    public string MetodoPago { get; set; } = "";
    public string ProductoNombre { get; set; } = "";
    public string VarianteNombre { get; set; } = "";
    public long Cantidad { get; set; }
    public long PrecioUnitario { get; set; }
    public long Subtotal { get; set; }
    public long CostoUnitario { get; set; }
}
