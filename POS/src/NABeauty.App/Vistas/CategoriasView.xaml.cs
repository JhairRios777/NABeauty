using System.Windows.Controls;
using System.Windows.Input;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.App.Vistas;

public partial class CategoriasView : UserControl
{
    public CategoriasView()
    {
        InitializeComponent();
        Loaded += (_, _) => CampoNombre.Focus();
    }

    /// <summary>Enter en el nombre guarda (crear varias categorías seguidas sin usar el mouse).</summary>
    private void Nombre_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not CategoriasViewModel vm) return;
        if (vm.Guardar() && vm.EsNueva) CampoNombre.Focus();
        e.Handled = true;
    }
}
