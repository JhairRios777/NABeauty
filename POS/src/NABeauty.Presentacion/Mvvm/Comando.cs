using System.Windows.Input;

namespace NABeauty.Presentacion.Mvvm;

/// <summary>ICommand simple. CanExecute se reevalúa con Refrescar() o cuando WPF lo pide.</summary>
public sealed class Comando(Action<object?> ejecutar, Func<object?, bool>? puede = null) : ICommand
{
    public Comando(Action ejecutar, Func<bool>? puede = null)
        : this(_ => ejecutar(), puede is null ? null : _ => puede()) { }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parametro) => puede?.Invoke(parametro) ?? true;
    public void Execute(object? parametro) => ejecutar(parametro);
    public void Refrescar() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Comando asíncrono: se deshabilita mientras corre (evita doble clic, p. ej. doble pago).</summary>
public sealed class ComandoAsync(Func<Task> ejecutar, Func<bool>? puede = null) : ICommand
{
    private bool _corriendo;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parametro) => !_corriendo && (puede?.Invoke() ?? true);

    public async void Execute(object? parametro)
    {
        if (_corriendo) return;
        _corriendo = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await ejecutar(); }
        finally
        {
            _corriendo = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
