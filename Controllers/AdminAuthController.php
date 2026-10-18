<?php

/**
 * AdminAuthController — Entrar y salir del panel.
 *
 *   GET  /admin/auth         formulario
 *   POST /admin/auth/entrar  verifica (LoginService)
 *   POST /admin/auth/salir   cierra la sesión (POST + CSRF: un enlace o
 *                            una imagen en otra página no puede sacarte)
 */
class AdminAuthController extends AdminBaseController
{
    public function index(): void
    {
        if ($this->usuario !== null) {
            $this->redirigir('admin');
        }

        $this->render('Auth/Login.php', [
            'pageTitle' => 'Entrar',
            'sinMenu'   => true,
            'ultimo'    => (string) ($_SESSION['ultimo_usuario'] ?? ''),
        ]);
    }

    public function entrar(): void
    {
        $this->exigirPost('admin/auth');

        $usuarioTxt = is_string($_POST['usuario'] ?? null) ? $_POST['usuario'] : '';
        $password   = is_string($_POST['password'] ?? null) ? $_POST['password'] : '';

        try {
            $usuario = (new LoginService())->intentar($usuarioTxt, $password, Auth::ip());
        } catch (DomainException $e) {
            $_SESSION['ultimo_usuario'] = mb_substr(trim($usuarioTxt), 0, 50);
            $this->flash('error', $e->getMessage());
            $this->redirigir('admin/auth');
        }

        $volver = (string) ($_SESSION['volver'] ?? 'admin');
        Auth::entrar($usuario);   // regenera la sesión (borra "volver" y "ultimo_usuario")

        $this->redirigir(str_starts_with($volver, 'admin') ? $volver : 'admin');
    }

    public function salir(): void
    {
        $this->exigirPost('admin');

        Auth::cerrarSesion();
        Auth::iniciarSesion();
        $this->flash('info', 'Cerraste sesión.');
        $this->redirigir('admin/auth');
    }
}
