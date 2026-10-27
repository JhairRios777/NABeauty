using System.Collections.ObjectModel;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>
/// Inventario: pestaña "Entradas" (mercadería que llega, suma stock) y pestaña "Ajustes"
/// (correcciones con motivo: conteo físico, dañados, regalos…). Cada pestaña se muestra
/// solo con su permiso.
/// </summary>
public sealed class EntradasViewModel : VistaBase
{
    private bool _verAjustes;
    private string _texto = "";
    private EntradaFila? _seleccionada;

    // Ajuste
    private string _buscarAjuste = "";
    private ArticuloCaja? _elegido;
    private bool _conteo = true;
    private string _cantidad = "";
    private string _motivo = "";

    public EntradasViewModel(Sesion s) : base(s)
    {
        Periodo = new SelectorPeriodo("mes");
        Periodo.Cambio += () => { BuscarEntradas(); CargarAjustes(); };
        _verAjustes = !PuedeEntradas;
        NuevaCommand = new Comando(() => Nav.Ir(new NuevaEntradaViewModel(S), "Nueva entrada"), () => PuedeEntradas);
        TabCommand = new Comando(p => VerAjustes = p as string == "ajustes");
        ElegirCommand = new Comando(p => { if (p is ArticuloCaja a) Elegido = a; });
        AjustarCommand = new Comando(() => Ajustar(), () => _elegido is not null);
        ModoCommand = new Comando(p => Conteo = p as string == "conteo");
    }

    public override string Titulo => "Entradas e inventario";

    public bool PuedeEntradas => S.Puede(Permiso.RegistrarEntradas);
    public bool PuedeAjustes => S.Puede(Permiso.AjustarInventario);
    public bool VerAjustes
    {
        get => _verAjustes;
        set
        {
            if (value && !PuedeAjustes || !value && !PuedeEntradas) return;
            if (Set(ref _verAjustes, value)) Avisar(nameof(VerEntradas));
        }
    }
    public bool VerEntradas => !_verAjustes;

    public SelectorPeriodo Periodo { get; }
    public Comando NuevaCommand { get; }
    public Comando TabCommand { get; }

    // ── Entradas ────────────────────────────────────────────────────
    public string Texto { get => _texto; set { if (Set(ref _texto, value)) BuscarEntradas(); } }
    public List<EntradaFila> Lista { get; private set; } = [];
    public bool HayEntradas => Lista.Count > 0;
    public string Resumen { get; private set; } = "";
    public EntradaFila? Seleccionada
    {
        get => _seleccionada;
        set
        {
            if (!Set(ref _seleccionada, value)) return;
            Lineas = value is null ? [] : new EntradaService(Ctx).Detalle(value.Uuid).Lineas;
            Avisar(nameof(Lineas), nameof(HaySeleccion));
        }
    }
    public bool HaySeleccion => _seleccionada is not null;
    public List<EntradaLinea> Lineas { get; private set; } = [];

    // ── Ajustes ─────────────────────────────────────────────────────
    public List<AjusteFila> Ajustes { get; private set; } = [];
    public bool HayAjustes => Ajustes.Count > 0;
    public string BuscarAjuste { get => _buscarAjuste; set { if (Set(ref _buscarAjuste, value)) BuscarArticulos(); } }
    public List<ArticuloCaja> Resultados { get; private set; } = [];
    public bool HayResultados => Resultados.Count > 0;
    public ArticuloCaja? Elegido
    {
        get => _elegido;
        set
        {
            if (!Set(ref _elegido, value)) return;
            _cantidad = value is null ? "" : _conteo ? value.Stock.ToString() : "";
            Avisar(nameof(HayElegido), nameof(Cantidad), nameof(StockActual), nameof(Vista));
            AjustarCommand.Refrescar();
        }
    }
    public bool HayElegido => _elegido is not null;
    public string StockActual => _elegido is null ? "" : $"Stock en el sistema: {_elegido.Stock}";
    /// <summary>true = escribir el stock real contado; false = sumar/restar unidades.</summary>
    public bool Conteo
    {
        get => _conteo;
        set
        {
            if (!Set(ref _conteo, value)) return;
            _cantidad = value && _elegido is not null ? _elegido.Stock.ToString() : "";
            Avisar(nameof(Cantidad), nameof(EtiquetaCantidad), nameof(Vista), nameof(SumarRestar));
        }
    }
    public bool SumarRestar => !_conteo;
    public string EtiquetaCantidad => _conteo ? "Stock real contado" : "Unidades a sumar (o restar con −)";
    public string Cantidad { get => _cantidad; set { if (Set(ref _cantidad, value)) Avisar(nameof(Vista)); } }
    public string Motivo { get => _motivo; set => Set(ref _motivo, value); }
    /// <summary>Vista previa del resultado: "5 → 3 (−2)".</summary>
    public string Vista
    {
        get
        {
            if (_elegido is null || !long.TryParse(_cantidad.Trim().Replace("−", "-"), out var n)) return "";
            var nuevo = _conteo ? n : _elegido.Stock + n;
            var dif = nuevo - _elegido.Stock;
            return $"{_elegido.Stock} → {nuevo} ({(dif >= 0 ? "+" : "−")}{Math.Abs(dif)})";
        }
    }

    public Comando ElegirCommand { get; }
    public Comando AjustarCommand { get; }
    public Comando ModoCommand { get; }

    public override void Cargar()
    {
        BuscarEntradas();
        CargarAjustes();
        BuscarArticulos();
    }

    private void BuscarEntradas()
    {
        if (!PuedeEntradas) return;
        var uuid = _seleccionada?.Uuid;
        Lista = new EntradaService(Ctx).Listar(Periodo.Periodo, _texto);
        var unidades = Lista.Sum(e => e.Unidades);
        Resumen = $"{(Lista.Count == 1 ? "1 entrada" : $"{Lista.Count} entradas")} · {unidades} unidades · {Dinero.Formatear(Lista.Sum(e => e.TotalCosto))} en costo";
        Avisar(nameof(Lista), nameof(HayEntradas), nameof(Resumen));
        Seleccionada = Lista.FirstOrDefault(e => e.Uuid == uuid);
    }

    private void CargarAjustes()
    {
        if (!PuedeAjustes) return;
        Ajustes = new EntradaService(Ctx).Ajustes(Periodo.Periodo);
        Avisar(nameof(Ajustes), nameof(HayAjustes));
    }

    private void BuscarArticulos()
    {
        if (!PuedeAjustes) return;
        Resultados = new EntradaService(Ctx).Buscar(_buscarAjuste, 30);
        Avisar(nameof(Resultados), nameof(HayResultados));
    }

    public bool Ajustar()
    {
        if (_elegido is not { } a) return false;
        if (!long.TryParse(_cantidad.Trim().Replace("−", "-"), out var n))
        {
            Dlg.Error("Escribe un número entero en la cantidad.");
            return false;
        }
        long dif = 0;
        if (!Intentar(() => dif = new EntradaService(Ctx).Ajustar(a.VarianteUuid, n, _conteo, _motivo))) return false;
        Dlg.Info($"Ajuste registrado: {a.Nombre} {(dif > 0 ? "+" : "")}{dif}. Stock nuevo: {a.Stock + dif}.");
        _motivo = "";
        Avisar(nameof(Motivo));
        CargarAjustes();
        BuscarArticulos();
        Elegido = Resultados.FirstOrDefault(r => r.VarianteUuid == a.VarianteUuid);
        return true;
    }
}

/// <summary>Línea editable de una entrada nueva.</summary>
public sealed class LineaEntradaFila(ArticuloCaja articulo, string costo) : Observable
{
    private string _cantidad = "1", _costo = costo;
    public ArticuloCaja Articulo { get; } = articulo;
    public string Nombre => Articulo.Nombre;
    public string StockActual => $"Hay {Articulo.Stock}";
    public string Cantidad { get => _cantidad; set { if (Set(ref _cantidad, value)) Avisar(nameof(Subtotal)); } }
    public string Costo { get => _costo; set { if (Set(ref _costo, value)) Avisar(nameof(Subtotal)); } }
    public long CantidadNum => long.TryParse(_cantidad.Trim(), out var n) ? n : 0;
    public long CostoNum => Dinero.TryParse(_costo, out var c) ? c : 0;
    public string Subtotal => Dinero.Formatear(CantidadNum * CostoNum);
}

/// <summary>
/// Entrada nueva: proveedor, productos (escáner o búsqueda), cantidad y costo unitario de cada uno.
/// El costo se propone con el costo actual del producto; al guardar puede quedar como nuevo costo.
/// </summary>
public sealed class NuevaEntradaViewModel : VistaBase
{
    private string _proveedor = "", _nota = "", _texto = "";
    private bool _actualizarCostos = true;

    public NuevaEntradaViewModel(Sesion s) : base(s)
    {
        AgregarCommand = new Comando(p => { if (p is ArticuloCaja a) Agregar(a); });
        QuitarCommand = new Comando(p => { if (p is LineaEntradaFila l) { Lineas.Remove(l); Totales(); } });
        GuardarCommand = new Comando(() => Guardar(), () => Lineas.Count > 0);
        CancelarCommand = new Comando(() => { if (Lineas.Count == 0 || Dlg.Confirmar("¿Descartar esta entrada?")) Nav.Volver(); });
        Lineas.CollectionChanged += (_, _) => { Totales(); GuardarCommand.Refrescar(); };
    }

    public override string Titulo => "Nueva entrada";

    public string Proveedor { get => _proveedor; set => Set(ref _proveedor, value); }
    public string Nota { get => _nota; set => Set(ref _nota, value); }
    public bool ActualizarCostos { get => _actualizarCostos; set => Set(ref _actualizarCostos, value); }
    public string Texto { get => _texto; set { if (Set(ref _texto, value)) Buscar(); } }
    public List<ArticuloCaja> Resultados { get; private set; } = [];
    public ObservableCollection<LineaEntradaFila> Lineas { get; } = [];
    public bool HayLineas => Lineas.Count > 0;
    public string Total => Dinero.Formatear(Lineas.Sum(l => l.CantidadNum * l.CostoNum));
    public string Unidades => $"{Lineas.Sum(l => l.CantidadNum)} unidades en {Lineas.Count} producto(s)";

    public Comando AgregarCommand { get; }
    public Comando QuitarCommand { get; }
    public Comando GuardarCommand { get; }
    public Comando CancelarCommand { get; }

    public override void Cargar() => Buscar();

    private void Buscar()
    {
        Resultados = new EntradaService(Ctx).Buscar(_texto);
        Avisar(nameof(Resultados));
    }

    /// <summary>Enter en la búsqueda (escáner): código exacto o resultado único.</summary>
    public void Enter()
    {
        if (Resultados.Count == 1) { Agregar(Resultados[0]); Texto = ""; }
    }

    public void Agregar(ArticuloCaja a)
    {
        var existente = Lineas.FirstOrDefault(l => l.Articulo.VarianteUuid == a.VarianteUuid);
        if (existente is not null)
        {
            existente.Cantidad = (existente.CantidadNum + 1).ToString();
        }
        else
        {
            var costo = new EntradaService(Ctx).CostoActual(a.VarianteUuid);
            var linea = new LineaEntradaFila(a, costo is { } c ? Dinero.ParaEditar(c) : "");
            linea.PropertyChanged += (_, _) => Totales();
            Lineas.Add(linea);
        }
        Totales();
    }

    private void Totales() => Avisar(nameof(Total), nameof(Unidades), nameof(HayLineas));

    public bool Guardar()
    {
        var lineas = new List<LineaEntrada>();
        foreach (var l in Lineas)
        {
            if (!long.TryParse(l.Cantidad.Trim(), out var n) || n <= 0) { Dlg.Error($"Cantidad de «{l.Nombre}»: escribe un número entero mayor a cero."); return false; }
            if (!LeerMonto(l.Costo, $"Costo de «{l.Nombre}»", out var c, permitirCero: true)) return false;
            lineas.Add(new LineaEntrada(l.Articulo.VarianteUuid, n, c));
        }
        string? numero = null;
        if (!Intentar(() => numero = new EntradaService(Ctx).Registrar(_proveedor, _nota, lineas, _actualizarCostos))) return false;
        Dlg.Info($"Entrada {numero} registrada: el stock ya está actualizado.");
        Nav.Volver();
        return true;
    }
}
