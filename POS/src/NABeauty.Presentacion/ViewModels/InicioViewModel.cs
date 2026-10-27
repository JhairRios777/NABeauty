using System.Globalization;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;

namespace NABeauty.Presentacion.ViewModels;

/// <summary>
/// Tablero de Inicio: cómo va el negocio hoy, en la semana y en el mes; ventas de los últimos
/// 14 días; lo más vendido; lo que se está agotando; las últimas ventas y el estado de la
/// conexión con la tienda en línea. Cada bloque aparece solo si el usuario tiene el permiso.
/// Con la base vacía muestra la guía de primeros pasos en vez de tarjetas en cero sin contexto.
/// </summary>
public sealed class InicioViewModel(Sesion s) : VistaBase(s)
{
    private static readonly CultureInfo Hn = CultureInfo.GetCultureInfo("es-HN");
    private static readonly string[] DiasCortos = ["dom", "lun", "mar", "mié", "jue", "vie", "sáb"];
    private const double AlturaMaxima = 150;

    private Metricas _m = new();

    public override string Titulo => "Inicio";

    public bool VerVentas => S.Puede(Permiso.VerVentas);
    public bool VerInventario => S.Puede(Permiso.VerProductos);
    public bool VerCostos => S.Puede(Permiso.VerCostos);

    public string Saludo
    {
        get
        {
            var h = DateTime.Now.Hour;
            var parte = h < 12 ? "Buenos días" : h < 19 ? "Buenas tardes" : "Buenas noches";
            var nombre = (S.Usuario?.Nombre ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return nombre.Length > 0 ? $"{parte}, {nombre}" : parte;
        }
    }
    public string Resumen { get; private set; } = "";

    public List<Kpi> KpisVentas { get; private set; } = [];
    public List<Kpi> KpisInventario { get; private set; } = [];

    public List<BarraDia> Barras { get; private set; } = [];
    public string TotalCatorce { get; private set; } = "";
    public bool SinVentasRecientes { get; private set; }

    public List<FilaPanel> Top { get; private set; } = [];
    public List<FilaPanel> Agotandose { get; private set; } = [];
    public List<FilaPanel> Ultimas { get; private set; } = [];
    public bool HayTop => Top.Count > 0;
    public bool HayAgotandose => Agotandose.Count > 0;
    public bool HayUltimas => Ultimas.Count > 0;

    // Primeros pasos (base vacía)
    public bool MostrarPrimerosPasos => _m.SinCatalogo;
    public List<Paso> Pasos { get; private set; } = [];

    // Conexión con la tienda en línea
    public bool SyncConfigurada { get; private set; }
    public string SyncTitulo { get; private set; } = "";
    public string SyncDetalle { get; private set; } = "";

    public Comando IrCommand => new(p => { if (p is string seccion) Nav.IrA(seccion); });
    public Comando RefrescarCommand => new(Cargar);

    public override void Cargar()
    {
        var panel = new PanelService(Ctx);
        _m = panel.Metricas();

        KpisVentas = VerVentas ? ArmarKpisVentas() : [];
        KpisInventario = VerInventario ? ArmarKpisInventario() : [];

        if (VerVentas)
        {
            var dias = panel.VentasPorDia(14);
            var max = Math.Max(1, dias.Max(d => d.Total));
            var hoy = Formato.ATexto(Formato.Hoy);
            Barras = [.. dias.Select(d =>
            {
                var f = Formato.DeTexto(d.Dia);
                var etiqueta = d.Dia == hoy ? "Hoy" : $"{DiasCortos[(int)f.DayOfWeek]} {f.Day}";
                return new BarraDia(etiqueta, Dinero.Formatear(d.Total),
                    d.Total == 0 ? 0 : Math.Max(4, d.Total * AlturaMaxima / max),
                    d.Ventas == 0 ? "sin ventas" : d.Ventas == 1 ? "1 venta" : $"{d.Ventas} ventas",
                    d.Dia == hoy);
            })];
            var total = dias.Sum(d => d.Total);
            SinVentasRecientes = total == 0;
            TotalCatorce = $"{Dinero.Formatear(total)} en {dias.Sum(d => d.Ventas)} venta(s)";

            Top = [.. panel.TopDelMes(5).Select((t, i) => new FilaPanel(
                $"{i + 1}. {t.Producto}", t.Marca ?? "", t.Unidades == 1 ? "1 unid." : $"{t.Unidades} unid.", Dinero.Formatear(t.Total)))];

            Ultimas = [.. panel.UltimasVentas(6).Select(v => new FilaPanel(
                v.Numero, $"{Formato.Fecha(v.Fecha, conHora: true)} · {MetodoPago(v.MetodoPago)}",
                Dinero.Formatear(v.Total), v.Unidades == 1 ? "1 unid." : $"{v.Unidades} unid.",
                v.Estado, v.Estado == "anulada" ? "Anulada" : ""))];
        }
        else
        {
            Barras = []; Top = []; Ultimas = [];
        }

        Agotandose = VerInventario
            ? [.. panel.PorAgotarse(8).Select(x => new FilaPanel(x.Producto, x.Detalle, "", "", x.Estado, x.EstadoTexto))]
            : [];

        Pasos =
        [
            new("1", "Crea tu contraseña", "Ya entraste con tu propio usuario.", !(S.Usuario?.DebeCambiarPassword ?? true)),
            new("2", "Carga el catálogo", "Categorías y productos con sus tonos, precios y fotos.", _m.ProductosActivos > 0),
            new("3", "Carga el stock", "Al crear cada producto escribe cuántas unidades tienes en tienda (stock inicial).", _m.ConStock > 0),
            new("4", "Abre la caja y vende", "Cada venta descuenta el stock y aparece aquí en el tablero.", _m.NumVentasMes > 0),
        ];

        var sync = panel.EstadoSync();
        SyncConfigurada = sync.Configurada;
        SyncTitulo = sync.Configurada ? "Conectado con la tienda en línea" : "Sin conectar con la tienda en línea";
        SyncDetalle = sync.Configurada
            ? (sync.Pendientes == 0 ? "Todo enviado" : $"{sync.Pendientes} cambio(s) por enviar") +
              (sync.UltimaSync is null ? "" : $" · última vez {Formato.Fecha(sync.UltimaSync, conHora: true)}")
            : "El POS funciona igual sin internet. La conexión se configura en Ajustes.";

        Resumen = ArmarResumen();
        Avisar(string.Empty);   // refresca todas las propiedades
    }

    private List<Kpi> ArmarKpisVentas()
    {
        var lista = new List<Kpi>
        {
            new("Ventas de hoy", Dinero.Formatear(_m.VentasHoy), Cantidad(_m.NumVentasHoy, "venta", "ventas"), "", Destacado: true,
                Variacion: Comparar(_m.VentasHoy, _m.VentasAyer, "ayer", out var t1), Tendencia: t1),
            new("Esta semana", Dinero.Formatear(_m.VentasSemana), Cantidad(_m.NumVentasSemana, "venta", "ventas"), "",
                Variacion: Comparar(_m.VentasSemana, _m.VentasSemanaAnterior, "la semana pasada", out var t2), Tendencia: t2),
            new("Este mes", Dinero.Formatear(_m.VentasMes), $"{Cantidad(_m.NumVentasMes, "venta", "ventas")} · {Cantidad(_m.UnidadesMes, "unidad", "unidades")}", "",
                Variacion: Comparar(_m.VentasMes, _m.VentasMesAnterior, "el mes pasado", out var t3), Tendencia: t3),
        };
        lista.Add(VerCostos
            ? new Kpi("Ganancia del mes", Dinero.Formatear(_m.GananciaMes),
                _m.VentasMes == 0 ? "Ventas menos costo de lo vendido" : $"Margen {_m.GananciaMes * 100.0 / _m.VentasMes:0.#}% · ticket {Dinero.Formatear(_m.TicketPromedioMes)}",
                "")
            : new Kpi("Ticket promedio", Dinero.Formatear(_m.TicketPromedioMes), "Promedio por venta este mes", ""));
        return lista;
    }

    private List<Kpi> ArmarKpisInventario() =>
    [
        new("Productos activos", _m.ProductosActivos.ToString("N0", Hn), Cantidad(_m.Categorias, "categoría", "categorías"), ""),
        new("Por agotarse", _m.PorAgotarse.ToString("N0", Hn), $"Con {Data.Repositorios.DashboardRepo.StockBajo} unidades o menos", ""),
        new("Agotados", _m.Agotados.ToString("N0", Hn), "Opciones a la venta sin stock", ""),
        new("Ventas anuladas", _m.AnuladasMes.ToString("N0", Hn), "Este mes · no suman a los totales", ""),
    ];

    private string ArmarResumen()
    {
        if (_m.SinCatalogo) return "La base de datos está lista. Empieza cargando el catálogo para ver aquí tus cifras.";
        if (!VerVentas) return $"{Cantidad(_m.ProductosActivos, "producto activo", "productos activos")} · {Cantidad(_m.PorAgotarse + _m.Agotados, "opción por reponer", "opciones por reponer")}.";
        if (_m.NumVentasHoy == 0) return "Todavía no hay ventas hoy.";
        return $"Hoy llevas {Cantidad(_m.NumVentasHoy, "venta", "ventas")} por {Dinero.Formatear(_m.VentasHoy)}.";
    }

    /// <summary>"▲ 12% vs ayer". Sin datos del período anterior no se inventa un porcentaje.</summary>
    private static string Comparar(long actual, long anterior, string contra, out string tendencia)
    {
        if (anterior == 0)
        {
            tendencia = "";
            return actual == 0 ? "" : $"Sin ventas {contra} para comparar";
        }
        var pct = (actual - anterior) * 100.0 / anterior;
        tendencia = pct >= 0 ? "sube" : "baja";
        return $"{(pct >= 0 ? "+" : "−")}{Math.Abs(pct):0.#}% vs {contra}";
    }

    private static string Cantidad(long n, string singular, string plural) =>
        n == 1 ? $"1 {singular}" : $"{n.ToString("N0", Hn)} {plural}";

    public static string MetodoPago(string clave) => clave switch
    {
        "efectivo" => "Efectivo",
        "tarjeta" => "Tarjeta",
        "transferencia" => "Transferencia",
        _ => "Otro",
    };
}
