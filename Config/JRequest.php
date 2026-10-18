<?php

/**
 * JRequest.php — Encapsula la petición HTTP entrante
 *
 * Responsabilidad única:
 *   1. Partir la URL que recibe el Front Controller en segmentos
 *   2. Exponer esos segmentos al JRouter
 *   3. Decir qué tipo de petición es (GET / POST / AJAX)
 *
 * NO sanea inputs. Política del sistema: se escapa al imprimir (en la vista),
 * no al recibir. Los controladores leen $_GET / $_POST y validan su tipo.
 */
class JRequest
{
    // "catalogo/maquillaje" → ['catalogo', 'maquillaje']
    private array $segments = [];

    public function __construct()
    {
        $url = $_GET['url'] ?? '';

        // Si llega un array (?url[]=x) se ignora en vez de romper con un warning
        $url = is_string($url) ? $url : '';
        $url = filter_var(trim($url, '/'), FILTER_SANITIZE_URL);

        $this->segments = ($url !== '' && $url !== false)
            ? array_values(array_filter(explode('/', $url), fn($s) => $s !== ''))
            : [];
    }

    public function getSegments(): array
    {
        return $this->segments;
    }

    public function getSegment(int $index): ?string
    {
        return $this->segments[$index] ?? null;
    }

    public function isPost(): bool
    {
        return ($_SERVER['REQUEST_METHOD'] ?? '') === 'POST';
    }

    public function isAjax(): bool
    {
        return strtolower($_SERVER['HTTP_X_REQUESTED_WITH'] ?? '') === 'xmlhttprequest';
    }
}
