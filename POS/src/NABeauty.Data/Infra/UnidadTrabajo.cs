using System.Data.Common;
using System.Reflection;
using NABeauty.Core.Dominio;

namespace NABeauty.Data.Infra;

/// <summary>
/// Conexión (y transacción opcional) compartida por los repositorios de una operación.
/// Todo el SQL pasa por aquí con parámetros (@nombre): nunca se concatena un valor.
/// Los errores de las reglas de la BD (triggers RAISE, CHECK, UNIQUE) se convierten
/// en ReglaNegocioException con un mensaje legible.
/// </summary>
public sealed class UnidadTrabajo(DbConnection conexion, DbTransaction? transaccion) : IDisposable
{
    // Mutable: tras Confirmar() queda en null. Microsoft.Data.Sqlite exige que cmd.Transaction
    // sea la transacción ACTIVA de la conexión; una ya confirmada provoca
    // "The transaction object is not associated with the same connection object as this command".
    private DbTransaction? _tx = transaccion;

    /// <summary>
    /// Guarda los cambios. Las lecturas posteriores en esta misma unidad siguen funcionando
    /// (corren en modo autocommit, ya sin transacción).
    /// </summary>
    public void Confirmar()
    {
        if (_tx is null) return;
        _tx.Commit();
        _tx.Dispose();
        _tx = null;
    }

    public void Dispose()
    {
        if (_tx is not null)
        {
            try { _tx.Rollback(); } catch { /* la conexión ya pudo cerrarse */ }
            _tx.Dispose();
            _tx = null;
        }
        conexion.Dispose();
    }

    // ─────────────────────────────────────────────
    public int Ejecutar(string sql, object? parametros = null)
        => Envolver(() => Comando(sql, parametros).ExecuteNonQuery());

    public T Escalar<T>(string sql, object? parametros = null)
        => Envolver(() => Mapeador.Convertir<T>(Comando(sql, parametros).ExecuteScalar()));

    /// <summary>INSERT y devuelve el id generado.</summary>
    public long Insertar(string sql, object? parametros = null)
        => Envolver(() =>
        {
            Comando(sql, parametros).ExecuteNonQuery();
            return Mapeador.Convertir<long>(Comando("SELECT last_insert_rowid()", null).ExecuteScalar());
        });

    public List<T> Lista<T>(string sql, object? parametros = null) where T : new()
        => Envolver(() =>
        {
            using var r = Comando(sql, parametros).ExecuteReader();
            return Mapeador.Leer<T>(r);
        });

    public T? Uno<T>(string sql, object? parametros = null) where T : class, new()
        => Lista<T>(sql, parametros).FirstOrDefault();

    /// <summary>Lista de valores de una sola columna.</summary>
    public List<T> Columna<T>(string sql, object? parametros = null)
        => Envolver(() =>
        {
            using var r = Comando(sql, parametros).ExecuteReader();
            var lista = new List<T>();
            while (r.Read()) lista.Add(Mapeador.Convertir<T>(r.IsDBNull(0) ? null : r.GetValue(0)));
            return lista;
        });

    // ─────────────────────────────────────────────
    private DbCommand Comando(string sql, object? parametros)
    {
        var cmd = conexion.CreateCommand();
        cmd.Transaction = _tx;
        cmd.CommandText = sql;
        if (parametros is null) return cmd;

        foreach (var p in parametros.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var param = cmd.CreateParameter();
            param.ParameterName = "@" + p.Name;
            param.Value = Mapeador.AParametro(p.GetValue(parametros));
            cmd.Parameters.Add(param);
        }
        return cmd;
    }

    private static T Envolver<T>(Func<T> accion)
    {
        try
        {
            return accion();
        }
        catch (DbException ex)
        {
            var regla = TraducirError(ex.Message);
            if (regla is not null) throw new ReglaNegocioException(regla, ex);
            throw;
        }
    }

    /// <summary>
    /// Microsoft.Data.Sqlite reporta un RAISE así: "SQLite Error 19: 'El monto supera…'."
    /// Se extrae el texto del trigger; CHECK/UNIQUE se traducen a mensajes generales.
    /// </summary>
    internal static string? TraducirError(string mensaje)
    {
        if (mensaje.Contains("UNIQUE constraint failed", StringComparison.Ordinal))
            return "Ya existe un registro con esos datos.";
        if (mensaje.Contains("CHECK constraint failed", StringComparison.Ordinal))
            return "Los datos no cumplen una regla del sistema.";
        if (mensaje.Contains("FOREIGN KEY constraint failed", StringComparison.Ordinal))
            return "El registro está relacionado con otros datos.";

        var ini = mensaje.IndexOf('\'');
        var fin = mensaje.LastIndexOf('\'');
        if (mensaje.Contains("Error 19", StringComparison.Ordinal) && ini >= 0 && fin > ini)
            return mensaje[(ini + 1)..fin];

        return null;
    }
}
