using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NABeauty.Presentacion.Mvvm;

/// <summary>Base de todo ViewModel: avisa a la UI cuando cambia una propiedad.</summary>
public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T campo, T valor, [CallerMemberName] string? propiedad = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor)) return false;
        campo = valor;
        Avisar(propiedad);
        return true;
    }

    protected void Avisar([CallerMemberName] string? propiedad = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));

    protected void Avisar(params string[] propiedades)
    {
        foreach (var p in propiedades) Avisar(p);
    }
}
