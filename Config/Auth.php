<?php

/**
 * Auth.php — Sesión del panel administrativo, permisos y CSRF.
 *
 * - La sesión solo se abre en rutas /admin: la tienda pública no envía
 *   cookies de sesión (más rápida y cacheable).
 * - La cookie vive solo bajo /admin, es HttpOnly y SameSite=Lax.
 * - En cada petición se recarga el usuario desde la base: si el POS lo
 *   desactiva o le quita un permiso, el cambio aplica de inmediato.
 */
final class Auth
{
    private static ?UsuarioEntity $usuario = null;
    private static bool $cargado = false;

    // ─────────────────────────────────────────────
    // SESIÓN
    // ─────────────────────────────────────────────
    public static function iniciarSesion(): void
    {
        if (session_status() === PHP_SESSION_ACTIVE) {
            return;
        }

        $https = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off')
              || (($_SERVER['HTTP_X_FORWARDED_PROTO'] ?? '') === 'https');

        session_name(ADMIN_SESSION_NAME);
        session_set_cookie_params([
            'lifetime' => 0,                       // se borra al cerrar el navegador
            'path'     => APP_URL . 'admin',
            'secure'   => $https,
            'httponly' => true,
            'samesite' => 'Lax',
        ]);
        ini_set('session.use_strict_mode', '1');  // rechaza IDs de sesión inventados
        session_start();

        // Cierre por inactividad
        $ahora = time();
        if (isset($_SESSION['ultima_actividad']) && $ahora - $_SESSION['ultima_actividad'] > ADMIN_INACTIVIDAD) {
            self::cerrarSesion();
            session_start();
            $_SESSION['aviso'] = 'Tu sesión se cerró por inactividad. Vuelve a entrar.';
        }
        $_SESSION['ultima_actividad'] = $ahora;
    }

    /**
     * Inicia sesión para un usuario ya verificado.
     * session_regenerate_id evita la fijación de sesión: el ID que tenía
     * el navegador antes de entrar deja de servir.
     */
    public static function entrar(UsuarioEntity $usuario): void
    {
        session_regenerate_id(true);
        $_SESSION = [
            'usuario_uuid'     => $usuario->uuid,
            'ultima_actividad' => time(),
            'csrf'             => bin2hex(random_bytes(32)),
        ];
        self::$usuario = $usuario;
        self::$cargado = true;
    }

    public static function cerrarSesion(): void
    {
        $_SESSION = [];
        if (ini_get('session.use_cookies')) {
            $p = session_get_cookie_params();
            setcookie(session_name(), '', time() - 3600, $p['path'], $p['domain'], $p['secure'], $p['httponly']);
        }
        session_destroy();
        self::$usuario = null;
        self::$cargado = true;
    }

    // ─────────────────────────────────────────────
    // USUARIO ACTUAL
    // ─────────────────────────────────────────────

    /** Usuario con sesión, activo y con acceso web; null en cualquier otro caso. */
    public static function usuario(): ?UsuarioEntity
    {
        if (self::$cargado) {
            return self::$usuario;
        }
        self::$cargado = true;

        $uuid = $_SESSION['usuario_uuid'] ?? null;
        if (!is_string($uuid)) {
            return null;
        }

        $usuario = (new UsuarioModel())->findActivoConPermisos($uuid);
        if ($usuario === null || !$usuario->tienePermiso(PERM_ACCESO)) {
            // Lo desactivaron o le quitaron el acceso web desde el POS
            unset($_SESSION['usuario_uuid']);
            return null;
        }

        return self::$usuario = $usuario;
    }

    public static function puede(string $permiso): bool
    {
        $u = self::usuario();
        return $u !== null && $u->tienePermiso($permiso);
    }

    // ─────────────────────────────────────────────
    // CSRF
    // Un token por sesión; todo formulario POST lo envía en un campo oculto.
    // Una página externa no puede leerlo, así que no puede falsificar
    // acciones (guardar, borrar, cerrar sesión) a nombre del usuario.
    // ─────────────────────────────────────────────
    public static function csrfToken(): string
    {
        if (empty($_SESSION['csrf'])) {
            $_SESSION['csrf'] = bin2hex(random_bytes(32));
        }
        return $_SESSION['csrf'];
    }

    public static function csrfValido(?string $token): bool
    {
        return is_string($token) && !empty($_SESSION['csrf']) && hash_equals($_SESSION['csrf'], $token);
    }

    // ─────────────────────────────────────────────
    // IP DEL CLIENTE (para el límite de intentos)
    // Se usa REMOTE_ADDR: los encabezados X-Forwarded-For los puede
    // inventar cualquiera y servirían para saltarse el límite.
    // ─────────────────────────────────────────────
    public static function ip(): string
    {
        return substr((string) ($_SERVER['REMOTE_ADDR'] ?? '0.0.0.0'), 0, 45);
    }
}
