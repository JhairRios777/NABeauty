using System.Globalization;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>
/// Historial de ventas: filtros arriba, tabla a la izquierda y detalle de la venta elegida a la
/// derecha (líneas, pago, quién vendió). Desde el detalle se reimprime el recibo y, con permiso,
/// se anula con motivo (el stock regresa solo).
/// </summary>
public sealed class VentasViewModel : VistaBase
{
    private static readonly CultureInfo Hn = CultureInfo.GetCultureInfo("es-HN");
    public static readonly IReadOnlyList<Opcion> Estados = [new(null, "Todas"), new("completada", "Completadas"), new("anulada", "Anuladas")];

    private Opcion _metodo = new(null, "Todos los pagos");
    private Opcion _vendedora = new(null, "Todas las vendedoras");
    private Opcion _estado = Estados[0];
    private string _numero = "";
    private VentaFila? _seleccionada;
    private string _motivo = "";
    private bool _cargando;

    public VentasViewModel(Sesion s) : base(s)
    {
        Periodo = new SelectorPeriodo("hoy");
        Periodo.Cambio += Buscar;
        Metodos = [new(null, "Todos los pagos"), .. MetodosPago.Todos.Select(m => new Opcion(m.Clave, m.Nombre))];
        AnularCommand = new Comando(() => Anular(), () => PuedeAnular);
        ImprimirCommand = new Comando(() => Imprimir(), () => _seleccionada is not null && S.Impresion is not null);
    }

    public override string Titulo => "Ventas";

    // ── Filtros ─────────────────────────────────────────────────────
    public SelectorPeriodo Periodo { get; }
    public IReadOnlyList<Opcion> Metodos { get; }
    public List<Opcion> Vendedoras { get; private set; } = [];
    public IReadOnlyList<Opcion> OpcionesEstado => Estados;
    public Opcion Metodo { get => _metodo; set { if (value is not null && Set(ref _metodo, value)) Buscar(); } }
    public Opcion Vendedora { get => _vendedora; set { if (value is not null && Set(ref _vendedora, value)) Buscar(); } }
    public Opcion Estado { get => _estado; set { if (value is not null && Set(ref _estado, value)) Buscar(); } }
    public string Numero { get => _numero; set { if (Set(ref _numero, value)) Buscar(); } }

    // ── Lista y totales ─────────────────────────────────────────────
    public List<VentaFila> Lista { get; private set; } = [];
    public bool HayVentas => Lista.Count > 0;
    public string TotalTexto { get; private set; } = "";
    public string ResumenTexto { get; private set; } = "";
    public string AnuladasTexto { get; private set; } = "";
    public bool HayAnuladas { get; private set; }
    public bool Recortado => Lista.Count >= VentaService.Limite;

    // ── Detalle ─────────────────────────────────────────────────────
    public VentaFila? Seleccionada
    {
        get => _seleccionada;
        set
        {
            if (!Set(ref _seleccionada, value)) return;
            CargarDetalle();
        }
    }
    public bool HaySeleccion => _seleccionada is not null;
    public List<VentaLinea> Lineas { get; private set; } = [];
    public string DetalleSubtotal => Dinero.Formatear(_seleccionada?.Subtotal ?? 0);
    public string DetalleDescuento => _seleccionada is { Descuento: > 0 } v ? "− " + Dinero.Formatear(v.Descuento) : "";
    public bool DetalleHayDescuento => _seleccionada?.Descuento > 0;
    public string DetalleTotal => Dinero.Formatear(_seleccionada?.Total ?? 0);
    public string DetallePago => _seleccionada is null ? "" : _seleccionada.MetodoPago == "efectivo" && _seleccionada.Recibido is { } r
        ? $"Efectivo · recibió {Dinero.Formatear(r)} · cambio {Dinero.Formatear(_seleccionada.Cambio)}"
        : _seleccionada.Metodo;
    public string DetalleAnulacion => _seleccionada is { Anulada: true } v
        ? $"Anulada {Formato.Fecha(v.AnuladaFecha, conHora: true)} por {v.AnuladaPor ?? "—"}: {v.MotivoAnulacion}"
        : "";

    public bool PuedeAnular => _seleccionada is { Anulada: false } && S.Puede(Permiso.AnularVentas);
    public bool MostrarAnular => S.Puede(Permiso.AnularVentas);
    public string Motivo { get => _motivo; set => Set(ref _motivo, value); }

    public Comando AnularCommand { get; }
    public Comando ImprimirCommand { get; }

    public override void Cargar()
    {
        _cargando = true;
        var actual = _vendedora.Clave;
        Vendedoras = [new(null, "Todas las vendedoras"), .. new VentaService(Ctx).Vendedoras()
            .Select(v => new Opcion(v.Id.ToString(CultureInfo.InvariantCulture), v.Nombre))];
        _vendedora = Vendedoras.FirstOrDefault(v => v.Clave == actual) ?? Vendedoras[0];
        Avisar(nameof(Vendedoras), nameof(Vendedora));
        _cargando = false;
        Buscar();
    }

    public void Buscar()
    {
        if (_cargando) return;
        var svc = new VentaService(Ctx);
        var f = new FiltroVentas(Periodo.Periodo, _metodo.Clave,
            _vendedora.Clave is { } id ? long.Parse(id, CultureInfo.InvariantCulture) : null, _estado.Clave, _numero);
        var uuid = _seleccionada?.Uuid;
        Lista = svc.Listar(f);
        var r = svc.Resumen(f);
        TotalTexto = Dinero.Formatear(r.Total);
        ResumenTexto = $"{(r.Ventas == 1 ? "1 venta" : $"{r.Ventas.ToString("N0", Hn)} ventas")} · {(r.Unidades == 1 ? "1 artículo" : $"{r.Unidades.ToString("N0", Hn)} artículos")} · {Periodo.Etiqueta}";
        HayAnuladas = r.Anuladas > 0;
        AnuladasTexto = r.Anuladas == 0 ? "" : $"{r.Anuladas} anulada(s) por {Dinero.Formatear(r.TotalAnulado)} (no suman)";
        _seleccionada = Lista.FirstOrDefault(v => v.Uuid == uuid);
        CargarDetalle();
        Avisar(nameof(Lista), nameof(HayVentas), nameof(TotalTexto), nameof(ResumenTexto), nameof(AnuladasTexto), nameof(HayAnuladas),
               nameof(Recortado), nameof(Seleccionada));
    }

    private void CargarDetalle()
    {
        Lineas = _seleccionada is null ? [] : new VentaService(Ctx).Detalle(_seleccionada.Uuid).Lineas;
        _motivo = "";
        Avisar(nameof(HaySeleccion), nameof(Lineas), nameof(DetalleSubtotal), nameof(DetalleDescuento), nameof(DetalleHayDescuento),
               nameof(DetalleTotal), nameof(DetallePago), nameof(DetalleAnulacion), nameof(PuedeAnular), nameof(Motivo));
        AnularCommand.Refrescar();
        ImprimirCommand.Refrescar();
    }

    public bool Anular()
    {
        if (_seleccionada is not { } v) return false;
        if (_motivo.Trim().Length < 5) { Dlg.Error("Escribe el motivo de la anulación (mínimo 5 caracteres)."); return false; }
        if (!Dlg.Confirmar($"¿Anular la venta {v.Numero} por {v.TotalTexto}? El stock de sus productos regresa al inventario. Esto no se puede deshacer.")) return false;
        if (!Intentar(() => new VentaService(Ctx).Anular(v.Uuid, _motivo))) return false;
        Buscar();
        return true;
    }

    public bool Imprimir()
    {
        if (_seleccionada is not { } v || S.Impresion is not { } imp) return false;
        Recibo? r = null;
        return Intentar(() => r = new VentaService(Ctx).Recibo(v.Uuid)) && imp.ImprimirRecibo(r!);
    }
}
