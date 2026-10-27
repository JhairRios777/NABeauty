using NABeauty.Core.Dominio;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>
/// Selector de período compartido por Ventas, Entradas y Reportes: un preset (Hoy, Este mes…)
/// o un rango con dos calendarios. Avisa con <see cref="Cambio"/> cada vez que el período cambia.
/// </summary>
public sealed class SelectorPeriodo : Observable
{
    private Opcion _preset;
    private DateTime? _desde, _hasta;

    public SelectorPeriodo(string presetInicial = "hoy")
    {
        Presets = [.. Periodo.Presets.Select(p => new Opcion(p.Clave, p.Nombre))];
        _preset = Presets.First(p => p.Clave == presetInicial);
        var p = Periodo.DePreset(presetInicial, Formato.Hoy);
        _desde = p.Desde.ToDateTime(TimeOnly.MinValue);
        _hasta = p.UltimoDia.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>Se dispara cuando cambia el período (preset o fechas del rango).</summary>
    public event Action? Cambio;

    public IReadOnlyList<Opcion> Presets { get; }

    public Opcion Preset
    {
        get => _preset;
        set
        {
            if (value is null || !Set(ref _preset, value)) return;
            if (value.Clave != "rango")
            {
                // Al elegir un preset, los calendarios muestran sus fechas (punto de partida si luego elige "Rango")
                var p = Periodo.DePreset(value.Clave!, Formato.Hoy);
                _desde = p.Desde.ToDateTime(TimeOnly.MinValue);
                _hasta = p.UltimoDia.ToDateTime(TimeOnly.MinValue);
            }
            Avisar(nameof(EsRango), nameof(Desde), nameof(Hasta), nameof(Etiqueta));
            Cambio?.Invoke();
        }
    }

    public bool EsRango => _preset.Clave == "rango";

    public DateTime? Desde { get => _desde; set { if (Set(ref _desde, value)) Cambiar(); } }
    public DateTime? Hasta { get => _hasta; set { if (Set(ref _hasta, value)) Cambiar(); } }

    public Periodo Periodo => EsRango && _desde is { } d && _hasta is { } h
        ? Periodo.Rango(DateOnly.FromDateTime(d), DateOnly.FromDateTime(h))
        : Periodo.DePreset(_preset.Clave ?? "hoy", Formato.Hoy);

    public string Etiqueta => Periodo.Etiqueta;

    /// <summary>Elige un preset por clave (desde código o pruebas).</summary>
    public void Elegir(string clave) => Preset = Presets.First(p => p.Clave == clave);

    /// <summary>Rango de fechas (cambia el preset a "Rango").</summary>
    public void ElegirRango(DateOnly desde, DateOnly hasta)
    {
        _desde = desde.ToDateTime(TimeOnly.MinValue);
        _hasta = hasta.ToDateTime(TimeOnly.MinValue);
        _preset = Presets.First(p => p.Clave == "rango");
        Avisar(nameof(Preset), nameof(EsRango), nameof(Desde), nameof(Hasta), nameof(Etiqueta));
        Cambio?.Invoke();
    }

    private void Cambiar()
    {
        if (!EsRango) return;
        Avisar(nameof(Etiqueta));
        Cambio?.Invoke();
    }
}
