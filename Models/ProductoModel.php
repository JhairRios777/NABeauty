<?php

/**
 * ProductoModel — tabla `productos`.
 *
 * Dos grupos de métodos:
 *   TIENDA → solo devuelven productos VISIBLES (regla en ProductoEntity), de
 *            modo que ningún controlador puede mostrar por descuido un
 *            producto archivado o uno de temporada agotado.
 *   PANEL  → ven todo (archivados, variantes inactivas) y guardan.
 */
class ProductoModel extends BaseModel
{
    // Formas de ordenar que acepta el catálogo. Lista cerrada: el valor que
    // llega por URL se compara contra estas claves, nunca se usa tal cual.
    public const ORDENES = [
        'nuevos'      => 'Más nuevos',
        'precio_asc'  => 'Precio: menor a mayor',
        'precio_desc' => 'Precio: mayor a menor',
        'nombre'      => 'Nombre (A–Z)',
    ];

    private CategoriaModel $categoriaModel;

    /** @var ProductoEntity[]|null caché por petición */
    private ?array $visibles = null;

    private const COLUMNAS = 'p.uuid, p.categoria_uuid, p.slug, p.nombre, p.marca, p.descripcion,
                              p.ciclo, p.estado, p.destacado, p.created_at, p.updated_at';

    public function __construct(?CategoriaModel $categoriaModel = null)
    {
        parent::__construct();
        $this->categoriaModel = $categoriaModel ?? new CategoriaModel();
    }

    // ─────────────────────────────────────────────
    // CARGA DE LA TIENDA
    //
    // Tres consultas en total (productos, variantes, fotos), no una por
    // producto: evita el problema N+1. El filtro fino (temporada agotada,
    // sin variantes) lo decide ProductoEntity::esVisibleEnTienda(), que es
    // la única fuente de esa regla.
    // ─────────────────────────────────────────────

    /** @return ProductoEntity[] */
    private function cargarVisibles(): array
    {
        if ($this->visibles !== null) {
            return $this->visibles;
        }

        $filas = $this->filas(
            'SELECT ' . self::COLUMNAS . '
               FROM productos p
               JOIN categorias c ON c.uuid = p.categoria_uuid AND c.activa = 1
              WHERE p.estado = \'activo\''
        );

        $productos = $this->hidratar($filas, soloVariantesActivas: true);

        $visibles = [];
        foreach ($productos as $producto) {
            $producto->categoria = $this->categoriaModel->findByUuid((string) $producto->categoria_uuid);
            if ($producto->categoria !== null && $producto->esVisibleEnTienda()) {
                $visibles[] = $producto;
            }
        }

        return $this->visibles = $visibles;
    }

    /**
     * Convierte filas de `productos` en entidades con variantes y fotos.
     * @param  array<int, array<string, mixed>> $filas
     * @return ProductoEntity[] en el mismo orden de $filas
     */
    private function hidratar(array $filas, bool $soloVariantesActivas): array
    {
        if (empty($filas)) {
            return [];
        }

        $productos = [];
        foreach ($filas as $fila) {
            $p = ProductoEntity::fromArray($fila);
            $productos[$p->uuid] = $p;
        }

        $uuids = array_keys($productos);
        $marcas = implode(',', array_fill(0, count($uuids), '?'));

        $variantes = $this->filas(
            'SELECT uuid, producto_uuid, sku, tipo, nombre, color_hex, precio, precio_oferta, stock, orden, activa
               FROM producto_variantes
              WHERE producto_uuid IN (' . $marcas . ')' . ($soloVariantesActivas ? ' AND activa = 1' : '') . '
              ORDER BY orden, nombre',
            $uuids
        );
        foreach ($variantes as $f) {
            $productos[$f['producto_uuid']]->variantes[] = VarianteEntity::fromArray($f);
        }

        foreach ((new ProductoImagenModel())->porProductos($uuids) as $productoUuid => $imagenes) {
            $productos[$productoUuid]->imagenes = $imagenes;
        }

        return array_values($productos);
    }

    // ─────────────────────────────────────────────
    // CONSULTAS PUNTUALES
    // ─────────────────────────────────────────────

    public function findBySlug(string $slug): ?ProductoEntity
    {
        foreach ($this->cargarVisibles() as $producto) {
            if ($producto->slug === $slug) {
                return $producto;
            }
        }

        return null;
    }

    /**
     * Productos marcados como destacados y con stock ("Lo más top").
     * @return ProductoEntity[]
     */
    public function findDestacados(int $limite = 4): array
    {
        $destacados = array_filter(
            $this->cargarVisibles(),
            fn(ProductoEntity $p) => $p->destacado === 1 && !$p->estaAgotado()
        );

        return array_slice(array_values($destacados), 0, $limite);
    }

    /**
     * Últimos productos dados de alta que tienen stock.
     * @return ProductoEntity[]
     */
    public function findNuevos(int $limite = 4): array
    {
        $conStock = array_filter($this->cargarVisibles(), fn(ProductoEntity $p) => !$p->estaAgotado());

        return array_slice($this->ordenar(array_values($conStock), 'nuevos'), 0, $limite);
    }

    /**
     * Otros productos de la misma categoría, con stock, sin incluir el actual.
     * @return ProductoEntity[]
     */
    public function findRelacionados(ProductoEntity $producto, int $limite = 4): array
    {
        $relacionados = array_filter(
            $this->cargarVisibles(),
            fn(ProductoEntity $p) => $p->uuid !== $producto->uuid
                && $p->categoria_uuid === $producto->categoria_uuid
                && !$p->estaAgotado()
        );

        return array_slice(array_values($relacionados), 0, $limite);
    }

    /**
     * Marcas que tienen productos visibles (opcionalmente dentro de una
     * categoría). Alimenta el filtro del catálogo.
     * @return string[]
     */
    public function marcasDisponibles(?string $categoriaUuid = null): array
    {
        $marcas = [];
        foreach ($this->cargarVisibles() as $producto) {
            if ($categoriaUuid === null || $producto->categoria_uuid === $categoriaUuid) {
                $marcas[(string) $producto->marca] = true;
            }
        }

        $marcas = array_keys($marcas);
        sort($marcas, SORT_NATURAL | SORT_FLAG_CASE);

        return $marcas;
    }

    // ─────────────────────────────────────────────
    // BÚSQUEDA DEL CATÁLOGO — filtros + orden + paginación
    //
    // $filtros (todos opcionales, ya validados por el controlador):
    //   categoria_uuid  string
    //   marcas          string[]
    //   solo_disponible bool   oculta los agotados
    //   solo_oferta     bool
    //   q               string texto en nombre o marca
    //
    // Retorna: ['items' => ProductoEntity[], 'total' => int,
    //           'pagina' => int, 'paginas' => int]
    // ─────────────────────────────────────────────
    public function buscar(array $filtros = [], string $orden = 'nuevos', int $pagina = 1): array
    {
        $categoriaUuid  = $filtros['categoria_uuid'] ?? null;
        $marcas         = $filtros['marcas'] ?? [];
        $soloDisponible = !empty($filtros['solo_disponible']);
        $soloOferta     = !empty($filtros['solo_oferta']);
        $q              = mb_strtolower(trim((string) ($filtros['q'] ?? '')), 'UTF-8');

        $resultado = array_filter(
            $this->cargarVisibles(),
            function (ProductoEntity $p) use ($categoriaUuid, $marcas, $soloDisponible, $soloOferta, $q): bool {
                if ($categoriaUuid !== null && $p->categoria_uuid !== $categoriaUuid) {
                    return false;
                }
                if (!empty($marcas) && !in_array($p->marca, $marcas, true)) {
                    return false;
                }
                if ($soloDisponible && $p->estaAgotado()) {
                    return false;
                }
                if ($soloOferta && !$p->tieneOferta()) {
                    return false;
                }
                if ($q !== '') {
                    $texto = mb_strtolower($p->nombre . ' ' . $p->marca, 'UTF-8');
                    if (!str_contains($texto, $q)) {
                        return false;
                    }
                }

                return true;
            }
        );

        $resultado = $this->ordenar(array_values($resultado), $orden);

        // Paginación: la página pedida se ajusta al rango real
        $total   = count($resultado);
        $paginas = max(1, (int) ceil($total / CATALOGO_POR_PAGINA));
        $pagina  = min(max(1, $pagina), $paginas);

        return [
            'items'   => array_slice($resultado, ($pagina - 1) * CATALOGO_POR_PAGINA, CATALOGO_POR_PAGINA),
            'total'   => $total,
            'pagina'  => $pagina,
            'paginas' => $paginas,
        ];
    }

    // ─────────────────────────────────────────────
    // ORDEN
    // En todos los criterios los agotados van al final: lo que se puede
    // comprar siempre se ve primero.
    // ─────────────────────────────────────────────

    /**
     * @param  ProductoEntity[] $productos
     * @return ProductoEntity[]
     */
    private function ordenar(array $productos, string $orden): array
    {
        $comparar = match ($orden) {
            'precio_asc'  => fn(ProductoEntity $a, ProductoEntity $b) => $a->precioDesde() <=> $b->precioDesde(),
            'precio_desc' => fn(ProductoEntity $a, ProductoEntity $b) => $b->precioDesde() <=> $a->precioDesde(),
            'nombre'      => fn(ProductoEntity $a, ProductoEntity $b) => strcasecmp((string) $a->nombre, (string) $b->nombre),
            default       => fn(ProductoEntity $a, ProductoEntity $b) => strcmp((string) $b->created_at, (string) $a->created_at),
        };

        usort($productos, function (ProductoEntity $a, ProductoEntity $b) use ($comparar): int {
            return ($a->estaAgotado() <=> $b->estaAgotado()) ?: $comparar($a, $b);
        });

        return $productos;
    }

    // ═════════════════════════════════════════════
    // PANEL ADMINISTRATIVO
    // ═════════════════════════════════════════════

    public const POR_PAGINA_ADMIN = 20;

    /**
     * Listado del panel con filtros y paginación en SQL.
     * $filtros: q, categoria_uuid, estado (activo|archivado), stock (agotado|bajo)
     * @return array{items: ProductoEntity[], total: int, pagina: int, paginas: int}
     */
    public function listarAdmin(array $filtros, int $pagina = 1): array
    {
        $where  = ['1 = 1'];
        $params = [];

        if (!empty($filtros['q'])) {
            $where[]  = '(p.nombre LIKE ? OR p.marca LIKE ? OR EXISTS (
                            SELECT 1 FROM producto_variantes sv WHERE sv.producto_uuid = p.uuid AND sv.sku LIKE ?))';
            $like     = '%' . addcslashes((string) $filtros['q'], '%_\\') . '%';
            array_push($params, $like, $like, $like);
        }
        if (!empty($filtros['categoria_uuid'])) {
            $where[]  = 'p.categoria_uuid = ?';
            $params[] = $filtros['categoria_uuid'];
        }
        if (in_array($filtros['estado'] ?? '', ['activo', 'archivado'], true)) {
            $where[]  = 'p.estado = ?';
            $params[] = $filtros['estado'];
        }
        // Stock total de las variantes activas
        $stockSql = '(SELECT COALESCE(SUM(GREATEST(v.stock, 0)), 0) FROM producto_variantes v
                       WHERE v.producto_uuid = p.uuid AND v.activa = 1)';
        if (($filtros['stock'] ?? '') === 'agotado') {
            $where[] = $stockSql . ' = 0';
        } elseif (($filtros['stock'] ?? '') === 'bajo') {
            $where[]  = $stockSql . ' BETWEEN 1 AND ?';
            $params[] = STOCK_BAJO;
        }

        $condicion = implode(' AND ', $where);
        $total     = (int) $this->valor('SELECT COUNT(*) FROM productos p WHERE ' . $condicion, $params);
        $paginas   = max(1, (int) ceil($total / self::POR_PAGINA_ADMIN));
        $pagina    = min(max(1, $pagina), $paginas);
        $offset    = ($pagina - 1) * self::POR_PAGINA_ADMIN;

        // LIMIT/OFFSET son enteros calculados aquí, no vienen de la URL
        $filas = $this->filas(
            'SELECT ' . self::COLUMNAS . ' FROM productos p WHERE ' . $condicion . '
              ORDER BY p.estado = \'archivado\', p.updated_at DESC
              LIMIT ' . self::POR_PAGINA_ADMIN . ' OFFSET ' . $offset,
            $params
        );

        $items = $this->hidratar($filas, soloVariantesActivas: false);
        foreach ($items as $p) {
            $p->categoria = $this->categoriaModel->find((string) $p->categoria_uuid);
        }

        return ['items' => $items, 'total' => $total, 'pagina' => $pagina, 'paginas' => $paginas];
    }

    /** Producto completo para editar (cualquier estado, todas sus variantes). */
    public function findAdmin(string $uuid): ?ProductoEntity
    {
        if (!self::esUuid($uuid)) {
            return null;
        }
        $fila = $this->fila('SELECT ' . self::COLUMNAS . ' FROM productos p WHERE p.uuid = ?', [$uuid]);
        if ($fila === null) {
            return null;
        }

        $producto = $this->hidratar([$fila], soloVariantesActivas: false)[0];
        $producto->categoria = $this->categoriaModel->find((string) $producto->categoria_uuid);
        return $producto;
    }

    /**
     * Guarda la ficha y sus variantes en UNA transacción: nunca queda un
     * producto sin variantes ni variantes huérfanas si algo falla a mitad.
     *
     * @throws DomainException si un SKU ya lo usa otro producto
     */
    public function guardar(ProductoEntity $p): ProductoEntity
    {
        return $this->transaccion(function () use ($p): ProductoEntity {
            $datos = [
                $p->categoria_uuid, trim((string) $p->nombre), trim((string) $p->marca),
                $p->descripcion !== null && trim($p->descripcion) !== '' ? trim($p->descripcion) : null,
                $p->ciclo, $p->estado, $p->destacado ? 1 : 0,
            ];

            if ($p->uuid === null) {
                $p->uuid = self::uuid();
                $p->slug = $this->slugUnico(slugify($p->marca . ' ' . $p->nombre));
                $this->ejecutar(
                    'INSERT INTO productos (categoria_uuid, nombre, marca, descripcion, ciclo, estado, destacado, uuid, slug)
                     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)',
                    [...$datos, $p->uuid, $p->slug]
                );
            } else {
                // El slug no cambia al editar: los enlaces ya compartidos siguen vivos
                $this->ejecutar(
                    'UPDATE productos SET categoria_uuid = ?, nombre = ?, marca = ?, descripcion = ?,
                            ciclo = ?, estado = ?, destacado = ?, updated_at = NOW()
                      WHERE uuid = ?',
                    [...$datos, $p->uuid]
                );
            }

            (new VarianteModel())->sincronizar($p->uuid, $p->variantes);
            $this->visibles = null;
            return $p;
        });
    }

    /** Marcas ya usadas (sugerencias del campo marca en el formulario). */
    public function marcasTodas(): array
    {
        return $this->ejecutar('SELECT DISTINCT marca FROM productos ORDER BY marca')->fetchAll(PDO::FETCH_COLUMN);
    }

    /** Cuántos productos activos hay (tarjeta del dashboard). */
    public function contarActivos(): int
    {
        return (int) $this->valor("SELECT COUNT(*) FROM productos WHERE estado = 'activo'");
    }

    private function slugUnico(string $base): string
    {
        $base = $base !== '' ? mb_substr($base, 0, 150) : 'producto';
        $slug = $base;
        for ($i = 2; $this->valor('SELECT 1 FROM productos WHERE slug = ?', [$slug]); $i++) {
            $slug = $base . '-' . $i;
        }
        return $slug;
    }
}
