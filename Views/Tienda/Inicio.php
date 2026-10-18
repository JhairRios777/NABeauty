<?php
/**
 * Portada de la tienda.
 *
 * Espera:
 *   $destacados     ProductoEntity[]  "Lo más top"
 *   $nuevos         ProductoEntity[]  "Recién llegados"
 *   $categoriasNav  CategoriaEntity[] (lo agrega el render a todas las vistas)
 */
$categoriaPrincipal = $categoriasNav[0] ?? null;
$otrasCategorias    = array_slice($categoriasNav, 1);
?>

<!-- ═════════ HERO ═════════ -->
<section class="contenedor">
    <div class="hero">
        <div class="hero__texto">
            <span class="pildora pildora--blanca">Nueva temporada · <?= date('Y') ?></span>
            <h1 class="titulo titulo--xl">Todo lo que <em>amas</em> en un solo lugar</h1>
            <div class="hero__cta">
                <a class="btn btn--vino btn--grande" href="<?= e(url('catalogo')) ?>">Comprar ahora →</a>
                <p>Belleza, ropa, zapatos, joyería y termos. Envíos a todo Honduras.</p>
            </div>
        </div>

        <div class="hero__fotos" aria-hidden="true">
            <!-- Bloques de color en lugar de las fotos de campaña (pendientes) -->
            <div class="hero__foto hero__foto--principal"></div>
            <div class="hero__sello">Envío<br>a todo<br>Honduras</div>
            <?php if (!empty($destacados)): ?>
                <?php $estrella = $destacados[count($destacados) - 1]; ?>
                <div class="hero__producto">
                    <div class="hero__producto-foto" style="--fondo: <?= e($estrella->colorFondo()) ?>"></div>
                    <span class="tarjeta__marca"><?= e($estrella->marca) ?></span>
                    <div class="hero__producto-fila">
                        <span><?= e($estrella->nombre) ?></span>
                        <strong><?= e(precio($estrella->precioDesde())) ?></strong>
                    </div>
                </div>
            <?php endif; ?>
        </div>
    </div>
</section>

<!-- ═════════ CINTA DE CATEGORÍAS ═════════ -->
<div class="cinta" aria-hidden="true">
    <div class="cinta__pista">
        <?php for ($vuelta = 0; $vuelta < 4; $vuelta++): /* 4 copias: dos mitades iguales que llenan pantallas anchas */ ?>
            <?php foreach ($categoriasNav as $cat): ?>
                <span><?= e($cat->nombre) ?></span><i>✦</i>
            <?php endforeach; ?>
        <?php endfor; ?>
    </div>
</div>

<!-- ═════════ MOSAICO DE CATEGORÍAS ═════════ -->
<section class="contenedor seccion">
    <div class="seccion__cabeza">
        <h2 class="titulo titulo--lg">¿Qué buscas hoy?</h2>
        <a class="btn btn--borde" href="<?= e(url('catalogo')) ?>">Ver todo el catálogo</a>
    </div>

    <div class="mosaico">
        <?php if ($categoriaPrincipal): ?>
            <a class="mosaico__item mosaico__item--grande<?= $categoriaPrincipal->esOscura() ? ' mosaico__item--oscuro' : '' ?>"
               href="<?= e($categoriaPrincipal->url()) ?>" style="--fondo: <?= e($categoriaPrincipal->colorSeguro()) ?>">
                <span class="mosaico__nombre"><?= e($categoriaPrincipal->nombre) ?></span>
                <span class="mosaico__flecha" aria-hidden="true">→</span>
            </a>
        <?php endif; ?>

        <?php foreach ($otrasCategorias as $cat): ?>
            <a class="mosaico__item<?= $cat->esOscura() ? ' mosaico__item--oscuro' : '' ?>"
               href="<?= e($cat->url()) ?>" style="--fondo: <?= e($cat->colorSeguro()) ?>">
                <span class="mosaico__nombre"><?= e($cat->nombre) ?></span>
                <span class="mosaico__flecha" aria-hidden="true">→</span>
            </a>
        <?php endforeach; ?>

        <a class="mosaico__item mosaico__item--oscuro mosaico__item--oferta" href="<?= e(url('catalogo?oferta=1')) ?>">
            <span class="mosaico__nombre">Ofertas</span>
            <span class="mosaico__flecha" aria-hidden="true">→</span>
        </a>
    </div>
</section>

<!-- ═════════ LO MÁS TOP ═════════ -->
<?php if (!empty($destacados)): ?>
<section class="contenedor seccion">
    <div class="seccion__cabeza">
        <h2 class="titulo titulo--lg">Lo más <em>top</em></h2>
        <a class="btn btn--borde" href="<?= e(url('catalogo')) ?>">Ver más</a>
    </div>
    <div class="rejilla rejilla--4">
        <?php foreach ($destacados as $p): ?>
            <?php require __DIR__ . '/Parciales/TarjetaProducto.php'; ?>
        <?php endforeach; ?>
    </div>
</section>
<?php endif; ?>

<!-- ═════════ PROMO ═════════ -->
<section class="contenedor seccion">
    <div class="promo">
        <div class="promo__texto">
            <span class="promo__antetitulo">Precios especiales</span>
            <h2 class="titulo titulo--xl">Activamos el <em>modo finde</em></h2>
            <a class="btn btn--rosa btn--grande" href="<?= e(url('catalogo?oferta=1')) ?>">Ver ofertas →</a>
        </div>
        <div class="promo__foto" aria-hidden="true"></div>
    </div>
</section>

<!-- ═════════ RECIÉN LLEGADOS ═════════ -->
<?php if (!empty($nuevos)): ?>
<section class="contenedor seccion">
    <div class="seccion__cabeza">
        <h2 class="titulo titulo--lg">Recién <em>llegados</em></h2>
        <a class="btn btn--borde" href="<?= e(url('catalogo')) ?>">Ver más</a>
    </div>
    <div class="rejilla rejilla--4">
        <?php foreach ($nuevos as $p): ?>
            <?php require __DIR__ . '/Parciales/TarjetaProducto.php'; ?>
        <?php endforeach; ?>
    </div>
</section>
<?php endif; ?>

<!-- ═════════ DOS BANNERS ═════════ -->
<section class="contenedor seccion">
    <div class="duo">
        <div class="duo__item duo__item--arena">
            <span class="promo__antetitulo">Para regalar</span>
            <h2 class="titulo titulo--md">Arma tu kit de maquillaje</h2>
            <p>Elige tus productos favoritos y te lo enviamos listo para regalar.</p>
            <a class="btn btn--negro" href="<?= e(url('catalogo/maquillaje')) ?>">Ver maquillaje</a>
        </div>
        <div class="duo__item duo__item--cielo">
            <span class="promo__antetitulo">Skincare</span>
            <h2 class="titulo titulo--md">Productos que no pueden faltar</h2>
            <p>Lo esencial para tu rutina diaria de cuidado de la piel.</p>
            <a class="btn btn--negro" href="<?= e(url('catalogo/skincare')) ?>">Ver skincare</a>
        </div>
    </div>
</section>
