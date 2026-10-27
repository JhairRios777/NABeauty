using NABeauty.Core.Dominio;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>
/// Login. La contraseña no se enlaza (PasswordBox no lo permite a propósito):
/// la ventana la entrega a Ingresar() y no queda guardada en el ViewModel.
/// </summary>
public sealed class LoginViewModel(Sesion sesion) : Observable
{
    private string _usuario = "";
    private string _error = "";

    public string Usuario { get => _usuario; set => Set(ref _usuario, value); }
    public string Error { get => _error; set { Set(ref _error, value); Avisar(nameof(HayError)); } }
    public bool HayError => Error.Length > 0;

    public bool Ingresar(string password)
    {
        try
        {
            sesion.Iniciar(new LoginService(sesion.Bd).Ingresar(Usuario, password));
            Error = "";
            return true;
        }
        catch (ReglaNegocioException ex)
        {
            Error = ex.Message;
            return false;
        }
    }
}

/// <summary>Cambio de contraseña (obligatorio si es temporal).</summary>
public sealed class CambiarClaveViewModel(Sesion sesion) : Observable
{
    private string _error = "";
    public string Error { get => _error; set { Set(ref _error, value); Avisar(nameof(HayError)); } }
    public bool HayError => Error.Length > 0;
    public bool Obligatorio => sesion.Usuario?.DebeCambiarPassword ?? false;

    public bool Cambiar(string actual, string nueva, string repetida)
    {
        try
        {
            new LoginService(sesion.Bd).CambiarPassword(sesion.Usuario!, actual, nueva, repetida);
            return true;
        }
        catch (ReglaNegocioException ex)
        {
            Error = ex.Message;
            return false;
        }
    }
}
