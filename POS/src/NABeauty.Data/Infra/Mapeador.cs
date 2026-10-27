using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.Reflection;

namespace NABeauty.Data.Infra;

/// <summary>
/// Convierte filas en objetos: la columna "fecha_pago" llena la propiedad "FechaPago".
/// Convierte tipos de SQLite (INTEGER=long, REAL=double, TEXT) a los de la entidad.
/// </summary>
internal static class Mapeador
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> Cache = new();

    public static List<T> Leer<T>(DbDataReader r) where T : new()
    {
        var props = Cache.GetOrAdd(typeof(T), t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name.ToLowerInvariant(), StringComparer.Ordinal));

        var columnas = new PropertyInfo?[r.FieldCount];
        for (var i = 0; i < r.FieldCount; i++)
        {
            var nombre = r.GetName(i).Replace("_", "").ToLowerInvariant();
            // "usuario" en la BD → "UsuarioLogin" en la entidad Usuario
            if (nombre == "usuario" && typeof(T).Name == "Usuario") nombre = "usuariologin";
            columnas[i] = props.GetValueOrDefault(nombre);
        }

        var lista = new List<T>();
        while (r.Read())
        {
            var obj = new T();
            for (var i = 0; i < columnas.Length; i++)
            {
                var p = columnas[i];
                if (p is null || r.IsDBNull(i)) continue;
                p.SetValue(obj, Convertir(r.GetValue(i), p.PropertyType));
            }
            lista.Add(obj);
        }
        return lista;
    }

    public static T Convertir<T>(object? valor) => (T)Convertir(valor, typeof(T))!;

    public static object? Convertir(object? valor, Type destino)
    {
        var tipo = Nullable.GetUnderlyingType(destino) ?? destino;
        if (valor is null || valor is DBNull)
            return destino.IsValueType && Nullable.GetUnderlyingType(destino) is null ? Activator.CreateInstance(destino) : null;

        if (tipo == typeof(string)) return Convert.ToString(valor, CultureInfo.InvariantCulture);
        if (tipo == typeof(bool)) return Convert.ToInt64(valor, CultureInfo.InvariantCulture) != 0;
        if (tipo == typeof(decimal)) return Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
        return Convert.ChangeType(valor, tipo, CultureInfo.InvariantCulture);
    }

    /// <summary>Valor C# → valor para SQLite.</summary>
    public static object AParametro(object? v) => v switch
    {
        null => DBNull.Value,
        bool b => b ? 1L : 0L,
        decimal d => (double)d,
        DateOnly f => f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime f => f.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        Enum e => e.ToString().ToLowerInvariant(),
        _ => v,
    };
}
