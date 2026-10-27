using System.Globalization;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Exportar;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;

namespace NABeauty.Data.Servicios;

/// <summary>
/// Reportes. <see cref="Generar"/> es la ÚNICA fuente: la pantalla, el Excel y el PDF muestran
/// exactamente los mismos números. Sin permiso de ver costos, costo y ganancia llegan en cero.
/// </summary>
public sealed class ReporteService(Contexto ctx)
{
    private static readonly CultureInfo Hn = CultureInfo.GetCultureInfo("es-HN");

    public Reporte Generar(Periodo p, bool completo = false)
    {
        ctx.Exigir(Permiso.VerReportes);
        var costos = ctx.Usuario.Puede(Permiso.VerCostos);
        using var u = ctx.Bd.Abrir();
        var repo = new ReporteRepo(u);
        var resumen = repo.Resumen(p);
        var anterior = repo.Resumen(p.Anterior());

        var r = new Reporte
        {
            Periodo = p,
            Resumen = resumen,
            Anterior = anterior,
            Serie = CompletarSerie(p, repo.Serie(p)),
            Productos = Porcentajes(repo.Productos(p, completo ? null : 15), resumen.Subtotal),
            Categorias = Porcentajes(repo.Categorias(p), resumen.Subtotal),
            Metodos = Porcentajes(repo.Metodos(p), resumen.Total),
            Vendedoras = Porcentajes(repo.Vendedoras(p), resumen.Total),
            IncluyeCostos = costos,
            Negocio = AjustesService.LeerNegocio(new ConfigRepo(u)).Nombre,
            GeneradoPor = ctx.Usuario.Nombre,
        };
        foreach (var m in r.Metodos) m.Nombre = MetodosPago.Nombre(m.Nombre);
        if (!costos)
        {
            resumen.Costo = 0; anterior.Costo = 0;
            foreach (var f in r.Productos.Concat(r.Categorias)) f.Costo = 0;
        }
        return r;
    }

    /// <summary>Etiqueta corta de una cubeta: "lun 3" (día) o "oct 2026" (mes).</summary>
    public static string EtiquetaCubeta(string clave, bool porMes)
    {
        if (porMes)
        {
            var m = DateOnly.ParseExact(clave + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return m.ToString("MMM yyyy", Hn).Replace(".", "");
        }
        var d = DateOnly.ParseExact(clave, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return d.ToString("ddd d", Hn).Replace(".", "");
    }

    /// <summary>
    /// Excel con 5 hojas: Resumen, Productos, Categorías, Pagos y Detalle (cada línea vendida).
    /// Las cifras salen de <see cref="Generar"/> (las mismas de la pantalla).
    /// </summary>
    public void ExportarExcel(Periodo p, string ruta)
    {
        ctx.Exigir(Permiso.ExportarReportes);
        var r = Generar(p, completo: true);
        List<DetalleExport> detalle;
        using (var u = ctx.Bd.Abrir()) detalle = new ReporteRepo(u).Detalle(p);
        var c = r.IncluyeCostos;

        var resumen = new Hoja("Resumen", 34, 20);
        resumen.Fila(new Celda($"{r.Negocio} — Reporte de ventas", EstiloCelda.Titulo))
               .Fila(new Celda(r.Periodo.Etiqueta, EstiloCelda.Tenue))
               .Fila(new Celda($"Generado el {r.Generado:dd/MM/yyyy HH:mm} por {r.GeneradoPor}", EstiloCelda.Tenue))
               .Vacia()
               .Fila(Celda.Enc("Indicador"), Celda.Enc("Valor"))
               .Fila("Ventas", r.Resumen.Ventas)
               .Fila("Total vendido", Celda.Dinero(r.Resumen.Total, negrita: true))
               .Fila("Subtotal (antes de descuentos)", Celda.Dinero(r.Resumen.Subtotal))
               .Fila("Descuentos", Celda.Dinero(r.Resumen.Descuentos))
               .Fila("Unidades vendidas", r.Resumen.Unidades)
               .Fila("Ticket promedio", Celda.Dinero(r.Resumen.Ticket));
        if (c)
            resumen.Fila("Costo de lo vendido", Celda.Dinero(r.Resumen.Costo))
                   .Fila("Ganancia", Celda.Dinero(r.Resumen.Ganancia, negrita: true))
                   .Fila("Margen", Celda.Pct(r.Resumen.Margen));
        resumen.Fila("Ventas anuladas (no suman)", r.Resumen.Anuladas)
               .Fila("Monto anulado", Celda.Dinero(r.Resumen.TotalAnulado))
               .Fila("Entradas de mercadería", r.Resumen.Entradas)
               .Fila("Unidades que ingresaron", r.Resumen.UnidadesEntrada)
               .Fila("Costo de las entradas", Celda.Dinero(r.Resumen.CostoEntradas))
               .Vacia()
               .Fila(Celda.Enc(r.Periodo.PorMes ? "Mes" : "Día"), Celda.Enc("Total"), Celda.Enc("Ventas"));
        foreach (var s in r.Serie) resumen.Fila(EtiquetaCubeta(s.Clave, r.Periodo.PorMes), Celda.Dinero(s.Total), s.Ventas);

        Hoja Agrupada(string nombre, string col, List<FilaReporte> filas, bool conCosto, bool conDetalle = false)
        {
            var h = new Hoja(nombre, conDetalle ? [34, 18, 10, 10, 16, 10, 16, 16] : [34, 10, 10, 16, 10, 16, 16]) { FijarHasta = 1 };
            var enc = new List<Celda> { Celda.Enc(col) };
            if (conDetalle) enc.Add(Celda.Enc("Opción"));
            enc.AddRange([Celda.Enc("Ventas"), Celda.Enc("Unidades"), Celda.Enc("Total"), Celda.Enc("% del total")]);
            if (conCosto) enc.AddRange([Celda.Enc("Costo"), Celda.Enc("Ganancia")]);
            h.Fila([.. enc]);
            foreach (var f in filas)
            {
                var fila = new List<Celda> { f.Nombre };
                if (conDetalle) fila.Add(f.Detalle ?? "");
                fila.AddRange([f.Ventas, f.Unidades, Celda.Dinero(f.Total), Celda.Pct(f.Porcentaje)]);
                if (conCosto) fila.AddRange([Celda.Dinero(f.Costo), Celda.Dinero(f.Ganancia)]);
                h.Fila([.. fila]);
            }
            return h;
        }

        var det = new Hoja("Detalle", 11, 17, 20, 14, 34, 18, 9, 13, 13, 13) { FijarHasta = 1 };
        var encDet = new List<Celda> { Celda.Enc("Venta"), Celda.Enc("Fecha"), Celda.Enc("Vendedora"), Celda.Enc("Pago"), Celda.Enc("Producto"),
                                       Celda.Enc("Opción"), Celda.Enc("Cant."), Celda.Enc("Precio"), Celda.Enc("Subtotal") };
        if (c) encDet.Add(Celda.Enc("Costo unit."));
        det.Fila([.. encDet]);
        foreach (var d in detalle)
        {
            var fila = new List<Celda> { d.Numero, Formato.Fecha(d.Fecha, conHora: true), d.Vendedora, MetodosPago.Nombre(d.MetodoPago),
                                         d.ProductoNombre, d.VarianteNombre == "Única" ? "" : d.VarianteNombre, d.Cantidad,
                                         Celda.Dinero(d.PrecioUnitario), Celda.Dinero(d.Subtotal) };
            if (c) fila.Add(Celda.Dinero(d.CostoUnitario));
            det.Fila([.. fila]);
        }

        LibroExcel.Guardar(ruta,
        [
            resumen,
            Agrupada("Productos", "Producto", r.Productos, c, conDetalle: true),
            Agrupada("Categorías", "Categoría", r.Categorias, c),
            Agrupada("Pagos y vendedoras", "Método de pago", r.Metodos, false)
                .Vacia().Fila(Celda.Enc("Vendedora"), Celda.Enc("Ventas"), Celda.Enc("Unidades"), Celda.Enc("Total"), Celda.Enc("% del total"))
                .Also(h => r.Vendedoras.ForEach(f => h.Fila(f.Nombre, f.Ventas, f.Unidades, Celda.Dinero(f.Total), Celda.Pct(f.Porcentaje)))),
            det,
        ]);
        using var w = ctx.Bd.Abrir(transaccional: true);
        new BitacoraRepo(w).Registrar(ctx.UsuarioId, "reporte.excel", "reporte", null, new { desde = p.DesdeSql, hasta = p.HastaSql });
        w.Confirmar();
    }

    /// <summary>Cubetas sin ventas en cero, para que el gráfico muestre todos los días (o meses) del período.</summary>
    private static List<PuntoSerie> CompletarSerie(Periodo p, List<PuntoSerie> datos)
    {
        var porClave = datos.ToDictionary(d => d.Clave);
        var lista = new List<PuntoSerie>();
        if (p.PorMes)
        {
            for (var m = new DateOnly(p.Desde.Year, p.Desde.Month, 1); m < p.Hasta; m = m.AddMonths(1))
            {
                var k = m.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                lista.Add(porClave.GetValueOrDefault(k) ?? new PuntoSerie { Clave = k });
            }
        }
        else
        {
            for (var d = p.Desde; d < p.Hasta; d = d.AddDays(1))
            {
                var k = Formato.ATexto(d);
                lista.Add(porClave.GetValueOrDefault(k) ?? new PuntoSerie { Clave = k });
            }
        }
        return lista;
    }

    private static List<FilaReporte> Porcentajes(List<FilaReporte> filas, long baseTotal)
    {
        foreach (var f in filas) f.Porcentaje = baseTotal > 0 ? f.Total * 100.0 / baseTotal : 0;
        return filas;
    }
}

internal static class HojaExtensiones
{
    /// <summary>Permite seguir encadenando después de una acción sobre la hoja.</summary>
    public static Hoja Also(this Hoja h, Action<Hoja> accion) { accion(h); return h; }
}
