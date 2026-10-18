<?php
/** Flecha de cambio vs período anterior. $var: ?float (%), $vs: texto ("ayer"). Color + flecha + texto: nunca solo color. */
?>
<?php if ($var === null): ?>
    <span class="var var--nd">Sin datos de <?= e($vs) ?></span>
<?php else: ?>
    <span class="var <?= $var >= 0 ? 'var--sube' : 'var--baja' ?>">
        <?= icono($var >= 0 ? 'sube' : 'baja') ?>
        <?= e(($var >= 0 ? '+' : '') . numero($var, abs($var) < 10 ? 1 : 0)) ?>% vs <?= e($vs) ?>
    </span>
<?php endif; ?>
