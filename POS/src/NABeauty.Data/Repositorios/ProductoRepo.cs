using NABeauty.Core.Entidades;
using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>Filtros del listado de productos. Estado: "activo", "archivado" o null (todos).</summary>
public sealed record FiltroProductos(string? Texto = null, string? CategoriaUuid = null, string? Estado = "activo", bool SoloPorReponer = false);

/// <summary>Tablas productos, producto_variantes y producto_imagenes. Solo SQL.</summary>
public sealed class ProductoRepo(UnidadTrabajo u)
{
    // ── Listado ─────────────────────────────────────────────────────
    public List<ProductoFila> Listar(FiltroProductos f)
    {
        var texto = string.IsNullOrWhiteSpace(f.Texto) ? null : $"%{f.Texto.Trim()}%";
        return u.Lista<ProductoFila>($"""
            SELECT p.uuid, p.nombre, p.marca, p.categoria_uuid, c.nombre AS categoria, c.color AS categoria_color,
                   p.estado, p.ciclo,
                   COALESCE((SELECT tipo FROM producto_variantes WHERE producto_uuid = p.uuid ORDER BY orden LIMIT 1), 'unica') AS tipo_variante,
                   COUNT(v.uuid) AS variantes,
                   COALESCE(SUM(v.stock), 0) AS stock,
                   COALESCE(SUM(CASE WHEN v.stock <= 0 THEN 1 ELSE 0 END), 0) AS agotadas,
                   COALESCE(MIN(COALESCE(v.precio_oferta, v.precio)), 0) AS precio_min,
                   COALESCE(MAX(COALESCE(v.precio_oferta, v.precio)), 0) AS precio_max,
                   (SELECT archivo FROM producto_imagenes i WHERE i.producto_uuid = p.uuid ORDER BY i.orden LIMIT 1) AS foto
              FROM productos p
              JOIN categorias c ON c.uuid = p.categoria_uuid
              LEFT JOIN producto_variantes v ON v.producto_uuid = p.uuid AND v.activa = 1
             WHERE (@estado IS NULL OR p.estado = @estado)
               AND (@cat IS NULL OR p.categoria_uuid = @cat)
               AND (@texto IS NULL OR p.nombre LIKE @texto OR p.marca LIKE @texto
                    OR EXISTS (SELECT 1 FROM producto_variantes x WHERE x.producto_uuid = p.uuid
                               AND (x.nombre LIKE @texto OR x.sku LIKE @texto OR x.codigo_barras = @exacto)))
             GROUP BY p.uuid
            {(f.SoloPorReponer ? $"HAVING SUM(CASE WHEN v.stock <= {Inventario.StockBajo} THEN 1 ELSE 0 END) > 0" : "")}
             ORDER BY p.nombre COLLATE NOCASE
            """, new { estado = f.Estado, cat = f.CategoriaUuid, texto, exacto = f.Texto?.Trim() });
    }

    // ── Detalle ─────────────────────────────────────────────────────
    public Producto? Obtener(string uuid)
    {
        var p = u.Uno<Producto>("""
            SELECT uuid, categoria_uuid, slug, nombre, marca, descripcion, ciclo, estado, destacado
              FROM productos WHERE uuid = @uuid
            """, new { uuid });
        if (p is null) return null;
        p.Variantes = Variantes(uuid);
        p.Fotos = Fotos(uuid);
        return p;
    }

    public List<Variante> Variantes(string productoUuid) => u.Lista<Variante>("""
        SELECT v.uuid, v.producto_uuid, v.sku, v.codigo_barras, v.tipo, v.nombre, v.color_hex,
               v.precio, v.precio_oferta, v.costo, v.stock, v.orden, v.activa,
               EXISTS (SELECT 1 FROM inventario_movimientos m WHERE m.variante_uuid = v.uuid) AS con_historial
          FROM producto_variantes v
         WHERE v.producto_uuid = @productoUuid
         ORDER BY v.orden, v.nombre
        """, new { productoUuid });

    public List<FotoProducto> Fotos(string productoUuid) => u.Lista<FotoProducto>(
        "SELECT uuid, producto_uuid, archivo, orden FROM producto_imagenes WHERE producto_uuid = @productoUuid ORDER BY orden, creado",
        new { productoUuid });

    // ── Validaciones de unicidad ────────────────────────────────────
    public bool SlugExiste(string slug, string? excepto) => u.Escalar<long>(
        "SELECT COUNT(*) FROM productos WHERE slug = @slug AND uuid <> COALESCE(@excepto, '')", new { slug, excepto }) > 0;

    /// <summary>Uuid de la variante que ya usa ese SKU o código de barras (null si está libre).</summary>
    public string? VarianteConCodigo(string columna, string valor, string? excepto)
    {
        if (columna is not ("sku" or "codigo_barras")) throw new ArgumentException("Columna no permitida", nameof(columna));
        return u.Escalar<string?>(
            $"SELECT uuid FROM producto_variantes WHERE {columna} = @valor AND uuid <> COALESCE(@excepto, '') LIMIT 1",
            new { valor, excepto });
    }

    public string? NombreDeVariante(string uuid) => u.Escalar<string?>("""
        SELECT p.nombre || CASE WHEN v.tipo = 'unica' THEN '' ELSE ' · ' || v.nombre END
          FROM producto_variantes v JOIN productos p ON p.uuid = v.producto_uuid WHERE v.uuid = @uuid
        """, new { uuid });

    // ── Escritura ───────────────────────────────────────────────────
    public void Crear(Producto p) => u.Ejecutar("""
        INSERT INTO productos (uuid, categoria_uuid, slug, nombre, marca, descripcion, ciclo, estado, destacado)
        VALUES (@Uuid, @CategoriaUuid, @Slug, @Nombre, @Marca, @Descripcion, @Ciclo, @Estado, @Destacado)
        """, p);

    public void Actualizar(Producto p) => u.Ejecutar("""
        UPDATE productos SET categoria_uuid = @CategoriaUuid, slug = @Slug, nombre = @Nombre, marca = @Marca,
               descripcion = @Descripcion, ciclo = @Ciclo, destacado = @Destacado, actualizado = datetime('now','localtime')
         WHERE uuid = @Uuid
        """, p);

    public void CambiarEstado(string uuid, string estado) => u.Ejecutar(
        "UPDATE productos SET estado = @estado, actualizado = datetime('now','localtime') WHERE uuid = @uuid", new { uuid, estado });

    public void CrearVariante(Variante v) => u.Ejecutar("""
        INSERT INTO producto_variantes (uuid, producto_uuid, sku, codigo_barras, tipo, nombre, color_hex, precio, precio_oferta, costo, orden, activa)
        VALUES (@Uuid, @ProductoUuid, @Sku, @CodigoBarras, @Tipo, @Nombre, @ColorHex, @Precio, @PrecioOferta, @Costo, @Orden, @Activa)
        """, v);

    /// <summary>Actualiza todo menos el stock (el stock solo cambia por movimientos).</summary>
    public void ActualizarVariante(Variante v) => u.Ejecutar("""
        UPDATE producto_variantes SET sku = @Sku, codigo_barras = @CodigoBarras, tipo = @Tipo, nombre = @Nombre,
               color_hex = @ColorHex, precio = @Precio, precio_oferta = @PrecioOferta, costo = @Costo, orden = @Orden,
               activa = @Activa, actualizado = datetime('now','localtime')
         WHERE uuid = @Uuid
        """, v);

    public void EliminarVariante(string uuid) => u.Ejecutar("DELETE FROM producto_variantes WHERE uuid = @uuid", new { uuid });

    public void AgregarFoto(FotoProducto f) => u.Ejecutar(
        "INSERT INTO producto_imagenes (uuid, producto_uuid, archivo, orden) VALUES (@Uuid, @ProductoUuid, @Archivo, @Orden)", f);

    public void OrdenFoto(string uuid, long orden) =>
        u.Ejecutar("UPDATE producto_imagenes SET orden = @orden WHERE uuid = @uuid", new { uuid, orden });

    public void EliminarFoto(string uuid) => u.Ejecutar("DELETE FROM producto_imagenes WHERE uuid = @uuid", new { uuid });
}
