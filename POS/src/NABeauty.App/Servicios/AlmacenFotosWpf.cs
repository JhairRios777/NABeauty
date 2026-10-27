using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NABeauty.Core.Dominio;

namespace NABeauty.App.Servicios;

/// <summary>
/// Guarda las fotos de los productos en %LOCALAPPDATA%\NABeautyPOS\Fotos con WPF (sin paquetes):
///   &lt;uuid&gt;.jpg       máx. 1200 px por lado, JPEG 85 — la que viaja a la tienda en línea
///   mini_&lt;uuid&gt;.jpg  máx. 200 px por lado — la que se muestra en la caja y en los listados
/// Corrige la orientación EXIF (las fotos de teléfono vienen "acostadas") y aplana la
/// transparencia de los PNG sobre blanco. Mismo criterio que ImagenService de la web.
/// </summary>
public sealed class AlmacenFotosWpf(string carpeta) : IAlmacenFotos
{
    public const int LadoGrande = 1200;
    public const int LadoMini = 200;

    public string Carpeta { get; } = Directory.CreateDirectory(carpeta).FullName;

    public string Guardar(string rutaOrigen)
    {
        BitmapSource original;
        try
        {
            using var fs = File.OpenRead(rutaOrigen);
            var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            original = Orientar(frame, LeerOrientacion(frame));
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or IOException or InvalidOperationException)
        {
            throw new ReglaNegocioException("El archivo no es una imagen válida o está dañado.", ex);
        }

        var nombre = $"{Guid.NewGuid():N}.jpg";
        Escribir(Reducir(original, LadoGrande), Path.Combine(Carpeta, nombre), 85);
        Escribir(Reducir(original, LadoMini), Path.Combine(Carpeta, "mini_" + nombre), 80);
        return nombre;
    }

    public void Eliminar(string archivo)
    {
        if (archivo.Contains('/') || archivo.Contains('\\')) return;   // solo nombres propios, nunca rutas
        foreach (var f in new[] { archivo, "mini_" + archivo })
        {
            try { File.Delete(Path.Combine(Carpeta, f)); }
            catch (IOException) { /* en uso o ya borrado: no impide seguir */ }
        }
    }

    /// <summary>Escala manteniendo proporción para que el lado mayor no pase de <paramref name="lado"/>. Nunca agranda.</summary>
    private static BitmapSource Reducir(BitmapSource img, int lado)
    {
        var escala = Math.Min(1.0, (double)lado / Math.Max(img.PixelWidth, img.PixelHeight));
        BitmapSource r = escala < 1 ? new TransformedBitmap(img, new ScaleTransform(escala, escala)) : img;

        // JPEG no tiene transparencia: se dibuja sobre fondo blanco (si no, queda negro)
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var rect = new System.Windows.Rect(0, 0, r.PixelWidth, r.PixelHeight);
            dc.DrawRectangle(Brushes.White, null, rect);
            dc.DrawImage(r, rect);
        }
        var rtb = new RenderTargetBitmap(r.PixelWidth, r.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private static void Escribir(BitmapSource img, string ruta, int calidad)
    {
        var enc = new JpegBitmapEncoder { QualityLevel = calidad };
        enc.Frames.Add(BitmapFrame.Create(img));
        using var fs = File.Create(ruta);
        enc.Save(fs);
    }

    /// <summary>Etiqueta EXIF 274 (Orientation): 1 normal, 3 = 180°, 6 = 90° horario, 8 = 270°.</summary>
    private static int LeerOrientacion(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata md && md.ContainsQuery("System.Photo.Orientation")
                && md.GetQuery("System.Photo.Orientation") is ushort o)
                return o;
        }
        catch (NotSupportedException) { /* PNG/BMP sin EXIF */ }
        return 1;
    }

    private static BitmapSource Orientar(BitmapSource img, int orientacion) => orientacion switch
    {
        3 => new TransformedBitmap(img, new RotateTransform(180)),
        6 => new TransformedBitmap(img, new RotateTransform(90)),
        8 => new TransformedBitmap(img, new RotateTransform(270)),
        _ => img,
    };
}
