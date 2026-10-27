using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;

namespace NABeauty.Presentacion.Mvvm;

/// <summary>Estado compartido de la ejecución: base de datos, usuario con sesión y diálogos.</summary>
public sealed class Sesion(BaseDatos bd, IDialogos dialogos)
{
    public BaseDatos Bd { get; } = bd;
    public IDialogos Dialogos { get; } = dialogos;
    public INavegador? Navegador { get; set; }
    /// <summary>Almacén de fotos de productos (la app WPF pone el real; las pruebas, uno falso).</summary>
    public IAlmacenFotos? Fotos { get; set; }
    /// <summary>Impresión de recibos y reportes.</summary>
    public IImpresion? Impresion { get; set; }
    /// <summary>Carpeta de respaldos (la define la app: %LOCALAPPDATA%\NABeautyPOS\Respaldos).</summary>
    public string? CarpetaRespaldos { get; set; }

    public Usuario? Usuario { get; private set; }
    public Contexto? Contexto { get; private set; }

    public void Iniciar(Usuario u)
    {
        Usuario = u;
        Contexto = new Contexto(Bd, u);
    }

    public void Cerrar()
    {
        Usuario = null;
        Contexto = null;
    }

    public bool Puede(Permiso p) => Usuario?.Puede(p) ?? false;
}
