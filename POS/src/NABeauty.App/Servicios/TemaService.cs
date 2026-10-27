using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NABeauty.App.Servicios;

/// <summary>
/// Tema claro / oscuro.
///   - Reemplaza en caliente MergedDictionaries[0] de App.xaml (contrato: ese índice es el tema).
///     Como todo el XAML usa DynamicResource, las pantallas abiertas cambian al instante.
///   - Guarda la preferencia en %LOCALAPPDATA%\NABeautyPOS\tema.config (por usuario de Windows).
///   - Pinta la barra de título nativa de Windows 10/11 en oscuro cuando corresponde (DWM).
/// Es una preocupación puramente visual: vive en la capa WPF, no en los ViewModels.
/// </summary>
internal static class TemaService
{
    public const string Claro = "claro";
    public const string Oscuro = "oscuro";

    private static string Archivo => Path.Combine(AppPaths.Carpeta, "tema.config");

    public static string Actual { get; private set; } = Claro;
    public static bool EsOscuro => Actual == Oscuro;

    /// <summary>Al iniciar: aplica el último tema elegido y engancha la barra de título de toda ventana nueva.</summary>
    public static void Inicializar()
    {
        string guardado = Claro;
        try { if (File.Exists(Archivo)) guardado = File.ReadAllText(Archivo).Trim(); }
        catch { /* preferencia ilegible: se usa el claro */ }

        Aplicar(guardado == Oscuro ? Oscuro : Claro, guardar: false);

        // Toda ventana (login, principal, diálogos, recibo) recibe la barra de título del tema al abrirse
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) => BarraTitulo((Window)s)));
    }

    public static void Alternar() => Aplicar(EsOscuro ? Claro : Oscuro);

    public static void Aplicar(string tema, bool guardar = true)
    {
        var uri = new Uri($"pack://application:,,,/Themes/{(tema == Oscuro ? "Oscuro" : "Claro")}.xaml");
        var diccionarios = Application.Current.Resources.MergedDictionaries;
        var nuevo = new ResourceDictionary { Source = uri };
        if (diccionarios.Count == 0) diccionarios.Add(nuevo);
        else diccionarios[0] = nuevo;

        Actual = tema == Oscuro ? Oscuro : Claro;

        foreach (Window w in Application.Current.Windows) BarraTitulo(w);

        if (!guardar) return;
        try { File.WriteAllText(Archivo, Actual); }
        catch (Exception ex) { App.Registrar(ex, "Tema"); }   // no poder guardarlo no impide usar el tema
    }

    // ─────────────────────────────────────────────
    // Barra de título oscura: DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (Windows 10 20H1+ / 11).
    // En versiones que no lo soportan la llamada devuelve error y simplemente no cambia nada.
    private const int DwmaModoOscuro = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamano);

    private static void BarraTitulo(Window w)
    {
        try
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;   // aún no tiene ventana nativa: se aplica en su Loaded
            int oscuro = EsOscuro ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DwmaModoOscuro, ref oscuro, sizeof(int));
        }
        catch { /* dwmapi no disponible: barra estándar */ }
    }
}
