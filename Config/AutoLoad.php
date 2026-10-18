<?php

/**
 * AutoLoad.php — Cargador automático de clases
 * PHP resuelve cualquier clase por su nombre, sin require_once manuales.
 *
 * Las librerías de terceros (vendor/: PhpSpreadsheet, Dompdf) NO se cargan
 * aquí: solo las carga el servicio de exportación cuando se pide un
 * archivo. Así, si a XAMPP le falta una extensión que ellas exigen, la
 * tienda y el panel siguen funcionando y solo falla la descarga.
 */
class AutoLoad
{
    // Carpetas donde se buscan clases, en este orden
    private static array $directories = [
        CONFIG_PATH,
        CONTROLLERS_PATH,
        MODELS_PATH,
        ENTITY_PATH,
        SERVICES_PATH,
    ];

    public static function run(): void
    {
        spl_autoload_register(function (string $className): void {

            // Solo letras, números y guion bajo: el sistema no usa namespaces.
            // Corta cualquier intento de path traversal ("..", "/", "\").
            if (!preg_match('/^[A-Za-z0-9_]+$/', $className)) {
                return;
            }

            foreach (self::$directories as $directory) {
                $file = $directory . $className . '.php';

                if (is_file($file)) {
                    require_once $file;
                    return;
                }
            }
        });
    }

    /** Carga el autoload de Composer (vendor/) bajo demanda. */
    public static function vendor(): void
    {
        static $cargado = false;
        if (!$cargado) {
            require_once VENDOR_PATH . 'autoload.php';
            $cargado = true;
        }
    }
}
