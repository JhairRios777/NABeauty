using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.App.Vistas;

/// <summary>Solo resalta la pestaña activa (la lógica está en EntradasViewModel).</summary>
public partial class EntradasView : UserControl
{
    public EntradasView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged viejo) viejo.PropertyChanged -= Cambio;
            if (e.NewValue is INotifyPropertyChanged nuevo) nuevo.PropertyChanged += Cambio;
            Pintar();
        };
    }

    private void Cambio(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EntradasViewModel.VerAjustes) or "" or null) Pintar();
    }

    private void Pintar()
    {
        if (DataContext is not EntradasViewModel vm) return;
        Marcar(TabEntradas, !vm.VerAjustes);
        Marcar(TabAjustes, vm.VerAjustes);
    }

    private static void Marcar(Button b, bool activa)
    {
        if (activa)
        {
            b.SetResourceReference(BackgroundProperty, "Acento");
            b.SetResourceReference(BorderBrushProperty, "Acento");
            b.SetResourceReference(ForegroundProperty, "SobreAcento");
        }
        else
        {
            b.ClearValue(BackgroundProperty);
            b.ClearValue(BorderBrushProperty);
            b.ClearValue(ForegroundProperty);
        }
    }
}
