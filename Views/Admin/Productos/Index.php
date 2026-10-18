<?php
/**
 * Listado de productos.
 *   $filtros, $categorias (CategoriaEntity[]), $resultado (items/total/pagina/paginas), $query
 * En el teléfono cada producto es una tarjeta; desde 720px, una fila de tabla.
 */
$items = $resultado['items'];
$urlPagina = fn(int $n) => url('admin/productos') . '?' . http_build_query($query + ['pagina' => $n]);
?>
<section class="encabezado">
    <div>
        <h2 class="encabezado__titulo"><?= numero($resultado['total']) ?> producto<?= $resultado['total'] === 1 ? '' : 's' ?></h2>
    </div>
    <a class="btn btn--primario" href="<?= e(url('admin/productos/nuevo')) ?>"><?= icono('mas') ?> Nuevo</a>
</section>

<form class="filtros" method="get" action="<?= e(url('admin/productos')) ?>" role="search" data-auto-enviar>
    <label class="filtros__buscar">
        <?= icono('buscar') ?>
        <input type="search" name="q" value="<?= e($filtros['q']) ?>" placeholder="Buscar por nombre, marca o SKU" aria-label="Buscar">
    </label>
    <select name="categoria" aria-label="Categoría">
        <option value="">Todas las categorías</option>
        <?php foreach ($categorias as $c): ?>
            <option value="<?= e($c->uuid) ?>" <?= $filtros['categoria_uuid'] === $c->uuid ? 'selected' : '' ?>><?= e($c->nombre) ?></option>
        <?php endforeach; ?>
    </select>
    <select name="estado" aria-label="Estado">
        <option value="">Activos y archivados</option>
        <option value="activo" <?= $filtros['estado'] === 'activo' ? 'selected' : '' ?>>Solo activos</option>
        <option value="archivado" <?= $filtros['estado'] === 'archivado' ? 'selected' : '' ?>>Solo archivados</option>
    </select>
    <select name="stock" aria-label="Existencias">
        <option value="">Cualquier stock</option>
        <option value="bajo" <?= $filtros['stock'] === 'bajo' ? 'selected' : '' ?>>Por agotarse (≤ <?= STOCK_BAJO ?>)</option>
        <option value="agotado" <?= $filtros['stock'] === 'agotado' ? 'selected' : '' ?>>Agotados</option>
    </select>
    <button class="btn btn--secundario filtros__enviar" type="submit">Filtrar</button>
    <?php if (!empty($query)): ?>
        <a class="enlace" href="<?= e(url('admin/productos')) ?>">Quitar filtros</a>
    <?php endif; ?>
</form>

<?php if (empty($items)): ?>
    <section class="vacio-grande">
        <?= icono('caja', 'ico--xl') ?>
        <h2><?= empty($query) ? 'Aún no hay productos' : 'Nada coincide con esos filtros' ?></h2>
        <?php if (empty($query)): ?>
            <a class="btn btn--primario" href="<?= e(url('admin/productos/nuevo')) ?>">Crear el primero</a>
        <?php endif; ?>
    </section>
<?php else: ?>
    <ul class="productos">
        <?php foreach ($items as $p): ?>
            <?php
            $stock   = $p->stockTotal();
            $foto    = $p->getImageUrl();
            $visible = $p->esVisibleEnTienda() && $p->categoria?->activa === 1;
            ?>
            <li class="producto-fila<?= $p->estado === 'archivado' ? ' producto-fila--archivado' : '' ?>">
                <a class="producto-fila__enlace" href="<?= e(url('admin/productos/editar/' . $p->uuid)) ?>">
                    <span class="producto-fila__foto" style="--color-foto: <?= e($p->colorFondo()) ?>">
                        <?php if ($foto): ?>
                            <img src="<?= e($foto) ?>" alt="" width="64" height="64" loading="lazy">
                        <?php else: ?>
                            <?= icono('foto') ?>
                        <?php endif; ?>
                    </span>
                    <span class="producto-fila__info">
                        <strong><?= e($p->nombre) ?></strong>
                        <small><?= e($p->marca) ?> · <?= e($p->categoria?->nombre ?? 'Sin categoría') ?></small>
                    </span>
                    <span class="producto-fila__precio">
                        <?= $p->tienePreciosDistintos() ? '<small>desde</small> ' : '' ?><?= e(precio($p->precioDesde())) ?>
                        <?php if ($p->tieneOferta()): ?><span class="chip chip--oferta">Oferta</span><?php endif; ?>
                    </span>
                    <span class="producto-fila__stock">
                        <span class="chip <?= $stock <= 0 ? 'chip--agotado' : ($stock <= STOCK_BAJO ? 'chip--bajo' : 'chip--ok') ?>">
                            <?= $stock <= 0 ? 'Agotado' : numero($stock) . ' en stock' ?>
                        </span>
                        <?php if (count($p->variantesActivas()) > 1): ?>
                            <small><?= count($p->variantesActivas()) ?> <?= e(['tono' => 'tonos', 'talla' => 'tallas', 'color' => 'colores', 'aroma' => 'aromas'][$p->tipoVariante()] ?? 'opciones') ?></small>
                        <?php endif; ?>
                    </span>
                    <span class="producto-fila__estado">
                        <?php if ($p->estado === 'archivado'): ?>
                            <span class="chip chip--neutro">Archivado</span>
                        <?php elseif ($visible): ?>
                            <span class="chip chip--ok"><?= icono('ojo') ?> En tienda</span>
                        <?php else: ?>
                            <span class="chip chip--neutro" title="Temporada sin stock o categoría apagada">Oculto</span>
                        <?php endif; ?>
                        <?php if ($p->destacado): ?><span class="chip chip--top"><?= icono('estrella') ?> Top</span><?php endif; ?>
                    </span>
                </a>
            </li>
        <?php endforeach; ?>
    </ul>

    <?php if ($resultado['paginas'] > 1): ?>
        <nav class="paginacion" aria-label="Páginas">
            <?php if ($resultado['pagina'] > 1): ?>
                <a class="btn btn--secundario" href="<?= e($urlPagina($resultado['pagina'] - 1)) ?>"><?= icono('izq') ?> Anterior</a>
            <?php endif; ?>
            <span>Página <?= $resultado['pagina'] ?> de <?= $resultado['paginas'] ?></span>
            <?php if ($resultado['pagina'] < $resultado['paginas']): ?>
                <a class="btn btn--secundario" href="<?= e($urlPagina($resultado['pagina'] + 1)) ?>">Siguiente <?= icono('der') ?></a>
            <?php endif; ?>
        </nav>
    <?php endif; ?>
<?php endif; ?>
