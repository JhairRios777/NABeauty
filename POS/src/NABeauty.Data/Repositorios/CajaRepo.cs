using NABeauty.Core.Entidades;
using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>Consultas y escrituras de la caja: búsqueda de lo que se vende y registro de la venta.</summary>
public sealed class CajaRepo(UnidadTrabajo u)
{
    // Solo lo vendible: producto activo, categoría activa y variante activa
    private const string Select = """
        SELECT v.uuid AS variante_uuid, p.uuid AS producto_uuid, p.nombre AS producto, p.marca, v.tipo,
               v.nombre AS variante, v.color_hex, v.sku, v.codigo_barras, c.uuid AS categoria_uuid, c.nombre AS categoria,
               v.precio, v.precio_oferta, v.stock,
               (SELECT archivo FROM producto_imagenes i WHERE i.producto_uuid = p.uuid ORDER BY i.orden LIMIT 1) AS foto
          FROM producto_variantes v
          JOIN productos p  ON p.uuid = v.producto_uuid
          JOIN categorias c ON c.uuid = p.categoria_uuid
         WHERE v.activa = 1 AND p.estado = 'activo'
        """;

    /// <summary>Busca por nombre, marca, opción o SKU. Primero lo que tiene stock.</summary>
    public List<ArticuloCaja> Buscar(string? texto, string? categoriaUuid, int limite)
    {
        var t = string.IsNullOrWhiteSpace(texto) ? null : $"%{texto.Trim()}%";
        return u.Lista<ArticuloCaja>($"""
            {Select}
               AND (@cat IS NULL OR c.uuid = @cat)
               AND (@t IS NULL OR p.nombre LIKE @t OR p.marca LIKE @t OR v.nombre LIKE @t OR v.sku LIKE @t OR v.codigo_barras = @exacto)
             ORDER BY (v.stock > 0) DESC, p.nombre COLLATE NOCASE, v.orden
             LIMIT @limite
            """, new { cat = categoriaUuid, t, exacto = texto?.Trim(), limite });
    }

    /// <summary>Coincidencia exacta por código de barras o SKU (lo que manda el escáner).</summary>
    public ArticuloCaja? PorCodigo(string codigo) => u.Uno<ArticuloCaja>(
        $"{Select} AND (v.codigo_barras = @codigo OR v.sku = @codigo COLLATE NOCASE) LIMIT 1", new { codigo });

    /// <summary>Estado actual (precio, stock, activa) de una variante para revalidar al cobrar.</summary>
    public ArticuloCaja? Articulo(string varianteUuid) =>
        u.Uno<ArticuloCaja>($"{Select} AND v.uuid = @varianteUuid", new { varianteUuid });

    public long? Costo(string varianteUuid) =>
        u.Escalar<long?>("SELECT costo FROM producto_variantes WHERE uuid = @varianteUuid", new { varianteUuid });

    public void CrearVenta(string uuid, string numero, long usuarioId, string metodoPago, long subtotal, long descuento, long total, long? recibido) =>
        u.Ejecutar("""
            INSERT INTO ventas (uuid, numero, usuario_id, metodo_pago, subtotal, descuento, total, recibido)
            VALUES (@uuid, @numero, @usuarioId, @metodoPago, @subtotal, @descuento, @total, @recibido)
            """, new { uuid, numero, usuarioId, metodoPago, subtotal, descuento, total, recibido });

    public void AgregarDetalle(string ventaUuid, ArticuloCaja a, long cantidad, long? costo) => u.Ejecutar("""
        INSERT INTO venta_detalle (venta_uuid, variante_uuid, producto_nombre, variante_nombre, cantidad, precio_unitario, costo_unitario, subtotal)
        VALUES (@ventaUuid, @VarianteUuid, @Producto, @Variante, @cantidad, @precio, @costo, @subtotal)
        """, new { ventaUuid, a.VarianteUuid, a.Producto, a.Variante, cantidad, precio = a.PrecioVenta, costo, subtotal = cantidad * a.PrecioVenta });
}
