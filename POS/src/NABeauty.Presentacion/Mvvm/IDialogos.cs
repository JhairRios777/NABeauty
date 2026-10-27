namespace NABeauty.Presentacion.Mvvm;

/// <summary>
/// Lo que un ViewModel necesita de la ventana (mensajes y confirmaciones).
/// La app WPF lo implementa con MessageBox; las pruebas, con respuestas fijas.
/// </summary>
public interface IDialogos
{
    void Info(string mensaje);
    void Error(string mensaje);
    bool Confirmar(string mensaje);

    /// <summary>Selector de archivos de imagen (varias a la vez). Lista vacía si se cancela.</summary>
    IReadOnlyList<string> ElegirImagenes();

    /// <summary>"Guardar como" para un archivo nuevo. null si se cancela.</summary>
    /// <param name="extension">Sin punto: "xlsx".</param>
    string? GuardarComo(string nombreSugerido, string extension, string descripcion);

    /// <summary>Abre un archivo o carpeta con el programa de Windows que corresponda.</summary>
    void Abrir(string ruta);
}

/// <summary>Impresión (la app la dibuja con WPF; en las pruebas se simula).</summary>
public interface IImpresion
{
    /// <summary>Recibo térmico (58 u 80 mm). false si se canceló el diálogo de impresión.</summary>
    /// <param name="preguntar">false = directo a la impresora predeterminada (impresión automática al cobrar).</param>
    bool ImprimirRecibo(NABeauty.Core.Entidades.Recibo recibo, bool preguntar = true);

    /// <summary>Reporte en hoja carta. Con "Microsoft Print to PDF" queda como PDF.</summary>
    bool ImprimirReporte(NABeauty.Core.Entidades.Reporte reporte);
}

/// <summary>Navegación dentro de la ventana principal.</summary>
public interface INavegador
{
    void Ir(object vista, string? titulo = null);
    void Volver();
    void IrA(string seccion);
}
