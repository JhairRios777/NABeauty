<?php

/**
 * ProductoService — Alta y edición de productos desde el panel.
 *
 * Convierte el formulario en entidades, valida, guarda (ficha + variantes
 * en una transacción) y procesa las fotos. El controlador solo le pasa
 * $_POST y $_FILES y muestra el resultado.
 */
final class ProductoService
{
    public function __construct(
        private ProductoModel $productos = new ProductoModel(),
        private CategoriaModel $categorias = new CategoriaModel(),
        private ProductoImagenModel $imagenes = new ProductoImagenModel(),
        private ImagenService $archivos = new ImagenService(),
    ) {}

    /**
     * @param ?ProductoEntity $existente null = producto nuevo
     * @return array{producto: ProductoEntity, errores: string[], avisos: string[]}
     *         errores → no se guardó nada; avisos → se guardó, pero alguna foto falló
     */
    public function guardar(array $post, array $files, ?ProductoEntity $existente): array
    {
        $producto = $this->desdeFormulario($post, $existente);
        $errores  = $producto->validar();

        if ($producto->categoria_uuid !== null && $this->categorias->find($producto->categoria_uuid) === null) {
            $errores[] = 'La categoría elegida no existe.';
        }
        if (!empty($errores)) {
            return ['producto' => $producto, 'errores' => $errores, 'avisos' => []];
        }

        try {
            $this->productos->guardar($producto);
        } catch (DomainException $e) {
            return ['producto' => $producto, 'errores' => [$e->getMessage()], 'avisos' => []];
        }

        $avisos = $this->subirFotos((string) $producto->uuid, ImagenService::archivosSubidos($files['fotos'] ?? null));

        return ['producto' => $producto, 'errores' => [], 'avisos' => $avisos];
    }

    /** @return string[] avisos de las fotos que no se pudieron guardar */
    public function subirFotos(string $productoUuid, array $archivos): array
    {
        $avisos = [];
        $libres = ProductoImagenModel::MAX_POR_PRODUCTO - $this->imagenes->contar($productoUuid);

        foreach ($archivos as $i => $archivo) {
            if ($i >= $libres) {
                $avisos[] = 'Solo se permiten ' . ProductoImagenModel::MAX_POR_PRODUCTO . ' fotos por producto; las demás no se subieron.';
                break;
            }
            try {
                $nombre = $this->archivos->guardar($archivo);
                try {
                    $this->imagenes->agregar($productoUuid, $nombre);
                } catch (Throwable $e) {
                    $this->archivos->eliminar($nombre);   // sin fila no debe quedar archivo huérfano
                    throw $e;
                }
            } catch (DomainException $e) {
                $avisos[] = $e->getMessage();
            }
        }
        return $avisos;
    }

    public function eliminarFoto(string $productoUuid, string $imagenUuid): bool
    {
        $archivo = $this->imagenes->eliminar($productoUuid, $imagenUuid);
        if ($archivo === null) {
            return false;
        }
        $this->archivos->eliminar($archivo);
        return true;
    }

    // ─────────────────────────────────────────────
    // FORMULARIO → ENTIDADES
    // Todo valor se normaliza aquí; la validación la hace la entidad.
    // ─────────────────────────────────────────────

    private function desdeFormulario(array $post, ?ProductoEntity $existente): ProductoEntity
    {
        $p = new ProductoEntity();
        if ($existente !== null) {
            $p->uuid       = $existente->uuid;
            $p->slug       = $existente->slug;
            $p->imagenes   = $existente->imagenes;
            $p->created_at = $existente->created_at;
        }

        $p->nombre         = self::texto($post['nombre'] ?? '', 120);
        $p->marca          = self::texto($post['marca'] ?? '', 80);
        $p->descripcion    = self::texto($post['descripcion'] ?? '', 2000, multilinea: true);
        $p->categoria_uuid = is_string($post['categoria_uuid'] ?? null) && BaseModel::esUuid($post['categoria_uuid']) ? $post['categoria_uuid'] : null;
        $p->ciclo          = ($post['ciclo'] ?? '') === 'temporada' ? 'temporada' : 'permanente';
        $p->estado         = ($post['estado'] ?? '') === 'archivado' ? 'archivado' : 'activo';
        $p->destacado      = !empty($post['destacado']) ? 1 : 0;

        // Stock actual por UUID: se muestra de nuevo si el formulario vuelve con errores
        $stockActual = [];
        foreach ($existente?->variantes ?? [] as $v) {
            $stockActual[$v->uuid] = $v->stock;
        }

        $tipo = is_string($post['tipo_variante'] ?? null) && isset(VarianteEntity::TIPOS[$post['tipo_variante']])
            ? $post['tipo_variante'] : 'unica';

        $filas = is_array($post['variantes'] ?? null) ? array_values($post['variantes']) : [];
        if ($tipo === 'unica') {
            $filas = array_slice($filas, 0, 1);   // producto sin opciones: un solo precio
        }

        foreach ($filas as $fila) {
            if (!is_array($fila)) {
                continue;
            }
            $nombre = $tipo === 'unica' ? 'Única' : self::texto($fila['nombre'] ?? '', 60);
            $precio = self::monto($fila['precio'] ?? null);

            // Fila vacía (el botón "agregar" sin llenar): se ignora
            if ($nombre === '' && $precio === null) {
                continue;
            }

            $v = new VarianteEntity();
            $v->uuid          = is_string($fila['uuid'] ?? null) && BaseModel::esUuid($fila['uuid']) ? $fila['uuid'] : null;
            $v->tipo          = $tipo;
            $v->nombre        = $nombre;
            $v->precio        = $precio ?? 0.0;
            $v->precio_oferta = self::monto($fila['precio_oferta'] ?? null);
            $v->sku           = ($sku = strtoupper(self::texto($fila['sku'] ?? '', 40))) !== '' ? $sku : null;
            $v->color_hex     = in_array($tipo, ['tono', 'color'], true) && is_string($fila['color_hex'] ?? null)
                                && preg_match('/^#[0-9A-Fa-f]{6}$/', $fila['color_hex']) ? strtoupper($fila['color_hex']) : null;
            $v->activa        = $tipo === 'unica' || !empty($fila['activa']) ? 1 : 0;
            $v->stock         = $stockActual[$v->uuid] ?? 0;
            $p->variantes[]   = $v;
        }

        return $p;
    }

    private static function texto(mixed $valor, int $max, bool $multilinea = false): string
    {
        if (!is_string($valor)) {
            return '';
        }
        $valor = $multilinea
            ? preg_replace("/\r\n?/", "\n", $valor)
            : preg_replace('/\s+/u', ' ', $valor);
        // Fuera caracteres de control (salvo el salto de línea)
        $valor = preg_replace('/[\x00-\x09\x0B-\x1F\x7F]/u', '', (string) $valor);
        return mb_substr(trim((string) $valor), 0, $max, 'UTF-8');
    }

    /** "1,250.50" / "L 450" / "450.5" → float; vacío o inválido → null */
    private static function monto(mixed $valor): ?float
    {
        if (!is_string($valor) && !is_numeric($valor)) {
            return null;
        }
        $limpio = str_replace([',', ' ', 'L', 'l'], '', (string) $valor);
        if ($limpio === '' || !is_numeric($limpio)) {
            return null;
        }
        return round((float) $limpio, 2);
    }
}
