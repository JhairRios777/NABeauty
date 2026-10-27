using System.Windows;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.App.Servicios;

/// <summary>Implementación WPF de los diálogos que piden los ViewModels.</summary>
public sealed class DialogosWpf : IDialogos
{
    public const string TituloApp = "N&A Beauty POS";

    private static Window? Duena => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

    public void Info(string mensaje) => Mostrar(mensaje, MessageBoxButton.OK, MessageBoxImage.Information);
    public void Error(string mensaje) => Mostrar(mensaje, MessageBoxButton.OK, MessageBoxImage.Warning);
    public bool Confirmar(string mensaje) => Mostrar(mensaje, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>Selector de Windows para elegir una o varias fotos.</summary>
    public IReadOnlyList<string> ElegirImagenes()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Elegir fotos del producto",
            Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        var ok = Duena is { } w ? dlg.ShowDialog(w) : dlg.ShowDialog();
        return ok == true ? dlg.FileNames : [];
    }

    /// <summary>"Guardar como" en Documentos. null si se cancela.</summary>
    public string? GuardarComo(string nombreSugerido, string extension, string descripcion)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar",
            FileName = nombreSugerido,
            DefaultExt = "." + extension,
            AddExtension = true,
            OverwritePrompt = true,
            Filter = $"{descripcion} (*.{extension})|*.{extension}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        var ok = Duena is { } w ? dlg.ShowDialog(w) : dlg.ShowDialog();
        return ok == true ? dlg.FileName : null;
    }

    /// <summary>Abre el archivo o la carpeta con el programa predeterminado de Windows (Excel, Explorador…).</summary>
    public void Abrir(string ruta)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Error($"No se pudo abrir {ruta}:\n{ex.Message}");
        }
    }

    /// <summary>MessageBox con dueño si hay ventana activa (no se esconde detrás de la principal).</summary>
    private static MessageBoxResult Mostrar(string mensaje, MessageBoxButton botones, MessageBoxImage icono) =>
        Duena is { } w
            ? MessageBox.Show(w, mensaje, TituloApp, botones, icono)
            : MessageBox.Show(mensaje, TituloApp, botones, icono);
}
