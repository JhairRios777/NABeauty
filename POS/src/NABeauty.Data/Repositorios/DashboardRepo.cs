using NABeauty.Core.Entidades;
using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>
/// Consultas del tablero de Inicio. Solo lectura.
/// Rangos con fin exclusivo: fecha &gt;= inicio AND fecha &lt; fin (un día termina a las 23:59:59.999).
/// Solo suman las ventas COMPLETADAS; las anuladas se cuentan aparte.
/// </summary>
public sealed class DashboardRepo(UnidadTrabajo u)
{
    /// <summary>Por agotarse = con stock entre 1 y este valor (mismo criterio que la tienda en línea).</summary>
    public const int StockBajo = Inventario.StockBajo;

    // Inicio de hoy, de la semana (lunes) y del mes en hora local de la tienda
    private const string Hoy = "date('now','localtime')";
    private const string Lunes = "date('now','localtime','-' || ((CAST(strftime('%w','now','localtime') AS INTEGER) + 6) % 7) || ' days')";
    private const string Mes = "date('now','localtime','start of month')";

    public Metricas Metricas() => u.Uno<Metricas>($"""
        SELECT
          (SELECT COALESCE(SUM(total),0) FROM ventas WHERE estado = 'completada' AND fecha >= {Hoy}) AS ventas_hoy,
          (SELECT COUNT(*)               FROM ventas WHERE estado = 'completada' AND fecha >= {Hoy}) AS num_ventas_hoy,
          (SELECT COALESCE(SUM(total),0) FROM ventas WHERE estado = 'completada'
              AND fecha >= date({Hoy},'-1 day') AND fecha < {Hoy}) AS ventas_ayer,
          (SELECT COALESCE(SUM(total),0) FROM ventas WHERE estado = 'completada' AND fecha >= {Lunes}) AS ventas_semana,
          (SELECT COUNT(*)               FROM ventas WHERE estado = 'completada' AND fecha >= {Lunes}) AS num_ventas_semana,
          (SELECT COALESCE(SUM(total),0) FROM ventas WHERE estado = 'completada'
              AND fecha >= date({Lunes},'-7 days') AND fecha < {Lunes}) AS ventas_semana_anterior,
          (SELECT COALESCE(SUM(total),0) FROM ventas WHERE estado = 'completada' AND fecha >= {Mes}) AS ventas_mes,
          (SELECT COUNT(*)               FROM ventas WHERE estado = 'completada' AND fecha >= {Mes}) AS num_ventas_mes,
          (SELECT COALESCE(SUM(total),0) FROM ventas WHERE estado = 'completada'
              AND fecha >= date({Mes},'-1 month') AND fecha < {Mes}) AS ventas_mes_anterior,
          (SELECT COALESCE(SUM(d.cantidad),0) FROM venta_detalle d JOIN ventas v ON v.uuid = d.venta_uuid
              WHERE v.estado = 'completada' AND v.fecha >= {Mes}) AS unidades_mes,
          (SELECT COALESCE(SUM(d.cantidad * COALESCE(d.costo_unitario,0)),0) FROM venta_detalle d JOIN ventas v ON v.uuid = d.venta_uuid
              WHERE v.estado = 'completada' AND v.fecha >= {Mes}) AS costo_mes,
          (SELECT COUNT(*) FROM ventas WHERE estado = 'anulada' AND fecha >= {Mes}) AS anuladas_mes,
          (SELECT COUNT(*) FROM productos WHERE estado = 'activo') AS productos_activos,
          (SELECT COUNT(*) FROM producto_variantes v JOIN productos p ON p.uuid = v.producto_uuid
              WHERE p.estado = 'activo' AND v.activa = 1 AND v.stock BETWEEN 1 AND {StockBajo}) AS por_agotarse,
          (SELECT COUNT(*) FROM producto_variantes v JOIN productos p ON p.uuid = v.producto_uuid
              WHERE p.estado = 'activo' AND v.activa = 1 AND v.stock <= 0) AS agotados,
          (SELECT COUNT(*) FROM producto_variantes v JOIN productos p ON p.uuid = v.producto_uuid
              WHERE p.estado = 'activo' AND v.activa = 1 AND v.stock > 0) AS con_stock,
          (SELECT COUNT(*) FROM categorias WHERE activa = 1) AS categorias
        """)!;

    /// <summary>Total vendido por día en los últimos <paramref name="dias"/> días (solo días con ventas).</summary>
    public List<VentaDia> VentasPorDia(int dias) => u.Lista<VentaDia>($"""
        SELECT date(fecha) AS dia, SUM(total) AS total, COUNT(*) AS ventas
          FROM ventas
         WHERE estado = 'completada' AND fecha >= date({Hoy}, @desde)
         GROUP BY date(fecha)
        """, new { desde = $"-{dias - 1} days" });

    /// <summary>Lo más vendido del mes, por producto (los nombres salen de la venta si el producto ya no existe).</summary>
    public List<TopProducto> TopDelMes(int limite) => u.Lista<TopProducto>($"""
        SELECT COALESCE(p.nombre, MAX(d.producto_nombre)) AS producto, p.marca,
               SUM(d.cantidad) AS unidades, SUM(d.subtotal) AS total
          FROM venta_detalle d
          JOIN ventas v ON v.uuid = d.venta_uuid
          LEFT JOIN producto_variantes pv ON pv.uuid = d.variante_uuid
          LEFT JOIN productos p ON p.uuid = pv.producto_uuid
         WHERE v.estado = 'completada' AND v.fecha >= {Mes}
         GROUP BY COALESCE(p.uuid, d.producto_nombre)
         ORDER BY unidades DESC, total DESC
         LIMIT @limite
        """, new { limite });

    /// <summary>Variantes a la venta con poco stock o agotadas (las agotadas primero).</summary>
    public List<StockBajo> PorAgotarse(int limite) => u.Lista<StockBajo>($"""
        SELECT p.nombre AS producto, p.marca, v.nombre AS variante, v.tipo, v.stock
          FROM producto_variantes v
          JOIN productos p ON p.uuid = v.producto_uuid
         WHERE p.estado = 'activo' AND v.activa = 1 AND v.stock <= {StockBajo}
         ORDER BY v.stock, p.nombre
         LIMIT @limite
        """, new { limite });

    public List<VentaReciente> UltimasVentas(int limite) => u.Lista<VentaReciente>("""
        SELECT v.numero, v.fecha, v.metodo_pago, v.total, v.estado, us.nombre AS vendedor,
               (SELECT COALESCE(SUM(cantidad),0) FROM venta_detalle d WHERE d.venta_uuid = v.uuid) AS unidades
          FROM ventas v
          LEFT JOIN usuarios us ON us.id = v.usuario_id
         ORDER BY v.fecha DESC, v.numero DESC
         LIMIT @limite
        """, new { limite });
}
