using System.Globalization;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>
/// Reportes por período: indicadores (comparados con el período anterior), gráfico por día o mes,
/// más vendidos, por categoría, por método de pago y por vendedora. Exporta a Excel y a PDF
/// con exactamente los mismos números (todo sale de ReporteService.Generar).
/// </summary>
public sealed class ReportesViewModel : VistaBase
{
    private static readonly CultureInfo Hn = CultureInfo.GetCultureInfo("es-HN");
    private const double AlturaMaxima = 170;
    private Reporte? _r;

    public ReportesViewModel(Sesion s) : base(s)
    {
        Periodo = new SelectorPeriodo("mes");
        Periodo.Cambio += Cargar;
        ExcelCommand = new Comando(() => ExportarExcel(), () => PuedeExportar);
        PdfCommand = new Comando(() => ImprimirPdf(), () => PuedeExportar && S.Impresion is not null);
    }

    public override string Titulo => "Reportes";

    public SelectorPeriodo Periodo { get; }
    public bool PuedeExportar => S.Puede(Permiso.ExportarReportes);
    public bool VerCostos => S.Puede(Permiso.VerCostos);

    public Reporte? Reporte => _r;
    public List<Kpi> Kpis { get; private set; } = [];
    public List<BarraDia> Barras { get; private set; } = [];
    public bool SinVentas => (_r?.Resumen.Ventas ?? 0) == 0;
    public string TituloGrafico => _r is null ? "" : _r.Periodo.PorMes ? "Ventas por mes" : "Ventas por día";
    public string Subtitulo { get; private set; } = "";
    public List<FilaReporte> Productos => _r?.Productos ?? [];
    public List<FilaReporte> Categorias => _r?.Categorias ?? [];
    public List<FilaReporte> Metodos => _r?.Metodos ?? [];
    public List<FilaReporte> Vendedoras => _r?.Vendedoras ?? [];
    public string EntradasTexto { get; private set; } = "";

    public Comando ExcelCommand { get; }
    public Comando PdfCommand { get; }

    public override void Cargar()
    {
        _r = new ReporteService(Ctx).Generar(Periodo.Periodo);
        var re = _r.Resumen;
        Subtitulo = $"{_r.Periodo.Etiqueta} · comparado con {_r.Periodo.Anterior().Etiqueta}";

        string? variacion = null; var tendencia = "";
        if (_r.Variacion is { } v) { variacion = $"{(v >= 0 ? "+" : "−")}{Math.Abs(v):0.#}% vs período anterior"; tendencia = v >= 0 ? "sube" : "baja"; }
        else if (re.Total > 0) variacion = "Sin ventas en el período anterior";

        Kpis =
        [
            new("Total vendido", Dinero.Formatear(re.Total), $"{N(re.Ventas, "venta", "ventas")} · {N(re.Unidades, "artículo", "artículos")}", "",
                Destacado: true, Variacion: variacion ?? "", Tendencia: tendencia),
            new("Ticket promedio", Dinero.Formatear(re.Ticket), re.Descuentos > 0 ? $"Descuentos: {Dinero.Formatear(re.Descuentos)}" : "Sin descuentos", ""),
            VerCostos
                ? new Kpi("Ganancia", Dinero.Formatear(re.Ganancia), re.Total == 0 ? "Ventas menos costo de lo vendido" : $"Margen {re.Margen:0.#}% · costo {Dinero.Formatear(re.Costo)}", "")
                : new Kpi("Artículos vendidos", re.Unidades.ToString("N0", Hn), "Unidades en ventas completadas", ""),
            new("Anuladas", re.Anuladas.ToString("N0", Hn), re.Anuladas == 0 ? "Ninguna en el período" : $"{Dinero.Formatear(re.TotalAnulado)} · no suman", ""),
        ];

        var max = Math.Max(1, _r.Serie.Count == 0 ? 1 : _r.Serie.Max(x => x.Total));
        var hoy = Formato.ATexto(Formato.Hoy);
        // Con muchos días las etiquetas se amontonan: se muestra una de cada N
        var paso = _r.Serie.Count <= 16 ? 1 : (int)Math.Ceiling(_r.Serie.Count / 16.0);
        Barras = [.. _r.Serie.Select((x, i) => new BarraDia(
            i % paso == 0 ? ReporteService.EtiquetaCubeta(x.Clave, _r.Periodo.PorMes) : "",
            Dinero.Formatear(x.Total),
            x.Total == 0 ? 0 : Math.Max(4, x.Total * AlturaMaxima / max),
            $"{ReporteService.EtiquetaCubeta(x.Clave, _r.Periodo.PorMes)} · {N(x.Ventas, "venta", "ventas")}",
            x.Clave == hoy))];

        EntradasTexto = re.Entradas == 0
            ? "Sin entradas de mercadería en el período."
            : $"{N(re.Entradas, "entrada", "entradas")} · {N(re.UnidadesEntrada, "unidad", "unidades")} · {Dinero.Formatear(re.CostoEntradas)} en costo";
        Avisar(string.Empty);
        ExcelCommand.Refrescar();
        PdfCommand.Refrescar();
    }

    public bool ExportarExcel()
    {
        var nombre = $"Reporte N&A Beauty {Periodo.Periodo.DesdeSql} a {Formato.ATexto(Periodo.Periodo.UltimoDia)}.xlsx".Replace("&", "y");
        var ruta = Dlg.GuardarComo(nombre, "xlsx", "Libro de Excel");
        if (ruta is null) return false;
        if (!Intentar(() => new ReporteService(Ctx).ExportarExcel(Periodo.Periodo, ruta))) return false;
        if (Dlg.Confirmar("Reporte guardado. ¿Abrirlo ahora?")) Dlg.Abrir(ruta);
        return true;
    }

    public bool ImprimirPdf() => _r is not null && S.Impresion is { } imp && imp.ImprimirReporte(new ReporteService(Ctx).Generar(Periodo.Periodo, completo: true));

    private static string N(long n, string uno, string varios) => n == 1 ? $"1 {uno}" : $"{n.ToString("N0", Hn)} {varios}";
}
