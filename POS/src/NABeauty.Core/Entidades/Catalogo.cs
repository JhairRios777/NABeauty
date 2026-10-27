using NABeauty.Core.Dominio;

namespace NABeauty.Core.Entidades;

/// <summary>Categoría del catálogo (tabla categorias). Misma forma que en la tienda en línea.</summary>
public sealed class Categoria
{
    public string Uuid { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Nombre { get; set; } = "";
    /// <summary>Color de la etiqueta en la caja y en la tienda (#RRGGBB).</summary>
    public string Color { get; set; } = "#F6D5DC";
    public long Orden { get; set; }
    public bool Activa { get; set; } = true;
    /// <summary>Solo en el listado: productos que la usan (no archivados).</summary>
    public long Productos { get; set; }

    public string Estado => Activa ? "activa" : "inactivo";
    public string EstadoTexto => Activa ? "Visible" : "Oculta";
    public string ProductosTexto => Productos == 1 ? "1 producto" : $"{Productos} productos";
}

/// <summary>Tipo de variante: lo mismo que acepta la tienda en línea.</summary>
public static class TiposVariante
{
    public static readonly IReadOnlyList<(string Clave, string Nombre)> Todos =
    [
        ("unica", "Sin opciones"),
        ("tono", "Tonos"),
        ("talla", "Tallas"),
        ("color", "Colores"),
        ("aroma", "Aromas"),
    ];

    public static bool Valido(string? t) => Todos.Any(x => x.Clave == t);

    /// <summary>Etiqueta de una opción: "Tono", "Talla"…</summary>
    public static string Etiqueta(string tipo) => tipo switch
    {
        "tono" => "Tono",
        "talla" => "Talla",
        "color" => "Color",
        "aroma" => "Aroma",
        _ => "Opción",
    };
}

/// <summary>Fila del listado de productos (un producto con el resumen de sus variantes).</summary>
public sealed class ProductoFila
{
    public string Uuid { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Marca { get; set; } = "";
    public string CategoriaUuid { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string CategoriaColor { get; set; } = "#F6D5DC";
    public string Estado { get; set; } = "activo";
    public string Ciclo { get; set; } = "permanente";
    public string TipoVariante { get; set; } = "unica";
    public long Variantes { get; set; }
    public long Stock { get; set; }
    public long Agotadas { get; set; }
    public long PrecioMin { get; set; }
    public long PrecioMax { get; set; }
    /// <summary>Archivo de la foto principal (null = sin foto).</summary>
    public string? Foto { get; set; }

    public string Precio => PrecioMin == PrecioMax ? Dinero.Formatear(PrecioMin) : $"{Dinero.Formatear(PrecioMin)} – {Dinero.Formatear(PrecioMax)}";
    public string Opciones => TipoVariante == "unica" ? "Sin opciones" : $"{Variantes} {TiposVariante.Todos.First(t => t.Clave == TipoVariante).Nombre.ToLowerInvariant()}";

    /// <summary>Estado del stock para la insignia: agotado / bajo / activo / inactivo (archivado).</summary>
    public string EstadoStock => Estado == "archivado" ? "inactivo" : Stock <= 0 ? "agotado" : Agotadas > 0 || Stock <= Inventario.StockBajo ? "bajo" : "activo";
    public string EstadoStockTexto => Estado == "archivado" ? "Archivado"
        : Stock <= 0 ? "Agotado"
        : Agotadas > 0 ? $"{Stock} en stock · {Agotadas} agotada(s)"
        : $"{Stock} en stock";
}

/// <summary>Producto completo para el formulario de edición.</summary>
public sealed class Producto
{
    public string Uuid { get; set; } = "";
    public string CategoriaUuid { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Marca { get; set; } = "";
    public string? Descripcion { get; set; }
    /// <summary>permanente: siempre visible en la web (sin stock dice "Agotado"); temporada: solo con stock.</summary>
    public string Ciclo { get; set; } = "permanente";
    public string Estado { get; set; } = "activo";
    public bool Destacado { get; set; }

    public List<Variante> Variantes { get; set; } = [];
    public List<FotoProducto> Fotos { get; set; } = [];
}

/// <summary>Lo que se vende y se cuenta. Todo producto tiene al menos una (tipo "unica").</summary>
public sealed class Variante
{
    public string Uuid { get; set; } = "";
    public string ProductoUuid { get; set; } = "";
    public string? Sku { get; set; }
    public string? CodigoBarras { get; set; }
    public string Tipo { get; set; } = "unica";
    public string Nombre { get; set; } = "Única";
    public string? ColorHex { get; set; }
    public long Precio { get; set; }
    public long? PrecioOferta { get; set; }
    public long? Costo { get; set; }
    public long Stock { get; set; }
    public long Orden { get; set; }
    public bool Activa { get; set; } = true;
    /// <summary>Tiene movimientos de inventario: ya no se puede quitar, solo desactivar.</summary>
    public bool ConHistorial { get; set; }
    /// <summary>Solo al crear la opción: unidades que ya hay en tienda (entra como ajuste "Stock inicial").</summary>
    public long StockInicial { get; set; }

    /// <summary>Precio que se cobra hoy (la oferta si hay).</summary>
    public long PrecioVenta => PrecioOferta ?? Precio;
}

public sealed class FotoProducto
{
    public string Uuid { get; set; } = "";
    public string ProductoUuid { get; set; } = "";
    public string Archivo { get; set; } = "";
    public long Orden { get; set; }
    public bool Principal => Orden == 0;
}

/// <summary>Una opción a la venta tal como la ve la caja (variante + datos de su producto).</summary>
public sealed class ArticuloCaja
{
    public string VarianteUuid { get; set; } = "";
    public string ProductoUuid { get; set; } = "";
    public string Producto { get; set; } = "";
    public string Marca { get; set; } = "";
    public string Tipo { get; set; } = "unica";
    public string Variante { get; set; } = "";
    public string? ColorHex { get; set; }
    public string? Sku { get; set; }
    public string? CodigoBarras { get; set; }
    public string CategoriaUuid { get; set; } = "";
    public string Categoria { get; set; } = "";
    public long Precio { get; set; }
    public long? PrecioOferta { get; set; }
    public long Stock { get; set; }
    public string? Foto { get; set; }

    public long PrecioVenta => PrecioOferta ?? Precio;
    public bool EnOferta => PrecioOferta is not null;
    public bool Agotado => Stock <= 0;
    public string Nombre => Tipo == "unica" ? Producto : $"{Producto} · {Variante}";
    public string PrecioTexto => Dinero.Formatear(PrecioVenta);
    public string PrecioAntes => PrecioOferta is null ? "" : Dinero.Formatear(Precio);
    public string StockTexto => Stock <= 0 ? "Agotado" : Stock == 1 ? "Queda 1" : $"{Stock} disponibles";
    public string EstadoStock => Stock <= 0 ? "agotado" : Stock <= Inventario.StockBajo ? "bajo" : "activo";
}

public static class Inventario
{
    /// <summary>Por agotarse = con stock entre 1 y este valor (mismo criterio que la tienda en línea).</summary>
    public const int StockBajo = 3;
}
