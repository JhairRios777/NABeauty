<?php

/**
 * LoginIntentoModel — Límite de intentos de inicio de sesión.
 *
 * Se bloquea por IP Y por nombre de usuario: así no sirve probar muchas
 * contraseñas desde una IP, ni atacar la cuenta de Nataly desde muchas IPs.
 */
class LoginIntentoModel extends BaseModel
{
    /**
     * ¿Hay demasiados intentos fallidos recientes para esta IP o este usuario?
     * Devuelve los minutos que faltan para poder intentar de nuevo (0 = libre).
     */
    public function minutosBloqueo(string $ip, string $usuario): int
    {
        $desde = date('Y-m-d H:i:s', time() - LOGIN_VENTANA_MIN * 60);

        $fila = $this->fila(
            'SELECT
                SUM(ip = ?)      AS por_ip,
                SUM(usuario = ?) AS por_usuario,
                MIN(fecha)       AS primero
               FROM login_intentos
              WHERE exitoso = 0 AND fecha >= ? AND (ip = ? OR usuario = ?)',
            [$ip, $usuario, $desde, $ip, $usuario]
        );

        $intentos = max((int) ($fila['por_ip'] ?? 0), (int) ($fila['por_usuario'] ?? 0));
        if ($intentos < LOGIN_MAX_INTENTOS || empty($fila['primero'])) {
            return 0;
        }

        // Se libera cuando el intento más viejo de la ventana sale de ella
        $libre = strtotime($fila['primero']) + LOGIN_VENTANA_MIN * 60;
        return max(1, (int) ceil(($libre - time()) / 60));
    }

    public function registrar(string $ip, string $usuario, bool $exitoso): void
    {
        $this->ejecutar(
            'INSERT INTO login_intentos (ip, usuario, exitoso, fecha) VALUES (?, ?, ?, NOW())',
            [$ip, mb_substr($usuario, 0, 50, 'UTF-8'), $exitoso ? 1 : 0]
        );

        // Al entrar bien, los fallos previos de ese usuario dejan de contar
        if ($exitoso) {
            $this->ejecutar('DELETE FROM login_intentos WHERE usuario = ? AND exitoso = 0', [$usuario]);
        }

        // Limpieza ocasional: la tabla no crece sin límite
        if (random_int(1, 50) === 1) {
            $this->ejecutar('DELETE FROM login_intentos WHERE fecha < NOW() - INTERVAL 30 DAY');
        }
    }
}
