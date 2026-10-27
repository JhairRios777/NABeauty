using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;

namespace NABeauty.Data.Servicios;

/// <summary>
/// Entradas de mercadería (suman stock) y ajustes de inventario (corrigen el stock con motivo).
/// Ninguna de las dos se edita ni se borra después: un error se corrige con otro ajuste.
/// </summary>
public sealed class EntradaService(Contexto ctx)
{
    public const int MaxLineas = 300;

    public List<EntradaFila> Listar(Periodo p, string? texto = null)
    {
        ctx.Exigir(Permiso.RegistrarEntradas);
        using var u = ctx.Bd.Abrir();
        return new EntradaRepo(u).Listar(p, texto);
    }

    public (EntradaFila Entrada, List<EntradaLinea> Lineas) Detalle(string uuid)
    {
        ctx.Exigir(Permiso.RegistrarEntradas);
        using var u = ctx.Bd.Abrir();
        var repo = new EntradaRepo(u);
        var e = repo.Obtener(uuid) ?? throw new ReglaNegocioException("La entrada ya no existe.");
        return (e, repo.Lineas(uuid));
    }

    /// <summary>Opciones del catálogo para agregar a una entrada o a un ajuste (incluye agotadas).</summary>
    public List<ArticuloCaja> Buscar(string? texto, int limite = 40)
    {
        if (!ctx.Usuario.Puede(Permiso.RegistrarEntradas)) ctx.Exigir(Permiso.AjustarInventario);
        using var u = ctx.Bd.Abrir();
        var repo = new CajaRepo(u);
        if (!string.IsNullOrWhiteSpace(texto) && repo.PorCodigo(texto.Trim()) is { } exacto) return [exacto];
        return repo.Buscar(texto, null, limite);
    }

    /// <summary>Costo actual de la opción (para proponerlo en la línea de la entrada).</summary>
    public long? CostoActual(string varianteUuid)
    {
        ctx.Exigir(Permiso.RegistrarEntradas);
        using var u = ctx.Bd.Abrir();
        return new CajaRepo(u).Costo(varianteUuid);
    }

    /// <returns>Número de la entrada (E-0001…).</returns>
    public string Registrar(string? proveedor, string? nota, IReadOnlyList<LineaEntrada> lineas, bool actualizarCostos)
    {
        ctx.Exigir(Permiso.RegistrarEntradas);
        if (lineas.Count == 0) throw new ReglaNegocioException("Agrega al menos un producto a la entrada.");
        if (lineas.Count > MaxLineas) throw new ReglaNegocioException($"Una entrada admite hasta {MaxLineas} productos.");
        if (lineas.GroupBy(l => l.VarianteUuid).Any(g => g.Count() > 1))
            throw new ReglaNegocioException("Hay un producto repetido: suma las cantidades en una sola línea.");
        foreach (var l in lineas)
        {
            if (l.Cantidad is <= 0 or > 100_000) throw new ReglaNegocioException("Cada cantidad debe ser un número entero mayor a cero.");
            if (l.CostoUnitario < 0) throw new ReglaNegocioException("El costo no puede ser negativo.");
        }

        using var u = ctx.Bd.Abrir(transaccional: true);
        var caja = new CajaRepo(u);
        var repo = new EntradaRepo(u);
        var inventario = new InventarioRepo(u);
        var uuid = Guid.NewGuid().ToString();
        var numero = $"E-{new SecuenciaRepo(u).Siguiente("entrada"):0000}";
        repo.Crear(uuid, numero, Validar.Texto(proveedor, 120), ctx.UsuarioId, lineas.Sum(l => l.Cantidad * l.CostoUnitario), Validar.Texto(nota, 500));

        foreach (var l in lineas)
        {
            // Se aceptan opciones agotadas, pero no archivadas ni desactivadas
            var a = caja.Articulo(l.VarianteUuid) ?? throw new ReglaNegocioException("Uno de los productos ya no está activo. Quítalo de la entrada.");
            repo.AgregarLinea(uuid, l.VarianteUuid, a.Producto, a.Variante, l.Cantidad, l.CostoUnitario);
            inventario.Movimiento(l.VarianteUuid, "entrada", l.Cantidad, ctx.UsuarioId, uuid, numero);
            if (actualizarCostos && l.CostoUnitario > 0) repo.ActualizarCosto(l.VarianteUuid, l.CostoUnitario);
        }
        new SyncRepo(u).Encolar("entrada", uuid, "crear");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "entrada.crear", "entrada", null, new { uuid, numero, lineas = lineas.Count });
        u.Confirmar();
        return numero;
    }

    public List<AjusteFila> Ajustes(Periodo p)
    {
        ctx.Exigir(Permiso.AjustarInventario);
        using var u = ctx.Bd.Abrir();
        return new EntradaRepo(u).Ajustes(p);
    }

    /// <summary>
    /// Ajuste de inventario.
    /// <paramref name="conteo"/> = true: <paramref name="cantidad"/> es el stock REAL contado (se registra la diferencia).
    /// <paramref name="conteo"/> = false: <paramref name="cantidad"/> se suma (positiva) o se resta (negativa).
    /// </summary>
    /// <returns>Diferencia aplicada.</returns>
    public long Ajustar(string varianteUuid, long cantidad, bool conteo, string? motivo)
    {
        ctx.Exigir(Permiso.AjustarInventario);
        var m = Validar.Texto(motivo, 200);
        if (m is null || m.Length < 4) throw new ReglaNegocioException("Escribe el motivo del ajuste (dañado, conteo físico, regalo…).");

        using var u = ctx.Bd.Abrir(transaccional: true);
        var inventario = new InventarioRepo(u);
        var nombre = new ProductoRepo(u).NombreDeVariante(varianteUuid) ?? throw new ReglaNegocioException("El producto ya no existe.");
        var actual = inventario.Stock(varianteUuid);
        var diferencia = conteo ? cantidad - actual : cantidad;
        if (conteo && cantidad < 0) throw new ReglaNegocioException("El conteo no puede ser negativo.");
        if (diferencia == 0) throw new ReglaNegocioException(conteo ? $"El stock de {nombre} ya es {actual}: no hay nada que ajustar." : "La cantidad no puede ser cero.");
        if (actual + diferencia < 0) throw new ReglaNegocioException($"{nombre} tiene {actual} en stock: no se pueden restar {-diferencia}.");

        inventario.Movimiento(varianteUuid, "ajuste", diferencia, ctx.UsuarioId, nota: m);
        new SyncRepo(u).Encolar("inventario", varianteUuid, "actualizar");
        new BitacoraRepo(u).Registrar(ctx.UsuarioId, "inventario.ajuste", "variante", null, new { varianteUuid, nombre, antes = actual, diferencia, motivo = m });
        u.Confirmar();
        return diferencia;
    }
}
