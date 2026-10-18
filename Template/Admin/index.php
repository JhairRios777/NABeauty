<?php
/**
 * Template del panel administrativo.
 *
 * Variables (las arma AdminBaseController::render):
 *   $content    HTML de la vista
 *   $pageTitle  título
 *   $usuario    ?UsuarioEntity (null en el login)
 *   $flash      mensajes [['tipo' => ok|error|info, 'texto' => …]]
 *   $csrf       token para los formularios POST
 *   $seccion    ítem activo del menú
 *   $sinMenu    true en el login
 *
 * Diseño mobile-first: en el teléfono el menú es una barra inferior fija
 * (al alcance del pulgar); desde 960px pasa a barra lateral.
 * Sin JS ni CSS en línea (CSP).
 */
$sinMenu = $sinMenu ?? false;

$menu = [];
if ($usuario !== null) {
    $menu[] = ['inicio', 'Inicio', 'admin', 'inicio'];
    if ($usuario->tienePermiso(PERM_PRODUCTOS)) {
        $menu[] = ['productos', 'Productos', 'admin/productos', 'caja'];
        $menu[] = ['categorias', 'Categorías', 'admin/categorias', 'etiqueta'];
    }
    if ($usuario->tienePermiso(PERM_REPORTES)) {
        $menu[] = ['reportes', 'Reportes', 'admin/reportes', 'grafico'];
    }
}
?>
<!doctype html>
<html lang="es-HN">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
    <meta name="robots" content="noindex, nofollow">
    <meta name="theme-color" content="#9E1B2A">
    <title><?= e($pageTitle) ?> · Panel <?= e(APP_NAME) ?></title>
    <link rel="icon" type="image/png" href="<?= e(asset('Demo/img/logo.png')) ?>">
    <link rel="stylesheet" href="<?= e(asset('Dist/css/admin.css')) ?>">
    <script src="<?= e(asset('Dist/js/admin.js')) ?>" defer></script>
</head>
<body class="<?= $sinMenu ? 'adm adm--solo' : 'adm' ?>">
<?php require VIEWS_PATH . 'Admin' . DS . 'Parciales' . DS . 'Iconos.php'; ?>

<?php if (!$sinMenu && $usuario !== null): ?>
    <a class="saltar" href="#contenido">Saltar al contenido</a>

    <!-- Barra lateral (escritorio) / barra inferior (teléfono) -->
    <nav class="menu" aria-label="Panel">
        <a class="menu__marca" href="<?= e(url('admin')) ?>">
            <img src="<?= e(asset('Demo/img/logo-blanco.png')) ?>" alt="<?= e(APP_NAME) ?>" width="120" height="48">
        </a>
        <ul class="menu__lista">
            <?php foreach ($menu as [$clave, $texto, $ruta, $ico]): ?>
                <li>
                    <a class="menu__item" href="<?= e(url($ruta)) ?>" <?= $seccion === $clave ? 'aria-current="page"' : '' ?>>
                        <?= icono($ico) ?><span><?= e($texto) ?></span>
                    </a>
                </li>
            <?php endforeach; ?>
        </ul>
        <a class="menu__item menu__item--tienda" href="<?= e(url()) ?>" target="_blank" rel="noopener">
            <?= icono('tienda') ?><span>Ver tienda</span>
        </a>
    </nav>

    <header class="barra">
        <h1 class="barra__titulo"><?= e($pageTitle) ?></h1>

        <details class="cuenta">
            <summary class="cuenta__boton" aria-label="Mi cuenta">
                <span class="avatar" aria-hidden="true"><?= e($usuario->iniciales()) ?></span>
                <span class="cuenta__nombre"><?= e($usuario->primerNombre()) ?></span>
            </summary>
            <div class="cuenta__menu">
                <p class="cuenta__quien">
                    <strong><?= e($usuario->nombre) ?></strong>
                    <span><?= e($usuario->usuario) ?></span>
                </p>
                <a class="cuenta__op" href="<?= e(url()) ?>" target="_blank" rel="noopener"><?= icono('tienda') ?> Ver tienda</a>
                <form method="post" action="<?= e(url('admin/auth/salir')) ?>">
                    <input type="hidden" name="_csrf" value="<?= e($csrf) ?>">
                    <button class="cuenta__op" type="submit"><?= icono('salir') ?> Cerrar sesión</button>
                </form>
            </div>
        </details>
    </header>
<?php endif; ?>

<main id="contenido" class="contenido">
    <?php if (!empty($flash)): ?>
        <div class="avisos" role="status">
            <?php foreach ($flash as $m): ?>
                <p class="aviso aviso--<?= e($m['tipo']) ?>">
                    <?= icono($m['tipo'] === 'ok' ? 'check' : ($m['tipo'] === 'error' ? 'alerta' : 'ojo')) ?>
                    <span><?= e($m['texto']) ?></span>
                </p>
            <?php endforeach; ?>
        </div>
    <?php endif; ?>

    <?= $content ?>
</main>

<?php if (!$sinMenu): ?>
    <footer class="pie-adm">
        Panel <?= e(APP_NAME) ?> · hecho por
        <a href="<?= e(whatsapp_url(DESKCOD_WA)) ?>" target="_blank" rel="noopener"><?= e(DESKCOD_NOMBRE) ?></a>
    </footer>
<?php endif; ?>

<!-- Globo de los gráficos (lo posiciona admin.js) -->
<div class="tip" role="tooltip" hidden></div>
</body>
</html>
