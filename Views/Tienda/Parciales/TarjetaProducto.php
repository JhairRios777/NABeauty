<?php
/**
 * Tarjeta de producto — se reutiliza en portada, catálogo, relacionados y 404.
 *
 * Espera: $p (ProductoEntity)
 *
 * El botón de la esquina cambia según el producto:
 *   - Agotado                → botón deshabilitado
 *   - Hay que elegir variante → enlace "Elegir" al detalle
 *   - Variante única          → "+" que agrega directo al carrito
 */
$etiqueta = $p->etiqueta();
$foto     = $p->getImageUrl();
$unica    = !$p->requiereEleccion() ? $p->varianteReferencia() : null;
?>
<article class="tarjeta<?= $p->estaAgotado() ? ' tarjeta--agotada' : '' ?>">
    <a class="tarjeta__foto" href="<?= e($p->url()) ?>" style="--fondo: <?= e($p->colorFondo()) ?>" tabindex="-1" aria-hidden="true">
        <?php if ($foto): ?>
            <img src="<?= e($foto) ?>" alt="" loading="lazy" width="400" height="400">
        <?php else: ?>
            <!-- Sin foto: bloque del color de la categoría con la inicial de la marca -->
            <span class="tarjeta__inicial"><?= e(mb_strtoupper(mb_substr((string) $p->marca, 0, 1, 'UTF-8'), 'UTF-8')) ?></span>
        <?php endif; ?>

        <?php if ($etiqueta): ?>
            <span class="etiqueta etiqueta--<?= e($etiqueta['tipo']) ?>"><?= e($etiqueta['texto']) ?></span>
        <?php endif; ?>
    </a>

    <div class="tarjeta__info">
        <span class="tarjeta__marca"><?= e($p->marca) ?></span>
        <h3 class="tarjeta__nombre"><a href="<?= e($p->url()) ?>"><?= e($p->nombre) ?></a></h3>
    </div>

    <div class="tarjeta__pie">
        <p class="tarjeta__precio">
            <?php if ($p->tienePreciosDistintos()): ?><span class="tarjeta__desde">Desde</span><?php endif; ?>
            <strong><?= e(precio($p->precioDesde())) ?></strong>
            <?php if ($p->precioAntes() !== null): ?>
                <s><span class="solo-lector">Antes </span><?= e(precio($p->precioAntes())) ?></s>
            <?php endif; ?>
        </p>

        <?php if ($p->estaAgotado()): ?>
            <button class="tarjeta__accion tarjeta__accion--texto" type="button" disabled>Agotado</button>
        <?php elseif ($unica): ?>
            <button class="tarjeta__accion" type="button"
                    data-agregar
                    data-variante="<?= e($unica->uuid) ?>"
                    data-slug="<?= e($p->slug) ?>"
                    data-nombre="<?= e($p->nombre) ?>"
                    data-marca="<?= e($p->marca) ?>"
                    data-detalle=""
                    data-precio="<?= e(number_format($unica->precioFinal(), 2, '.', '')) ?>"
                    data-max="<?= (int) $unica->stock ?>"
                    data-color="<?= e($p->colorFondo()) ?>"
                    aria-label="Agregar <?= e($p->nombre) ?> al carrito">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" aria-hidden="true"><path d="M12 5v14M5 12h14"></path></svg>
            </button>
        <?php else: ?>
            <a class="tarjeta__accion tarjeta__accion--texto" href="<?= e($p->url()) ?>" aria-label="Elegir opciones de <?= e($p->nombre) ?>">Elegir</a>
        <?php endif; ?>
    </div>
</article>
