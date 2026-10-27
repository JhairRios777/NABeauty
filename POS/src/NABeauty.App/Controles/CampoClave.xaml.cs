using System.Windows;
using System.Windows.Controls;

namespace NABeauty.App.Controles;

/// <summary>
/// Contraseña con botón de ojo. Expone Password (lectura/escritura), Limpiar() y Enfocar(),
/// y el evento ClaveCambiada. Se usa en el login, cambiar contraseña y ajustes.
/// </summary>
public partial class CampoClave : UserControl
{
    private const string IconoVer = "";      // ojo
    private const string IconoOcultar = "";  // ojo tachado
    private bool _sincronizando;

    public static readonly DependencyProperty RellenoProperty = DependencyProperty.Register(
        nameof(Relleno), typeof(Thickness), typeof(CampoClave), new PropertyMetadata(new Thickness(9, 6, 40, 6)));

    /// <summary>Padding interno (a la derecha siempre queda espacio para el ojo).</summary>
    public Thickness Relleno { get => (Thickness)GetValue(RellenoProperty); set => SetValue(RellenoProperty, value); }

    public event EventHandler? ClaveCambiada;

    public CampoClave()
    {
        InitializeComponent();
        MinHeight = 36;
    }

    public bool Mostrando => CajaVisible.Visibility == Visibility.Visible;

    public string Password
    {
        get => Mostrando ? CajaVisible.Text : CajaOculta.Password;
        set { _sincronizando = true; CajaOculta.Password = value; CajaVisible.Text = value; _sincronizando = false; }
    }

    /// <summary>Vacía el campo y vuelve a ocultarlo.</summary>
    public void Limpiar()
    {
        Password = "";
        if (Mostrando) Alternar();
    }

    public void Enfocar()
    {
        if (Mostrando) { CajaVisible.Focus(); CajaVisible.CaretIndex = CajaVisible.Text.Length; }
        else CajaOculta.Focus();
    }

    private void Ojo_Click(object sender, RoutedEventArgs e)
    {
        Alternar();
        Enfocar();
    }

    private void Alternar()
    {
        _sincronizando = true;
        if (Mostrando)
        {
            CajaOculta.Password = CajaVisible.Text;
            CajaVisible.Text = "";               // el texto en claro no queda en el TextBox oculto
            CajaVisible.Visibility = Visibility.Collapsed;
            CajaOculta.Visibility = Visibility.Visible;
            Ojo.Content = IconoVer;
            Ojo.ToolTip = "Mostrar contraseña";
        }
        else
        {
            CajaVisible.Text = CajaOculta.Password;
            CajaOculta.Visibility = Visibility.Collapsed;
            CajaVisible.Visibility = Visibility.Visible;
            Ojo.Content = IconoOcultar;
            Ojo.ToolTip = "Ocultar contraseña";
        }
        _sincronizando = false;
    }

    private void CajaOculta_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_sincronizando) ClaveCambiada?.Invoke(this, EventArgs.Empty);
    }

    private void CajaVisible_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_sincronizando) ClaveCambiada?.Invoke(this, EventArgs.Empty);
    }
}
