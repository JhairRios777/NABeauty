using System.Windows.Controls;
using System.Windows.Input;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.App.Vistas;

public partial class NuevaEntradaView : UserControl
{
    public NuevaEntradaView()
    {
        InitializeComponent();
        Loaded += (_, _) => Buscador.Focus();
    }

    /// <summary>Enter (o el escáner) agrega el producto si la búsqueda deja uno solo.</summary>
    private void Buscador_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not NuevaEntradaViewModel vm) return;
        Buscador.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        vm.Enter();
        e.Handled = true;
    }
}
