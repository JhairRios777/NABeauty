using System.Text.Json;
using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

public sealed class EntradaBitacora
{
    public long Id { get; set; }
    public string? Usuario { get; set; }
    public string Accion { get; set; } = "";
    public string? Detalle { get; set; }
    public string Creado { get; set; } = "";
    public string Texto => $"{NABeauty.Core.Dominio.Formato.Fecha(Creado, true)} · {Usuario ?? "Sistema"} — {Accion}";
}

/// <summary>Registro de acciones sensibles. Solo INSERT (la BD bloquea UPDATE/DELETE).</summary>
public sealed class BitacoraRepo(UnidadTrabajo u)
{
    public void Registrar(long? usuarioId, string accion, string entidad, long? entidadId, object? detalle = null) => u.Ejecutar(
        "INSERT INTO bitacora (usuario_id, accion, entidad, entidad_id, detalle) VALUES (@usuarioId, @accion, @entidad, @entidadId, @json)",
        new { usuarioId, accion, entidad, entidadId, json = detalle is null ? null : JsonSerializer.Serialize(detalle) });

    public List<EntradaBitacora> De(string entidad, long id) => u.Lista<EntradaBitacora>(
        """
        SELECT b.id, u.nombre AS usuario, b.accion, b.detalle, b.creado
          FROM bitacora b LEFT JOIN usuarios u ON u.id = b.usuario_id
         WHERE b.entidad = @entidad AND b.entidad_id = @id ORDER BY b.id DESC LIMIT 50
        """, new { entidad, id });
}
