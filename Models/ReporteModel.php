<?php

/**
 * ReporteModel — Consultas de ventas y entradas para reportes.
 *
 * Solo lectura. Las ventas y entradas las registra el POS y llegan por
 * sincronización; aquí únicamente se agregan y se agrupan.
 *
 * Reglas comunes:
 *   - Rango de fechas [desde, hasta): `hasta` es exclusivo.
 *   - Los totales de dinero solo cuentan ventas COMPLETADAS; las anuladas
 *     se informan aparte (cuántas y por cuánto) para que no desaparezcan.
 */
class ReporteModel extends BaseModel
{
    private static function rango(DateTimeImmutable $desde, DateTimeImmutable $hasta): array
    {
        return [$desde->format('Y-m-d H:i:s'), $hasta->format('Y-m-d H:i:s')];
    }

    // ─────────────────────────────────────────────
    // VENTAS
    // ─────────────────────────────────────────────

    /**
     * Totales del período.
     * @return array{ventas:int, unidades:int, subtotal:float, descuento:float, total:float,
     *               costo:float, ganancia:float, ticket:float, anuladas:int, total_anuladas:float}
     */
    public function resumenVentas(DateTimeImmutable $desde, DateTimeImmutable $hasta): array
    {
        $r = self::rango($desde, $hasta);

        $v = $this->fila(
            "SELECT SUM(estado = 'completada')                        AS ventas,
                    COALESCE(SUM(IF(estado = 'completada', subtotal, 0)), 0)  AS subtotal,
                    COALESCE(SUM(IF(estado = 'completada', descuento, 0)), 0) AS descuento,
                    COALESCE(SUM(IF(estado = 'completada', total, 0)), 0)     AS total,
                    SUM(estado = 'anulada')                           AS anuladas,
                    COALESCE(SUM(IF(estado = 'anulada', total, 0)), 0)        AS total_anuladas
               FROM ventas
              WHERE fecha >= ? AND fecha < ?",
            $r
        );

        // Costo de lo vendido. El descuento de la venta se reparte entre sus
        // líneas al calcular la ganancia (total − costo), no aquí.
        $d = $this->fila(
            "SELECT COALESCE(SUM(d.cantidad), 0)                          AS unidades,
                    COALESCE(SUM(d.cantidad * COALESCE(d.costo_unitario, 0)), 0) AS costo
               FROM venta_detalle d
               JOIN ventas v ON v.uuid = d.venta_uuid
              WHERE v.estado = 'completada' AND v.fecha >= ? AND v.fecha < ?",
            $r
        );

        $ventas = (int) ($v['ventas'] ?? 0);
        $total  = (float) $v['total'];
        $costo  = (float) $d['costo'];

        return [
            'ventas'         => $ventas,
            'unidades'       => (int) $d['unidades'],
            'subtotal'       => (float) $v['subtotal'],
            'descuento'      => (float) $v['descuento'],
            'total'          => $total,
            'costo'          => $costo,
            'ganancia'       => $total - $costo,
            'ticket'         => $ventas > 0 ? $total / $ventas : 0.0,
            'anuladas'       => (int) ($v['anuladas'] ?? 0),
            'total_anuladas' => (float) $v['total_anuladas'],
        ];
    }

    /** @return array<string, array{total: float, ventas: int}> por cubeta del período */
    public function serieVentas(PeriodoReporte $p): array
    {
        $grupo = $p->sqlGrupo('fecha');
        $filas = $this->filas(
            "SELECT $grupo AS clave, SUM(total) AS total, COUNT(*) AS ventas
               FROM ventas
              WHERE estado = 'completada' AND fecha >= ? AND fecha < ?
              GROUP BY clave",
            self::rango($p->desde, $p->hasta)
        );

        $serie = [];
        foreach ($filas as $f) {
            $serie[$f['clave']] = ['total' => (float) $f['total'], 'ventas' => (int) $f['ventas']];
        }
        return $serie;
    }

    /**
     * Productos más vendidos. Se agrupa por el producto actual de la
     * variante; si el producto ya no existe se usa el nombre guardado en la venta.
     * @return array<int, array{producto:string, marca:?string, unidades:int, total:float, costo:float}>
     */
    public function topProductos(DateTimeImmutable $desde, DateTimeImmutable $hasta, ?int $limite = 10): array
    {
        return array_map(fn(array $f) => [
            'producto' => (string) $f['producto'],
            'marca'    => $f['marca'],
            'unidades' => (int) $f['unidades'],
            'total'    => (float) $f['total'],
            'costo'    => (float) $f['costo'],
        ], $this->filas(
            "SELECT COALESCE(p.nombre, MAX(d.producto_nombre)) AS producto, p.marca,
                    SUM(d.cantidad) AS unidades, SUM(d.subtotal) AS total,
                    SUM(d.cantidad * COALESCE(d.costo_unitario, 0)) AS costo
               FROM venta_detalle d
               JOIN ventas v              ON v.uuid = d.venta_uuid
               LEFT JOIN producto_variantes pv ON pv.uuid = d.variante_uuid
               LEFT JOIN productos p      ON p.uuid = pv.producto_uuid
              WHERE v.estado = 'completada' AND v.fecha >= ? AND v.fecha < ?
              GROUP BY COALESCE(p.uuid, d.producto_nombre), p.nombre, p.marca
              ORDER BY unidades DESC, total DESC" . ($limite !== null ? ' LIMIT ' . max(1, $limite) : ''),
            self::rango($desde, $hasta)
        ));
    }

    /** @return array<int, array{categoria:string, color:?string, unidades:int, total:float}> */
    public function porCategoria(DateTimeImmutable $desde, DateTimeImmutable $hasta): array
    {
        return array_map(fn(array $f) => [
            'categoria' => (string) $f['categoria'],
            'color'     => $f['color'],
            'unidades'  => (int) $f['unidades'],
            'total'     => (float) $f['total'],
        ], $this->filas(
            "SELECT COALESCE(c.nombre, 'Sin categoría') AS categoria, c.color,
                    SUM(d.cantidad) AS unidades, SUM(d.subtotal) AS total
               FROM venta_detalle d
               JOIN ventas v              ON v.uuid = d.venta_uuid
               LEFT JOIN producto_variantes pv ON pv.uuid = d.variante_uuid
               LEFT JOIN productos p      ON p.uuid = pv.producto_uuid
               LEFT JOIN categorias c     ON c.uuid = p.categoria_uuid
              WHERE v.estado = 'completada' AND v.fecha >= ? AND v.fecha < ?
              GROUP BY c.uuid, c.nombre, c.color
              ORDER BY total DESC",
            self::rango($desde, $hasta)
        ));
    }

    /** @return array<int, array{metodo:string, ventas:int, total:float}> */
    public function porMetodoPago(DateTimeImmutable $desde, DateTimeImmutable $hasta): array
    {
        return array_map(fn(array $f) => [
            'metodo' => (string) $f['metodo_pago'],
            'ventas' => (int) $f['ventas'],
            'total'  => (float) $f['total'],
        ], $this->filas(
            "SELECT metodo_pago, COUNT(*) AS ventas, SUM(total) AS total
               FROM ventas
              WHERE estado = 'completada' AND fecha >= ? AND fecha < ?
              GROUP BY metodo_pago
              ORDER BY total DESC",
            self::rango($desde, $hasta)
        ));
    }

    /**
     * Listado de ventas (completadas y anuladas), la más reciente primero.
     * @return array<int, array<string, mixed>>
     */
    public function ventas(DateTimeImmutable $desde, DateTimeImmutable $hasta, ?int $limite = null): array
    {
        return $this->filas(
            "SELECT v.numero, v.fecha, v.metodo_pago, v.subtotal, v.descuento, v.total, v.estado,
                    u.nombre AS vendedor,
                    (SELECT COALESCE(SUM(d.cantidad), 0) FROM venta_detalle d WHERE d.venta_uuid = v.uuid) AS unidades
               FROM ventas v
               LEFT JOIN usuarios u ON u.uuid = v.usuario_uuid
              WHERE v.fecha >= ? AND v.fecha < ?
              ORDER BY v.fecha DESC" . ($limite !== null ? ' LIMIT ' . max(1, $limite) : ''),
            self::rango($desde, $hasta)
        );
    }

    /** Cada línea vendida (hoja "Detalle" del Excel). */
    public function ventasDetalle(DateTimeImmutable $desde, DateTimeImmutable $hasta): array
    {
        return $this->filas(
            "SELECT v.numero, v.fecha, v.estado, d.producto_nombre, d.variante_nombre, pv.sku,
                    d.cantidad, d.precio_unitario, d.costo_unitario, d.subtotal
               FROM venta_detalle d
               JOIN ventas v ON v.uuid = d.venta_uuid
               LEFT JOIN producto_variantes pv ON pv.uuid = d.variante_uuid
              WHERE v.fecha >= ? AND v.fecha < ?
              ORDER BY v.fecha DESC, d.id",
            self::rango($desde, $hasta)
        );
    }

    // ─────────────────────────────────────────────
    // ENTRADAS (mercadería que ingresa)
    // ─────────────────────────────────────────────

    /** @return array{entradas:int, unidades:int, costo:float} */
    public function resumenEntradas(DateTimeImmutable $desde, DateTimeImmutable $hasta): array
    {
        $f = $this->fila(
            'SELECT COUNT(DISTINCT e.uuid) AS entradas,
                    COALESCE(SUM(d.cantidad), 0) AS unidades,
                    COALESCE(SUM(d.subtotal), 0) AS costo
               FROM entradas e
               LEFT JOIN entrada_detalle d ON d.entrada_uuid = e.uuid
              WHERE e.fecha >= ? AND e.fecha < ?',
            self::rango($desde, $hasta)
        );
        return ['entradas' => (int) $f['entradas'], 'unidades' => (int) $f['unidades'], 'costo' => (float) $f['costo']];
    }

    /** @return array<string, array{costo: float, unidades: int}> por cubeta */
    public function serieEntradas(PeriodoReporte $p): array
    {
        $grupo = $p->sqlGrupo('e.fecha');
        $filas = $this->filas(
            "SELECT $grupo AS clave, SUM(d.subtotal) AS costo, SUM(d.cantidad) AS unidades
               FROM entradas e
               JOIN entrada_detalle d ON d.entrada_uuid = e.uuid
              WHERE e.fecha >= ? AND e.fecha < ?
              GROUP BY clave",
            self::rango($p->desde, $p->hasta)
        );

        $serie = [];
        foreach ($filas as $f) {
            $serie[$f['clave']] = ['costo' => (float) $f['costo'], 'unidades' => (int) $f['unidades']];
        }
        return $serie;
    }

    public function entradas(DateTimeImmutable $desde, DateTimeImmutable $hasta): array
    {
        return $this->filas(
            'SELECT e.numero, e.fecha, e.proveedor, e.nota, e.total_costo, u.nombre AS registro,
                    (SELECT COALESCE(SUM(d.cantidad), 0) FROM entrada_detalle d WHERE d.entrada_uuid = e.uuid) AS unidades,
                    (SELECT COUNT(*) FROM entrada_detalle d WHERE d.entrada_uuid = e.uuid) AS lineas
               FROM entradas e
               LEFT JOIN usuarios u ON u.uuid = e.usuario_uuid
              WHERE e.fecha >= ? AND e.fecha < ?
              ORDER BY e.fecha DESC',
            self::rango($desde, $hasta)
        );
    }

    /** Cada línea ingresada (hoja "Entradas" del Excel). */
    public function entradasDetalle(DateTimeImmutable $desde, DateTimeImmutable $hasta): array
    {
        return $this->filas(
            'SELECT e.numero, e.fecha, e.proveedor, d.producto_nombre, d.variante_nombre, pv.sku,
                    d.cantidad, d.costo_unitario, d.subtotal
               FROM entrada_detalle d
               JOIN entradas e ON e.uuid = d.entrada_uuid
               LEFT JOIN producto_variantes pv ON pv.uuid = d.variante_uuid
              WHERE e.fecha >= ? AND e.fecha < ?
              ORDER BY e.fecha DESC, d.id',
            self::rango($desde, $hasta)
        );
    }
}
