namespace NABeauty.Core.Dominio;

/// <summary>
/// Dónde y cómo se guardan los archivos de las fotos. La app WPF lo implementa reduciendo
/// la imagen (máx. 1200 px) y creando la miniatura (máx. 200 px); las pruebas, con un falso.
/// El servicio de catálogo solo conoce esta interfaz: la base guarda el nombre del archivo.
/// </summary>
public interface IAlmacenFotos
{
    /// <summary>Extensiones aceptadas al elegir una foto.</summary>
    static readonly string[] Extensiones = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];

    /// <summary>Tamaño máximo del archivo original (las fotos de teléfono pesan 3–8 MB).</summary>
    const long MaxBytes = 20 * 1024 * 1024;

    /// <summary>Procesa y copia la imagen de origen. Devuelve el nombre del archivo guardado.</summary>
    /// <exception cref="ReglaNegocioException">El archivo no es una imagen válida.</exception>
    string Guardar(string rutaOrigen);

    /// <summary>Borra el archivo y su miniatura (si existen). No falla si ya no están.</summary>
    void Eliminar(string archivo);
}
