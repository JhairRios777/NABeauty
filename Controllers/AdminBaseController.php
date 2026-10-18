<?php

/**
 * AdminBaseController — Base de todos los controladores del panel /admin.
 *
 * Centraliza lo que ninguna pantalla del panel puede olvidar:
 *   - sesión propia del panel (Auth) y encabezados "no guardar en caché";
 *   - exigir(): sesión iniciada + permiso, o redirección/403;
 *   - exigirPost(): método POST + token CSRF válido;
 *   - render(): vista dentro del template del panel;
 *   - mensajes flash (se muestran una sola vez tras redirigir).
 *
 * Es abstracta y su nombre contiene "Base": el router nunca la ejecuta.
 */
abstract class AdminBaseController
{
    protected ?UsuarioEntity $usuario;

    public function __construct()
    {
        Auth::iniciarSesion();

        // Datos del negocio: el navegador no debe guardarlos en caché ni
        // mostrarlos con "atrás" después de cerrar sesión
        header('Cache-Control: no-store, private');
        header('Pragma: no-cache');
        header('X-Robots-Tag: noindex, nofollow');

        $this->usuario = Auth::usuario();
    }

    // ─────────────────────────────────────────────
    // ACCESO
    // ─────────────────────────────────────────────

    /** Exige sesión y permiso. Sin sesión → login; sin permiso → 403. */
    protected function exigir(string $permiso = PERM_ACCESO): UsuarioEntity
    {
        if ($this->usuario === null) {
            // Volver a la página pedida después de entrar (solo rutas del panel, solo GET)
            if (($_SERVER['REQUEST_METHOD'] ?? 'GET') === 'GET') {
                $_SESSION['volver'] = $this->rutaActual();
            }
            $this->redirigir('admin/auth');
        }

        if (!$this->usuario->tienePermiso($permiso)) {
            http_response_code(403);
            $this->render('Errores/SinPermiso.php', ['pageTitle' => 'Sin permiso']);
            exit;
        }

        return $this->usuario;
    }

    /**
     * Toda acción que cambia datos llega por POST con el token CSRF.
     * Si el formulario superó post_max_size, PHP entrega $_POST vacío:
     * se avisa con un mensaje claro en vez de "token inválido".
     */
    protected function exigirPost(string $volverA): void
    {
        if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') {
            http_response_code(405);
            header('Allow: POST');
            $this->redirigir($volverA);
        }

        if (empty($_POST) && (int) ($_SERVER['CONTENT_LENGTH'] ?? 0) > 0) {
            $this->flash('error', 'Lo que intentaste subir pesa demasiado. Sube menos fotos a la vez.');
            $this->redirigir($volverA);
        }

        if (!Auth::csrfValido($_POST['_csrf'] ?? null)) {
            $this->flash('error', 'La página estuvo abierta mucho tiempo. Vuelve a intentarlo.');
            $this->redirigir($volverA);
        }
    }

    // ─────────────────────────────────────────────
    // RESPUESTAS
    // ─────────────────────────────────────────────

    protected function render(string $vista, array $vars = []): void
    {
        $vars['usuario']   = $this->usuario;
        $vars['flash']     = $this->tomarFlash();
        $vars['csrf']      = Auth::csrfToken();
        $vars['seccion']   = $vars['seccion'] ?? '';
        $vars['pageTitle'] = $vars['pageTitle'] ?? 'Panel';

        extract($vars, EXTR_SKIP);

        ob_start();
        require VIEWS_PATH . 'Admin' . DS . str_replace('/', DS, $vista);
        $content = ob_get_clean();

        require TEMPLATE_PATH . 'Admin' . DS . 'index.php';
    }

    /** Redirección interna (303: el navegador la sigue con GET) y fin de la petición. */
    protected function redirigir(string $ruta, array $query = []): never
    {
        $destino = url($ruta) . ($query ? '?' . http_build_query($query) : '');
        header('Location: ' . $destino, true, 303);
        exit;
    }

    protected function flash(string $tipo, string|array $mensajes): void
    {
        foreach ((array) $mensajes as $m) {
            $_SESSION['flash'][] = ['tipo' => $tipo, 'texto' => (string) $m];
        }
    }

    private function tomarFlash(): array
    {
        $mensajes = $_SESSION['flash'] ?? [];
        unset($_SESSION['flash']);

        if (!empty($_SESSION['aviso'])) {
            $mensajes[] = ['tipo' => 'info', 'texto' => (string) $_SESSION['aviso']];
            unset($_SESSION['aviso']);
        }
        return $mensajes;
    }

    /** Ruta pedida, relativa a APP_URL (ej. "admin/reportes?periodo=mes"). */
    protected function rutaActual(): string
    {
        $uri  = (string) ($_SERVER['REQUEST_URI'] ?? '');
        $ruta = substr($uri, strlen(APP_URL));
        return str_starts_with($ruta, 'admin') ? $ruta : 'admin';
    }

    /** Parámetro GET de texto, recortado; '' si no viene o no es texto. */
    protected function get(string $clave, int $max = 100): string
    {
        $v = $_GET[$clave] ?? '';
        return is_string($v) ? mb_substr(trim($v), 0, $max, 'UTF-8') : '';
    }
}
