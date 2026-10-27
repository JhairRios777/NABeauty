using System.Data;
using System.Data.Common;
using System.Reflection;
using NABeauty.Core.Seguridad;

namespace NABeauty.Data.Infra;

/// <summary>
/// Punto de entrada a la base de datos local del POS. Recibe una fábrica de conexiones
/// (la app usa Microsoft.Data.Sqlite; las pruebas, un proveedor mínimo) y entrega unidades de trabajo.
/// </summary>
public sealed class BaseDatos(Func<DbConnection> fabrica)
{
    /// <summary>Usuario del primer administrador.</summary>
    public const string UsuarioInicial = "admin";

    /// <summary>Contraseña del primer administrador. La app obliga a cambiarla al primer ingreso.</summary>
    public const string ClaveInicial = "NAbeauty2026*";

    /// <summary>
    /// Abre una unidad de trabajo. Con transaccional=true todo lo que se haga
    /// dentro se guarda junto al llamar Confirmar(), o nada si ocurre un error.
    /// </summary>
    public UnidadTrabajo Abrir(bool transaccional = false)
    {
        var cn = fabrica();
        cn.Open();
        using (var cmd = cn.CreateCommand())
        {
            // Integridad referencial (SQLite la trae apagada) y espera de hasta 5 s si la base está ocupada.
            cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            cmd.ExecuteNonQuery();
        }
        return new UnidadTrabajo(cn, transaccional ? cn.BeginTransaction(IsolationLevel.Serializable) : null);
    }

    /// <summary>
    /// Crea/actualiza tablas, vistas y triggers y siembra lo mínimo para poder entrar:
    /// roles iniciales y un administrador con contraseña temporal. La base nace vacía de
    /// productos y ventas (el catálogo se carga desde el POS o se descarga de la tienda en línea).
    /// </summary>
    public void Inicializar()
    {
        using var uow = Abrir(transaccional: true);
        Migrar(uow);
        uow.Ejecutar(LeerRecurso("NABeauty.Data.schema.sql"));
        SembrarRoles(uow);

        if (uow.Escalar<long>("SELECT COUNT(*) FROM usuarios") == 0)
        {
            uow.Ejecutar(
                "INSERT INTO usuarios (uuid, nombre, usuario, password_hash, rol, debe_cambiar_password) VALUES (@uuid, @n, @u, @h, 'admin', 1)",
                new { uuid = Guid.NewGuid().ToString(), n = "Administrador", u = UsuarioInicial, h = PasswordHasher.Hash(ClaveInicial) });
        }

        uow.Ejecutar("INSERT OR IGNORE INTO config (clave, valor) VALUES ('negocio_nombre', 'N&A Beauty')");
        uow.Confirmar();
    }

    /// <summary>
    /// Columnas agregadas después de la primera versión. schema.sql ya las trae para una base nueva
    /// (CREATE TABLE IF NOT EXISTS no toca una tabla existente); aquí se agregan a una base anterior.
    /// Corre ANTES del esquema: los índices nuevos pueden depender de estas columnas.
    /// </summary>
    private static readonly (string Tabla, string Columna, string Tipo)[] ColumnasNuevas =
    [
        ("ventas", "recibido", "INTEGER"),
        ("ventas", "anulada_por", "INTEGER REFERENCES usuarios(id)"),
        ("ventas", "anulada_fecha", "TEXT"),
    ];

    private static void Migrar(UnidadTrabajo uow)
    {
        foreach (var (tabla, columna, tipo) in ColumnasNuevas)
        {
            var existeTabla = uow.Escalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @tabla", new { tabla }) > 0;
            if (!existeTabla) continue;   // base nueva: la crea schema.sql con todo
            var existe = uow.Escalar<long>($"SELECT COUNT(*) FROM pragma_table_info('{tabla}') WHERE name = @columna", new { columna }) > 0;
            if (!existe) uow.Ejecutar($"ALTER TABLE {tabla} ADD COLUMN {columna} {tipo}");
        }
    }

    /// <summary>
    /// Administrador (de sistema, protegido) + Cajera y Socio con sus permisos iniciales.
    /// Idempotente: solo inserta lo que falta y nunca pisa lo que el usuario editó.
    /// </summary>
    private static void SembrarRoles(UnidadTrabajo uow)
    {
        uow.Ejecutar("""
            INSERT OR IGNORE INTO roles (clave, nombre, descripcion, sistema)
            VALUES ('admin', 'Administrador', 'Acceso total. Rol del sistema: no se edita ni se elimina.', 1)
            """);
        if (uow.Escalar<long>("SELECT COUNT(*) FROM roles WHERE sistema = 0") > 0) return;   // ya hay roles propios

        foreach (var (clave, (nombre, descripcion, permisos)) in Permisos.Predeterminados)
        {
            var id = uow.Insertar("INSERT INTO roles (clave, nombre, descripcion) VALUES (@clave, @nombre, @descripcion)",
                new { clave, nombre, descripcion });
            foreach (var p in permisos)
                uow.Ejecutar("INSERT INTO rol_permisos (rol_id, permiso) VALUES (@id, @permiso)", new { id, permiso = p.ToString() });
        }
    }

    /// <summary>
    /// Respaldo en caliente con VACUUM INTO: copia consistente aunque la app esté en uso.
    /// Uno por día (nabeauty-AAAA-MM-DD.db); se conservan los últimos <paramref name="conservar"/>.
    /// Devuelve la ruta creada o null si el de hoy ya existía.
    /// </summary>
    public string? Respaldar(string carpeta, int conservar = 30, bool manual = false)
    {
        Directory.CreateDirectory(carpeta);
        // El automático es uno por día; el manual (botón en Ajustes) lleva la hora y siempre se crea
        var destino = Path.Combine(carpeta, manual ? $"nabeauty-{DateTime.Now:yyyy-MM-dd-HHmmss}.db" : $"nabeauty-{DateTime.Now:yyyy-MM-dd}.db");
        if (File.Exists(destino)) return null;

        using (var uow = Abrir()) uow.Ejecutar("VACUUM INTO @destino", new { destino });

        foreach (var viejo in Directory.GetFiles(carpeta, "nabeauty-*.db").OrderByDescending(f => f).Skip(conservar))
            File.Delete(viejo);
        return destino;
    }

    private static string LeerRecurso(string nombre)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(nombre)
                      ?? throw new InvalidOperationException($"Falta el recurso {nombre}");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
