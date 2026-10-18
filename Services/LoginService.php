<?php

/**
 * LoginService — Verifica credenciales del panel con límite de intentos.
 *
 * Reglas:
 *   - LOGIN_MAX_INTENTOS fallos en LOGIN_VENTANA_MIN minutos bloquean
 *     esa IP y ese usuario (ver LoginIntentoModel).
 *   - El mensaje de error es el mismo si el usuario no existe o si la
 *     contraseña está mal: no revela qué usuarios existen.
 *   - Cuando el usuario no existe igual se ejecuta password_verify contra
 *     un hash falso, para que la respuesta tarde lo mismo en ambos casos.
 */
final class LoginService
{
    // Hash bcrypt de una cadena aleatoria: solo sirve para igualar tiempos
    private const HASH_FALSO = '$2y$12$yHBqmeniKw55fOBbwgZb6OPKrHcZx36O2.WEucKDhXUC0IeJFqdha';

    public function __construct(
        private UsuarioModel $usuarios = new UsuarioModel(),
        private LoginIntentoModel $intentos = new LoginIntentoModel(),
    ) {}

    /**
     * @throws DomainException con el mensaje para la pantalla de login
     */
    public function intentar(string $usuario, string $password, string $ip): UsuarioEntity
    {
        $usuario = mb_strtolower(trim($usuario), 'UTF-8');

        if ($usuario === '' || $password === '') {
            throw new DomainException('Escribe tu usuario y tu contraseña.');
        }
        if (mb_strlen($usuario) > 50 || strlen($password) > 200) {
            throw new DomainException('Usuario o contraseña incorrectos.');
        }

        $minutos = $this->intentos->minutosBloqueo($ip, $usuario);
        if ($minutos > 0) {
            throw new DomainException(
                'Demasiados intentos fallidos. Espera ' . $minutos . ' minuto' . ($minutos === 1 ? '' : 's') . ' y vuelve a intentarlo.'
            );
        }

        $encontrado = $this->usuarios->findParaLogin($usuario);
        $valido = password_verify($password, $encontrado?->password_hash ?? self::HASH_FALSO);

        if ($encontrado === null || !$valido) {
            $this->intentos->registrar($ip, $usuario, false);
            throw new DomainException('Usuario o contraseña incorrectos.');
        }

        // Contraseña correcta pero el POS no le dio acceso a la web
        if (!$encontrado->tienePermiso(PERM_ACCESO)) {
            $this->intentos->registrar($ip, $usuario, false);
            throw new DomainException('Tu usuario no tiene acceso al panel web. Pídelo desde el sistema de la tienda (POS).');
        }

        $this->intentos->registrar($ip, $usuario, true);
        $this->usuarios->registrarAcceso((string) $encontrado->uuid);
        $encontrado->password_hash = null;   // el hash no sale de aquí

        return $encontrado;
    }
}
