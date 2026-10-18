<?php
/**
 * Catálogo — listado con filtros, orden y paginación.
 *
 * Espera (todo ya validado por TiendaController::catalogo):
 *   $titulo, $categoria (CategoriaEntity|null), $productos (ProductoEntity[]),
 *   $total, $pagina, $paginas, $marcasDisponibles, $marcas (seleccionadas),
 *   $q, $orden, $ordenes, $soloDisponible, $soloOferta, $hayFiltros,
 *   $urlBase (URL del listado sin filtros), $query (filtros activos)
 *
 * Los filtros son un formulario GET: funcionan sin JavaScript y cada
 * combinación tiene su propia URL, que se puede compartir.
 */

// Arma la URL del listado conservando los filtros activos y cambiando
// solo lo que se le pase (por ejemplo, la página).
$enlace = static function (array $cambios = []) use ($urlBase, $query): string {
    $params = array_filter(array_merge($query, $cambios), fn($v) => $v !== '' && $v !== null && $v !== []);
    return $urlBase . (empty($params) ? '' : '?' . http_build_query($params));
};
?>

<section class="contenedor encabezado-pagina">
    <nav class="migas" aria-label="Ruta de navegación">
        <a href="<?= e(url()) ?>">Inicio</a>
        <span aria-hidden="true">/</span>
        <?php if ($categoria): ?>
            <a href="<?= e(url('catalogo')) ?>">Catálogo</a>
            <span aria-hidden="true">/</span>
            <span aria-current="page"><?= e($categoria->nombre) ?></span>
        <?php else: ?>
            <span aria-current="page">Catálogo</span>
        <?php endif; ?>
    </nav>

    <h1 class="titulo titulo--lg"><?= e($titulo) ?></h1>

    <!-- Categorías como píldoras: cambiar de categoría con un toque -->
    <div class="pildoras" role="list">
        <a role="listitem" class="pildora<?= $categoria === null ? ' pildora--activa' : '' ?>" href="<?= e(url('catalogo')) ?>">Todo</a>
        <?php foreach ($categoriasNav as $cat): ?>
            <a role="listitem" class="pildora<?= ($categoria && $categoria->uuid === $cat->uuid) ? ' pildora--activa' : '' ?>"
               href="<?= e($cat->url()) ?>"><?= e($cat->nombre) ?></a>
        <?php endforeach; ?>
    </div>
</section>

<section class="contenedor catalogo">

    <!-- ═════════ FILTROS ═════════ -->
    <aside class="filtros" aria-label="Filtros">
        <!-- <details> abierto: sin JavaScript los filtros siempre se ven.
             En pantallas chicas tienda.js lo pliega al cargar, salvo que haya
             filtros aplicados. En escritorio el CSS lo deja fijo y abierto. -->
        <details class="filtros__caja" open data-filtros data-activos="<?= $hayFiltros ? '1' : '0' ?>">
            <summary class="filtros__resumen">
                Filtros
                <?php if ($hayFiltros): ?><span class="contador contador--vino"><?= count($marcas) + (int) $soloDisponible + (int) $soloOferta + (int) ($q !== '') ?></span><?php endif; ?>
            </summary>

            <form class="filtros__form" action="<?= e($urlBase) ?>" method="get">
                <?php if ($q !== ''): ?>
                    <input type="hidden" name="q" value="<?= e($q) ?>">
                <?php endif; ?>

                <?php if (!empty($marcasDisponibles)): ?>
                <fieldset>
                    <legend>Marca</legend>
                    <?php foreach ($marcasDisponibles as $marca): ?>
                        <label class="check">
                            <input type="checkbox" name="marca[]" value="<?= e($marca) ?>" <?= in_array($marca, $marcas, true) ? 'checked' : '' ?>>
                            <span><?= e($marca) ?></span>
                        </label>
                    <?php endforeach; ?>
                </fieldset>
                <?php endif; ?>

                <fieldset>
                    <legend>Disponibilidad</legend>
                    <label class="check">
                        <input type="checkbox" name="disponible" value="1" <?= $soloDisponible ? 'checked' : '' ?>>
                        <span>Solo con existencia</span>
                    </label>
                    <label class="check">
                        <input type="checkbox" name="oferta" value="1" <?= $soloOferta ? 'checked' : '' ?>>
                        <span>En oferta</span>
                    </label>
                </fieldset>

                <fieldset>
                    <legend><label for="orden">Ordenar por</label></legend>
                    <select id="orden" name="orden" class="campo">
                        <?php foreach ($ordenes as $clave => $texto): ?>
                            <option value="<?= e($clave) ?>" <?= $orden === $clave ? 'selected' : '' ?>><?= e($texto) ?></option>
                        <?php endforeach; ?>
                    </select>
                </fieldset>

                <div class="filtros__botones">
                    <button class="btn btn--negro" type="submit">Aplicar</button>
                    <?php if ($hayFiltros || $orden !== 'nuevos'): ?>
                        <a class="btn btn--borde" href="<?= e($urlBase) ?>">Limpiar</a>
                    <?php endif; ?>
                </div>
            </form>
        </details>

        <div class="ayuda">
            <strong>¿No encuentras algo?</strong>
            <p>Escríbenos y lo buscamos por ti.</p>
            <a href="#" data-wa-enlace data-wa-mensaje="Hola, estoy buscando un producto que no encuentro en la tienda.">Escribir por WhatsApp →</a>
        </div>
    </aside>

    <!-- ═════════ RESULTADOS ═════════ -->
    <div class="catalogo__lista">
        <p class="catalogo__conteo">
            <?= (int) $total ?> <?= $total === 1 ? 'producto' : 'productos' ?>
            <?php if ($paginas > 1): ?> · página <?= (int) $pagina ?> de <?= (int) $paginas ?><?php endif; ?>
        </p>

        <?php if (empty($productos)): ?>
            <!-- Estado vacío: dice qué pasó y ofrece la salida -->
            <div class="vacio">
                <h2 class="titulo titulo--md">No hay productos con esos filtros</h2>
                <p>Prueba quitando algún filtro o revisa todo el catálogo.</p>
                <a class="btn btn--vino" href="<?= e(url('catalogo')) ?>">Ver todo el catálogo</a>
            </div>
        <?php else: ?>
            <div class="rejilla rejilla--3">
                <?php foreach ($productos as $p): ?>
                    <?php require __DIR__ . '/Parciales/TarjetaProducto.php'; ?>
                <?php endforeach; ?>
            </div>
        <?php endif; ?>

        <?php if ($paginas > 1): ?>
            <nav class="paginacion" aria-label="Páginas del catálogo">
                <?php if ($pagina > 1): ?>
                    <a class="paginacion__paso" href="<?= e($enlace(['pagina' => $pagina - 1 > 1 ? $pagina - 1 : ''])) ?>" rel="prev">← Anterior</a>
                <?php endif; ?>

                <?php for ($n = 1; $n <= $paginas; $n++): ?>
                    <?php if ($n === $pagina): ?>
                        <span class="paginacion__num paginacion__num--actual" aria-current="page"><?= $n ?></span>
                    <?php else: ?>
                        <a class="paginacion__num" href="<?= e($enlace(['pagina' => $n > 1 ? $n : ''])) ?>" aria-label="Página <?= $n ?>"><?= $n ?></a>
                    <?php endif; ?>
                <?php endfor; ?>

                <?php if ($pagina < $paginas): ?>
                    <a class="paginacion__paso" href="<?= e($enlace(['pagina' => $pagina + 1])) ?>" rel="next">Siguiente →</a>
                <?php endif; ?>
            </nav>
        <?php endif; ?>
    </div>
</section>
