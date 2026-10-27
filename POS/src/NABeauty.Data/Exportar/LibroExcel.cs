using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace NABeauty.Data.Exportar;

/// <summary>Formato de una celda.</summary>
public enum EstiloCelda
{
    Normal = 0,
    Encabezado = 1,
    Dinero = 2,
    Entero = 3,
    Titulo = 4,
    Porcentaje = 5,
    Tenue = 6,
    DineroNegrita = 7,
}

/// <summary>Celda: texto o número con su estilo.</summary>
public readonly record struct Celda(object? Valor, EstiloCelda Estilo = EstiloCelda.Normal)
{
    public static implicit operator Celda(string? s) => new(s);
    public static implicit operator Celda(long n) => new(n, EstiloCelda.Entero);
    /// <summary>Centavos → lempiras con formato "L #,##0.00".</summary>
    public static Celda Dinero(long centavos, bool negrita = false) => new(centavos / 100m, negrita ? EstiloCelda.DineroNegrita : EstiloCelda.Dinero);
    public static Celda Pct(double porcentaje) => new((decimal)Math.Round(porcentaje / 100, 4), EstiloCelda.Porcentaje);
    public static Celda Enc(string s) => new(s, EstiloCelda.Encabezado);
}

/// <summary>Hoja: filas de celdas y anchos de columna (en caracteres).</summary>
public sealed class Hoja(string nombre, params double[] anchos)
{
    public string Nombre { get; } = nombre.Length > 31 ? nombre[..31] : nombre;
    public double[] Anchos { get; } = anchos;
    public List<Celda[]> Filas { get; } = [];
    /// <summary>Fila (base 1) que queda fija al desplazar (encabezado de tabla). 0 = ninguna.</summary>
    public int FijarHasta { get; set; }

    public Hoja Fila(params Celda[] celdas) { Filas.Add(celdas); return this; }
    public Hoja Vacia() { Filas.Add([]); return this; }
}

/// <summary>
/// Escritor mínimo de .xlsx (Office Open XML) sin dependencias: un .xlsx es un zip con XML.
/// Textos en línea (inlineStr), números como número (se pueden sumar en Excel), estilos fijos.
/// Abre en Excel, LibreOffice y Google Sheets.
/// </summary>
public static class LibroExcel
{
    public static void Guardar(string ruta, IReadOnlyList<Hoja> hojas)
    {
        if (hojas.Count == 0) throw new ArgumentException("El libro necesita al menos una hoja.", nameof(hojas));
        var temporal = ruta + ".tmp";
        using (var fs = File.Create(temporal))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            Escribir(zip, "[Content_Types].xml", TiposContenido(hojas.Count));
            Escribir(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Escribir(zip, "xl/workbook.xml", Libro(hojas));
            Escribir(zip, "xl/_rels/workbook.xml.rels", RelacionesLibro(hojas.Count));
            Escribir(zip, "xl/styles.xml", Estilos);
            for (var i = 0; i < hojas.Count; i++) Escribir(zip, $"xl/worksheets/sheet{i + 1}.xml", HojaXml(hojas[i]));
        }
        // Se reemplaza al final: si algo falla no queda un archivo a medias con el nombre final
        File.Move(temporal, ruta, overwrite: true);
    }

    private static void Escribir(ZipArchive zip, string nombre, string contenido)
    {
        using var w = new StreamWriter(zip.CreateEntry(nombre, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        w.Write(contenido.Trim());
    }

    private static string TiposContenido(int n)
    {
        var sb = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            """);
        for (var i = 1; i <= n; i++)
            sb.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        return sb.Append("</Types>").ToString();
    }

    private static string Libro(IReadOnlyList<Hoja> hojas)
    {
        var sb = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>
            """);
        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < hojas.Count; i++)
        {
            var nombre = LimpiarNombre(hojas[i].Nombre);
            for (var k = 2; !usados.Add(nombre); k++) nombre = $"{LimpiarNombre(hojas[i].Nombre)[..Math.Min(28, hojas[i].Nombre.Length)]} {k}";
            sb.Append($"""<sheet name="{Xml(nombre)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
        }
        return sb.Append("</sheets></workbook>").ToString();
    }

    private static string RelacionesLibro(int n)
    {
        var sb = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            """);
        for (var i = 1; i <= n; i++)
            sb.Append($"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>""");
        sb.Append($"""<Relationship Id="rId{n + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");
        return sb.Append("</Relationships>").ToString();
    }

    private static string HojaXml(Hoja h)
    {
        var sb = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
            """);
        if (h.FijarHasta > 0)
            sb.Append($"""<sheetViews><sheetView workbookViewId="0"><pane ySplit="{h.FijarHasta}" topLeftCell="A{h.FijarHasta + 1}" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""");
        if (h.Anchos.Length > 0)
        {
            sb.Append("<cols>");
            for (var c = 0; c < h.Anchos.Length; c++)
                sb.Append(string.Create(CultureInfo.InvariantCulture, $"""<col min="{c + 1}" max="{c + 1}" width="{h.Anchos[c]}" customWidth="1"/>"""));
            sb.Append("</cols>");
        }
        sb.Append("<sheetData>");
        for (var r = 0; r < h.Filas.Count; r++)
        {
            sb.Append($"""<row r="{r + 1}">""");
            var fila = h.Filas[r];
            for (var c = 0; c < fila.Length; c++)
            {
                var celda = fila[c];
                if (celda.Valor is null && celda.Estilo == EstiloCelda.Normal) continue;
                var refe = $"{Columna(c)}{r + 1}";
                var s = (int)celda.Estilo;
                switch (celda.Valor)
                {
                    case null:
                        sb.Append($"""<c r="{refe}" s="{s}"/>""");
                        break;
                    case string texto:
                        sb.Append($"""<c r="{refe}" s="{s}" t="inlineStr"><is><t xml:space="preserve">{Xml(texto)}</t></is></c>""");
                        break;
                    default:
                        var num = Convert.ToDecimal(celda.Valor, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                        sb.Append($"""<c r="{refe}" s="{s}"><v>{num}</v></c>""");
                        break;
                }
            }
            sb.Append("</row>");
        }
        return sb.Append("</sheetData></worksheet>").ToString();
    }

    /// <summary>0 → A, 25 → Z, 26 → AA.</summary>
    public static string Columna(int indice)
    {
        var s = "";
        for (var n = indice + 1; n > 0; n = (n - 1) / 26) s = (char)('A' + (n - 1) % 26) + s;
        return s;
    }

    private static string LimpiarNombre(string n)
    {
        foreach (var c in @"[]:*?/\") n = n.Replace(c, '-');
        return n.Length == 0 ? "Hoja" : n;
    }

    /// <summary>Escapa XML y quita caracteres de control que Excel rechaza.</summary>
    private static string Xml(string s) =>
        SecurityElement.Escape(new string([.. s.Where(c => c >= ' ' || c is '\t' or '\n' or '\r')])) ?? "";

    // Índices de cellXfs = valores de EstiloCelda (no reordenar)
    private const string Estilos = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
        <numFmts count="3"><numFmt numFmtId="164" formatCode="&quot;L &quot;#,##0.00"/><numFmt numFmtId="165" formatCode="#,##0"/><numFmt numFmtId="166" formatCode="0.0%"/></numFmts>
        <fonts count="5">
          <font><sz val="11"/><name val="Calibri"/></font>
          <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
          <font><b/><sz val="15"/><color rgb="FF9E1B2A"/><name val="Calibri"/></font>
          <font><sz val="10"/><color rgb="FF76656A"/><name val="Calibri"/></font>
          <font><b/><sz val="11"/><name val="Calibri"/></font>
        </fonts>
        <fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill>
          <fill><patternFill patternType="solid"><fgColor rgb="FF9E1B2A"/><bgColor indexed="64"/></patternFill></fill></fills>
        <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
        <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
        <cellXfs count="8">
          <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
          <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
          <xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
          <xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
          <xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/>
          <xf numFmtId="166" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
          <xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1"/>
          <xf numFmtId="164" fontId="4" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/>
        </cellXfs>
        <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;
}
