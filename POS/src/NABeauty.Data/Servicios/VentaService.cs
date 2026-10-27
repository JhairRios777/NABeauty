using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;

namespace NABeauty.Data.Servicios;

/// <summary>
/// Historial de ventas, anulación y recibo.
/// Anular = estado 'anulada' con motivo + devolución del stock (movimiento 'anulacion' por línea),
/// todo en una transacción. La venta nunca se borra.
/// </summary>
public sealed class VentaService(Contexto ctx)
{
    public const int Limite = 500;

    public List<VentaFila> Listar(FiltroVentas f)
    {
        ctx.Exigir(Permiso.VerVentas);
        using var u = ctx.Bd.Abrir();
        return new VentaRepo(u).Listar(f, Limite);
    }

    public ResumenVentas Resumen(FiltroVentas f)
    {
        ctx.Exigir(Permiso.VerVentas);
        using var u = ctx.Bd.Abrir();
        return new VentaRepo(u).Resumen(f);
    }

    public (VentaFila Venta, List<VentaLinea> Lineas) Detalle(string uuid)
    {
        ctx.Exigir(Permiso.VerVentas);
        using var u = ctx.Bd.Abrir();
        var repo = new VentaRepo(u);
        var v = repo.Obtener(uuid) ?? throw new ReglaNegocioException("La venta ya no existe.");
        var lineas = repo.Lineas(uuid);
        if (!ctx.Usuario.Puede(Permiso.VerCostos)) lineas.ForEach(l => l.CostoUnitario = null);
        return (v, lineas);
    }

    public List<(long Id, string Nombre)> Vendedoras()
    {
        ctx.Exigir(Permiso.VerVentas);
        using var u = ctx.Bd.Abrir();
        return new VentaRepo(u).Vendedoras();
    }

    public void Anular(string uuid, string? motivo)
    {
        ctx.Exigir(Permiso.AnularVentas);
        var m = Validar.Texto(motivo, 200);
        if (m is null || m.Length < 5) throw new ReglaNegocioException("Escribe el motivo de la anulación (mínimo 5 caracteres).");

        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new VentaRepo(u);
        var v = repo.Obtener(uuid) ?? throw new ReglaNegocioException("La venta ya no existe.");
        if (v.Anulada) throw new ReglaNegocioException($"La venta {v.Numero} ya estaba anulada.");

        repo.Anular(uuid, m, ctx.UsuarioId);
        var inventario = new InventarioRepo(u);
        foreach (var l in repo.Lineas(uuid))
            inventario.Movimiento(l.VarianteUuid, "anulacion", l.Cantidad, ctx.UsuarioId, uuid, $"Anulación {v.Numero}");
        new SyncRepo(u).Encolar("venta", uuid, "anular");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "venta.anular", "venta", null, new { uuid, v.Numero, v.Total, motivo = m });
        u.Confirmar();
    }

    /// <summary>
    /// Recibo de una venta. La cajera puede imprimir el de la venta que acaba de cobrar
    /// aunque su rol no vea el historial (Vender basta para el recibo).
    /// </summary>
    public Recibo Recibo(string uuid)
    {
        if (!ctx.Usuario.Puede(Permiso.VerVentas)) ctx.Exigir(Permiso.Vender);
        using var u = ctx.Bd.Abrir();
        var repo = new VentaRepo(u);
        var v = repo.Obtener(uuid) ?? throw new ReglaNegocioException("La venta ya no existe.");
        var negocio = AjustesService.LeerNegocio(new ConfigRepo(u));
        return new Recibo(negocio, v.Numero, v.FechaTexto, v.Vendedora ?? "", repo.Lineas(uuid),
            v.Subtotal, v.Descuento, v.Total, v.MetodoPago, v.Recibido, v.Anulada);
    }
}
