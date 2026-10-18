<?php
/**
 * Página 404 con el diseño de la tienda.
 * Espera: $sugeridos (ProductoEntity[])
 */
?>
<section class="contenedor">
    <div class="vacio vacio--grande">
        <span class="pildora pildora--blanca">Error 404</span>
        <h1 class="titulo titulo--lg">No encontramos esta página</h1>
        <p>Puede que el producto ya no esté disponible o que el enlace esté mal escrito.</p>
        <div class="vacio__botones">
            <a class="btn btn--vino" href="<?= e(url('catalogo')) ?>">Ver el catálogo</a>
            <a class="btn btn--borde" href="<?= e(url()) ?>">Ir al inicio</a>
        </div>
    </div>
</section>

<?php if (!empty($sugeridos)): ?>
<section class="contenedor seccion">
    <div class="seccion__cabeza">
        <h2 class="titulo titulo--lg">Recién <em>llegados</em></h2>
    </div>
    <div class="rejilla rejilla--4">
        <?php foreach ($sugeridos as $p): ?>
            <?php require __DIR__ . '/Parciales/TarjetaProducto.php'; ?>
        <?php endforeach; ?>
    </div>
</section>
<?php endif; ?>
