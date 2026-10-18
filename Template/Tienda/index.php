<?php
/**
 * Template de la tienda — cabecera, pie y el hueco donde va cada vista.
 *
 * Variables que recibe (las arma TiendaController::render):
 *   $content          HTML de la vista ya renderizado
 *   $pageTitle        título de la página
 *   $metaDescripcion  descripción para buscadores
 *   $canonical        ruta oficial de la página (se le antepone APP_ORIGIN)
 *   $categoriasNav    CategoriaEntity[] para el menú
 *   $noIndexar        true en carrito y 404
 *
 * No hay <script> ni <style> en línea: la CSP del sitio los bloquea.
 * Los datos que necesita el JS viajan en atributos data-* del <body>.
 */
$rutaActual = strtolower(trim((string) ($_GET['url'] ?? ''), '/'));
?>
<!doctype html>
<html lang="es-HN">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title><?= e($pageTitle) ?> | <?= e(APP_NAME) ?></title>
    <?php if (!empty($metaDescripcion)): ?>
        <meta name="description" content="<?= e($metaDescripcion) ?>">
    <?php endif; ?>
    <?php if ($noIndexar): ?>
        <meta name="robots" content="noindex, follow">
    <?php endif; ?>
    <link rel="canonical" href="<?= e(APP_ORIGIN . $canonical) ?>">
    <meta name="theme-color" content="#9E1B2A">

    <!-- Datos para compartir en WhatsApp / redes -->
    <meta property="og:site_name" content="<?= e(APP_NAME) ?>">
    <meta property="og:title" content="<?= e($pageTitle) ?>">
    <meta property="og:type" content="website">
    <meta property="og:url" content="<?= e(APP_ORIGIN . $canonical) ?>">

    <link rel="icon" type="image/png" href="<?= e(asset('Demo/img/logo.png')) ?>">

    <!-- La fuente de títulos se precarga para que el titular no "salte" al cargar -->
    <link rel="preload" href="<?= e(url('Content/Dist/fonts/anton-latin-400-normal.woff2')) ?>" as="font" type="font/woff2" crossorigin>
    <link rel="stylesheet" href="<?= e(asset('Dist/css/tienda.css')) ?>">
</head>
<body data-base="<?= e(APP_URL) ?>"
      data-stock-bajo="<?= (int) STOCK_BAJO ?>"
      data-wa="<?= e(WA_NUMBER) ?>"
      data-moneda="<?= e(MONEDA_SIMBOLO) ?>"
      data-tienda="<?= e(APP_NAME) ?>">

    <a class="saltar" href="#contenido">Saltar al contenido</a>

    <!-- ═════════ CABECERA ═════════ -->
    <header class="cabecera">
        <div class="contenedor cabecera__fila">
            <a class="cabecera__logo" href="<?= e(url()) ?>" aria-label="<?= e(APP_NAME) ?>, ir al inicio">
                <!-- Versión blanca del logo: el encabezado es vino -->
                <img src="<?= e(asset('Demo/img/logo-blanco.png')) ?>" alt="<?= e(APP_NAME) ?>" width="84" height="60">
            </a>

            <nav class="cabecera__nav" id="menu-principal" aria-label="Categorías">
                <?php foreach ($categoriasNav as $cat): ?>
                    <?php $activa = $rutaActual === 'catalogo/' . $cat->slug; ?>
                    <a class="nav-enlace<?= $activa ? ' nav-enlace--activo' : '' ?>"
                       href="<?= e($cat->url()) ?>"<?= $activa ? ' aria-current="page"' : '' ?>><?= e($cat->nombre) ?></a>
                <?php endforeach; ?>
                <a class="nav-enlace nav-enlace--oferta" href="<?= e(url('catalogo?oferta=1')) ?>">Ofertas</a>
            </nav>

            <div class="cabecera__acciones">
                <form class="buscador" action="<?= e(url('catalogo')) ?>" method="get" role="search">
                    <label class="solo-lector" for="buscar">Buscar productos o marcas</label>
                    <input id="buscar" type="search" name="q" placeholder="Buscar" maxlength="60" autocomplete="off">
                    <button type="submit" aria-label="Buscar">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="11" cy="11" r="7"></circle><path d="M20 20l-3.5-3.5"></path></svg>
                    </button>
                </form>

                <a class="btn btn--negro cabecera__carrito" href="<?= e(url('carrito')) ?>">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round" aria-hidden="true"><path d="M5 8h14l-1 12H6L5 8z"></path><path d="M9 8V6a3 3 0 0 1 6 0v2"></path></svg>
                    <span class="cabecera__carrito-texto">Carrito</span>
                    <!-- tienda.js escribe aquí la cantidad de artículos -->
                    <span class="contador" data-carrito-contador aria-live="polite">0</span>
                </a>

                <button class="btn-icono cabecera__menu" type="button" aria-controls="menu-principal" aria-expanded="false" data-menu-boton>
                    <span class="solo-lector">Abrir menú</span>
                    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M4 7h16M4 12h16M4 17h16"></path></svg>
                </button>
            </div>
        </div>
    </header>

    <!-- ═════════ CONTENIDO DE CADA VISTA ═════════ -->
    <main id="contenido">
        <?= $content ?>
    </main>

    <!-- ═════════ PIE ═════════ -->
    <footer class="contenedor">
        <div class="pie">
            <div class="pie__arriba">
                <div class="pie__llamado">
                    <h2 class="titulo titulo--md">Entérate primero de los nuevos ingresos</h2>
                    <!-- Redes de la tienda como iconos. Sin texto visible: el nombre de
                         cada enlace va en aria-label (lectores de pantalla) y en title
                         (aparece al pasar el cursor). -->
                    <div class="redes">
                        <?php if (WA_NUMBER !== ''): ?>
                            <a class="red" href="<?= e(whatsapp_url(WA_NUMBER, 'Hola, vengo de la tienda en línea de ' . APP_NAME . '.')) ?>"
                               target="_blank" rel="noopener" aria-label="WhatsApp de <?= e(APP_NAME) ?>" title="WhatsApp de <?= e(APP_NAME) ?>">
                                <span class="icono icono--whatsapp" aria-hidden="true"></span>
                            </a>
                        <?php endif; ?>
                        <a class="red" href="https://www.instagram.com/<?= e(INSTAGRAM) ?>/"
                           target="_blank" rel="noopener" aria-label="Instagram de <?= e(APP_NAME) ?>" title="Instagram de <?= e(APP_NAME) ?>">
                            <span class="icono icono--instagram" aria-hidden="true"></span>
                        </a>
                    </div>
                </div>

                <div class="pie__columnas">
                    <div class="pie__col">
                        <h3>Tienda</h3>
                        <?php foreach ($categoriasNav as $cat): ?>
                            <a href="<?= e($cat->url()) ?>"><?= e($cat->nombre) ?></a>
                        <?php endforeach; ?>
                    </div>
                    <div class="pie__col">
                        <h3>Compra</h3>
                        <a href="<?= e(url('catalogo')) ?>">Todo el catálogo</a>
                        <a href="<?= e(url('catalogo?oferta=1')) ?>">Ofertas</a>
                        <a href="<?= e(url('carrito')) ?>">Mi carrito</a>
                    </div>
                    <div class="pie__col">
                        <h3>Pedidos y envíos</h3>
                        <span>Pedidos por WhatsApp o DM</span>
                        <span><?= CIUDAD !== '' ? e(CIUDAD) . ', Honduras' : 'Envíos en Honduras' ?></span>
                    </div>
                </div>
            </div>

            <div class="pie__abajo">
                <span class="pie__marca" aria-hidden="true">N·A BEAUTY</span>
                <span class="pie__legal">Proverbios 16:3 · © <?= date('Y') ?> <?= e(APP_NAME) ?></span>
            </div>
        </div>

        <!-- Crédito del desarrollador: franja discreta bajo el pie de la tienda.
             Los datos salen de las constantes DESKCOD_* de Define.php. -->
        <div class="credito">
            <p>
                Tienda desarrollada por
                <a class="credito__marca" href="https://www.instagram.com/<?= e(DESKCOD_INSTAGRAM) ?>/" target="_blank" rel="noopener"><?= e(DESKCOD_NOMBRE) ?></a>
                · <?= e(DESKCOD_LEMA) ?>
            </p>
            <div class="redes redes--chicas">
                <a class="red" href="<?= e(whatsapp_url(DESKCOD_WA, 'Hola DeskCod, vi la tienda de ' . APP_NAME . ' y quiero información para mi negocio.')) ?>"
                   target="_blank" rel="noopener" aria-label="WhatsApp de <?= e(DESKCOD_NOMBRE) ?>" title="WhatsApp de <?= e(DESKCOD_NOMBRE) ?>">
                    <span class="icono icono--whatsapp" aria-hidden="true"></span>
                </a>
                <a class="red" href="https://www.instagram.com/<?= e(DESKCOD_INSTAGRAM) ?>/"
                   target="_blank" rel="noopener" aria-label="Instagram de <?= e(DESKCOD_NOMBRE) ?>" title="Instagram de <?= e(DESKCOD_NOMBRE) ?>">
                    <span class="icono icono--instagram" aria-hidden="true"></span>
                </a>
            </div>
        </div>
    </footer>

    <!-- Carrito flotante: en teléfono queda fijo abajo a la derecha y reemplaza
         al botón de la cabecera. En la página del carrito no se muestra
         (ya estás ahí). El contador lo actualiza tienda.js igual que el otro. -->
    <?php if ($rutaActual !== 'carrito'): ?>
        <a class="carrito-flotante" href="<?= e(url('carrito')) ?>" aria-label="Ver carrito">
            <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round" aria-hidden="true"><path d="M5 8h14l-1 12H6L5 8z"></path><path d="M9 8V6a3 3 0 0 1 6 0v2"></path></svg>
            <span class="contador contador--vino" data-carrito-contador>0</span>
        </a>
    <?php endif; ?>

    <!-- Aviso flotante ("Agregado al carrito") que maneja tienda.js -->
    <div class="aviso" data-aviso role="status" aria-live="polite" hidden></div>

    <script src="<?= e(asset('Dist/js/tienda.js')) ?>" defer></script>
</body>
</html>
