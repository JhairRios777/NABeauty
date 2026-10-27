using NABeauty.Core.Entidades;
using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>Historial de ventas, detalle y anulación. Solo SQL.</summary>
public sealed class VentaRepo(UnidadTrabajo u)
{
    private const string Select = """
        SELECT v.uuid, v.numero, v.fecha, v.usuario_id, us.nombre AS vendedora, v.metodo_pago, v.subtotal, v.descuento,
               v.total, v.recibido, v.estado, v.motivo_anulacion, an.nombre AS anulada_por, v.anulada_fecha,
               (SELECT COALESCE(SUM(cantidad),0) FROM venta_detalle d WHERE d.venta_uuid = v.uuid) AS unidades,
               (SELECT COUNT(*) FROM venta_detalle d WHERE d.venta_uuid = v.uuid) AS lineas
          FROM ventas v
          LEFT JOIN usuarios us ON us.id = v.usuario_id
          LEFT JOIN usuarios an ON an.id = v.anulada_por
        """;

    private const string Where = """
         WHERE v.fecha >= @desde AND v.fecha < @hasta
           AND (@metodo IS NULL OR v.metodo_pago = @metodo)
           AND (@usuario IS NULL OR v.usuario_id = @usuario)
           AND (@estado IS NULL OR v.estado = @estado)
           AND (@numero IS NULL OR v.numero LIKE @numero)
        """;

    private static object Parametros(FiltroVentas f) => new
    {
        desde = f.Periodo.DesdeSql,
        hasta = f.Periodo.HastaSql,
        metodo = f.MetodoPago,
        usuario = f.UsuarioId,
        estado = f.Estado,
        numero = string.IsNullOrWhiteSpace(f.Numero) ? null : $"%{f.Numero.Trim()}%",
    };

    public List<VentaFila> Listar(FiltroVentas f, int limite) =>
        u.Lista<VentaFila>($"{Select} {Where} ORDER BY v.fecha DESC, v.numero DESC LIMIT {Math.Clamp(limite, 1, 5000)}", Parametros(f));

    public ResumenVentas Resumen(FiltroVentas f) => u.Uno<ResumenVentas>($"""
        SELECT COALESCE(SUM(CASE WHEN v.estado = 'completada' THEN 1 ELSE 0 END), 0) AS ventas,
               COALESCE(SUM(CASE WHEN v.estado = 'completada' THEN v.total ELSE 0 END), 0) AS total,
               COALESCE(SUM(CASE WHEN v.estado = 'completada'
                    THEN (SELECT SUM(cantidad) FROM venta_detalle d WHERE d.venta_uuid = v.uuid) ELSE 0 END), 0) AS unidades,
               COALESCE(SUM(CASE WHEN v.estado = 'anulada' THEN 1 ELSE 0 END), 0) AS anuladas,
               COALESCE(SUM(CASE WHEN v.estado = 'anulada' THEN v.total ELSE 0 END), 0) AS total_anulado
          FROM ventas v {Where}
        """, Parametros(f)) ?? new ResumenVentas();

    public VentaFila? Obtener(string uuid) => u.Uno<VentaFila>($"{Select} WHERE v.uuid = @uuid", new { uuid });

    public List<VentaLinea> Lineas(string ventaUuid) => u.Lista<VentaLinea>("""
        SELECT variante_uuid, producto_nombre, variante_nombre, cantidad, precio_unitario, costo_unitario, subtotal
          FROM venta_detalle WHERE venta_uuid = @ventaUuid ORDER BY id
        """, new { ventaUuid });

    /// <summary>Quienes han vendido algo (para el filtro "Vendedora").</summary>
    public List<(long Id, string Nombre)> Vendedoras() =>
        [.. u.Lista<IdNombre>("""
            SELECT DISTINCT us.id, us.nombre FROM ventas v JOIN usuarios us ON us.id = v.usuario_id ORDER BY us.nombre
            """).Select(x => (x.Id, x.Nombre))];

    /// <summary>Un solo UPDATE: el trigger exige motivo y que no estuviera anulada.</summary>
    public void Anular(string uuid, string motivo, long usuarioId) => u.Ejecutar("""
        UPDATE ventas SET estado = 'anulada', motivo_anulacion = @motivo, anulada_por = @usuarioId,
               anulada_fecha = datetime('now','localtime')
         WHERE uuid = @uuid
        """, new { uuid, motivo, usuarioId });

    private sealed class IdNombre
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = "";
    }
}
