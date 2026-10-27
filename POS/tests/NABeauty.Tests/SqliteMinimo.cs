using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace NABeauty.Tests;

/// <summary>
/// Proveedor ADO.NET mínimo sobre la librería nativa sqlite3 del sistema.
/// SOLO PARA PRUEBAS: permite correr los servicios reales de NABeauty.Data sin
/// el paquete Microsoft.Data.Sqlite. Formatea los errores igual que ese paquete
/// ("SQLite Error 19: 'mensaje'.") para probar también la traducción de errores.
/// </summary>
internal static partial class Nativo
{
    private const string Lib = "libsqlite3.so.0";
    public const int OK = 0, ROW = 100, DONE = 101;
    public const int INTEGER = 1, FLOAT = 2, TEXT = 3, BLOB = 4, NULL = 5;
    public static readonly IntPtr Transient = new(-1);

    [LibraryImport(Lib, EntryPoint = "sqlite3_open_v2", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int Open(string nombre, out IntPtr db, int flags, IntPtr vfs);
    [LibraryImport(Lib, EntryPoint = "sqlite3_close_v2")] public static partial int Close(IntPtr db);
    [LibraryImport(Lib, EntryPoint = "sqlite3_prepare_v2")] public static partial int Prepare(IntPtr db, IntPtr sql, int n, out IntPtr stmt, out IntPtr tail);
    [LibraryImport(Lib, EntryPoint = "sqlite3_step")] public static partial int Step(IntPtr stmt);
    [LibraryImport(Lib, EntryPoint = "sqlite3_finalize")] public static partial int Finalize(IntPtr stmt);
    [LibraryImport(Lib, EntryPoint = "sqlite3_errmsg")] public static partial IntPtr ErrMsg(IntPtr db);
    [LibraryImport(Lib, EntryPoint = "sqlite3_extended_errcode")] public static partial int ErrCode(IntPtr db);
    [LibraryImport(Lib, EntryPoint = "sqlite3_bind_parameter_index", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int BindIndex(IntPtr stmt, string nombre);
    [LibraryImport(Lib, EntryPoint = "sqlite3_bind_int64")] public static partial int BindInt64(IntPtr stmt, int i, long v);
    [LibraryImport(Lib, EntryPoint = "sqlite3_bind_double")] public static partial int BindDouble(IntPtr stmt, int i, double v);
    [LibraryImport(Lib, EntryPoint = "sqlite3_bind_text")] public static partial int BindText(IntPtr stmt, int i, byte[] v, int n, IntPtr destructor);
    [LibraryImport(Lib, EntryPoint = "sqlite3_bind_null")] public static partial int BindNull(IntPtr stmt, int i);
    [LibraryImport(Lib, EntryPoint = "sqlite3_column_count")] public static partial int ColumnCount(IntPtr stmt);
    [LibraryImport(Lib, EntryPoint = "sqlite3_column_name")] public static partial IntPtr ColumnName(IntPtr stmt, int i);
    [LibraryImport(Lib, EntryPoint = "sqlite3_column_type")] public static partial int ColumnType(IntPtr stmt, int i);
    [LibraryImport(Lib, EntryPoint = "sqlite3_column_int64")] public static partial long ColumnInt64(IntPtr stmt, int i);
    [LibraryImport(Lib, EntryPoint = "sqlite3_column_double")] public static partial double ColumnDouble(IntPtr stmt, int i);
    [LibraryImport(Lib, EntryPoint = "sqlite3_column_text")] public static partial IntPtr ColumnText(IntPtr stmt, int i);
    [LibraryImport(Lib, EntryPoint = "sqlite3_column_bytes")] public static partial int ColumnBytes(IntPtr stmt, int i);
    [LibraryImport(Lib, EntryPoint = "sqlite3_changes")] public static partial int Changes(IntPtr db);

    public static string Utf8(IntPtr p) => Marshal.PtrToStringUTF8(p) ?? "";
}

internal sealed class ErrorSqlite(string m) : DbException(m);

internal sealed class ConexionMinima(string archivo) : DbConnection
{
    internal IntPtr Db;
    private ConnectionState _estado = ConnectionState.Closed;
    [AllowNull] public override string ConnectionString { get; set; } = archivo;
    public override string Database => "main";
    public override string DataSource => archivo;
    public override string ServerVersion => "3";
    public override ConnectionState State => _estado;

    public override void Open()
    {
        // 0x06 = READWRITE | CREATE; 0x40 = URI (permite "file:x?mode=memory&cache=shared")
        if (Nativo.Open(archivo, out Db, 0x06 | 0x40, IntPtr.Zero) != Nativo.OK) throw new ErrorSqlite("No se pudo abrir la BD");
        _estado = ConnectionState.Open;
    }
    public override void Close() { if (Db != IntPtr.Zero) { Nativo.Close(Db); Db = IntPtr.Zero; } _estado = ConnectionState.Closed; }
    protected override void Dispose(bool disposing) { Close(); base.Dispose(disposing); }
    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
    protected override DbCommand CreateDbCommand() => new ComandoMinimo { Connection = this };
    /// <summary>Transacción en curso. Igual que Microsoft.Data.Sqlite: queda en null tras Commit/Rollback.</summary>
    internal TransaccionMinima? Activa;
    protected override DbTransaction BeginDbTransaction(IsolationLevel il)
    {
        Ejecutar("BEGIN IMMEDIATE");
        return Activa = new TransaccionMinima(this);
    }
    internal void Ejecutar(string sql) { using var c = new ComandoMinimo { Connection = this, Interno = true }; c.CommandText = sql; c.ExecuteNonQuery(); }
}

internal sealed class TransaccionMinima(ConexionMinima cn) : DbTransaction
{
    private bool _fin;
    public override IsolationLevel IsolationLevel => IsolationLevel.Serializable;
    protected override DbConnection DbConnection => cn;
    public override void Commit() { cn.Ejecutar("COMMIT"); _fin = true; cn.Activa = null; }
    public override void Rollback() { if (!_fin) { cn.Ejecutar("ROLLBACK"); _fin = true; cn.Activa = null; } }
}

internal sealed class ParametroMinimo : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; } = true;
    [AllowNull] public override string ParameterName { get; set; } = "";
    [AllowNull] public override string SourceColumn { get; set; } = "";
    public override object? Value { get; set; }
    public override bool SourceColumnNullMapping { get; set; }
    public override int Size { get; set; }
    public override void ResetDbType() { }
}

internal sealed class ParametrosMinimos : DbParameterCollection
{
    internal readonly List<ParametroMinimo> L = [];
    public override int Count => L.Count;
    public override object SyncRoot => L;
    public override int Add(object value) { L.Add((ParametroMinimo)value); return L.Count - 1; }
    public override void AddRange(Array values) { foreach (var v in values) Add(v); }
    public override void Clear() => L.Clear();
    public override bool Contains(object value) => L.Contains((ParametroMinimo)value);
    public override bool Contains(string value) => L.Any(p => p.ParameterName == value);
    public override void CopyTo(Array array, int index) => ((ICollection)L).CopyTo(array, index);
    public override IEnumerator GetEnumerator() => L.GetEnumerator();
    public override int IndexOf(object value) => L.IndexOf((ParametroMinimo)value);
    public override int IndexOf(string parameterName) => L.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => L.Insert(index, (ParametroMinimo)value);
    public override void Remove(object value) => L.Remove((ParametroMinimo)value);
    public override void RemoveAt(int index) => L.RemoveAt(index);
    public override void RemoveAt(string parameterName) => L.RemoveAt(IndexOf(parameterName));
    protected override DbParameter GetParameter(int index) => L[index];
    protected override DbParameter GetParameter(string parameterName) => L[IndexOf(parameterName)];
    protected override void SetParameter(int index, DbParameter value) => L[index] = (ParametroMinimo)value;
    protected override void SetParameter(string parameterName, DbParameter value) => L[IndexOf(parameterName)] = (ParametroMinimo)value;
}

internal sealed class ComandoMinimo : DbCommand
{
    private readonly ParametrosMinimos _p = new();
    [AllowNull] public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => _p;
    protected override DbTransaction? DbTransaction { get; set; }
    public override void Cancel() { }
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => new ParametroMinimo();
    private IntPtr Db => ((ConexionMinima)Connection!).Db;
    internal bool Interno;

    /// <summary>
    /// Misma validación que Microsoft.Data.Sqlite (SqliteCommand.ExecuteReader): la transacción
    /// del comando debe ser exactamente la activa de la conexión (o ambas null).
    /// </summary>
    private void ValidarTransaccion()
    {
        if (Interno) return;
        var activa = ((ConexionMinima)Connection!).Activa;
        if (!ReferenceEquals(Transaction, activa))
            throw new InvalidOperationException(Transaction is null
                ? "Execute requires the command to have a transaction object when the connection assigned to the command is in a pending local transaction."
                : "The transaction object is not associated with the same connection object as this command.");
    }

    /// <summary>Ejecuta todas las sentencias del texto; devuelve las filas de la ÚLTIMA que produce columnas.</summary>
    private (List<string> Cols, List<object?[]> Filas, int Cambios) Correr()
    {
        ValidarTransaccion();
        var bytes = Encoding.UTF8.GetBytes(CommandText + "\0");
        var h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var actual = h.AddrOfPinnedObject();
            var fin = actual + bytes.Length - 1;
            List<string> cols = []; List<object?[]> filas = []; var cambios = 0;
            while (actual < fin)
            {
                if (Nativo.Prepare(Db, actual, -1, out var st, out var tail) != Nativo.OK) Fallar();
                actual = tail;
                if (st == IntPtr.Zero) continue;           // espacio o comentario final
                try
                {
                    foreach (var p in _p.L)
                    {
                        var i = Nativo.BindIndex(st, p.ParameterName);
                        if (i == 0) continue;
                        switch (p.Value)
                        {
                            case null or DBNull: Nativo.BindNull(st, i); break;
                            case long l: Nativo.BindInt64(st, i, l); break;
                            case int n: Nativo.BindInt64(st, i, n); break;
                            case double d: Nativo.BindDouble(st, i, d); break;
                            default:
                                var b = Encoding.UTF8.GetBytes(Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)!);
                                Nativo.BindText(st, i, b, b.Length, Nativo.Transient); break;
                        }
                    }
                    var n2 = Nativo.ColumnCount(st);
                    var c = Enumerable.Range(0, n2).Select(i => Nativo.Utf8(Nativo.ColumnName(st, i))).ToList();
                    var f = new List<object?[]>();
                    int rc;
                    while ((rc = Nativo.Step(st)) == Nativo.ROW)
                    {
                        var fila = new object?[n2];
                        for (var i = 0; i < n2; i++)
                            fila[i] = Nativo.ColumnType(st, i) switch
                            {
                                Nativo.INTEGER => Nativo.ColumnInt64(st, i),
                                Nativo.FLOAT => Nativo.ColumnDouble(st, i),
                                Nativo.NULL => null,
                                _ => Nativo.Utf8(Nativo.ColumnText(st, i)),
                            };
                        f.Add(fila);
                    }
                    if (rc != Nativo.DONE) Fallar();
                    if (n2 > 0) { cols = c; filas = f; } else cambios = Nativo.Changes(Db);
                }
                finally { Nativo.Finalize(st); }
            }
            return (cols, filas, cambios);
        }
        finally { h.Free(); }
    }

    [DoesNotReturn]
    private void Fallar() => throw new ErrorSqlite($"SQLite Error {Nativo.ErrCode(Db) & 0xFF}: '{Nativo.Utf8(Nativo.ErrMsg(Db))}'.");

    public override int ExecuteNonQuery() => Correr().Cambios;
    public override object? ExecuteScalar() { var r = Correr(); return r.Filas.Count > 0 ? r.Filas[0][0] : null; }
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) { var r = Correr(); return new LectorMinimo(r.Cols, r.Filas); }
}

internal sealed class LectorMinimo(List<string> cols, List<object?[]> filas) : DbDataReader
{
    private int _i = -1;
    public override bool Read() => ++_i < filas.Count;
    public override int FieldCount => cols.Count;
    public override string GetName(int ordinal) => cols[ordinal];
    public override object GetValue(int ordinal) => filas[_i][ordinal] ?? DBNull.Value;
    public override bool IsDBNull(int ordinal) => filas[_i][ordinal] is null;
    public override int GetOrdinal(string name) => cols.IndexOf(name);
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override bool HasRows => filas.Count > 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => 0;
    public override int Depth => 0;
    public override bool NextResult() => false;
    public override IEnumerator GetEnumerator() => throw new NotSupportedException();
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
    public override Type GetFieldType(int ordinal) => typeof(object);
    public override int GetValues(object[] values) { for (var i = 0; i < cols.Count; i++) values[i] = GetValue(i); return cols.Count; }
    public override bool GetBoolean(int o) => Convert.ToBoolean(GetValue(o));
    public override byte GetByte(int o) => Convert.ToByte(GetValue(o));
    public override long GetBytes(int o, long d, byte[]? b, int bo, int l) => throw new NotSupportedException();
    public override char GetChar(int o) => Convert.ToChar(GetValue(o));
    public override long GetChars(int o, long d, char[]? b, int bo, int l) => throw new NotSupportedException();
    public override DateTime GetDateTime(int o) => Convert.ToDateTime(GetValue(o));
    public override decimal GetDecimal(int o) => Convert.ToDecimal(GetValue(o));
    public override double GetDouble(int o) => Convert.ToDouble(GetValue(o));
    public override float GetFloat(int o) => Convert.ToSingle(GetValue(o));
    public override Guid GetGuid(int o) => Guid.Parse(GetString(o));
    public override short GetInt16(int o) => Convert.ToInt16(GetValue(o));
    public override int GetInt32(int o) => Convert.ToInt32(GetValue(o));
    public override long GetInt64(int o) => Convert.ToInt64(GetValue(o));
    public override string GetString(int o) => Convert.ToString(GetValue(o))!;
}
