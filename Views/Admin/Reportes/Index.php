<?php
/**
 * Reportes de ventas y entradas.
 *   $r              arreglo de ReporteService::generar()
 *   $puedeExportar  bool
 */
/** @var PeriodoReporte $p */
$p   = $r['periodo'];
$s   = $r['resumen'];
$ent = $r['entradasResumen'];
$parciales = VIEWS_PATH . 'Admin' . DS . 'Parciales' . DS;

$urlCon = fn(array $q, string $ruta = 'admin/reportes') => url($ruta) . '?' . http_build_query($q);
$hoy    = new DateTimeImmutable('today');
$vsTxt  = ['dia' => 'el día anterior', 'semana' => 'la semana anterior', 'mes' => 'el mes anterior',
           'anio' => 'el año anterior', 'rango' => 'el período anterior'][$p->tipo];
$margen = $s['total'] > 0 ? $s['ganancia'] / $s['total'] * 100 : 0;
$agr    = mb_strtolower($p->etiquetaAgrupacion());
?>
<section class="periodo">
    <nav class="periodo__tipos" aria-label="Tipo de período">
        <?php foreach (PeriodoReporte::TIPOS as $clave => $texto): ?>
            <?php
            $q = $clave === 'rango'
                ? ['periodo' => 'rango', 'desde' => $p->desde->format('Y-m-d'), 'hasta' => $p->ultimoDia()->format('Y-m-d')]
                : ['periodo' => $clave, 'fecha' => min($p->ultimoDia(), $hoy)->format('Y-m-d')];
            ?>
            <a class="periodo__tipo" href="<?= e($urlCon($q)) ?>" <?= $p->tipo === $clave ? 'aria-current="page"' : '' ?>><?= e($texto) ?></a>
        <?php endforeach; ?>
    </nav>

    <div class="periodo__nav">
        <a class="btn btn--icono" href="<?= e($urlCon($p->anterior()->query())) ?>" aria-label="Período anterior"><?= icono('izq') ?></a>
        <h2 class="periodo__etiqueta"><?= e($p->etiqueta()) ?></h2>
        <?php if ($p->tieneSiguiente()): ?>
            <a class="btn btn--icono" href="<?= e($urlCon($p->siguiente()->query())) ?>" aria-label="Período siguiente"><?= icono('der') ?></a>
        <?php else: ?>
            <span class="btn btn--icono btn--apagado" aria-hidden="true"><?= icono('der') ?></span>
        <?php endif; ?>
    </div>

    <?php if ($p->tipo === 'rango'): ?>
        <form class="periodo__rango" method="get" action="<?= e(url('admin/reportes')) ?>">
            <input type="hidden" name="periodo" value="rango">
            <label class="campo"><span class="campo__etiqueta">Desde</span>
                <input type="date" name="desde" value="<?= e($p->desde->format('Y-m-d')) ?>" max="<?= e($hoy->format('Y-m-d')) ?>" required></label>
            <label class="campo"><span class="campo__etiqueta">Hasta</span>
                <input type="date" name="hasta" value="<?= e($p->ultimoDia()->format('Y-m-d')) ?>" max="<?= e($hoy->format('Y-m-d')) ?>" required></label>
            <button class="btn btn--secundario" type="submit">Ver</button>
        </form>
    <?php endif; ?>

    <?php if ($puedeExportar): ?>
        <div class="periodo__exportar">
            <a class="btn btn--secundario" href="<?= e($urlCon($p->query(), 'admin/reportes/excel')) ?>" download><?= icono('excel') ?> Excel</a>
            <a class="btn btn--secundario" href="<?= e($urlCon($p->query(), 'admin/reportes/pdf')) ?>" download><?= icono('pdf') ?> PDF</a>
        </div>
    <?php endif; ?>
</section>

<!-- ── CIFRAS ── -->
<section class="kpis kpis--6" aria-label="Resumen del período">
    <div class="kpi kpi--destacado">
        <span class="kpi__titulo">Ingresos</span>
        <span class="kpi__valor"><?= e(precio($s['total'])) ?></span>
        <?php $var = $r['variacion']['total']; $vs = $vsTxt; require $parciales . 'Variacion.php'; ?>
    </div>
    <div class="kpi">
        <span class="kpi__titulo">Ventas</span>
        <span class="kpi__valor"><?= numero($s['ventas']) ?></span>
        <?php $var = $r['variacion']['ventas']; require $parciales . 'Variacion.php'; ?>
    </div>
    <div class="kpi">
        <span class="kpi__titulo">Ticket promedio</span>
        <span class="kpi__valor"><?= e(precio($s['ticket'])) ?></span>
        <span class="kpi__sub"><?= numero($s['unidades']) ?> unidades vendidas</span>
    </div>
    <div class="kpi">
        <span class="kpi__titulo">Ganancia bruta</span>
        <span class="kpi__valor"><?= e(precio($s['ganancia'])) ?></span>
        <span class="kpi__sub">Margen <?= numero($margen, 1) ?>% · costo <?= e(precio($s['costo'])) ?></span>
    </div>
    <div class="kpi">
        <span class="kpi__titulo">Descuentos</span>
        <span class="kpi__valor"><?= e(precio($s['descuento'])) ?></span>
        <span class="kpi__sub">
            <?= $s['anuladas'] > 0 ? numero($s['anuladas']) . ' anulada' . ($s['anuladas'] === 1 ? '' : 's') . ' (' . e(precio($s['total_anuladas'])) . ')' : 'Sin ventas anuladas' ?>
        </span>
    </div>
    <div class="kpi">
        <span class="kpi__titulo">Mercadería ingresada</span>
        <span class="kpi__valor"><?= e(precio($ent['costo'])) ?></span>
        <span class="kpi__sub"><?= numero($ent['unidades']) ?> unid. en <?= numero($ent['entradas']) ?> entrada<?= $ent['entradas'] === 1 ? '' : 's' ?></span>
    </div>
</section>

<!-- ── DESGLOSE ── -->
<div class="rejilla rejilla--2">
    <section class="tarjeta">
        <?php
        $g = GraficoBarras::preparar(array_map(fn($f) => [
            'etiqueta' => $f['etiqueta'], 'valor' => $f['total'],
            'detalle'  => $f['ventas'] . ' venta' . ($f['ventas'] === 1 ? '' : 's'),
        ], $r['serie']));
        $gTitulo = 'Ingresos por ' . $agr;
        $gTabla  = [[$p->etiquetaAgrupacion(), 'Ventas', 'Ingresos'],
                    array_map(fn($f) => [$f['etiqueta'], $f['ventas'], precio($f['total'])], $r['serie'])];
        require $parciales . 'GraficoBarras.php';
        ?>
    </section>
    <section class="tarjeta">
        <?php
        $g = GraficoBarras::preparar(array_map(fn($f) => [
            'etiqueta' => $f['etiqueta'], 'valor' => $f['entradas'],
            'detalle'  => $f['unidades_entrada'] . ' unidades',
        ], $r['serie']));
        $gTitulo = 'Mercadería ingresada (costo) por ' . $agr;
        $gTabla  = [[$p->etiquetaAgrupacion(), 'Unidades', 'Costo'],
                    array_map(fn($f) => [$f['etiqueta'], $f['unidades_entrada'], precio($f['entradas'])], $r['serie'])];
        require $parciales . 'GraficoBarras.php';
        ?>
    </section>
</div>

<div class="rejilla rejilla--2">
    <section class="tarjeta">
        <h2 class="tarjeta__titulo">Productos más vendidos</h2>
        <?php if (empty($r['top'])): ?>
            <p class="vacio">Sin ventas en este período.</p>
        <?php else: ?>
            <div class="tabla-scroll">
                <table class="tabla">
                    <thead><tr><th scope="col">#</th><th scope="col">Producto</th><th scope="col" class="num">Unid.</th><th scope="col" class="num">Ingresos</th></tr></thead>
                    <tbody>
                        <?php foreach ($r['top'] as $i => $t): ?>
                            <tr>
                                <td class="tabla__pos"><?= $i + 1 ?></td>
                                <th scope="row"><?= e($t['producto']) ?><?php if ($t['marca']): ?><small><?= e($t['marca']) ?></small><?php endif; ?></th>
                                <td class="num"><?= numero($t['unidades']) ?></td>
                                <td class="num"><?= e(precio($t['total'])) ?></td>
                            </tr>
                        <?php endforeach; ?>
                    </tbody>
                </table>
            </div>
        <?php endif; ?>
    </section>

    <section class="tarjeta">
        <h2 class="tarjeta__titulo">Por categoría</h2>
        <?php if (empty($r['categorias'])): ?>
            <p class="vacio">Sin ventas en este período.</p>
        <?php else: ?>
            <ul class="barras-h">
                <?php $maxCat = max(array_column($r['categorias'], 'total')) ?: 1; ?>
                <?php foreach ($r['categorias'] as $c): ?>
                    <li class="barras-h__item">
                        <span class="barras-h__nombre"><?= e($c['categoria']) ?></span>
                        <span class="barras-h__pista"><span class="barras-h__barra" style="--w: <?= round($c['total'] / $maxCat * 100, 2) ?>%"></span></span>
                        <span class="barras-h__valor"><?= e(precio($c['total'])) ?><small><?= numero($c['porcentaje'], 1) ?>% · <?= numero($c['unidades']) ?> u.</small></span>
                    </li>
                <?php endforeach; ?>
            </ul>
        <?php endif; ?>

        <h2 class="tarjeta__titulo tarjeta__titulo--sep">Por método de pago</h2>
        <?php if (empty($r['metodos'])): ?>
            <p class="vacio">Sin ventas en este período.</p>
        <?php else: ?>
            <ul class="barras-h">
                <?php $maxMet = max(array_column($r['metodos'], 'total')) ?: 1; ?>
                <?php foreach ($r['metodos'] as $m): ?>
                    <li class="barras-h__item">
                        <span class="barras-h__nombre"><?= e(ReporteService::metodoPago($m['metodo'])) ?></span>
                        <span class="barras-h__pista"><span class="barras-h__barra" style="--w: <?= round($m['total'] / $maxMet * 100, 2) ?>%"></span></span>
                        <span class="barras-h__valor"><?= e(precio($m['total'])) ?><small><?= numero($m['porcentaje'], 1) ?>% · <?= numero($m['ventas']) ?> ventas</small></span>
                    </li>
                <?php endforeach; ?>
            </ul>
        <?php endif; ?>
    </section>
</div>

<!-- ── LISTADOS ── -->
<section class="tarjeta">
    <div class="tarjeta__cabeza">
        <h2 class="tarjeta__titulo">Ventas</h2>
        <span class="tarjeta__nota">
            <?php $totalListado = $s['ventas'] + $s['anuladas']; ?>
            <?= count($r['ventas']) < $totalListado ? 'Últimas ' . count($r['ventas']) . ' de ' . numero($totalListado) . ($puedeExportar ? ' · el Excel trae todas' : '') : numero($totalListado) . ' en total' ?>
        </span>
    </div>
    <?php if (empty($r['ventas'])): ?>
        <p class="vacio">Sin ventas en este período.</p>
    <?php else: ?>
        <div class="tabla-scroll">
            <table class="tabla tabla--filas">
                <thead><tr>
                    <th scope="col">Número</th><th scope="col">Fecha</th><th scope="col">Pago</th>
                    <th scope="col" class="num">Unid.</th><th scope="col" class="num">Total</th><th scope="col">Estado</th>
                </tr></thead>
                <tbody>
                    <?php foreach ($r['ventas'] as $v): ?>
                        <tr class="<?= $v['estado'] === 'anulada' ? 'fila--anulada' : '' ?>">
                            <th scope="row"><?= e($v['numero']) ?></th>
                            <td><?= e(fecha_hora($v['fecha'])) ?></td>
                            <td><?= e(ReporteService::metodoPago($v['metodo_pago'])) ?></td>
                            <td class="num"><?= numero((int) $v['unidades']) ?></td>
                            <td class="num">
                                <?= e(precio((float) $v['total'])) ?>
                                <?php if ((float) $v['descuento'] > 0): ?><small>−<?= e(precio((float) $v['descuento'])) ?></small><?php endif; ?>
                            </td>
                            <td><span class="chip <?= $v['estado'] === 'anulada' ? 'chip--agotado' : 'chip--ok' ?>"><?= e(ucfirst($v['estado'])) ?></span></td>
                        </tr>
                    <?php endforeach; ?>
                </tbody>
            </table>
        </div>
    <?php endif; ?>
</section>

<section class="tarjeta">
    <h2 class="tarjeta__titulo"><?= icono('entrada') ?> Entradas de mercadería</h2>
    <?php if (empty($r['entradas'])): ?>
        <p class="vacio">No se registraron entradas en este período.</p>
    <?php else: ?>
        <div class="tabla-scroll">
            <table class="tabla tabla--filas">
                <thead><tr>
                    <th scope="col">Número</th><th scope="col">Fecha</th><th scope="col">Proveedor</th>
                    <th scope="col" class="num">Productos</th><th scope="col" class="num">Unid.</th><th scope="col" class="num">Costo</th>
                </tr></thead>
                <tbody>
                    <?php foreach ($r['entradas'] as $en): ?>
                        <tr>
                            <th scope="row"><?= e($en['numero']) ?></th>
                            <td><?= e(fecha_hora($en['fecha'])) ?></td>
                            <td><?= e($en['proveedor'] ?: '—') ?></td>
                            <td class="num"><?= numero((int) $en['lineas']) ?></td>
                            <td class="num"><?= numero((int) $en['unidades']) ?></td>
                            <td class="num"><?= e(precio((float) $en['total_costo'])) ?></td>
                        </tr>
                    <?php endforeach; ?>
                </tbody>
            </table>
        </div>
    <?php endif; ?>
</section>

<p class="ayuda">Las ventas y entradas las registra el sistema de la tienda (POS). Por ahora se muestran datos de prueba.</p>
