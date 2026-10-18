<?php

/**
 * ProductoImagenModel — tabla `producto_imagenes`.
 * Solo filas: los archivos los guarda y borra ImagenService.
 * orden 0 = foto principal (la que sale en la tarjeta de la tienda).
 */
class ProductoImagenModel extends BaseModel
{
    public const MAX_POR_PRODUCTO = 8;

    /**
     * Fotos de varios productos en una sola consulta.
     * @param  string[] $productoUuids
     * @return array<string, ProductoImagenEntity[]> por producto, ordenadas
     */
    public function porProductos(array $productoUuids): array
    {
        if (empty($productoUuids)) {
            return [];
        }
        $marcas = implode(',', array_fill(0, count($productoUuids), '?'));
        $filas  = $this->filas(
            'SELECT uuid, producto_uuid, archivo, orden FROM producto_imagenes
              WHERE producto_uuid IN (' . $marcas . ') ORDER BY orden, created_at',
            array_values($productoUuids)
        );

        $resultado = [];
        foreach ($filas as $f) {
            $resultado[$f['producto_uuid']][] = ProductoImagenEntity::fromArray($f);
        }
        return $resultado;
    }

    public function contar(string $productoUuid): int
    {
        return (int) $this->valor('SELECT COUNT(*) FROM producto_imagenes WHERE producto_uuid = ?', [$productoUuid]);
    }

    /** Agrega la foto al final. */
    public function agregar(string $productoUuid, string $archivo): ProductoImagenEntity
    {
        $orden = (int) $this->valor(
            'SELECT COALESCE(MAX(orden) + 1, 0) FROM producto_imagenes WHERE producto_uuid = ?',
            [$productoUuid]
        );
        $img = ProductoImagenEntity::fromArray([
            'uuid' => self::uuid(), 'producto_uuid' => $productoUuid, 'archivo' => $archivo, 'orden' => $orden,
        ]);
        $this->ejecutar(
            'INSERT INTO producto_imagenes (uuid, producto_uuid, archivo, orden) VALUES (?, ?, ?, ?)',
            [$img->uuid, $img->producto_uuid, $img->archivo, $img->orden]
        );
        return $img;
    }

    /**
     * Borra la fila y devuelve el nombre del archivo para que el servicio
     * lo elimine del disco. El producto_uuid en el WHERE impide borrar una
     * foto de otro producto cambiando el UUID en el formulario.
     */
    public function eliminar(string $productoUuid, string $imagenUuid): ?string
    {
        return $this->transaccion(function () use ($productoUuid, $imagenUuid): ?string {
            $archivo = $this->valor(
                'SELECT archivo FROM producto_imagenes WHERE uuid = ? AND producto_uuid = ?',
                [$imagenUuid, $productoUuid]
            );
            if ($archivo === null) {
                return null;
            }
            $this->ejecutar('DELETE FROM producto_imagenes WHERE uuid = ?', [$imagenUuid]);
            $this->renumerar($productoUuid);
            return (string) $archivo;
        });
    }

    /** Pone la foto en primer lugar; las demás conservan su orden relativo. */
    public function hacerPrincipal(string $productoUuid, string $imagenUuid): bool
    {
        return $this->transaccion(function () use ($productoUuid, $imagenUuid): bool {
            $n = $this->ejecutar(
                'UPDATE producto_imagenes SET orden = -1 WHERE uuid = ? AND producto_uuid = ?',
                [$imagenUuid, $productoUuid]
            )->rowCount();
            $this->renumerar($productoUuid);
            return $n > 0 || $this->valor('SELECT 1 FROM producto_imagenes WHERE uuid = ? AND producto_uuid = ?', [$imagenUuid, $productoUuid]) !== null;
        });
    }

    private function renumerar(string $productoUuid): void
    {
        $uuids = $this->ejecutar(
            'SELECT uuid FROM producto_imagenes WHERE producto_uuid = ? ORDER BY orden, created_at',
            [$productoUuid]
        )->fetchAll(PDO::FETCH_COLUMN);

        foreach ($uuids as $i => $uuid) {
            $this->ejecutar('UPDATE producto_imagenes SET orden = ? WHERE uuid = ?', [$i, $uuid]);
        }
    }
}
