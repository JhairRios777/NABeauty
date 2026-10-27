using NABeauty.Core.Entidades;
using NABeauty.Data.Infra;

namespace NABeauty.Data.Repositorios;

/// <summary>Tabla categorias. Solo SQL: las reglas viven en CatalogoService.</summary>
public sealed class CategoriaRepo(UnidadTrabajo u)
{
    private const string Select = """
        SELECT c.uuid, c.slug, c.nombre, c.color, c.orden, c.activa,
               (SELECT COUNT(*) FROM productos p WHERE p.categoria_uuid = c.uuid AND p.estado = 'activo') AS productos
          FROM categorias c
        """;

    public List<Categoria> Listar(bool soloActivas = false) => u.Lista<Categoria>(
        $"{Select} {(soloActivas ? "WHERE c.activa = 1" : "")} ORDER BY c.orden, c.nombre COLLATE NOCASE");

    public Categoria? Obtener(string uuid) => u.Uno<Categoria>($"{Select} WHERE c.uuid = @uuid", new { uuid });

    public bool NombreExiste(string nombre, string? excepto) => u.Escalar<long>(
        "SELECT COUNT(*) FROM categorias WHERE nombre = @nombre COLLATE NOCASE AND uuid <> COALESCE(@excepto, '')",
        new { nombre, excepto }) > 0;

    public bool SlugExiste(string slug, string? excepto) => u.Escalar<long>(
        "SELECT COUNT(*) FROM categorias WHERE slug = @slug AND uuid <> COALESCE(@excepto, '')", new { slug, excepto }) > 0;

    public long SiguienteOrden() => u.Escalar<long>("SELECT COALESCE(MAX(orden), 0) + 1 FROM categorias");

    /// <summary>Todos los productos (también archivados): una categoría con historial no se borra.</summary>
    public long ProductosTotales(string uuid) =>
        u.Escalar<long>("SELECT COUNT(*) FROM productos WHERE categoria_uuid = @uuid", new { uuid });

    public void Crear(Categoria c) => u.Ejecutar(
        "INSERT INTO categorias (uuid, slug, nombre, color, orden, activa) VALUES (@Uuid, @Slug, @Nombre, @Color, @Orden, @Activa)", c);

    public void Actualizar(Categoria c) => u.Ejecutar("""
        UPDATE categorias SET slug = @Slug, nombre = @Nombre, color = @Color, orden = @Orden, activa = @Activa,
               actualizado = datetime('now','localtime')
         WHERE uuid = @Uuid
        """, c);

    public void Eliminar(string uuid) => u.Ejecutar("DELETE FROM categorias WHERE uuid = @uuid", new { uuid });
}
