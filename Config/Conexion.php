<?php

/**
 * Conexion.php — Única conexión PDO por petición.
 *
 * Solo los Models la usan (vía BaseModel). Controladores y vistas nunca
 * tocan la base de datos.
 */
final class Conexion
{
    private static ?PDO $pdo = null;

    public static function obtener(): PDO
    {
        if (self::$pdo !== null) {
            return self::$pdo;
        }

        $dsn = sprintf('mysql:host=%s;port=%s;dbname=%s;charset=utf8mb4', DB_HOST, DB_PORT, DB_NAME);

        self::$pdo = new PDO($dsn, DB_USER, DB_PASS, [
            // Un error de SQL lanza excepción en vez de fallar en silencio
            PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
            // Sentencias preparadas reales en el servidor (no emuladas):
            // los parámetros viajan separados del SQL → sin inyección
            PDO::ATTR_EMULATE_PREPARES   => false,
            PDO::ATTR_STRINGIFY_FETCHES  => false,
        ]);

        // Misma hora que PHP: NOW() y CURDATE() coinciden con los
        // rangos "hoy / semana / mes" que calcula el sistema
        self::$pdo->exec("SET time_zone = '" . (new DateTime())->format('P') . "'");

        return self::$pdo;
    }
}
