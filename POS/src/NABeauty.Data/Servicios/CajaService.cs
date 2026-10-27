using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;

namespace NABeauty.Data.Servicios;

/// <summary>
/// Caja: busca lo que se vende y registra el cobro.
/// El cobro NO confía en el carrito de la pantalla: dentro de la transacción vuelve a leer
/// precio, stock y estado de cada opción. Si algo cambió desde que se agregó (otra caja vendió
/// lo último, se archivó, se cambió el precio) la venta no se registra y se avisa qué pasó.
/// </summary>
public sealed class CajaService(Contexto ctx)
{
    public const int LimiteBusqueda = 60;

    public List<ArticuloCaja> Buscar(string? texto, string? categoriaUuid = null, int limite = LimiteBusqueda)
    {
        ctx.Exigir(Permiso.Vender);
        using var u = ctx.Bd.Abrir();
        return new CajaRepo(u).Buscar(texto, categoriaUuid, limite);
    }

    /// <summary>Lo que manda el escáner (o un SKU escrito completo). null si no existe.</summary>
    public ArticuloCaja? PorCodigo(string codigo)
    {
        ctx.Exigir(Permiso.Vender);
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        using var u = ctx.Bd.Abrir();
        return new CajaRepo(u).PorCodigo(codigo.Trim());
    }

    public List<Categoria> Categorias()
    {
        ctx.Exigir(Permiso.Vender);
        using var u = ctx.Bd.Abrir();
        return [.. new CategoriaRepo(u).Listar(soloActivas: true).Where(c => c.Productos > 0)];
    }

    /// <param name="recibido">Efectivo entregado por la clienta (centavos). En otros métodos se ignora.</param>
    public VentaRegistrada Cobrar(Carrito carrito, string metodoPago, long recibido)
    {
        ctx.Exigir(Permiso.Vender);
        if (carrito.Vacio) throw new ReglaNegocioException("Agrega al menos un producto a la venta.");
        if (!MetodosPago.Valido(metodoPago)) throw new ReglaNegocioException("Elige el método de pago.");

        using var u = ctx.Bd.Abrir(transaccional: true);
        var repo = new CajaRepo(u);
        var inventario = new InventarioRepo(u);

        // 1. Revalidar cada línea contra la base (dentro de la transacción)
        var lineas = new List<(ArticuloCaja Articulo, int Cantidad)>();
        foreach (var l in carrito.Lineas)
        {
            var a = repo.Articulo(l.Articulo.VarianteUuid)
                    ?? throw new ReglaNegocioException($"{l.Articulo.Nombre} ya no está a la venta. Quítalo de la venta.");
            if (a.PrecioVenta != l.PrecioUnitario)
                throw new ReglaNegocioException($"El precio de {a.Nombre} cambió a {Dinero.Formatear(a.PrecioVenta)}. Quítalo y vuelve a agregarlo.");
            if (a.Stock < l.Cantidad)
                throw new ReglaNegocioException(a.Stock <= 0
                    ? $"{a.Nombre} se agotó. Quítalo de la venta."
                    : $"Solo quedan {a.Stock} de {a.Nombre}. Ajusta la cantidad.");
            lineas.Add((a, l.Cantidad));
        }

        var subtotal = lineas.Sum(x => x.Cantidad * x.Articulo.PrecioVenta);
        var descuento = carrito.Descuento;
        if (descuento < 0 || descuento > subtotal) throw new ReglaNegocioException("El descuento no puede ser mayor que el subtotal.");
        var total = subtotal - descuento;

        if (metodoPago == "efectivo")
        {
            if (recibido < total)
                throw new ReglaNegocioException($"El efectivo recibido ({Dinero.Formatear(recibido)}) no cubre el total ({Dinero.Formatear(total)}).");
        }
        else recibido = total;

        // 2. Registrar: venta, detalle (con costo del momento), salida de inventario, cola de sincronización
        var uuid = Guid.NewGuid().ToString();
        var numero = $"V-{new SecuenciaRepo(u).Siguiente("venta"):000000}";
        repo.CrearVenta(uuid, numero, ctx.UsuarioId, metodoPago, subtotal, descuento, total, metodoPago == "efectivo" ? recibido : null);
        foreach (var (a, cantidad) in lineas)
        {
            repo.AgregarDetalle(uuid, a, cantidad, repo.Costo(a.VarianteUuid));
            inventario.Movimiento(a.VarianteUuid, "venta", -cantidad, ctx.UsuarioId, uuid);
        }
        new SyncRepo(u).Encolar("venta", uuid, "crear");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "venta.crear", "venta", null, new { uuid, numero, total, metodoPago });
        u.Confirmar();

        return new VentaRegistrada(uuid, numero, total, recibido, metodoPago, lineas.Sum(x => x.Cantidad));
    }
}
