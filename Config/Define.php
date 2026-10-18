<?php

/**
 * Define.php — Constantes globales del sistema
 * N&A Beauty — DeskCod
 *
 * ÚNICO archivo donde se definen rutas y configuración base.
 * El resto del sistema consume estas constantes; nadie hardcodea rutas.
 */

// ─────────────────────────────────────────────
// 1. RUTAS DEL SISTEMA DE ARCHIVOS
// ─────────────────────────────────────────────
define('DS', DIRECTORY_SEPARATOR);

define('ROOT',             realpath(__DIR__ . DS . '..') . DS);
define('CONFIG_PATH',      ROOT . 'Config'      . DS);
define('CONTROLLERS_PATH', ROOT . 'Controllers' . DS);
define('MODELS_PATH',      ROOT . 'Models'      . DS);
define('ENTITY_PATH',      ROOT . 'Entity'      . DS);
define('VIEWS_PATH',       ROOT . 'Views'       . DS);
define('TEMPLATE_PATH',    ROOT . 'Template'    . DS);
define('CONTENT_PATH',     ROOT . 'Content'     . DS);
define('BD_PATH',          ROOT . 'BD'          . DS);
define('SERVICES_PATH',    ROOT . 'Services'    . DS);
define('VENDOR_PATH',      ROOT . 'vendor'      . DS);

// Fotos de producto que se suben desde el panel. Carpeta pública (la tienda
// las muestra) pero con su propio .htaccess que impide ejecutar scripts.
define('PRODUCT_IMAGE_DIR', CONTENT_PATH . 'Uploads' . DS . 'Productos' . DS);
define('PRODUCT_IMAGE_URL', 'Content/Uploads/Productos/');

// Zona horaria del negocio: fechas de reportes, "hoy", "esta semana", etc.
date_default_timezone_set('America/Tegucigalpa');

// ─────────────────────────────────────────────
// 2. URL BASE — se calcula sola
//
// APP_URL es solo la RUTA base ("/" o "/NABeauty/"), sin protocolo ni
// dominio. Todos los enlaces, css, js e imágenes salen como "/catalogo",
// "/Content/...": el navegador los completa con la dirección por la que
// entró el visitante.
//
// Por qué así: detrás de un túnel (VS Code, ngrok) o de un proxy, Apache
// no ve la dirección pública; ve "localhost" y "http". Si la URL llevara
// dominio, la clienta recibiría enlaces a http://localhost:8082 y la
// página le cargaría sin estilos. Con rutas relativas funciona igual en
// localhost, en subcarpeta, por túnel y en el dominio final.
// ─────────────────────────────────────────────
$base = rtrim(str_replace('\\', '/', dirname($_SERVER['SCRIPT_NAME'] ?? '/')), '/') . '/';

define('APP_URL', $base);

// Origen público del sitio, sin barra final (https://dominio.com). Solo se
// usa donde la URL DEBE ser absoluta: canonical y datos para compartir.
// En producción conviene fijarlo con la variable de entorno SITE_URL para
// no depender del encabezado Host que envía el visitante.
$protocol = ((!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off')
          || ($_SERVER['HTTP_X_FORWARDED_PROTO'] ?? '') === 'https') ? 'https' : 'http';
$host     = preg_replace('/[^A-Za-z0-9.\-:\[\]]/', '', (string) ($_SERVER['HTTP_HOST'] ?? 'localhost'));

define('APP_ORIGIN', rtrim(getenv('SITE_URL') ?: $protocol . '://' . $host, '/'));

// ─────────────────────────────────────────────
// 3. DATOS DE LA TIENDA
// Los valores vacíos son datos que aún debe dar la clienta.
// Se leen de variables de entorno si existen, para no dejar datos
// del negocio (ni, más adelante, credenciales) dentro del repositorio.
// ─────────────────────────────────────────────
define('APP_NAME',    getenv('APP_NAME') ?: 'N&A Beauty');
define('APP_SLOGAN',  'Todo lo que amas en un solo lugar');
define('APP_VERSION', '0.1.0');
define('APP_LOCALE',  'es_HN');

define('MONEDA_SIMBOLO', 'L');

// WhatsApp en formato internacional sin "+" ni espacios (504 + 8 dígitos).
// Es el número al que llegan los pedidos y las consultas de la tienda.
define('WA_NUMBER',  getenv('WA_NUMBER') ?: '50431523121');
define('INSTAGRAM',  'nya_beautystore_hn');
define('CIUDAD',     getenv('TIENDA_CIUDAD') ?: '');

// ─────────────────────────────────────────────
// 3b. CRÉDITO DEL DESARROLLADOR
// Aparece en la franja inferior del pie de todas las páginas.
// ─────────────────────────────────────────────
define('DESKCOD_NOMBRE',    'DeskCod');
define('DESKCOD_LEMA',      'Sistemas web y de escritorio');
define('DESKCOD_WA',        '50493429641');
define('DESKCOD_INSTAGRAM', 'deskcod_');

// Productos por página en el catálogo
define('CATALOGO_POR_PAGINA', 12);

// Con este stock o menos (y mayor que 0) la tarjeta dice "Últimas unidades"
define('STOCK_BAJO', 3);

// Un producto cuenta como "Nuevo" durante estos días desde su alta
define('DIAS_NUEVO', 30);

// ─────────────────────────────────────────────
// 3c. BASE DE DATOS
// Valores por defecto = XAMPP local (usuario root sin contraseña).
// En el servidor se definen con variables de entorno y un usuario propio
// con permisos solo sobre esta base, nunca root.
// ─────────────────────────────────────────────
define('DB_HOST',    getenv('DB_HOST') ?: '127.0.0.1');
define('DB_PORT',    getenv('DB_PORT') ?: '3306');
define('DB_NAME',    getenv('DB_NAME') ?: 'nabeauty');
define('DB_USER',    getenv('DB_USER') ?: 'root');
define('DB_PASS',    getenv('DB_PASS') !== false ? (string) getenv('DB_PASS') : '');

// ─────────────────────────────────────────────
// 3d. PANEL ADMINISTRATIVO
// ─────────────────────────────────────────────
define('ADMIN_SESSION_NAME', 'nabeauty_admin');
define('ADMIN_INACTIVIDAD',  2 * 60 * 60);   // cierra sesión tras 2 h sin actividad
define('LOGIN_MAX_INTENTOS', 5);             // intentos fallidos permitidos…
define('LOGIN_VENTANA_MIN',  15);            // …dentro de esta ventana (minutos)
define('IMAGEN_MAX_BYTES',   8 * 1024 * 1024);
define('IMAGEN_MAX_LADO',    1200);          // px del lado mayor al guardar

// Permisos que el POS asigna a cada usuario y que la web respeta
define('PERM_ACCESO',    'web.acceso');
define('PERM_PRODUCTOS', 'web.productos');
define('PERM_REPORTES',  'web.reportes');
define('PERM_EXPORTAR',  'web.exportar');

// ─────────────────────────────────────────────
// 4. ENTORNO
// 'development' muestra errores; 'production' los oculta y los loguea.
// ─────────────────────────────────────────────
define('APP_ENV', getenv('APP_ENV') ?: 'development');

if (APP_ENV === 'development') {
    ini_set('display_errors', '1');
    error_reporting(E_ALL);
} else {
    ini_set('display_errors', '0');
    ini_set('log_errors', '1');
    error_reporting(E_ALL);
}

// ─────────────────────────────────────────────
// 5. HELPERS GLOBALES DE VISTA
// ─────────────────────────────────────────────

/**
 * Escapa texto para imprimirlo en HTML.
 * Política del sistema: se escapa AL IMPRIMIR (en la vista), nunca al recibir.
 */
function e(?string $texto): string
{
    return htmlspecialchars((string) $texto, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
}

/**
 * Construye una URL interna a partir de una ruta relativa.
 * url('catalogo/maquillaje') → /catalogo/maquillaje  (o /NABeauty/catalogo/...)
 */
function url(string $ruta = ''): string
{
    return APP_URL . ltrim($ruta, '/');
}

/**
 * URL de un archivo estático con versión por fecha de modificación.
 * El "?v=" cambia solo cuando el archivo cambia, así el navegador puede
 * cachearlo un año sin servir nunca una versión vieja.
 */
function asset(string $ruta): string
{
    $ruta   = ltrim($ruta, '/');
    $fisica = CONTENT_PATH . str_replace('/', DS, $ruta);
    $v      = is_file($fisica) ? filemtime($fisica) : APP_VERSION;

    return APP_URL . 'Content/' . $ruta . '?v=' . $v;
}

/**
 * Formatea un monto en lempiras: 1234.5 → "L 1,234.50"
 */
function precio(float $monto): string
{
    return MONEDA_SIMBOLO . ' ' . number_format($monto, 2, '.', ',');
}

/**
 * Enlace directo a un chat de WhatsApp, con mensaje inicial opcional.
 * Se arma en el servidor para que el enlace funcione aunque el JS no cargue.
 * preg_replace deja solo dígitos: el número nunca puede romper la URL.
 */
function whatsapp_url(string $numero, string $mensaje = ''): string
{
    $url = 'https://wa.me/' . preg_replace('/\D/', '', $numero);

    return $mensaje !== '' ? $url . '?text=' . rawurlencode($mensaje) : $url;
}

/**
 * Número hondureño legible: "50431523121" → "+504 3152-3121".
 * Si no tiene la forma 504 + 8 dígitos se devuelve tal cual, sin adivinar.
 */
function telefono(string $numero): string
{
    $digitos = preg_replace('/\D/', '', $numero);

    return preg_match('/^504(\d{4})(\d{4})$/', $digitos, $m)
        ? '+504 ' . $m[1] . '-' . $m[2]
        : $numero;
}

/**
 * Convierte un texto en slug para URL: "Joyería Dorada" → "joyeria-dorada"
 */
function slugify(string $texto): string
{
    $texto = mb_strtolower($texto, 'UTF-8');
    $texto = strtr($texto, [
        'á' => 'a', 'é' => 'e', 'í' => 'i', 'ó' => 'o', 'ú' => 'u',
        'ñ' => 'n', 'ü' => 'u',
    ]);
    $texto = preg_replace('/[^a-z0-9\s-]/', '', $texto);
    $texto = preg_replace('/[\s-]+/', '-', $texto);

    return trim($texto, '-');
}

/**
 * Ícono del panel: referencia al sprite SVG que imprime el template.
 * icono('caja') → <svg class="ico"><use href="#i-caja"></use></svg>
 */
function icono(string $nombre, string $clase = ''): string
{
    $nombre = preg_replace('/[^a-z0-9-]/', '', $nombre);
    return '<svg class="ico' . ($clase !== '' ? ' ' . e($clase) : '') . '" aria-hidden="true" focusable="false">'
         . '<use href="#i-' . $nombre . '"></use></svg>';
}

/** Número con separador de miles: 1234 → "1,234" */
function numero(float|int $n, int $decimales = 0): string
{
    return number_format($n, $decimales, '.', ',');
}

/** Fecha y hora cortas para tablas: "18/10/2026 14:33" */
function fecha_hora(?string $fecha, bool $conHora = true): string
{
    if (empty($fecha) || ($t = strtotime($fecha)) === false) {
        return '—';
    }
    return date($conHora ? 'd/m/Y H:i' : 'd/m/Y', $t);
}
