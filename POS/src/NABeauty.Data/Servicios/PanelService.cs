using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;

namespace NABeauty.Data.Servicios;

/// <summary>
/// Tablero de Inicio. Cada bloque exige su permiso AQUÍ, no solo en la pantalla:
/// una cajera no recibe los totales del mes aunque alguien muestre el bloque por error.
/// </summary>
public sealed class PanelService(Contexto ctx)
{
    public Metricas Metricas()
    {
        ctx.Exigir(Permiso.VerInicio);
        using var u = ctx.Bd.Abrir();
        var m = new DashboardRepo(u).Metricas();
        if (!ctx.Usuario.Puede(Permiso.VerVentas))
        {
            // Sin permiso de ver ventas: solo quedan los datos de inventario
            m = new Metricas { ProductosActivos = m.ProductosActivos, PorAgotarse = m.PorAgotarse, Agotados = m.Agotados, ConStock = m.ConStock, Categorias = m.Categorias };
        }
        else if (!ctx.Usuario.Puede(Permiso.VerCostos))
        {
            m.CostoMes = 0;
        }
        return m;
    }

    /// <summary>Ventas de los últimos N días, uno por día, incluidos los días sin ventas (en cero).</summary>
    public List<VentaDia> VentasPorDia(int dias = 14)
    {
        ctx.Exigir(Permiso.VerVentas);
        using var u = ctx.Bd.Abrir();
        var datos = new DashboardRepo(u).VentasPorDia(dias).ToDictionary(x => x.Dia);
        return [.. Enumerable.Range(0, dias).Reverse()
            .Select(i => Formato.ATexto(Formato.Hoy.AddDays(-i)))
            .Select(d => datos.GetValueOrDefault(d) ?? new VentaDia { Dia = d })];
    }

    public List<TopProducto> TopDelMes(int limite = 5)
    {
        ctx.Exigir(Permiso.VerVentas);
        using var u = ctx.Bd.Abrir();
        return new DashboardRepo(u).TopDelMes(limite);
    }

    public List<StockBajo> PorAgotarse(int limite = 8)
    {
        ctx.Exigir(Permiso.VerProductos);
        using var u = ctx.Bd.Abrir();
        return new DashboardRepo(u).PorAgotarse(limite);
    }

    public List<VentaReciente> UltimasVentas(int limite = 6)
    {
        ctx.Exigir(Permiso.VerVentas);
        using var u = ctx.Bd.Abrir();
        return new DashboardRepo(u).UltimasVentas(limite);
    }

    /// <summary>Estado de la conexión con la tienda en línea (Ajustes → URL de la tienda).</summary>
    public (bool Configurada, string? Url, string? UltimaSync, long Pendientes) EstadoSync()
    {
        using var u = ctx.Bd.Abrir();
        var cfg = new ConfigRepo(u);
        var url = cfg.Get("web_url");
        var pendientes = u.Escalar<long>("SELECT COUNT(*) FROM sync_cola WHERE enviado IS NULL");
        return (!string.IsNullOrWhiteSpace(url), url, cfg.Get("web_ultima_sync"), pendientes);
    }

    public string NombreNegocio()
    {
        using var u = ctx.Bd.Abrir();
        return new ConfigRepo(u).Get("negocio_nombre") ?? "N&A Beauty";
    }
}
