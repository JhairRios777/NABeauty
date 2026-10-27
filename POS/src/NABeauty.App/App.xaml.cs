using System.IO;
using System.Windows;
using NABeauty.App.Servicios;
using NABeauty.App.Ventanas;
using NABeauty.Data.Infra;
using NABeauty.Presentacion.Mvvm;
using NABeauty.Presentacion.ViewModels;
using Microsoft.Data.Sqlite;

namespace NABeauty.App;

/// <summary>
/// Arranque: prepara la base de datos (crea/actualiza el esquema + respaldo diario),
/// pide login (y cambio de contraseña si es temporal) y abre la ventana principal.
/// </summary>
public partial class App : Application
{
    private Sesion? _sesion;
    internal Sesion? SesionActual => _sesion;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Registrar(args.Exception, "UI");
            MessageBox.Show($"Ocurrió un error inesperado:\n\n{args.Exception.Message}\n\nEl detalle quedó en {AppPaths.Log}.",
                DialogosWpf.TituloApp, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;   // la caja sigue abierta
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Registrar(args.ExceptionObject as Exception, "AppDomain");

        // Tema guardado (claro/oscuro) ANTES de la primera ventana: si no, el login parpadea
        TemaService.Inicializar();

        try
        {
            var bd = new BaseDatos(() => new SqliteConnection($"Data Source={AppPaths.BaseDatos}"));
            bd.Inicializar();
            try { bd.Respaldar(AppPaths.Respaldos); }
            catch (Exception ex) { Registrar(ex, "Respaldo"); }   // un respaldo fallido no impide vender

            _sesion = new Sesion(bd, new DialogosWpf())
            {
                Fotos = new AlmacenFotosWpf(AppPaths.Fotos),
                Impresion = new ImpresionWpf(),
                CarpetaRespaldos = AppPaths.Respaldos,
            };
            PedirLoginYAbrir();
        }
        catch (Exception ex)
        {
            Registrar(ex, "Inicio");
            MessageBox.Show($"No se pudo iniciar el POS:\n\n{ex.Message}", DialogosWpf.TituloApp, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary>
    /// Único lugar donde nace una sesión: al iniciar y al cerrar sesión (cambio de turno).
    /// ShutdownMode = OnExplicitShutdown mientras no hay ventana principal: si no, WPF cerraría
    /// la app al cerrarse el login (efecto "entro y desaparece", visto en AnaMarcolPOS).
    /// </summary>
    private void PedirLoginYAbrir()
    {
        var s = _sesion!;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (new LoginWindow(new LoginViewModel(s)).ShowDialog() != true) { Shutdown(); return; }

        if (s.Usuario!.DebeCambiarPassword &&
            new CambiarClaveWindow(new CambiarClaveViewModel(s)).ShowDialog() != true)
        {
            s.Cerrar();
            Shutdown();
            return;
        }

        var main = new MainWindow(new ShellViewModel(s));
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
    }

    /// <summary>Vuelve al login sin cerrar el proceso.</summary>
    internal void CerrarSesion()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _sesion?.Cerrar();
        MainWindow?.Close();
        MainWindow = null;
        Dispatcher.InvokeAsync(PedirLoginYAbrir);
    }

    internal static void Registrar(Exception? ex, string origen)
    {
        try { File.AppendAllText(AppPaths.Log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {origen}\n{ex}\n\n"); }
        catch { /* sin permiso de escritura no hay más que hacer */ }
    }
}
