<?php
/**
 * Gráfico de barras (una sola serie, color vino).
 *   $g      resultado de GraficoBarras::preparar()
 *   $gTitulo título visible (nombra la serie: no hace falta leyenda)
 *   $gTabla  [encabezados, filas] para la vista de tabla equivalente
 * Cada barra lleva su valor en aria-label y en data-tip (globo de admin.js).
 */
?>
<figure class="grafico">
    <figcaption class="grafico__titulo"><?= e($gTitulo) ?></figcaption>

    <?php if ($g['vacio']): ?>
        <p class="vacio">Sin movimientos en este período.</p>
    <?php else: ?>
        <div class="grafico__area">
            <div class="grafico__guias" aria-hidden="true">
                <?php foreach ($g['guias'] as $guia): ?>
                    <span class="grafico__guia" style="--pos: <?= (float) $guia['pos'] ?>%"><?= e($guia['texto']) ?></span>
                <?php endforeach; ?>
            </div>
            <ol class="grafico__barras" style="--n: <?= count($g['barras']) ?>">
                <?php foreach ($g['barras'] as $b): ?>
                    <li class="grafico__col">
                        <span class="grafico__barra<?= $b['vacia'] ? ' grafico__barra--cero' : '' ?>"
                              style="--h: <?= (float) $b['altura'] ?>%"
                              tabindex="0"
                              role="img"
                              aria-label="<?= e($b['etiqueta'] . ': ' . $b['valorTexto'] . ($b['detalle'] ? ', ' . $b['detalle'] : '')) ?>"
                              data-tip-titulo="<?= e($b['etiqueta']) ?>"
                              data-tip="<?= e($b['valorTexto']) ?>"
                              data-tip-detalle="<?= e($b['detalle']) ?>"></span>
                        <span class="grafico__eje<?= $b['mostrarEje'] ? '' : ' grafico__eje--oculto' ?>"><?= e($b['etiqueta']) ?></span>
                    </li>
                <?php endforeach; ?>
            </ol>
        </div>
    <?php endif; ?>

    <?php if (!empty($gTabla)): ?>
        <details class="grafico__tabla">
            <summary>Ver como tabla</summary>
            <div class="tabla-scroll">
                <table class="tabla tabla--compacta">
                    <thead><tr><?php foreach ($gTabla[0] as $th): ?><th scope="col"><?= e($th) ?></th><?php endforeach; ?></tr></thead>
                    <tbody>
                        <?php foreach ($gTabla[1] as $fila): ?>
                            <tr><?php foreach ($fila as $i => $td): ?><<?= $i === 0 ? 'th scope="row"' : 'td class="num"' ?>><?= e((string) $td) ?></<?= $i === 0 ? 'th' : 'td' ?>><?php endforeach; ?></tr>
                        <?php endforeach; ?>
                    </tbody>
                </table>
            </div>
        </details>
    <?php endif; ?>
</figure>
