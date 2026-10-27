using System.Windows;
using NABeauty.App.Servicios;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.App.Ventanas;

public partial class CambiarClaveWindow : Window
{
    private readonly CambiarClaveViewModel _vm;

    public CambiarClaveWindow(CambiarClaveViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        Loaded += (_, _) => Actual.Enfocar();
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Cambiar(Actual.Password, Nueva.Password, Repetida.Password))
        {
            MessageBox.Show(this, "Contraseña actualizada.", DialogosWpf.TituloApp, MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            return;
        }
        Nueva.Limpiar();
        Repetida.Limpiar();
    }
}
