using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.App.Servicios;

/// <summary>
/// Impresión con WPF (FlowDocument), sin paquetes:
///  * Recibo: ancho de papel térmico (58 u 80 mm), texto negro, sin colores (las térmicas no los tienen).
///  * Reporte: hoja carta. Eligiendo "Microsoft Print to PDF" en el diálogo queda como PDF.
/// Siempre en blanco y negro sobre blanco: no depende del tema claro/oscuro.
/// </summary>
public sealed class ImpresionWpf : IImpresion
{
    private const double PxPorMm = 96 / 25.4;
    private static readonly FontFamily Fuente = new("Segoe UI");
    private static readonly FontFamily Mono = new("Consolas");

    // ═══════════════════════ Recibo ═══════════════════════
    public bool ImprimirRecibo(Recibo r, bool preguntar = true)
    {
        var dlg = new PrintDialog();
        if (preguntar && dlg.ShowDialog() != true) return false;

        var ancho = r.Negocio.AnchoRecibo * PxPorMm;
        var doc = new FlowDocument
        {
            PageWidth = ancho,
            ColumnWidth = ancho,
            PagePadding = new Thickness(r.Negocio.AnchoRecibo == 58 ? 6 : 10, 8, r.Negocio.AnchoRecibo == 58 ? 6 : 10, 16),
            FontFamily = Fuente,
            FontSize = r.Negocio.AnchoRecibo == 58 ? 9.5 : 11,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            TextAlignment = TextAlignment.Left,
        };

        Centro(doc, r.Negocio.Nombre, 1.45, bold: true);
        if (!string.IsNullOrWhiteSpace(r.Negocio.Rtn)) Centro(doc, $"RTN {r.Negocio.Rtn}");
        if (!string.IsNullOrWhiteSpace(r.Negocio.Direccion)) Centro(doc, r.Negocio.Direccion!);
        if (!string.IsNullOrWhiteSpace(r.Negocio.Telefono)) Centro(doc, $"Tel. {Validar.TelefonoLegible(Validar.Telefono(r.Negocio.Telefono))}");
        Linea(doc);
        if (r.Anulada) Centro(doc, "*** VENTA ANULADA ***", 1.15, bold: true);
        Par(doc, $"Venta {r.Numero}", r.Fecha);
        if (!string.IsNullOrWhiteSpace(r.Vendedora)) doc.Blocks.Add(P($"Atendió: {r.Vendedora}"));
        Linea(doc);

        foreach (var l in r.Lineas)
        {
            doc.Blocks.Add(P(l.Nombre, bold: true));
            Par(doc, $"  {l.Cantidad} x {Dinero.Formatear(l.PrecioUnitario)}", Dinero.Formatear(l.Subtotal));
        }
        Linea(doc);
        Par(doc, "Subtotal", Dinero.Formatear(r.Subtotal));
        if (r.Descuento > 0) Par(doc, "Descuento", "− " + Dinero.Formatear(r.Descuento));
        Par(doc, "TOTAL", Dinero.Formatear(r.Total), 1.3, bold: true);
        Par(doc, "Pago", MetodosPago.Nombre(r.MetodoPago));
        if (r.MetodoPago == "efectivo" && r.Recibido is { } rec)
        {
            Par(doc, "Recibido", Dinero.Formatear(rec));
            Par(doc, "Cambio", Dinero.Formatear(r.Cambio), bold: true);
        }
        Linea(doc);
        Centro(doc, $"{r.Unidades} artículo(s)");
        if (!string.IsNullOrWhiteSpace(r.Negocio.MensajeRecibo)) Centro(doc, r.Negocio.MensajeRecibo!, 1.05, bold: true);

        // Alto: lo que ocupe (las térmicas cortan al final). Se mide para no partir en páginas.
        doc.PageHeight = Math.Max(300, 120 + doc.Blocks.Count * doc.FontSize * 1.9);
        Imprimir(dlg, doc, $"Recibo {r.Numero}");
        return true;
    }

    // ═══════════════════════ Reporte ═══════════════════════
    public bool ImprimirReporte(Reporte r)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return false;

        // Carta: 8.5 x 11 pulgadas
        var doc = new FlowDocument
        {
            PageWidth = 8.5 * 96,
            PageHeight = 11 * 96,
            ColumnWidth = 8.5 * 96,
            PagePadding = new Thickness(56, 52, 56, 52),
            FontFamily = Fuente,
            FontSize = 11,
            Foreground = Brushes.Black,
            Background = Brushes.White,
        };
        var vino = new SolidColorBrush(Color.FromRgb(0x9E, 0x1B, 0x2A));
        doc.Blocks.Add(new Paragraph(new Run($"{r.Negocio} — Reporte de ventas")) { FontSize = 20, FontWeight = FontWeights.Bold, Foreground = vino, Margin = new Thickness(0) });
        doc.Blocks.Add(new Paragraph(new Run(r.Periodo.Etiqueta)) { FontSize = 13, Margin = new Thickness(0, 2, 0, 0) });
        doc.Blocks.Add(new Paragraph(new Run($"Generado el {r.Generado:dd/MM/yyyy HH:mm} por {r.GeneradoPor}")) { FontSize = 9.5, Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 14) });

        var re = r.Resumen;
        var resumen = new List<(string, string)>
        {
            ("Total vendido", Dinero.Formatear(re.Total)),
            ("Ventas", re.Ventas.ToString("N0")),
            ("Unidades", re.Unidades.ToString("N0")),
            ("Ticket promedio", Dinero.Formatear(re.Ticket)),
            ("Descuentos", Dinero.Formatear(re.Descuentos)),
        };
        if (r.IncluyeCostos)
            resumen.AddRange([("Costo de lo vendido", Dinero.Formatear(re.Costo)), ("Ganancia", Dinero.Formatear(re.Ganancia)), ("Margen", $"{re.Margen:0.#}%")]);
        resumen.AddRange([
            ("Período anterior", Dinero.Formatear(r.Anterior.Total) + (r.Variacion is { } v ? $"  ({(v >= 0 ? "+" : "−")}{Math.Abs(v):0.#}%)" : "")),
            ("Anuladas (no suman)", $"{re.Anuladas} · {Dinero.Formatear(re.TotalAnulado)}"),
            ("Entradas de mercadería", $"{re.Entradas} · {re.UnidadesEntrada} unid. · {Dinero.Formatear(re.CostoEntradas)}"),
        ]);
        doc.Blocks.Add(Seccion("Resumen", vino));
        doc.Blocks.Add(Tabla([2, 1.4], ["Indicador", "Valor"], resumen.Select(x => new[] { x.Item1, x.Item2 }), [false, true], vino));

        string[] Fila(FilaReporte f, bool detalle) => detalle
            ? [f.Nombre + (f.Detalle is null ? "" : $" · {f.Detalle}"), f.Unidades.ToString("N0"), Dinero.Formatear(f.Total), f.PorcentajeTexto]
            : [f.Nombre, f.Ventas.ToString("N0"), Dinero.Formatear(f.Total), f.PorcentajeTexto];

        doc.Blocks.Add(Seccion("Productos más vendidos", vino));
        doc.Blocks.Add(Tabla([3, 0.8, 1.2, 0.8], ["Producto", "Unid.", "Total", "%"], r.Productos.Take(40).Select(f => Fila(f, true)), [false, true, true, true], vino));
        doc.Blocks.Add(Seccion("Por categoría", vino));
        doc.Blocks.Add(Tabla([3, 0.8, 1.2, 0.8], ["Categoría", "Ventas", "Total", "%"], r.Categorias.Select(f => Fila(f, false)), [false, true, true, true], vino));
        doc.Blocks.Add(Seccion("Por método de pago", vino));
        doc.Blocks.Add(Tabla([3, 0.8, 1.2, 0.8], ["Método", "Ventas", "Total", "%"], r.Metodos.Select(f => Fila(f, false)), [false, true, true, true], vino));
        doc.Blocks.Add(Seccion("Por vendedora", vino));
        doc.Blocks.Add(Tabla([3, 0.8, 1.2, 0.8], ["Vendedora", "Ventas", "Total", "%"], r.Vendedoras.Select(f => Fila(f, false)), [false, true, true, true], vino));
        doc.Blocks.Add(Seccion(r.Periodo.PorMes ? "Ventas por mes" : "Ventas por día", vino));
        doc.Blocks.Add(Tabla([2, 0.8, 1.2], [r.Periodo.PorMes ? "Mes" : "Día", "Ventas", "Total"],
            r.Serie.Where(s => s.Ventas > 0).Select(s => new[] { ReporteService.EtiquetaCubeta(s.Clave, r.Periodo.PorMes), s.Ventas.ToString("N0"), Dinero.Formatear(s.Total) }),
            [false, true, true], vino));

        Imprimir(dlg, doc, $"Reporte {r.Periodo.Etiqueta}");
        return true;
    }

    // ═══════════════════════ Auxiliares ═══════════════════════
    private static void Imprimir(PrintDialog dlg, FlowDocument doc, string titulo)
    {
        // Sin diálogo (impresión automática) PrintQueue es la predeterminada del sistema
        dlg.PrintQueue ??= LocalPrintServer.GetDefaultPrintQueue();
        var paginador = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        paginador.PageSize = new Size(doc.PageWidth, doc.PageHeight);
        dlg.PrintDocument(paginador, titulo);
    }

    private static Paragraph P(string texto, bool bold = false, double escala = 1) => new Paragraph(new Run(texto))
    {
        Margin = new Thickness(0, 1, 0, 1),
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
    }.Escalar(escala);

    private static void Centro(FlowDocument doc, string texto, double escala = 1, bool bold = false)
    {
        var p = P(texto, bold, escala);
        p.TextAlignment = TextAlignment.Center;
        if (escala != 1) p.FontSize = doc.FontSize * escala;
        doc.Blocks.Add(p);
    }

    /// <summary>Texto a la izquierda y monto a la derecha en la misma línea.</summary>
    private static void Par(FlowDocument doc, string izq, string der, double escala = 1, bool bold = false)
    {
        var t = new Table { CellSpacing = 0, Margin = new Thickness(0, 1, 0, 1) };
        t.Columns.Add(new TableColumn { Width = new GridLength(1.4, GridUnitType.Star) });
        t.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var fila = new TableRow();
        fila.Cells.Add(new TableCell(new Paragraph(new Run(izq)) { Margin = new Thickness(0) }));
        fila.Cells.Add(new TableCell(new Paragraph(new Run(der)) { Margin = new Thickness(0), TextAlignment = TextAlignment.Right }));
        var g = new TableRowGroup();
        g.Rows.Add(fila);
        t.RowGroups.Add(g);
        if (bold) t.FontWeight = FontWeights.Bold;
        if (escala != 1) t.FontSize = doc.FontSize * escala;
        doc.Blocks.Add(t);
    }

    private static void Linea(FlowDocument doc) =>
        doc.Blocks.Add(new Paragraph(new Run(new string('-', 60))) { FontFamily = Mono, FontSize = 8, Margin = new Thickness(0, 3, 0, 3), TextAlignment = TextAlignment.Left });

    private static Paragraph Seccion(string titulo, Brush color) =>
        new(new Run(titulo)) { FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = color, Margin = new Thickness(0, 14, 0, 4) };

    private static Table Tabla(double[] anchos, string[] encabezados, IEnumerable<string[]> filas, bool[] derecha, Brush color)
    {
        var t = new Table { CellSpacing = 0, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1) };
        foreach (var a in anchos) t.Columns.Add(new TableColumn { Width = new GridLength(a, GridUnitType.Star) });
        var g = new TableRowGroup();
        var enc = new TableRow { Background = color, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };
        for (var i = 0; i < encabezados.Length; i++) enc.Cells.Add(Celda(encabezados[i], derecha[i]));
        g.Rows.Add(enc);
        var n = 0;
        foreach (var f in filas)
        {
            var row = new TableRow { Background = n++ % 2 == 1 ? new SolidColorBrush(Color.FromRgb(0xF7, 0xF2, 0xF3)) : Brushes.White };
            for (var i = 0; i < f.Length; i++) row.Cells.Add(Celda(f[i], derecha[i]));
            g.Rows.Add(row);
        }
        if (n == 0)
        {
            var vacia = new TableRow();
            vacia.Cells.Add(new TableCell(new Paragraph(new Run("Sin datos en el período")) { Foreground = Brushes.Gray, Margin = new Thickness(6, 4, 6, 4) }) { ColumnSpan = anchos.Length });
            g.Rows.Add(vacia);
        }
        t.RowGroups.Add(g);
        return t;
    }

    private static TableCell Celda(string texto, bool derecha) =>
        new(new Paragraph(new Run(texto)) { Margin = new Thickness(6, 4, 6, 4), TextAlignment = derecha ? TextAlignment.Right : TextAlignment.Left });
}

internal static class ExtensionesParrafo
{
    public static Paragraph Escalar(this Paragraph p, double escala)
    {
        if (escala != 1) p.FontSize *= escala;
        return p;
    }
}
