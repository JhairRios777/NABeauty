using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>Entradas de mercadería y ajustes de inventario. Solo SQL.</summary>
public sealed class EntradaRepo(UnidadTrabajo u)
{
    private const string Select = """
        SELECT e.uuid, e.numero, e.fecha, e.proveedor, us.nombre AS usuario, e.total_costo, e.nota,
               (SELECT COALESCE(SUM(cantidad),0) FROM entrada_detalle d WHERE d.entrada_uuid = e.uuid) AS unidades,
               (SELECT COUNT(*) FROM entrada_detalle d WHERE d.entrada_uuid = e.uuid) AS lineas
          FROM entradas e LEFT JOIN usuarios us ON us.id = e.usuario_id
        """;

    public List<EntradaFila> Listar(Periodo p, string? texto)
    {
        var t = string.IsNullOrWhiteSpace(texto) ? null : $"%{texto.Trim()}%";
        return u.Lista<EntradaFila>($"""
            {Select}
             WHERE e.fecha >= @desde AND e.fecha < @hasta
               AND (@t IS NULL OR e.numero LIKE @t OR e.proveedor LIKE @t OR e.nota LIKE @t
                    OR EXISTS (SELECT 1 FROM entrada_detalle d WHERE d.entrada_uuid = e.uuid AND d.producto_nombre LIKE @t))
             ORDER BY e.fecha DESC, e.numero DESC LIMIT 1000
            """, new { desde = p.DesdeSql, hasta = p.HastaSql, t });
    }

    public EntradaFila? Obtener(string uuid) => u.Uno<EntradaFila>($"{Select} WHERE e.uuid = @uuid", new { uuid });

    public List<EntradaLinea> Lineas(string entradaUuid) => u.Lista<EntradaLinea>("""
        SELECT variante_uuid, producto_nombre, variante_nombre, cantidad, costo_unitario, subtotal
          FROM entrada_detalle WHERE entrada_uuid = @entradaUuid ORDER BY id
        """, new { entradaUuid });

    public void Crear(string uuid, string numero, string? proveedor, long usuarioId, long totalCosto, string? nota) => u.Ejecutar("""
        INSERT INTO entradas (uuid, numero, proveedor, usuario_id, total_costo, nota)
        VALUES (@uuid, @numero, @proveedor, @usuarioId, @totalCosto, @nota)
        """, new { uuid, numero, proveedor, usuarioId, totalCosto, nota });

    public void AgregarLinea(string entradaUuid, string varianteUuid, string producto, string variante, long cantidad, long costo) => u.Ejecutar("""
        INSERT INTO entrada_detalle (entrada_uuid, variante_uuid, producto_nombre, variante_nombre, cantidad, costo_unitario, subtotal)
        VALUES (@entradaUuid, @varianteUuid, @producto, @variante, @cantidad, @costo, @subtotal)
        """, new { entradaUuid, varianteUuid, producto, variante, cantidad, costo, subtotal = cantidad * costo });

    /// <summary>El costo de la última compra pasa a ser el costo del producto (lo usa la ganancia de las próximas ventas).</summary>
    public void ActualizarCosto(string varianteUuid, long costo) => u.Ejecutar(
        "UPDATE producto_variantes SET costo = @costo, actualizado = datetime('now','localtime') WHERE uuid = @varianteUuid",
        new { varianteUuid, costo });

    public List<AjusteFila> Ajustes(Periodo p) => u.Lista<AjusteFila>("""
        SELECT m.id, m.fecha, pr.nombre AS producto, v.nombre AS variante, m.cantidad, m.nota, us.nombre AS usuario
          FROM inventario_movimientos m
          JOIN producto_variantes v ON v.uuid = m.variante_uuid
          JOIN productos pr ON pr.uuid = v.producto_uuid
          LEFT JOIN usuarios us ON us.id = m.usuario_id
         WHERE m.tipo = 'ajuste' AND m.fecha >= @desde AND m.fecha < @hasta
         ORDER BY m.id DESC LIMIT 1000
        """, new { desde = p.DesdeSql, hasta = p.HastaSql });
}
