using System.Collections.ObjectModel;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>Pestaña de categoría en la caja ("Todo" = sin filtro).</summary>
public sealed class FiltroCaja(string? uuid, string nombre, string color) : Observable
{
    private bool _activa;
    public string? Uuid { get; } = uuid;
    public string Nombre { get; } = nombre;
    public string Color { get; } = color;
    public bool Activa { get => _activa; set => Set(ref _activa, value); }
}

/// <summary>Línea del carrito para la pantalla (envuelve la línea del dominio).</summary>
public sealed class LineaCaja(LineaCarrito l)
{
    public LineaCarrito Linea { get; } = l;
    public ArticuloCaja Articulo => Linea.Articulo;
    public string Nombre => Articulo.Producto;
    public string Detalle => Articulo.Tipo == "unica" ? Articulo.Marca : $"{TiposVariante.Etiqueta(Articulo.Tipo)}: {Articulo.Variante}";
    public int Cantidad => Linea.Cantidad;
    public string Unitario => $"{Dinero.Formatear(Linea.PrecioUnitario)} c/u";
    public string Subtotal => Dinero.Formatear(Linea.Subtotal);
    public bool AlTope => Linea.Cantidad >= Articulo.Stock;
}

/// <summary>Botón de método de pago (Activa = elegido). Icono = glifo de Segoe Fluent Icons.</summary>
public sealed class OpcionPago(string clave, string nombre, string icono) : Observable
{
    private bool _activa;
    public string Clave { get; } = clave;
    public string Nombre { get; } = nombre;
    public string Icono { get; } = icono;
    public bool Activa { get => _activa; set => Set(ref _activa, value); }
}

/// <summary>Monto rápido de efectivo (billetes de lempira).</summary>
public sealed record MontoRapido(string Texto, long Centavos);

/// <summary>
/// Caja. Izquierda: búsqueda (o escáner) y opciones a la venta con su miniatura.
/// Derecha: la venta en curso, descuento, método de pago, efectivo recibido y cambio.
/// Teclado: Enter en la búsqueda agrega (código exacto o único resultado), F12 cobra,
/// Esc limpia la búsqueda. El cobro lo valida CajaService dentro de la transacción.
/// </summary>
public sealed class CajaViewModel : VistaBase
{
    private static readonly long[] Billetes = [10000, 20000, 50000, 100000];

    private readonly Carrito _carrito = new();
    private string _texto = "";
    private string _descuento = "";
    private string _recibido = "";
    private string _metodo = "efectivo";
    private FiltroCaja? _filtro;
    private string _aviso = "";
    private bool _avisoError;
    private VentaRegistrada? _ultima;

    public CajaViewModel(Sesion s) : base(s)
    {
        AgregarCommand = new Comando(p => { if (p is ArticuloCaja a) Agregar(a); });
        MasCommand = new Comando(p => { if (p is LineaCaja l) Cambiar(l, l.Cantidad + 1); });
        MenosCommand = new Comando(p => { if (p is LineaCaja l) Cambiar(l, l.Cantidad - 1); });
        QuitarCommand = new Comando(p => { if (p is LineaCaja l) { _carrito.Quitar(l.Articulo.VarianteUuid); Refrescar(); } });
        FiltroCommand = new Comando(p => { if (p is FiltroCaja f) ElegirFiltro(f); });
        MetodoCommand = new Comando(p => { if (p is string m) MetodoPago = m; });
        MontoCommand = new Comando(p => { if (p is MontoRapido m) Recibido = Dinero.ParaEditar(m.Centavos); });
        CobrarCommand = new Comando(() => Cobrar(), () => !_carrito.Vacio);
        CancelarCommand = new Comando(CancelarVenta, () => !_carrito.Vacio);
        CerrarAvisoCommand = new Comando(() => { Aviso = ""; UltimaVenta = null; });
        ImprimirReciboCommand = new Comando(() => ImprimirRecibo(preguntar: true), () => _ultima is not null && S.Impresion is not null);
        Metodos[0].Activa = true;
    }

    public override string Titulo => "Caja";

    // ── Búsqueda ────────────────────────────────────────────────────
    public string Texto { get => _texto; set { if (Set(ref _texto, value)) Buscar(); } }
    public ObservableCollection<FiltroCaja> Filtros { get; } = [];
    public List<ArticuloCaja> Resultados { get; private set; } = [];
    public bool HayResultados => Resultados.Count > 0;
    public bool CatalogoVacio { get; private set; }
    public string MensajeVacio => CatalogoVacio
        ? "Todavía no hay productos a la venta. Cárgalos en Productos (con su stock inicial) y vuelve aquí."
        : $"No se encontró «{_texto.Trim()}». Prueba con otra palabra, el SKU o el código de barras.";
    public string ConteoResultados => Resultados.Count >= CajaService.LimiteBusqueda
        ? $"Primeros {CajaService.LimiteBusqueda} resultados · escribe para afinar"
        : Resultados.Count == 1 ? "1 opción" : $"{Resultados.Count} opciones";

    // ── Venta en curso ──────────────────────────────────────────────
    public ObservableCollection<LineaCaja> Lineas { get; } = [];
    public bool CarritoVacio => _carrito.Vacio;
    public string Unidades => _carrito.Unidades == 1 ? "1 artículo" : $"{_carrito.Unidades} artículos";
    public string Subtotal => Dinero.Formatear(_carrito.Subtotal);
    public string DescuentoAplicado => _carrito.Descuento > 0 ? "− " + Dinero.Formatear(_carrito.Descuento) : "";
    public bool HayDescuento => _carrito.Descuento > 0;
    public string Total => Dinero.Formatear(_carrito.Total);

    /// <summary>Descuento en lempiras. Se aplica al salir del campo o al cobrar.</summary>
    public string Descuento
    {
        get => _descuento;
        set { if (Set(ref _descuento, value)) AplicarDescuento(mostrarError: false); }
    }

    public List<OpcionPago> Metodos { get; } =
    [
        new("efectivo", "Efectivo", "\uE8C7"),
        new("tarjeta", "Tarjeta", "\uE8C7"),
        new("transferencia", "Transferencia", "\uE8AB"),
        new("otro", "Otro", "\uE712"),
    ];
    public string MetodoPago
    {
        get => _metodo;
        set
        {
            if (!MetodosPago.Valido(value) || !Set(ref _metodo, value)) return;
            foreach (var m in Metodos) m.Activa = m.Clave == value;
            Avisar(nameof(EsEfectivo), nameof(Cambio), nameof(Falta), nameof(HayFalta));
        }
    }
    public bool EsEfectivo => _metodo == "efectivo";
    public string Recibido { get => _recibido; set { if (Set(ref _recibido, value)) Avisar(nameof(Cambio), nameof(Falta), nameof(HayFalta)); } }
    public List<MontoRapido> MontosRapidos { get; private set; } = [];

    private long RecibidoCentavos => Dinero.TryParse(_recibido, out var c) ? c : 0;
    public string Cambio => EsEfectivo && RecibidoCentavos >= _carrito.Total && !_carrito.Vacio ? Dinero.Formatear(RecibidoCentavos - _carrito.Total) : "—";
    public bool HayFalta => EsEfectivo && !_carrito.Vacio && _recibido.Trim().Length > 0 && RecibidoCentavos < _carrito.Total;
    public string Falta => HayFalta ? $"Faltan {Dinero.Formatear(_carrito.Total - RecibidoCentavos)}" : "";

    // ── Avisos ──────────────────────────────────────────────────────
    public string Aviso { get => _aviso; private set { if (Set(ref _aviso, value)) Avisar(nameof(HayAviso)); } }
    public bool HayAviso => _aviso.Length > 0;
    public bool AvisoError { get => _avisoError; private set => Set(ref _avisoError, value); }
    public VentaRegistrada? UltimaVenta { get => _ultima; private set { if (Set(ref _ultima, value)) Avisar(nameof(HayUltimaVenta), nameof(UltimaTitulo), nameof(UltimaDetalle), nameof(UltimaCambio)); } }
    public bool HayUltimaVenta => _ultima is not null;
    public string UltimaTitulo => _ultima is null ? "" : $"Venta {_ultima.Numero} registrada";
    public string UltimaDetalle => _ultima is null ? "" : $"{Dinero.Formatear(_ultima.Total)} · {MetodosPago.Nombre(_ultima.MetodoPago)} · {(_ultima.Unidades == 1 ? "1 artículo" : $"{_ultima.Unidades} artículos")}";
    public string UltimaCambio => _ultima is null || _ultima.MetodoPago != "efectivo" ? "" : $"Cambio: {Dinero.Formatear(_ultima.Cambio)}";

    public Comando AgregarCommand { get; }
    public Comando MasCommand { get; }
    public Comando MenosCommand { get; }
    public Comando QuitarCommand { get; }
    public Comando FiltroCommand { get; }
    public Comando MetodoCommand { get; }
    public Comando MontoCommand { get; }
    public Comando CobrarCommand { get; }
    public Comando CancelarCommand { get; }
    public Comando CerrarAvisoCommand { get; }
    public Comando ImprimirReciboCommand { get; }
    public bool PuedeImprimir => S.Impresion is not null;

    // ── Carga y búsqueda ────────────────────────────────────────────
    public override void Cargar()
    {
        var svc = new CajaService(Ctx);
        var activo = _filtro?.Uuid;
        Filtros.Clear();
        Filtros.Add(new FiltroCaja(null, "Todo", "#FFFFFF"));
        foreach (var c in svc.Categorias()) Filtros.Add(new FiltroCaja(c.Uuid, c.Nombre, c.Color));
        _filtro = Filtros.FirstOrDefault(f => f.Uuid == activo) ?? Filtros[0];
        _filtro.Activa = true;
        Buscar();
        Refrescar();
    }

    private void ElegirFiltro(FiltroCaja f)
    {
        foreach (var x in Filtros) x.Activa = x == f;
        _filtro = f;
        Buscar();
    }

    public void Buscar()
    {
        var svc = new CajaService(Ctx);
        Resultados = svc.Buscar(_texto, _filtro?.Uuid);
        CatalogoVacio = Resultados.Count == 0 && _texto.Trim().Length == 0 && _filtro?.Uuid is null;
        Avisar(nameof(Resultados), nameof(HayResultados), nameof(CatalogoVacio), nameof(MensajeVacio), nameof(ConteoResultados));
    }

    /// <summary>
    /// Enter en la búsqueda. Un escáner "teclea" el código y manda Enter: si coincide exacto
    /// con un código de barras o SKU se agrega; si la búsqueda deja un solo resultado, también.
    /// </summary>
    public void Enter()
    {
        var t = _texto.Trim();
        if (t.Length == 0) return;
        var exacto = new CajaService(Ctx).PorCodigo(t);
        var elegido = exacto ?? (Resultados.Count == 1 ? Resultados[0] : null);
        if (elegido is null)
        {
            MostrarAviso(Resultados.Count == 0 ? $"No hay ningún producto con «{t}»." : "Hay varias opciones: tócala en la lista para agregarla.", error: Resultados.Count == 0);
            return;
        }
        if (Agregar(elegido)) Texto = "";   // listo para el siguiente escaneo
    }

    public void LimpiarBusqueda() => Texto = "";

    // ── Carrito ─────────────────────────────────────────────────────
    public bool Agregar(ArticuloCaja a)
    {
        try
        {
            _carrito.Agregar(a);
            UltimaVenta = null;
            Aviso = "";
            Refrescar();
            return true;
        }
        catch (ReglaNegocioException ex)
        {
            MostrarAviso(ex.Message, error: true);
            return false;
        }
    }

    private void Cambiar(LineaCaja l, int cantidad)
    {
        try { _carrito.CambiarCantidad(l.Articulo.VarianteUuid, cantidad); Aviso = ""; }
        catch (ReglaNegocioException ex) { MostrarAviso(ex.Message, error: true); }
        Refrescar();
    }

    private bool AplicarDescuento(bool mostrarError)
    {
        long centavos = 0;
        if (_descuento.Trim().Length > 0 && !Dinero.TryParse(_descuento, out centavos))
        {
            if (mostrarError) MostrarAviso("Descuento: escribe un monto válido (máximo 2 decimales).", error: true);
            return false;
        }
        try { _carrito.AplicarDescuento(centavos); }
        catch (ReglaNegocioException ex)
        {
            if (mostrarError) MostrarAviso(ex.Message, error: true);
            return false;
        }
        Refrescar();
        return true;
    }

    private void CancelarVenta()
    {
        if (_carrito.Vacio || !Dlg.Confirmar("¿Cancelar la venta en curso? Se quitarán todos los artículos.")) return;
        Reiniciar();
    }

    private void Reiniciar()
    {
        _carrito.Vaciar();
        _descuento = "";
        _recibido = "";
        MetodoPago = "efectivo";
        Avisar(nameof(Descuento), nameof(Recibido));
        Refrescar();
    }

    // ── Cobro ───────────────────────────────────────────────────────
    public bool Cobrar()
    {
        if (_carrito.Vacio) return false;
        if (!AplicarDescuento(mostrarError: true)) return false;
        if (EsEfectivo && _recibido.Trim().Length == 0) Recibido = Dinero.ParaEditar(_carrito.Total);   // sin dato = pago exacto
        if (EsEfectivo && !Dinero.TryParse(_recibido, out _))
        {
            MostrarAviso("Efectivo recibido: escribe un monto válido.", error: true);
            return false;
        }

        try
        {
            var venta = new CajaService(Ctx).Cobrar(_carrito, _metodo, RecibidoCentavos);
            Reiniciar();
            Aviso = "";
            UltimaVenta = venta;
            ImprimirReciboCommand.Refrescar();
            Buscar();   // el stock de la lista ya cambió
            // Impresión automática (Ajustes → Recibo): directo a la impresora predeterminada
            if (S.Impresion is not null && new AjustesService(Ctx).Negocio().ImprimirAlCobrar) ImprimirRecibo(preguntar: false);
            return true;
        }
        catch (ReglaNegocioException ex)
        {
            MostrarAviso(ex.Message, error: true);
            return false;
        }
    }

    /// <summary>Recibo de la última venta cobrada. Un fallo de impresora no deshace la venta: solo avisa.</summary>
    public bool ImprimirRecibo(bool preguntar)
    {
        if (_ultima is null || S.Impresion is not { } imp) return false;
        try
        {
            return imp.ImprimirRecibo(new VentaService(Ctx).Recibo(_ultima.Uuid), preguntar);
        }
        catch (Exception ex) when (ex is not ReglaNegocioException)
        {
            MostrarAviso($"La venta quedó registrada, pero no se pudo imprimir el recibo: {ex.Message}", error: true);
            return false;
        }
    }

    private void MostrarAviso(string texto, bool error)
    {
        AvisoError = error;
        Aviso = texto;
    }

    /// <summary>Vuelve a armar las líneas y los totales después de cualquier cambio del carrito.</summary>
    private void Refrescar()
    {
        Lineas.Clear();
        foreach (var l in _carrito.Lineas) Lineas.Add(new LineaCaja(l));
        if (_descuento.Trim().Length > 0 && _carrito.Descuento == 0 && _carrito.Vacio) _descuento = "";

        // Exacto + los billetes que alcanzan a cubrir el total
        var total = _carrito.Total;
        MontosRapidos = total <= 0 ? [] :
            [new("Exacto", total), .. Billetes.Where(b => b > total).Take(3).Select(b => new MontoRapido(Dinero.Formatear(b).Replace(".00", ""), b))];

        Avisar(nameof(CarritoVacio), nameof(Unidades), nameof(Subtotal), nameof(DescuentoAplicado), nameof(HayDescuento),
               nameof(Total), nameof(Cambio), nameof(Falta), nameof(HayFalta), nameof(MontosRapidos), nameof(Descuento));
        CobrarCommand.Refrescar();
        CancelarCommand.Refrescar();
    }
}
