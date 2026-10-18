<?php

/**
 * ProductoEntity — tabla `productos`
 *
 * El producto es la ficha (nombre, marca, descripción). Lo que se vende
 * y se cuenta son sus variantes; por eso precio y stock se calculan
 * desde ellas y no hay columnas `precio` ni `stock` en el producto.
 */
class ProductoEntity extends BaseEntity
{
    // ─────────────────────────────────────────────
    // COLUMNAS
    // ─────────────────────────────────────────────
    public ?string $uuid           = null;   // UUID: lo genera quien crea el producto (web)
    public ?string $categoria_uuid = null;
    public ?string $slug           = null;   // /producto/{slug}
    public ?string $nombre         = null;
    public ?string $marca          = null;
    public ?string $descripcion    = null;
    public int     $destacado      = 0;      // aparece en "Lo más top"
    public ?string $created_at     = null;
    public ?string $updated_at     = null;

    // permanente: skincare, aromas, maquillaje — se reabastece.
    // temporada:  ropa, zapatos — al agotarse sale sola de la tienda.
    public string $ciclo = 'permanente';

    // activo | archivado. Archivar reemplaza a borrar: los pedidos viejos
    // siguen apuntando al producto.
    public string $estado = 'activo';

    // ─────────────────────────────────────────────
    // RELACIONES — las llena ProductoModel, no vienen en la fila
    // ─────────────────────────────────────────────
    /** @var VarianteEntity[] */
    public array $variantes = [];
    /** @var ProductoImagenEntity[] ordenadas; la primera es la principal */
    public array $imagenes = [];
    public ?CategoriaEntity $categoria = null;

    // Las relaciones no se asignan desde el array de datos: evita que una
    // fila traiga "variantes", "imagenes" o "categoria" en crudo.
    protected function fill(array $data): void
    {
        unset($data['variantes'], $data['imagenes'], $data['categoria']);
        parent::fill($data);
    }

    // ─────────────────────────────────────────────
    // EXISTENCIAS
    // ─────────────────────────────────────────────

    /** @return VarianteEntity[] solo las variantes que están a la venta */
    public function variantesActivas(): array
    {
        return array_values(array_filter($this->variantes, fn(VarianteEntity $v) => $v->activa === 1));
    }

    public function stockTotal(): int
    {
        return array_sum(array_map(fn(VarianteEntity $v) => max(0, $v->stock), $this->variantesActivas()));
    }

    public function estaAgotado(): bool
    {
        return $this->stockTotal() === 0;
    }

    public function esPermanente(): bool
    {
        return $this->ciclo === 'permanente';
    }

    /**
     * REGLA DE NEGOCIO — ¿este producto se muestra en la tienda?
     *
     *   - Archivado o sin variantes a la venta → nunca.
     *   - Permanente → siempre; si no hay stock se muestra como "Agotado"
     *     para no perder la consulta del cliente.
     *   - Temporada → solo mientras tenga stock. Cuando la clienta vuelve
     *     a cargarle cantidad en el POS, reaparece solo.
     */
    public function esVisibleEnTienda(): bool
    {
        if ($this->estado !== 'activo' || empty($this->variantesActivas())) {
            return false;
        }

        return $this->esPermanente() || !$this->estaAgotado();
    }

    // ─────────────────────────────────────────────
    // PRECIOS
    // ─────────────────────────────────────────────

    /**
     * Variante que representa el precio en la tarjeta: la más barata entre
     * las que tienen stock (o entre todas, si el producto está agotado).
     */
    public function varianteReferencia(): ?VarianteEntity
    {
        $activas   = $this->variantesActivas();
        $conStock  = array_filter($activas, fn(VarianteEntity $v) => $v->hayStock());
        $candidatas = !empty($conStock) ? $conStock : $activas;

        $referencia = null;
        foreach ($candidatas as $v) {
            if ($referencia === null || $v->precioFinal() < $referencia->precioFinal()) {
                $referencia = $v;
            }
        }

        return $referencia;
    }

    public function precioDesde(): float
    {
        $v = $this->varianteReferencia();
        return $v ? $v->precioFinal() : 0.0;
    }

    // Precio tachado (el de antes de la oferta), o null si no hay oferta
    public function precioAntes(): ?float
    {
        $v = $this->varianteReferencia();
        return ($v && $v->tieneOferta()) ? $v->precio : null;
    }

    public function tieneOferta(): bool
    {
        return $this->precioAntes() !== null;
    }

    // true si las variantes cuestan distinto → la tarjeta dice "Desde L ..."
    public function tienePreciosDistintos(): bool
    {
        $precios = array_unique(array_map(
            fn(VarianteEntity $v) => number_format($v->precioFinal(), 2, '.', ''),
            $this->variantesActivas()
        ));

        return count($precios) > 1;
    }

    // ─────────────────────────────────────────────
    // VARIANTES
    // ─────────────────────────────────────────────

    // ¿El cliente tiene que elegir tono / talla / color?
    public function requiereEleccion(): bool
    {
        $activas = $this->variantesActivas();
        return count($activas) > 1 || (count($activas) === 1 && !$activas[0]->esUnica());
    }

    // Texto del selector: "Elige tu tono", "Elige tu talla"...
    public function tipoVariante(): string
    {
        $activas = $this->variantesActivas();
        return $activas[0]->tipo ?? 'unica';
    }

    // ─────────────────────────────────────────────
    // ETIQUETA DE LA TARJETA
    // Una sola, por prioridad: lo que más le importa al cliente primero.
    // Retorna ['texto' => ..., 'tipo' => ...] o null.
    // ─────────────────────────────────────────────
    public function etiqueta(): ?array
    {
        if ($this->estaAgotado()) {
            return ['texto' => 'Agotado', 'tipo' => 'agotado'];
        }
        if ($this->tieneOferta()) {
            return ['texto' => 'Oferta', 'tipo' => 'oferta'];
        }
        if ($this->stockTotal() <= STOCK_BAJO) {
            return ['texto' => 'Últimas unidades', 'tipo' => 'ultimas'];
        }
        if ($this->esNuevo()) {
            return ['texto' => 'Nuevo', 'tipo' => 'nuevo'];
        }

        return null;
    }

    public function esNuevo(): bool
    {
        if (empty($this->created_at)) {
            return false;
        }

        $alta = strtotime($this->created_at);
        return $alta !== false && $alta >= strtotime('-' . DIAS_NUEVO . ' days');
    }

    // ─────────────────────────────────────────────
    // URLS
    // ─────────────────────────────────────────────
    public function url(): string
    {
        return url('producto/' . rawurlencode((string) $this->slug));
    }

    /**
     * URL de la foto principal, o null si el producto aún no tiene.
     * Con null la vista pinta un bloque del color de la categoría, de modo
     * que la tienda se ve completa aunque falten fotos.
     */
    public function getImageUrl(): ?string
    {
        foreach ($this->imagenes as $imagen) {
            if ($url = $imagen->url()) {
                return $url;
            }
        }
        return null;
    }

    // ─────────────────────────────────────────────
    // VALIDACIÓN (al guardar desde el panel)
    // ─────────────────────────────────────────────

    /** @return string[] mensajes de error; vacío = válido */
    public function validar(): array
    {
        $errores = [];

        if (trim((string) $this->nombre) === '' || mb_strlen((string) $this->nombre) > 120) {
            $errores[] = 'El nombre es obligatorio (máximo 120 caracteres).';
        }
        if (trim((string) $this->marca) === '' || mb_strlen((string) $this->marca) > 80) {
            $errores[] = 'La marca es obligatoria (máximo 80 caracteres).';
        }
        if (!BaseModel::esUuid($this->categoria_uuid)) {
            $errores[] = 'Elige una categoría.';
        }
        if (!in_array($this->ciclo, ['permanente', 'temporada'], true)) {
            $errores[] = 'Ciclo no válido.';
        }
        if (!in_array($this->estado, ['activo', 'archivado'], true)) {
            $errores[] = 'Estado no válido.';
        }
        if (mb_strlen((string) $this->descripcion) > 2000) {
            $errores[] = 'La descripción admite hasta 2000 caracteres.';
        }

        $activas = array_filter($this->variantes, fn(VarianteEntity $v) => $v->activa === 1);
        if (empty($activas)) {
            $errores[] = 'El producto necesita al menos una variante activa con precio.';
        }
        foreach ($this->variantes as $i => $v) {
            foreach ($v->validar() as $error) {
                $errores[] = 'Variante ' . ($i + 1) . ': ' . $error;
            }
        }

        // SKU repetido dentro del mismo producto
        $skus = array_filter(array_map(fn(VarianteEntity $v) => $v->sku, $this->variantes));
        if (count($skus) !== count(array_unique($skus))) {
            $errores[] = 'Hay códigos SKU repetidos.';
        }

        return $errores;
    }

    // Color de respaldo cuando no hay foto: el de su categoría
    public function colorFondo(): string
    {
        return $this->categoria ? $this->categoria->colorSeguro() : '#F6D5DC';
    }
}
