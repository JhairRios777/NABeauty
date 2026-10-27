using NABeauty.Data.Infra;

namespace NABeauty.Tests;

/// <summary>Datos mínimos para probar el tablero (la app arranca con la base vacía).</summary>
internal static class Datos
{
    public static string Uuid() => Guid.NewGuid().ToString();

    /// <summary>Categoría + producto con una variante; devuelve el uuid de la variante.</summary>
    public static string Producto(UnidadTrabajo u, string nombre, long precio, long costo, string tipo = "unica", string variante = "Única")
    {
        var cat = u.Escalar<string?>("SELECT uuid FROM categorias LIMIT 1");
        if (cat is null)
        {
            cat = Uuid();
            u.Ejecutar("INSERT INTO categorias (uuid, slug, nombre) VALUES (@cat, 'maquillaje', 'Maquillaje')", new { cat });
        }
        var prod = Uuid(); var vari = Uuid();
        u.Ejecutar("INSERT INTO productos (uuid, categoria_uuid, slug, nombre, marca) VALUES (@prod, @cat, @slug, @nombre, 'Marca')",
            new { prod, cat, slug = prod, nombre });
        u.Ejecutar("""
            INSERT INTO producto_variantes (uuid, producto_uuid, tipo, nombre, precio, costo)
            VALUES (@vari, @prod, @tipo, @variante, @precio, @costo)
            """, new { vari, prod, tipo, variante, precio, costo });
        return vari;
    }

    public static void Entrada(UnidadTrabajo u, string variante, int cantidad) =>
        u.Ejecutar("INSERT INTO inventario_movimientos (variante_uuid, tipo, cantidad) VALUES (@variante, 'entrada', @cantidad)",
            new { variante, cantidad });

    /// <summary>Venta de una línea en la fecha indicada (por defecto ahora), con su movimiento de inventario.</summary>
    public static string Venta(UnidadTrabajo u, string numero, string variante, int cantidad, long precio, long costo,
                               string? fecha = null, string estado = "completada")
    {
        var venta = Uuid();
        fecha ??= DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var total = cantidad * precio;
        u.Ejecutar("""
            INSERT INTO ventas (uuid, numero, fecha, usuario_id, subtotal, descuento, total)
            VALUES (@venta, @numero, @fecha, 1, @total, 0, @total)
            """, new { venta, numero, fecha, total });
        u.Ejecutar("""
            INSERT INTO venta_detalle (venta_uuid, variante_uuid, producto_nombre, variante_nombre, cantidad, precio_unitario, costo_unitario, subtotal)
            VALUES (@venta, @variante, 'Producto', 'Única', @cantidad, @precio, @costo, @total)
            """, new { venta, variante, cantidad, precio, costo, total });
        u.Ejecutar("INSERT INTO inventario_movimientos (variante_uuid, tipo, cantidad, referencia_uuid, fecha) VALUES (@variante, 'venta', @menos, @venta, @fecha)",
            new { variante, menos = -cantidad, venta, fecha });
        if (estado == "anulada")
        {
            u.Ejecutar("UPDATE ventas SET estado = 'anulada', motivo_anulacion = 'prueba' WHERE uuid = @venta", new { venta });
            u.Ejecutar("INSERT INTO inventario_movimientos (variante_uuid, tipo, cantidad, referencia_uuid) VALUES (@variante, 'anulacion', @cantidad, @venta)",
                new { variante, cantidad, venta });
        }
        return venta;
    }
}
