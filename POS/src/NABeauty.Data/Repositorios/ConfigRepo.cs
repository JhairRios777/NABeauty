using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>Ajustes clave/valor (empresa, landing, correo).</summary>
public sealed class ConfigRepo(UnidadTrabajo u)
{
    public string? Get(string clave) => u.Escalar<string?>("SELECT valor FROM config WHERE clave = @clave", new { clave });

    public void Set(string clave, string? valor) => u.Ejecutar(
        "INSERT INTO config (clave, valor) VALUES (@clave, @valor) ON CONFLICT(clave) DO UPDATE SET valor = excluded.valor",
        new { clave, valor });
}
