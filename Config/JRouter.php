<?php

/**
 * JRouter.php — Decide qué controlador y método atiende cada URL
 *
 * Dos formas de llegar a un método:
 *
 *   A) Rutas limpias de la tienda (la tienda es la ruta principal):
 *        /                      → TiendaController::index()
 *        /catalogo/maquillaje   → TiendaController::catalogo('maquillaje')
 *        /producto/buttermelt   → TiendaController::producto('buttermelt')
 *        /carrito               → TiendaController::carrito()
 *
 *   B) Panel administrativo, con prefijo /admin:
 *        /admin                       → AdminDashboardController::index()
 *        /admin/productos             → AdminProductosController::index()
 *        /admin/productos/editar/UUID → AdminProductosController::editar('UUID')
 *
 *   C) Convención general Controlador/metodo/parametros, para los módulos
 *      que vengan después (API de sincronización con el POS):
 *        /Api/productos         → ApiController::productos()
 *
 * SEGURIDAD:
 *   - Solo se instancian clases cuyo archivo existe en /Controllers/.
 *   - Solo se ejecutan métodos públicos, no estáticos y no mágicos:
 *     un método private como render() no es alcanzable desde la URL.
 */
class JRouter
{
    private static string $defaultController = 'TiendaController';
    private static string $defaultMethod     = 'index';

    // Primer segmento de URL que se atiende directo en TiendaController.
    // Lista explícita: agregar una página pública nueva es agregarla aquí.
    private static array $rutasTienda = ['catalogo', 'producto', 'carrito'];

    private static ?array $controllerWhitelist = null;

    // ─────────────────────────────────────────────
    // PUNTO DE ENTRADA
    // ─────────────────────────────────────────────
    public static function run(JRequest $request): void
    {
        [$controllerName, $methodName, $params] = self::resolve($request->getSegments());

        if (!in_array($controllerName, self::getControllerWhitelist(), true)
            || !class_exists($controllerName)) {
            self::notFound();
            return;
        }

        if (!self::esMetodoEjecutable($controllerName, $methodName)) {
            self::notFound();
            return;
        }

        $controller = new $controllerName();

        // Más segmentos en la URL que parámetros acepta el método → 404,
        // en vez de un error fatal por argumentos de más.
        $aceptados = (new ReflectionMethod($controller, $methodName))->getNumberOfParameters();
        if (count($params) > $aceptados) {
            self::notFound();
            return;
        }

        $controller->$methodName(...$params);
    }

    // ─────────────────────────────────────────────
    // RESOLUCIÓN — de segmentos a [controlador, método, parámetros]
    // ─────────────────────────────────────────────
    private static function resolve(array $segments): array
    {
        // URL vacía → portada de la tienda
        if (empty($segments)) {
            return [self::$defaultController, self::$defaultMethod, []];
        }

        $primero = strtolower($segments[0]);

        // A) Ruta limpia de la tienda: el primer segmento ES el método
        if (in_array($primero, self::$rutasTienda, true)) {
            return [self::$defaultController, $primero, array_slice($segments, 1)];
        }

        // B) Panel: /admin/{modulo}/{metodo}/{parametros}
        if ($primero === 'admin') {
            $modulo = preg_replace('/[^a-z0-9]/', '', strtolower($segments[1] ?? 'dashboard'));
            $modulo = $modulo !== '' ? $modulo : 'dashboard';
            $method = preg_replace('/[^A-Za-z0-9_]/', '', $segments[2] ?? '');

            return [
                'Admin' . ucfirst($modulo) . 'Controller',
                $method !== '' ? lcfirst($method) : self::$defaultMethod,
                array_slice($segments, 3),
            ];
        }

        // C) Convención Controlador/metodo/parametros
        $controller = preg_replace('/[^A-Za-z0-9_]/', '', $segments[0]);
        $controller = ucfirst(strtolower($controller)) . 'Controller';

        $method = preg_replace('/[^A-Za-z0-9_]/', '', $segments[1] ?? '');
        $method = $method !== '' ? lcfirst($method) : self::$defaultMethod;

        return [$controller, $method, array_slice($segments, 2)];
    }

    // ─────────────────────────────────────────────
    // WHITELIST DE CONTROLADORES
    // Se arma una vez por petición leyendo /Controllers/.
    // ─────────────────────────────────────────────
    private static function getControllerWhitelist(): array
    {
        if (self::$controllerWhitelist !== null) {
            return self::$controllerWhitelist;
        }

        $whitelist = [];
        foreach (glob(CONTROLLERS_PATH . '*Controller.php') ?: [] as $file) {
            $className = basename($file, '.php');
            // Las clases base abstractas (AdminBaseController) no son destino de URL
            if (preg_match('/^[A-Za-z0-9_]+Controller$/', $className)
                && !str_contains($className, 'Base')) {
                $whitelist[] = $className;
            }
        }

        return self::$controllerWhitelist = $whitelist;
    }

    // ─────────────────────────────────────────────
    // ¿SE PUEDE LLAMAR ESTE MÉTODO DESDE UNA URL?
    // Público, no estático y sin prefijo "__" (constructores y mágicos fuera).
    // ─────────────────────────────────────────────
    private static function esMetodoEjecutable(string $controllerName, string $methodName): bool
    {
        if ($methodName === '' || str_starts_with($methodName, '__')
            || !method_exists($controllerName, $methodName)) {
            return false;
        }

        $reflection = new ReflectionMethod($controllerName, $methodName);

        return $reflection->isPublic() && !$reflection->isStatic();
    }

    // ─────────────────────────────────────────────
    // 404
    // La respuesta no revela nombres de clases ni métodos, ni en desarrollo:
    // ese detalle le sirve a quien explora el sitio buscando rutas.
    // ─────────────────────────────────────────────
    public static function notFound(): void
    {
        http_response_code(404);

        if (class_exists('TiendaController')) {
            (new TiendaController())->noEncontrado();
            return;
        }

        echo '<h1>404 | Página no encontrada</h1>';
    }
}
