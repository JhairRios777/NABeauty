using System.Windows.Controls;
using System.Windows.Input;
using NABeauty.Core.Entidades;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.App.Vistas;

public partial class ProductosView : UserControl
{
    public ProductosView()
    {
        InitializeComponent();
        Loaded += (_, _) => Buscador.Focus();
    }

    /// <summary>Enter sobre una fila abre el producto (el DataGrid por defecto baja a la siguiente fila).</summary>
    private void Tabla_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Tabla.SelectedItem is not ProductoFila f || DataContext is not ProductosViewModel vm) return;
        vm.Abrir(f.Uuid);
        e.Handled = true;
    }
}
