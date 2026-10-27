using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>
/// Libro de inventario (solo INSERT). Cada fila mueve el stock de su variante por trigger:
/// nadie escribe producto_variantes.stock directamente.
/// </summary>
public sealed class InventarioRepo(UnidadTrabajo u)
{
    /// <param name="tipo">entrada | venta | ajuste | anulacion</param>
    /// <param name="cantidad">positiva = entra; negativa = sale</param>
    public void Movimiento(string varianteUuid, string tipo, long cantidad, long usuarioId, string? referenciaUuid = null, string? nota = null) =>
        u.Ejecutar("""
            INSERT INTO inventario_movimientos (variante_uuid, tipo, cantidad, referencia_uuid, usuario_id, nota)
            VALUES (@varianteUuid, @tipo, @cantidad, @referenciaUuid, @usuarioId, @nota)
            """, new { varianteUuid, tipo, cantidad, referenciaUuid, usuarioId, nota });

    public long Stock(string varianteUuid) =>
        u.Escalar<long>("SELECT stock FROM producto_variantes WHERE uuid = @varianteUuid", new { varianteUuid });
}

/// <summary>Numeraciones correlativas sin huecos (V-000001…). Se usa dentro de la transacción de la venta.</summary>
public sealed class SecuenciaRepo(UnidadTrabajo u)
{
    public long Siguiente(string clave)
    {
        u.Ejecutar("""
            INSERT INTO secuencias (clave, ultimo) VALUES (@clave, 1)
            ON CONFLICT(clave) DO UPDATE SET ultimo = ultimo + 1
            """, new { clave });
        return u.Escalar<long>("SELECT ultimo FROM secuencias WHERE clave = @clave", new { clave });
    }
}

/// <summary>Cola de salida hacia la tienda en línea: cada cambio que la web debe conocer deja una fila.</summary>
public sealed class SyncRepo(UnidadTrabajo u)
{
    /// <param name="operacion">crear | actualizar | anular</param>
    public void Encolar(string entidad, string uuid, string operacion)
    {
        // Si ya hay un cambio pendiente de la misma entidad, basta con uno: el sincronizador envía el estado actual
        var pendiente = u.Escalar<long>(
            "SELECT COUNT(*) FROM sync_cola WHERE entidad = @entidad AND uuid = @uuid AND enviado IS NULL", new { entidad, uuid });
        if (pendiente > 0 && operacion == "actualizar") return;
        u.Ejecutar("INSERT INTO sync_cola (entidad, uuid, operacion) VALUES (@entidad, @uuid, @operacion)", new { entidad, uuid, operacion });
    }
}
