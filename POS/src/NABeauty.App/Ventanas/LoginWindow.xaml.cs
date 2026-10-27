using System.Windows;
using System.Windows.Input;
using NABeauty.App.Servicios;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.App.Ventanas;

/// <summary>
/// Login (diseño de AnaMarcolPOS). La contraseña se entrega al ViewModel solo al pulsar Entrar
/// y se limpia si falla: nunca queda enlazada ni guardada en el ViewModel.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public LoginWindow(LoginViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        // Sin esto la ventana abre sin foco: habría que dar un clic antes de escribir
        Loaded += (_, _) => { TxtUsuario.Focus(); RevisarMayusculas(); };
    }

    private void Entrar_Click(object sender, RoutedEventArgs e) => Entrar();

    // Enter en la contraseña entra; en el usuario pasa a la contraseña
    private void TxtClave_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Entrar();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && TxtUsuario.IsKeyboardFocusWithin) { TxtClave.Enfocar(); e.Handled = true; }
        else if (e.Key == Key.Escape) Cerrar_Click(sender, e);
        RevisarMayusculas();
    }

    // Bloq Mayús puede activarse fuera de la ventana: se revisa también al recuperar el foco
    private void Window_Activated(object? sender, EventArgs e) => RevisarMayusculas();

    private void RevisarMayusculas()
        => AvisoMayus.Visibility = Keyboard.IsKeyToggled(Key.CapsLock) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>La ventana no tiene barra de título: se arrastra desde cualquier parte libre.</summary>
    private void Ventana_Arrastrar(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Tema_Click(object sender, RoutedEventArgs e) => TemaService.Alternar();

    private void Cerrar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Entrar()
    {
        if (_vm.Ingresar(TxtClave.Password))
        {
            DialogResult = true;
            return;
        }
        // Intento fallido: limpiar la contraseña y volver a ella, sin obligar a borrar a mano
        TxtClave.Limpiar();
        TxtClave.Enfocar();
    }
}
