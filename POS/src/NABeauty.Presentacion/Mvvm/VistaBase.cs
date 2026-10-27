using NABeauty.Core.Dominio;
using NABeauty.Data.Infra;

namespace NABeauty.Presentacion.Mvvm;

/// <summary>
/// Base de las pantallas: acceso al contexto (usuario + BD), navegación y diálogos,
/// y un envoltorio que muestra los errores de negocio sin cerrar la app.
/// </summary>
public abstract class VistaBase(Sesion sesion) : Observable
{
    protected Sesion S { get; } = sesion;
    protected Contexto Ctx => S.Contexto!;
    protected IDialogos Dlg => S.Dialogos;
    protected INavegador Nav => S.Navegador!;

    public virtual string Titulo => "";

    /// <summary>Recarga los datos al entrar a la pantalla.</summary>
    public virtual void Cargar() { }

    /// <summary>Ejecuta una acción mostrando el mensaje de una regla de negocio si falla.</summary>
    protected bool Intentar(Action accion)
    {
        try { accion(); return true; }
        catch (ReglaNegocioException ex) { Dlg.Error(ex.Message); return false; }
    }

    /// <summary>Lee un monto escrito por el usuario; muestra error si no es válido.</summary>
    protected bool LeerMonto(string? texto, string etiqueta, out long centavos, bool permitirCero = false)
    {
        if (string.IsNullOrWhiteSpace(texto) && permitirCero) { centavos = 0; return true; }
        if (!Dinero.TryParse(texto, out centavos) || (!permitirCero && centavos == 0))
        {
            Dlg.Error($"{etiqueta}: escribe un monto válido (máximo 2 decimales).");
            return false;
        }
        return true;
    }
}
