using NABeauty.Core.Entidades;

namespace NABeauty.Core.Dominio;

/// <summary>Línea del carrito: una opción a la venta y cuántas se llevan.</summary>
public sealed class LineaCarrito(ArticuloCaja articulo, int cantidad)
{
    public ArticuloCaja Articulo { get; } = articulo;
    public int Cantidad { get; internal set; } = cantidad;
    public long PrecioUnitario => Articulo.PrecioVenta;
    public long Subtotal => Cantidad * PrecioUnitario;
}

/// <summary>
/// Carrito de la caja. Reglas puras (sin base de datos):
///  * Nunca se agrega más de lo que hay en stock (el stock que se vio al buscar; el servicio
///    lo vuelve a comprobar al cobrar, dentro de la transacción).
///  * Agregar la misma opción suma cantidad en vez de duplicar la línea.
///  * El descuento es un monto (no %) y no puede superar el subtotal.
/// </summary>
public sealed class Carrito
{
    private readonly List<LineaCarrito> _lineas = [];
    public IReadOnlyList<LineaCarrito> Lineas => _lineas;

    public long Descuento { get; private set; }
    public long Subtotal => _lineas.Sum(l => l.Subtotal);
    public long Total => Subtotal - Descuento;
    public int Unidades => _lineas.Sum(l => l.Cantidad);
    public bool Vacio => _lineas.Count == 0;

    /// <summary>Agrega una unidad (o <paramref name="cantidad"/>). Devuelve la línea afectada.</summary>
    public LineaCarrito Agregar(ArticuloCaja a, int cantidad = 1)
    {
        if (cantidad <= 0) throw new ReglaNegocioException("La cantidad debe ser mayor a cero.");
        if (a.Agotado) throw new ReglaNegocioException($"{a.Nombre} está agotado.");

        var linea = _lineas.FirstOrDefault(l => l.Articulo.VarianteUuid == a.VarianteUuid);
        var nueva = (linea?.Cantidad ?? 0) + cantidad;
        if (nueva > a.Stock)
            throw new ReglaNegocioException($"Solo hay {a.Stock} de {a.Nombre}.");

        if (linea is null)
        {
            linea = new LineaCarrito(a, cantidad);
            _lineas.Add(linea);
        }
        else linea.Cantidad = nueva;
        AjustarDescuento();
        return linea;
    }

    public void CambiarCantidad(string varianteUuid, int cantidad)
    {
        var linea = _lineas.FirstOrDefault(l => l.Articulo.VarianteUuid == varianteUuid)
                    ?? throw new ReglaNegocioException("Esa línea ya no está en la venta.");
        if (cantidad <= 0) { Quitar(varianteUuid); return; }
        if (cantidad > linea.Articulo.Stock)
            throw new ReglaNegocioException($"Solo hay {linea.Articulo.Stock} de {linea.Articulo.Nombre}.");
        linea.Cantidad = cantidad;
        AjustarDescuento();
    }

    public void Quitar(string varianteUuid)
    {
        _lineas.RemoveAll(l => l.Articulo.VarianteUuid == varianteUuid);
        AjustarDescuento();
    }

    public void AplicarDescuento(long centavos)
    {
        if (centavos < 0) throw new ReglaNegocioException("El descuento no puede ser negativo.");
        if (centavos > Subtotal) throw new ReglaNegocioException($"El descuento no puede ser mayor que el subtotal ({Dinero.Formatear(Subtotal)}).");
        Descuento = centavos;
    }

    public void Vaciar()
    {
        _lineas.Clear();
        Descuento = 0;
    }

    /// <summary>Si se quitan productos, el descuento nunca queda mayor que el nuevo subtotal.</summary>
    private void AjustarDescuento()
    {
        if (Descuento > Subtotal) Descuento = Subtotal;
    }
}

/// <summary>Métodos de pago aceptados (mismos valores que la columna ventas.metodo_pago).</summary>
public static class MetodosPago
{
    public static readonly IReadOnlyList<(string Clave, string Nombre)> Todos =
    [
        ("efectivo", "Efectivo"),
        ("tarjeta", "Tarjeta"),
        ("transferencia", "Transferencia"),
        ("otro", "Otro"),
    ];

    public static bool Valido(string? m) => Todos.Any(x => x.Clave == m);
    public static string Nombre(string clave) => Todos.FirstOrDefault(x => x.Clave == clave).Nombre ?? "Otro";
}

/// <summary>Resultado de un cobro.</summary>
public sealed record VentaRegistrada(string Uuid, string Numero, long Total, long Recibido, string MetodoPago, int Unidades)
{
    public long Cambio => Math.Max(0, Recibido - Total);
}
