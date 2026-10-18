<?php

/**
 * GraficoBarras — Prepara una serie para el gráfico de barras del panel.
 *
 * Calcula una escala "redonda" (0, 5 000, 10 000…) para el eje, la altura
 * de cada barra en % y qué etiquetas del eje X caben sin encimarse.
 * La vista (Parciales/GraficoBarras.php) solo imprime.
 */
final class GraficoBarras
{
    /**
     * @param array<int, array{etiqueta: string, valor: float|int, detalle?: string}> $puntos
     * @param 'moneda'|'numero' $formato
     */
    public static function preparar(array $puntos, string $formato = 'moneda', int $maxEtiquetas = 8): array
    {
        $max   = max(array_map(fn($p) => (float) $p['valor'], $puntos) ?: [0]);
        $techo = self::techo($max);
        $paso  = max(1, (int) ceil(count($puntos) / $maxEtiquetas));
        $fmt   = fn(float $v) => $formato === 'moneda' ? precio($v) : numero($v);

        $barras = [];
        foreach (array_values($puntos) as $i => $p) {
            $v = (float) $p['valor'];
            $barras[] = [
                'etiqueta'    => $p['etiqueta'],
                'altura'      => $techo > 0 ? round($v / $techo * 100, 2) : 0,
                'valorTexto'  => $fmt($v),
                'detalle'     => $p['detalle'] ?? '',
                'mostrarEje'  => $i % $paso === 0,
                'vacia'       => $v <= 0,
            ];
        }

        // Tres líneas guía: techo, mitad y cero
        $guias = [];
        foreach ([1, 0.5, 0] as $f) {
            $guias[] = ['pos' => $f * 100, 'texto' => self::corto($techo * $f, $formato)];
        }

        return ['barras' => $barras, 'guias' => $guias, 'vacio' => $max <= 0];
    }

    /** Siguiente número "redondo" ≥ $max: 1, 2, 2.5, 5 × 10ⁿ */
    private static function techo(float $max): float
    {
        if ($max <= 0) {
            return 0;
        }
        $base = 10 ** floor(log10($max));
        foreach ([1, 2, 2.5, 5, 10] as $m) {
            if ($m * $base >= $max) {
                return $m * $base;
            }
        }
        return 10 * $base;
    }

    /** Etiqueta compacta del eje: 12500 → "L 12.5k" */
    private static function corto(float $v, string $formato): string
    {
        $pre = $formato === 'moneda' ? MONEDA_SIMBOLO . ' ' : '';
        if ($v >= 1000) {
            return $pre . rtrim(rtrim(number_format($v / 1000, 1, '.', ''), '0'), '.') . 'k';
        }
        return $pre . numero($v);
    }
}
