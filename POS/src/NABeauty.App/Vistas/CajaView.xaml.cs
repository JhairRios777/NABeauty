using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.App.Vistas;

/// <summary>
/// Solo manejo de teclado y foco (la lógica está en CajaViewModel):
/// la búsqueda recupera el foco después de cada acción para que el escáner siempre "escriba" ahí.
/// </summary>
public partial class CajaView : UserControl
{
    public CajaView()
    {
        InitializeComponent();
        Loaded += (_, _) => Buscador.Focus();
    }

    private CajaViewModel? Vm => DataContext as CajaViewModel;

    private void Buscador_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                // El binding tiene Delay: se fuerza a mandar lo escrito antes de buscar el código
                Buscador.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                Vm?.Enter();
                e.Handled = true;
                break;
            case Key.Escape:
                Vm?.LimpiarBusqueda();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Enter en el efectivo recibido cobra directamente.</summary>
    private void Recibido_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Vm is not { } vm) return;
        CampoRecibido.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (vm.Cobrar()) Buscador.Focus();
        e.Handled = true;
    }

    /// <summary>Después de tocar un producto, el foco vuelve a la búsqueda (listo para escanear).</summary>
    private void Articulo_Click(object sender, RoutedEventArgs e) =>
        Dispatcher.InvokeAsync(() => { Buscador.Focus(); });

    /// <summary>
    /// Si se escribe (o escanea) con el foco fuera de un campo de texto, lo escrito va a la búsqueda.
    /// </summary>
    private void Caja_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.OriginalSource is TextBox || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
        Buscador.Focus();
        Buscador.Text += e.Text;
        Buscador.CaretIndex = Buscador.Text.Length;
        e.Handled = true;
    }
}
