using System.Windows;
using System.Windows.Input;
using NABeauty.App.Servicios;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.App.Ventanas;

/// <summary>Ventana principal. Solo conecta botones de la barra con el ViewModel y la app.</summary>
public partial class MainWindow : Window
{
    private readonly ShellViewModel _vm;

    public MainWindow(ShellViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        // F5 recarga los datos de la pantalla actual
        InputBindings.Add(new KeyBinding(new RelayCommand(_vm.Refrescar), Key.F5, ModifierKeys.None));
    }

    private void Refrescar_Click(object sender, RoutedEventArgs e) => _vm.Refrescar();

    private void Tema_Click(object sender, RoutedEventArgs e) => TemaService.Alternar();

    private void CambiarClave_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var w = new CambiarClaveWindow(new CambiarClaveViewModel(app.SesionActual!)) { Owner = this };
        w.ShowDialog();
    }

    private void CerrarSesion_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).CerrarSesion();

    /// <summary>ICommand mínimo para atajos de teclado de la ventana.</summary>
    private sealed class RelayCommand(Action accion) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => accion();
    }
}
