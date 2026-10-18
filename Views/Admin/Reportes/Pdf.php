<?php
/**
 * HTML del reporte en PDF (lo convierte Dompdf; no se muestra en el navegador).
 *   $r            arreglo de ReporteService::generar(..., completo: true)
 *   $generadoPor  nombre de quien lo descarga
 *   $logoDataUri  logo embebido (Dompdf no descarga nada de fuera)
 * CSS sencillo a propósito: Dompdf soporta CSS 2.1 + algo de CSS3.
 */
/** @var PeriodoReporte $p */
$p   = $r['periodo'];
$s   = $r['resumen'];
$ent = $r['entradasResumen'];
$maxSerie = max(array_column($r['serie'], 'total') ?: [0]) ?: 1;
$fmtVar = fn(?float $v) => $v === null ? '—' : (($v >= 0 ? '▲ +' : '▼ ') . numero($v, 1) . '%');
?>
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<style>
    @page { margin: 26mm 14mm 18mm 14mm; }
    body { font-family: 'DejaVu Sans', sans-serif; font-size: 9pt; color: #1A1416; }
    header { position: fixed; top: -19mm; left: 0; right: 0; height: 15mm; border-bottom: 2px solid #9E1B2A; }
    header img { height: 12mm; float: left; }
    header .tit { float: right; text-align: right; }
    header .tit strong { font-size: 12pt; color: #9E1B2A; display: block; }
    footer { position: fixed; bottom: -12mm; left: 0; right: 0; font-size: 7pt; color: #6B6B6B; }
    h2 { font-size: 10.5pt; color: #9E1B2A; margin: 16px 0 6px; padding-bottom: 3px; border-bottom: 1px solid #F1DEE2; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 4px 5px; text-align: left; vertical-align: top; }
    thead th { background: #9E1B2A; color: #fff; font-weight: bold; font-size: 8pt; }
    tbody tr:nth-child(even) td, tbody tr:nth-child(even) th { background: #FBF3F4; }
    tfoot td, tfoot th { font-weight: bold; border-top: 1.5px solid #9E1B2A; background: #F6D5DC; }
    .num { text-align: right; white-space: nowrap; }
    .kpis td { width: 33.33%; border: 1px solid #F1DEE2; padding: 7px 8px; }
    .kpis .t { font-size: 7.5pt; color: #6B5A5E; text-transform: uppercase; letter-spacing: .3px; }
    .kpis .v { font-size: 13pt; font-weight: bold; margin-top: 2px; }
    .kpis .s { font-size: 7.5pt; color: #6B5A5E; }
    .sube { color: #1E5B2E; } .baja { color: #9E1B2A; }
    .barra { height: 9px; background: #9E1B2A; border-radius: 0 3px 3px 0; }
    .pista { background: #F6EEF0; }
    .anulada td, .anulada th { color: #9A8C8F; text-decoration: line-through; }
    .nota { color: #6B5A5E; font-size: 7.5pt; margin-top: 4px; }
    .salto { page-break-before: always; }
    small { color: #6B5A5E; }
</style>
</head>
<body>
<header>
    <?php if ($logoDataUri): ?><img src="<?= $logoDataUri ?>" alt=""><?php endif; ?>
    <div class="tit">
        <strong>Reporte de ventas y entradas</strong>
        <?= e($p->etiqueta()) ?>
    </div>
</header>
<footer>
    <?= e(APP_NAME) ?> · Generado el <?= date('d/m/Y H:i') ?> por <?= e($generadoPor) ?>
</footer>

<table class="kpis">
    <tr>
        <td><div class="t">Ingresos</div><div class="v"><?= e(precio($s['total'])) ?></div>
            <div class="s <?= ($r['variacion']['total'] ?? 0) >= 0 ? 'sube' : 'baja' ?>"><?= e($fmtVar($r['variacion']['total'])) ?> vs <?= e($r['etiquetaPrevio']) ?></div></td>
        <td><div class="t">Ventas</div><div class="v"><?= numero($s['ventas']) ?></div>
            <div class="s"><?= numero($s['unidades']) ?> unidades · ticket <?= e(precio($s['ticket'])) ?></div></td>
        <td><div class="t">Ganancia bruta</div><div class="v"><?= e(precio($s['ganancia'])) ?></div>
            <div class="s">Costo <?= e(precio($s['costo'])) ?> · margen <?= numero($s['total'] > 0 ? $s['ganancia'] / $s['total'] * 100 : 0, 1) ?>%</div></td>
    </tr>
    <tr>
        <td><div class="t">Descuentos</div><div class="v"><?= e(precio($s['descuento'])) ?></div>
            <div class="s">Subtotal <?= e(precio($s['subtotal'])) ?></div></td>
        <td><div class="t">Anuladas</div><div class="v"><?= numero($s['anuladas']) ?></div>
            <div class="s">Por <?= e(precio($s['total_anuladas'])) ?> (no suman)</div></td>
        <td><div class="t">Mercadería ingresada</div><div class="v"><?= e(precio($ent['costo'])) ?></div>
            <div class="s"><?= numero($ent['unidades']) ?> unid. en <?= numero($ent['entradas']) ?> entradas</div></td>
    </tr>
</table>

<h2>Ingresos por <?= e(mb_strtolower($p->etiquetaAgrupacion())) ?></h2>
<table>
    <thead><tr><th style="width:22%"><?= e($p->etiquetaAgrupacion()) ?></th><th></th><th class="num" style="width:10%">Ventas</th><th class="num" style="width:17%">Ingresos</th><th class="num" style="width:17%">Entradas (costo)</th></tr></thead>
    <tbody>
        <?php foreach ($r['serie'] as $f): ?>
            <tr>
                <th><?= e($f['etiqueta']) ?></th>
                <td class="pista"><div class="barra" style="width: <?= round($f['total'] / $maxSerie * 100, 1) ?>%"></div></td>
                <td class="num"><?= numero($f['ventas']) ?></td>
                <td class="num"><?= e(precio($f['total'])) ?></td>
                <td class="num"><?= $f['entradas'] > 0 ? e(precio($f['entradas'])) : '—' ?></td>
            </tr>
        <?php endforeach; ?>
    </tbody>
    <tfoot><tr><th>Total</th><td></td><td class="num"><?= numero($s['ventas']) ?></td><td class="num"><?= e(precio($s['total'])) ?></td><td class="num"><?= e(precio($ent['costo'])) ?></td></tr></tfoot>
</table>

<h2>Por categoría</h2>
<?php if (empty($r['categorias'])): ?><p class="nota">Sin ventas en este período.</p><?php else: ?>
<table>
    <thead><tr><th>Categoría</th><th class="num">Unidades</th><th class="num">Ingresos</th><th class="num">% del total</th></tr></thead>
    <tbody><?php foreach ($r['categorias'] as $c): ?>
        <tr><th><?= e($c['categoria']) ?></th><td class="num"><?= numero($c['unidades']) ?></td><td class="num"><?= e(precio($c['total'])) ?></td><td class="num"><?= numero($c['porcentaje'], 1) ?>%</td></tr>
    <?php endforeach; ?></tbody>
</table>
<?php endif; ?>

<h2>Por método de pago</h2>
<?php if (empty($r['metodos'])): ?><p class="nota">Sin ventas en este período.</p><?php else: ?>
<table>
    <thead><tr><th>Método</th><th class="num">Ventas</th><th class="num">Total</th><th class="num">%</th></tr></thead>
    <tbody><?php foreach ($r['metodos'] as $m): ?>
        <tr><th><?= e(ReporteService::metodoPago($m['metodo'])) ?></th><td class="num"><?= numero($m['ventas']) ?></td><td class="num"><?= e(precio($m['total'])) ?></td><td class="num"><?= numero($m['porcentaje'], 1) ?>%</td></tr>
    <?php endforeach; ?></tbody>
</table>
<?php endif; ?>

<h2 class="salto">Productos vendidos</h2>
<?php if (empty($r['top'])): ?><p class="nota">Sin ventas en este período.</p><?php else: ?>
<table>
    <thead><tr><th style="width:5%">#</th><th>Producto</th><th class="num">Unid.</th><th class="num">Ingresos</th><th class="num">Costo</th><th class="num">Ganancia</th></tr></thead>
    <tbody><?php foreach ($r['top'] as $i => $t): ?>
        <tr><td><?= $i + 1 ?></td><th><?= e($t['producto']) ?><?= $t['marca'] ? ' <small>· ' . e($t['marca']) . '</small>' : '' ?></th>
            <td class="num"><?= numero($t['unidades']) ?></td><td class="num"><?= e(precio($t['total'])) ?></td>
            <td class="num"><?= e(precio($t['costo'])) ?></td><td class="num"><?= e(precio($t['total'] - $t['costo'])) ?></td></tr>
    <?php endforeach; ?></tbody>
</table>
<?php endif; ?>

<h2>Entradas de mercadería</h2>
<?php if (empty($r['entradas'])): ?><p class="nota">No se registraron entradas en este período.</p><?php else: ?>
<table>
    <thead><tr><th>Número</th><th>Fecha</th><th>Proveedor</th><th class="num">Productos</th><th class="num">Unid.</th><th class="num">Costo</th></tr></thead>
    <tbody><?php foreach ($r['entradas'] as $en): ?>
        <tr><th><?= e($en['numero']) ?></th><td><?= e(fecha_hora($en['fecha'])) ?></td><td><?= e($en['proveedor'] ?: '—') ?></td>
            <td class="num"><?= numero((int) $en['lineas']) ?></td><td class="num"><?= numero((int) $en['unidades']) ?></td><td class="num"><?= e(precio((float) $en['total_costo'])) ?></td></tr>
    <?php endforeach; ?></tbody>
    <tfoot><tr><th colspan="4">Total</th><td class="num"><?= numero($ent['unidades']) ?></td><td class="num"><?= e(precio($ent['costo'])) ?></td></tr></tfoot>
</table>
<?php endif; ?>

<h2>Ventas del período</h2>
<?php if (empty($r['ventas'])): ?><p class="nota">Sin ventas en este período.</p><?php else: ?>
<table>
    <thead><tr><th>Número</th><th>Fecha</th><th>Pago</th><th class="num">Unid.</th><th class="num">Descuento</th><th class="num">Total</th></tr></thead>
    <tbody><?php foreach ($r['ventas'] as $v): ?>
        <tr class="<?= $v['estado'] === 'anulada' ? 'anulada' : '' ?>"><th><?= e($v['numero']) ?></th><td><?= e(fecha_hora($v['fecha'])) ?></td>
            <td><?= e(ReporteService::metodoPago($v['metodo_pago'])) ?><?= $v['estado'] === 'anulada' ? ' (anulada)' : '' ?></td>
            <td class="num"><?= numero((int) $v['unidades']) ?></td><td class="num"><?= (float) $v['descuento'] > 0 ? e(precio((float) $v['descuento'])) : '—' ?></td>
            <td class="num"><?= e(precio((float) $v['total'])) ?></td></tr>
    <?php endforeach; ?></tbody>
    <tfoot><tr><th colspan="5">Total cobrado (sin anuladas)</th><td class="num"><?= e(precio($s['total'])) ?></td></tr></tfoot>
</table>
<?php endif; ?>
</body>
</html>
