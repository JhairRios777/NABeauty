using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace NABeauty.App;

/// <summary>true → Collapsed, false → Visible.</summary>
public sealed class BoolInvertidoVis : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>true → false (IsEnabled mientras algo NO pase).</summary>
public sealed class Negar : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is not true;
}

/// <summary>null o texto vacío → Collapsed. Con ConverterParameter=inv, al revés.</summary>
public sealed class NuloVis : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var vacio = value is null || value is string { Length: 0 };
        if (p as string == "inv") vacio = !vacio;
        return vacio ? Visibility.Collapsed : Visibility.Visible;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Texto en mayúsculas (encabezados de grupo del menú). WPF no tiene text-transform.</summary>
public sealed class Mayusculas : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => (value as string)?.ToUpper(c) ?? "";
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>
/// Doble clic en una fila de tabla → ejecuta un comando con el elemento de esa fila.
/// Uso: loc:Comportamientos.DobleClic="{Binding AbrirCommand}"
/// </summary>
public static class Comportamientos
{
    public static readonly DependencyProperty DobleClicProperty = DependencyProperty.RegisterAttached(
        "DobleClic", typeof(ICommand), typeof(Comportamientos), new PropertyMetadata(null, AlCambiar));

    public static ICommand? GetDobleClic(DependencyObject o) => (ICommand?)o.GetValue(DobleClicProperty);
    public static void SetDobleClic(DependencyObject o, ICommand? v) => o.SetValue(DobleClicProperty, v);

    private static void AlCambiar(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Selector lista) return;
        lista.MouseDoubleClick -= DobleClic;
        if (e.NewValue is not null) lista.MouseDoubleClick += DobleClic;
    }

    private static void DobleClic(object sender, MouseButtonEventArgs e)
    {
        var lista = (Selector)sender;
        // Solo si el doble clic fue sobre una fila (no en el encabezado ni en la barra de desplazamiento)
        var origen = e.OriginalSource as DependencyObject;
        while (origen is not null and not DataGridRow and not ListBoxItem) origen = VisualTreeHelper.GetParent(origen);
        if (origen is null) return;

        var item = (origen as FrameworkElement)?.DataContext;
        var cmd = GetDobleClic(lista);
        if (item is not null && cmd?.CanExecute(item) == true) cmd.Execute(item);
    }

    /// <summary>
    /// Tablas dentro de una pantalla con scroll: la rueda del mouse mueve la pantalla
    /// (sin esto, la tabla "se come" la rueda y la página no baja).
    /// </summary>
    public static readonly DependencyProperty PasarRuedaProperty = DependencyProperty.RegisterAttached(
        "PasarRueda", typeof(bool), typeof(Comportamientos), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not UIElement el) return;
            el.PreviewMouseWheel -= PasarRueda;
            if (e.NewValue is true) el.PreviewMouseWheel += PasarRueda;
        }));

    public static bool GetPasarRueda(DependencyObject o) => (bool)o.GetValue(PasarRuedaProperty);
    public static void SetPasarRueda(DependencyObject o, bool v) => o.SetValue(PasarRuedaProperty, v);

    private static void PasarRueda(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || VisualTreeHelper.GetParent((DependencyObject)sender) is not UIElement padre) return;
        e.Handled = true;
        padre.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = sender });
    }
}

/// <summary>
/// Foto de producto → imagen, SIEMPRE pequeña. Valor: nombre del archivo guardado (usa la
/// miniatura mini_*.jpg de 200 px) o una ruta completa (foto elegida y aún no subida).
/// ConverterParameter = ancho de decodificación en píxeles (por defecto 96): WPF decodifica
/// solo ese tamaño, así una lista de 60 productos no carga 60 fotos grandes en memoria.
/// Se carga con OnLoad (no deja el archivo bloqueado) y se guarda en caché por ruta+ancho
/// (los nombres son uuid: un archivo nunca cambia de contenido).
/// </summary>
public sealed class FotoConverter : IValueConverter
{
    private static readonly Dictionary<string, System.Windows.Media.Imaging.BitmapImage?> Cache = [];

    public object? Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not string archivo || archivo.Length == 0) return null;
        var ancho = int.TryParse(p as string, out var a) ? a : 96;
        var ruta = System.IO.Path.IsPathRooted(archivo)
            ? archivo
            : System.IO.Path.Combine(AppPaths.Fotos, "mini_" + archivo);
        var clave = $"{ruta}|{ancho}";
        if (Cache.TryGetValue(clave, out var enCache)) return enCache;

        System.Windows.Media.Imaging.BitmapImage? img = null;
        if (System.IO.File.Exists(ruta))
        {
            try
            {
                img = new System.Windows.Media.Imaging.BitmapImage();
                img.BeginInit();
                img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                img.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile;
                img.DecodePixelWidth = ancho;
                img.UriSource = new Uri(ruta, UriKind.Absolute);
                img.EndInit();
                img.Freeze();
            }
            catch (Exception) { img = null; }   // archivo dañado: se ve el ícono de "sin foto"
        }
        if (Cache.Count > 2000) Cache.Clear();
        Cache[clave] = img;
        return img;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>"#F6D5DC" → pincel (color de categoría o de tono). Inválido → transparente.</summary>
public sealed class ColorHexConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        try
        {
            if (value is string s && s.Length == 7 && s[0] == '#')
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s));
                b.Freeze();
                return b;
            }
        }
        catch (FormatException) { }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
