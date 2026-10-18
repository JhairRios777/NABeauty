<?php

/**
 * VarianteModel — tabla `producto_variantes`.
 *
 * El stock NO se escribe aquí: es el acumulado del libro de inventario
 * (entradas y ventas del POS). La web edita nombre, precio, oferta y SKU.
 */
class VarianteModel extends BaseModel
{
    /**
     * Deja las variantes del producto iguales a las del formulario.
     *
     *   - UUID que ya pertenece a ESTE producto → se actualiza.
     *   - Sin UUID (o con uno ajeno/inventado)  → se crea con stock 0.
     *   - Las que ya no vienen:
     *       con historial (ventas, entradas, movimientos) → se desactivan,
     *         para que los reportes viejos sigan apuntando a algo real;
     *       sin historial → se borran.
     *
     * Debe llamarse dentro de la transacción de ProductoModel::guardar().
     *
     * @param VarianteEntity[] $variantes
     * @throws DomainException si un SKU ya lo usa otra variante
     */
    public function sincronizar(string $productoUuid, array $variantes): void
    {
        $existentes = array_flip($this->ejecutar(
            'SELECT uuid FROM producto_variantes WHERE producto_uuid = ?',
            [$productoUuid]
        )->fetchAll(PDO::FETCH_COLUMN));

        $conservadas = [];

        foreach (array_values($variantes) as $orden => $v) {
            $v->producto_uuid = $productoUuid;
            $v->orden         = $orden;

            if ($v->uuid !== null && isset($existentes[$v->uuid])) {
                $this->validarSkuLibre($v->sku, $v->uuid);
                $this->ejecutar(
                    'UPDATE producto_variantes
                        SET sku = ?, tipo = ?, nombre = ?, color_hex = ?, precio = ?, precio_oferta = ?,
                            orden = ?, activa = ?
                      WHERE uuid = ? AND producto_uuid = ?',
                    [$v->sku, $v->tipo, trim((string) $v->nombre), $v->colorSeguro(), $v->precio,
                     $v->precio_oferta, $v->orden, $v->activa, $v->uuid, $productoUuid]
                );
            } else {
                $v->uuid  = self::uuid();
                $v->stock = 0;
                $this->validarSkuLibre($v->sku, $v->uuid);
                $this->ejecutar(
                    'INSERT INTO producto_variantes
                        (uuid, producto_uuid, sku, tipo, nombre, color_hex, precio, precio_oferta, stock, orden, activa)
                     VALUES (?, ?, ?, ?, ?, ?, ?, ?, 0, ?, ?)',
                    [$v->uuid, $productoUuid, $v->sku, $v->tipo, trim((string) $v->nombre), $v->colorSeguro(),
                     $v->precio, $v->precio_oferta, $v->orden, $v->activa]
                );
            }
            $conservadas[$v->uuid] = true;
        }

        foreach (array_keys(array_diff_key($existentes, $conservadas)) as $uuid) {
            if ($this->tieneHistorial($uuid)) {
                $this->ejecutar('UPDATE producto_variantes SET activa = 0 WHERE uuid = ?', [$uuid]);
            } else {
                $this->ejecutar('DELETE FROM producto_variantes WHERE uuid = ?', [$uuid]);
            }
        }
    }

    /**
     * Variantes activas con poco stock (incluye agotadas) de productos activos.
     * @return array<int, array<string, mixed>>
     */
    public function stockBajo(int $limite = 10): array
    {
        return $this->filas(
            "SELECT p.uuid AS producto_uuid, p.nombre AS producto, p.marca, p.ciclo,
                    v.nombre AS variante, v.tipo, v.sku, v.stock
               FROM producto_variantes v
               JOIN productos p ON p.uuid = v.producto_uuid AND p.estado = 'activo'
              WHERE v.activa = 1 AND v.stock <= ?
              ORDER BY v.stock, p.nombre
              LIMIT " . max(1, $limite),
            [STOCK_BAJO]
        );
    }

    private function validarSkuLibre(?string $sku, string $uuidPropio): void
    {
        if ($sku === null) {
            return;
        }
        $otro = $this->valor(
            'SELECT p.nombre FROM producto_variantes v JOIN productos p ON p.uuid = v.producto_uuid
              WHERE v.sku = ? AND v.uuid <> ?',
            [$sku, $uuidPropio]
        );
        if ($otro !== null) {
            throw new DomainException('El SKU «' . $sku . '» ya lo usa «' . $otro . '».');
        }
    }

    private function tieneHistorial(string $uuid): bool
    {
        return (bool) $this->valor(
            'SELECT EXISTS(SELECT 1 FROM inventario_movimientos WHERE variante_uuid = ?)
                 OR EXISTS(SELECT 1 FROM venta_detalle          WHERE variante_uuid = ?)
                 OR EXISTS(SELECT 1 FROM entrada_detalle        WHERE variante_uuid = ?)',
            [$uuid, $uuid, $uuid]
        );
    }
}
