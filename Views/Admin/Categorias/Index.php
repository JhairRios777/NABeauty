<?php
/**
 * Categorías: cada una es un formulario pequeño (editar en el lugar) y
 * al final va el de "nueva". $categorias: CategoriaEntity[] con total_productos.
 */
$campos = function (CategoriaEntity $c): void { ?>
    <label class="campo cat__nombre">
        <span class="campo__etiqueta">Nombre</span>
        <input type="text" name="nombre" value="<?= e($c->nombre) ?>" maxlength="80" required>
    </label>
    <label class="campo cat__color">
        <span class="campo__etiqueta">Color</span>
        <input type="color" name="color" value="<?= e($c->colorSeguro()) ?>">
    </label>
    <label class="campo cat__orden">
        <span class="campo__etiqueta">Orden</span>
        <input type="number" name="orden" value="<?= (int) $c->orden ?>" min="0" max="999" inputmode="numeric">
    </label>
    <label class="interruptor cat__activa">
        <input type="checkbox" name="activa" value="1" <?= $c->activa ? 'checked' : '' ?>>
        <span>Visible en la tienda</span>
    </label>
<?php };
$nueva = new CategoriaEntity();
?>
<section class="encabezado">
    <div>
        <h2 class="encabezado__titulo"><?= count($categorias) ?> categorías</h2>
        <p class="encabezado__sub">El orden define cómo aparecen en el menú y en la portada de la tienda.</p>
    </div>
</section>

<ul class="categorias">
    <?php foreach ($categorias as $c): ?>
        <li class="tarjeta cat<?= $c->activa ? '' : ' cat--oculta' ?>" style="--color: <?= e($c->colorSeguro()) ?>">
            <form class="cat__form" method="post" action="<?= e(url('admin/categorias/guardar/' . $c->uuid)) ?>" data-un-envio>
                <input type="hidden" name="_csrf" value="<?= e($csrf) ?>">
                <div class="cat__cabeza">
                    <span class="cat__muestra" aria-hidden="true"></span>
                    <strong><?= e($c->nombre) ?></strong>
                    <small><?= numero($c->total_productos) ?> producto<?= $c->total_productos === 1 ? '' : 's' ?> activo<?= $c->total_productos === 1 ? '' : 's' ?></small>
                    <a class="enlace" href="<?= e(url('admin/productos') . '?' . http_build_query(['categoria' => $c->uuid])) ?>">Ver productos</a>
                </div>
                <div class="cat__campos">
                    <?php $campos($c); ?>
                    <button class="btn btn--secundario" type="submit">Guardar</button>
                </div>
                <?php if (!$c->activa && $c->total_productos > 0): ?>
                    <p class="ayuda ayuda--alerta"><?= icono('alerta') ?> Está oculta: sus <?= $c->total_productos ?> productos no se ven en la tienda.</p>
                <?php endif; ?>
            </form>
        </li>
    <?php endforeach; ?>

    <li class="tarjeta cat cat--nueva">
        <form class="cat__form" method="post" action="<?= e(url('admin/categorias/guardar')) ?>" data-un-envio>
            <input type="hidden" name="_csrf" value="<?= e($csrf) ?>">
            <div class="cat__cabeza"><strong><?= icono('mas') ?> Nueva categoría</strong></div>
            <div class="cat__campos">
                <?php $nueva->orden = count($categorias) + 1; $nueva->color = '#F6D5DC'; $campos($nueva); ?>
                <button class="btn btn--primario" type="submit">Crear</button>
            </div>
        </form>
    </li>
</ul>
