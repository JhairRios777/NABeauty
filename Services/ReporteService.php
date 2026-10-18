<?php

/**
 * ReporteService — Arma el reporte de un período.
 *
 * Una sola fuente para la pantalla, el Excel y el PDF: los tres muestran
 * exactamente los mismos números porque salen de este mismo arreglo.
 */
final class ReporteService
{
    public const METODOS_PAGO = [
        'efectivo'      => 'Efectivo',
        'tarjeta'       => 'Tarjeta',
        'transferencia' => 'Transferencia',
        'otro'          => 'Otro',
    ];

    public function __construct(private ReporteModel $modelo = new ReporteModel()) {}

    /**
     * @param bool $completo true = incluye todas las ventas y líneas (exportación);
     *                       false = lista corta para la pantalla.
     */
    public function generar(PeriodoReporte $p, bool $completo = false): array
    {
        $resumen  = $this->modelo->resumenVentas($p->desde, $p->hasta);
        $anterior = $p->anterior();
        $previo   = $this->modelo->resumenVentas($anterior->desde, $anterior->hasta);

        $reporte = [
            'periodo'         => $p,
            'resumen'         => $resumen,
            'variacion'       => [
                'total'  => self::variacion($resumen['total'], $previo['total']),
                'ventas' => self::variacion($resumen['ventas'], $previo['ventas']),
            ],
            'etiquetaPrevio'  => $anterior->etiqueta(),
            'serie'           => $this->serie($p),
            'top'             => $this->modelo->topProductos($p->desde, $p->hasta, $completo ? null : 10),
            'categorias'      => $this->conParticipacion($this->modelo->porCategoria($p->desde, $p->hasta), $resumen['subtotal']),
            'metodos'         => $this->conParticipacion($this->modelo->porMetodoPago($p->desde, $p->hasta), $resumen['total']),
            'entradasResumen' => $this->modelo->resumenEntradas($p->desde, $p->hasta),
            'entradas'        => $this->modelo->entradas($p->desde, $p->hasta),
            'ventas'          => $this->modelo->ventas($p->desde, $p->hasta, $completo ? null : 25),
        ];

        if ($completo) {
            $reporte['ventasDetalle']   = $this->modelo->ventasDetalle($p->desde, $p->hasta);
            $reporte['entradasDetalle'] = $this->modelo->entradasDetalle($p->desde, $p->hasta);
        }

        return $reporte;
    }

    /**
     * Desglose por cubeta con ventas y entradas juntas; las cubetas sin
     * movimiento salen en cero.
     * @return array<int, array{clave:string, etiqueta:string, total:float, ventas:int, entradas:float, unidades_entrada:int}>
     */
    public function serie(PeriodoReporte $p): array
    {
        $ventas   = $this->modelo->serieVentas($p);
        $entradas = $this->modelo->serieEntradas($p);

        $serie = [];
        foreach ($p->cubetas() as $clave => $etiqueta) {
            $serie[] = [
                'clave'            => $clave,
                'etiqueta'         => $etiqueta,
                'total'            => $ventas[$clave]['total'] ?? 0.0,
                'ventas'           => $ventas[$clave]['ventas'] ?? 0,
                'entradas'         => $entradas[$clave]['costo'] ?? 0.0,
                'unidades_entrada' => $entradas[$clave]['unidades'] ?? 0,
            ];
        }
        return $serie;
    }

    /** % de cambio contra el período anterior; null si antes no hubo nada. */
    public static function variacion(float|int $actual, float|int $previo): ?float
    {
        return $previo > 0 ? ($actual - $previo) / $previo * 100 : null;
    }

    public static function metodoPago(string $clave): string
    {
        return self::METODOS_PAGO[$clave] ?? ucfirst($clave);
    }

    private function conParticipacion(array $filas, float $base): array
    {
        foreach ($filas as &$f) {
            $f['porcentaje'] = $base > 0 ? $f['total'] / $base * 100 : 0.0;
        }
        return $filas;
    }
}
