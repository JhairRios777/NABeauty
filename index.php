<?php

/**
 * index.php — Front Controller
 * Punto de entrada único: toda petición pasa por aquí.
 * N&A Beauty — DeskCod
 *
 * Tienda pública (/) y panel administrativo (/admin) sobre MySQL.
 * La sesión solo se abre en /admin (ver Config/Auth.php).
 */

// ─────────────────────────────────────────────
// 1. HEADERS DE SEGURIDAD HTTP
// ─────────────────────────────────────────────
header('X-Content-Type-Options: nosniff');
header('X-Frame-Options: SAMEORIGIN');
header('Referrer-Policy: strict-origin-when-cross-origin');
header('Permissions-Policy: geolocation=(), microphone=(), camera=(), payment=()');

// HSTS solo cuando la petición realmente entra por HTTPS
$esHttps = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off')
        || (($_SERVER['HTTP_X_FORWARDED_PROTO'] ?? '') === 'https');
if ($esHttps) {
    header('Strict-Transport-Security: max-age=31536000; includeSubDomains');
}

// Content-Security-Policy
//   - script-src 'self'  → SIN 'unsafe-inline': la tienda no usa <script> en
//     línea ni atributos onclick; todo el JS vive en Content/Dist/js. Un XSS
//     que logre inyectar HTML no puede ejecutar código.
//   - style-src permite 'unsafe-inline' solo por los atributos style="--color"
//     que pintan el color de cada categoría y de cada tono.
//   - Fuentes e imágenes son propias (no hay CDN): font-src e img-src 'self'.
//     blob: permite la vista previa de las fotos antes de subirlas (panel).
header(
    "Content-Security-Policy: default-src 'self'; "
    . "script-src 'self'; "
    . "style-src 'self' 'unsafe-inline'; "
    . "img-src 'self' data: blob:; "
    . "font-src 'self'; "
    . "connect-src 'self'; "
    . "frame-ancestors 'self'; "
    . "form-action 'self'; "
    . "base-uri 'self'; "
    . "object-src 'none'"
);

// ─────────────────────────────────────────────
// 2. CORE
// ─────────────────────────────────────────────
require_once __DIR__ . '/Config/Define.php';
require_once __DIR__ . '/Config/AutoLoad.php';
require_once __DIR__ . '/Config/JRequest.php';
require_once __DIR__ . '/Config/JRouter.php';

AutoLoad::run();

// ─────────────────────────────────────────────
// 3. ROUTER
// La URL vacía ("/") resuelve a TiendaController::index — la tienda es
// la ruta principal del sitio. El template lo aplica el propio controlador.
// ─────────────────────────────────────────────
JRouter::run(new JRequest());
