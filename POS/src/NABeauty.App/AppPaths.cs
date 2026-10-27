using System.IO;

namespace NABeauty.App;

/// <summary>
/// Dónde vive cada archivo. Todo en %LOCALAPPDATA%\NABeautyPOS (por usuario de Windows):
///   nabeauty.db   base de datos
///   Respaldos\    copia diaria automática (últimos 30 días)
///   error.log     errores inesperados
///   tema.config   tema claro u oscuro
///   Fotos\       fotos de los productos (tamaño web + miniatura)
/// No se usa la carpeta del programa porque OneDrive o Program Files pueden
/// bloquear o sincronizar a medias un archivo SQLite abierto.
/// </summary>
internal static class AppPaths
{
    public static string Carpeta { get; } = Crear(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NABeautyPOS"));

    public static string BaseDatos => Path.Combine(Carpeta, "nabeauty.db");
    public static string Respaldos => Path.Combine(Carpeta, "Respaldos");
    public static string Log => Path.Combine(Carpeta, "error.log");
    /// <summary>Fotos de productos: &lt;uuid&gt;.jpg (1200 px) y mini_&lt;uuid&gt;.jpg (200 px).</summary>
    public static string Fotos => Path.Combine(Carpeta, "Fotos");

    private static string Crear(string ruta)
    {
        Directory.CreateDirectory(ruta);
        return ruta;
    }
}
