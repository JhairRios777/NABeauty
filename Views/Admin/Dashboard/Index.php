<?php
/**
 * Inicio del panel.
 *   $tarjetas, $serie, $top   (solo con permiso de reportes)
 *   $stockBajo, $productosActivos
 */
$parciales = VIEWS_PATH . 'Admin' . DS . 'Parciales' . DS;
$hora = (int) date('G');
$saludo = $hora < 12 ? 'Buenos días' : ($hora < 19 ? 'Buenas tardes' : 'Buenas noches');
?>
<section class="encabezado">
    <div>
        <p class="encabezado__pre"><?= e($saludo) ?>,</p>
        <h2 class="encabezado__titulo"><?= e($usuario->primerNombre()) ?></h2>
    </div>
    <?php if ($usuario->tienePermiso(PERM_PRODUCTOS)): ?>
        <a class="btn btn--primario" href="<?= e(url('admin/productos/nuevo')) ?>"><?= icono('mas') ?> Nuevo producto</a>
    <?php endif; ?>
</section>

<?php if (isset($tarjetas)): ?>
    <section class="kpis" aria-label="Ventas">
        <?php foreach ($tarjetas as $t): ?>
            <a class="kpi kpi--enlace" href="<?= e(url('admin/reportes') . '?' . http_build_query($t['query'])) ?>">
                <span class="kpi__titulo"><?= e($t['titulo']) ?></span>
                <span class="kpi__valor"><?= e(precio($t['resumen']['total'])) ?></span>
                <span class="kpi__sub"><?= numero($t['resumen']['ventas']) ?> venta<?= $t['resumen']['ventas'] === 1 ? '' : 's' ?> · <?= numero($t['resumen']['unidades']) ?> unid.</span>
                <?php $var = $t['variacion']; $vs = $t['vs']; require $parciales . 'Variacion.php'; ?>
            </a>
        <?php endforeach; ?>
    </section>

    <div class="rejilla rejilla--2-1">
        <section class="tarjeta">
            <?php
            $g = GraficoBarras::preparar(array_map(fn($s) => [
                'etiqueta' => $s['etiqueta'],
                'valor'    => $s['total'],
                'detalle'  => $s['ventas'] . ' venta' . ($s['ventas'] === 1 ? '' : 's'),
            ], $serie));
            $gTitulo = 'Ingresos de los últimos 14 días';
            $gTabla  = [['Día', 'Ventas', 'Ingresos'], array_map(fn($s) => [$s['etiqueta'], $s['ventas'], precio($s['total'])], $serie)];
            require $parciales . 'GraficoBarras.php';
            ?>
        </section>

        <section class="tarjeta">
            <div class="tarjeta__cabeza">
                <h2 class="tarjeta__titulo">Lo más vendido del mes</h2>
                <a class="enlace" href="<?= e(url('admin/reportes?periodo=mes')) ?>">Ver todo</a>
            </div>
            <?php if (empty($top)): ?>
                <p class="vacio">Todavía no hay ventas este mes.</p>
            <?php else: ?>
                <ol class="ranking">
                    <?php foreach ($top as $i => $p): ?>
                        <li class="ranking__item">
                            <span class="ranking__pos"><?= $i + 1 ?></span>
                            <span class="ranking__nombre">
                                <?= e($p['producto']) ?>
                                <?php if ($p['marca']): ?><small><?= e($p['marca']) ?></small><?php endif; ?>
                            </span>
                            <span class="ranking__dato"><?= numero($p['unidades']) ?> u.<small><?= e(precio($p['total'])) ?></small></span>
                        </li>
                    <?php endforeach; ?>
                </ol>
            <?php endif; ?>
        </section>
    </div>
<?php endif; ?>

<?php if (isset($stockBajo)): ?>
    <section class="tarjeta">
        <div class="tarjeta__cabeza">
            <h2 class="tarjeta__titulo"><?= icono('alerta') ?> Por agotarse</h2>
            <span class="tarjeta__nota"><?= numero($productosActivos) ?> productos activos</span>
        </div>
        <?php if (empty($stockBajo)): ?>
            <p class="vacio">Todo el inventario está por encima de <?= STOCK_BAJO ?> unidades.</p>
        <?php else: ?>
            <ul class="lista-stock">
                <?php foreach ($stockBajo as $s): ?>
                    <li class="lista-stock__item">
                        <span class="lista-stock__nombre">
                            <?php if ($usuario->tienePermiso(PERM_PRODUCTOS)): ?>
                                <a href="<?= e(url('admin/productos/editar/' . $s['producto_uuid'])) ?>"><?= e($s['producto']) ?></a>
                            <?php else: ?>
                                <?= e($s['producto']) ?>
                            <?php endif; ?>
                            <small><?= e($s['marca']) ?><?= $s['tipo'] !== 'unica' ? ' · ' . e($s['variante']) : '' ?></small>
                        </span>
                        <span class="chip <?= (int) $s['stock'] <= 0 ? 'chip--agotado' : 'chip--bajo' ?>">
                            <?= (int) $s['stock'] <= 0 ? 'Agotado' : (int) $s['stock'] . ' en stock' ?>
                        </span>
                    </li>
                <?php endforeach; ?>
            </ul>
            <p class="ayuda">El stock se actualiza con las entradas y ventas que registra el POS.</p>
        <?php endif; ?>
    </section>
<?php endif; ?>
