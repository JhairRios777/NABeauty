<?php

/**
 * TiendaController — Tienda pública de N&A Beauty (ruta principal del sitio).
 *
 * Solo coordina: lee y valida lo que llega por URL, pide los datos a los
 * Models y entrega las entidades a la vista. No hay consultas ni reglas de
 * negocio aquí (esas viven en Models y Entities).
 *
 * Rutas:
 *   /                       index()
 *   /catalogo               catalogo()
 *   /catalogo/{categoria}   catalogo('{categoria}')
 *   /producto/{slug}        producto('{slug}')
 *   /carrito                carrito()   → cotización por WhatsApp
 */
class TiendaController
{
    private ProductoModel  $productoModel;
    private CategoriaModel $categoriaModel;

    public function __construct()
    {
        // Un solo CategoriaModel compartido: ProductoModel lo reutiliza y
        // las categorías se leen una vez por petición.
        $this->categoriaModel = new CategoriaModel();
        $this->productoModel  = new ProductoModel($this->categoriaModel);
    }

    // ─────────────────────────────────────────────
    // PORTADA
    // ─────────────────────────────────────────────
    public function index(): void
    {
        $this->render('Inicio.php', [
            'pageTitle'       => APP_SLOGAN,
            'metaDescripcion' => 'Maquillaje, skincare, ropa, zapatos, joyería y termos de moda. Envíos a todo Honduras.',
            'canonical'       => url(),
            'destacados'      => $this->productoModel->findDestacados(4),
            'nuevos'          => $this->productoModel->findNuevos(4),
        ]);
    }

    // ─────────────────────────────────────────────
    // CATÁLOGO — listado con filtros, orden y paginación
    // ─────────────────────────────────────────────
    public function catalogo(string $categoriaSlug = ''): void
    {
        // 1. Categoría de la URL. Si viene un slug que no existe → 404
        //    (no se muestra "todo el catálogo" bajo una URL inventada).
        $categoria = null;
        if ($categoriaSlug !== '') {
            $categoria = $this->categoriaModel->findBySlug(strtolower($categoriaSlug));
            if ($categoria === null) {
                $this->noEncontrado();
                return;
            }
        }

        // 2. Filtros del query string. Cada valor se valida contra lo que
        //    el sistema conoce; lo que no encaja se descarta en silencio.
        $marcasDisponibles = $this->productoModel->marcasDisponibles($categoria?->uuid);

        $marcas = $_GET['marca'] ?? [];
        $marcas = is_array($marcas) ? $marcas : [$marcas];
        $marcas = array_values(array_intersect($marcasDisponibles, array_filter($marcas, 'is_string')));

        $q = $_GET['q'] ?? '';
        $q = is_string($q) ? mb_substr(trim($q), 0, 60, 'UTF-8') : '';

        $orden = $_GET['orden'] ?? 'nuevos';
        $orden = (is_string($orden) && isset(ProductoModel::ORDENES[$orden])) ? $orden : 'nuevos';

        $soloDisponible = ($_GET['disponible'] ?? '') === '1';
        $soloOferta     = ($_GET['oferta'] ?? '') === '1';
        $pagina = $_GET['pagina'] ?? 1;
        $pagina = is_scalar($pagina) ? max(1, (int) $pagina) : 1;

        // 3. Búsqueda (toda la lógica de filtrado vive en el Model)
        $resultado = $this->productoModel->buscar([
            'categoria_uuid'  => $categoria?->uuid,
            'marcas'          => $marcas,
            'solo_disponible' => $soloDisponible,
            'solo_oferta'     => $soloOferta,
            'q'               => $q,
        ], $orden, $pagina);

        // 4. Query limpio (solo filtros activos) para armar los enlaces de
        //    paginación y de "quitar filtro" sin arrastrar basura de la URL.
        $query = array_filter([
            'q'          => $q,
            'marca'      => $marcas,
            'disponible' => $soloDisponible ? '1' : '',
            'oferta'     => $soloOferta ? '1' : '',
            'orden'      => $orden !== 'nuevos' ? $orden : '',
        ]);

        $urlBase = $categoria ? $categoria->url() : url('catalogo');

        $titulo = $categoria ? $categoria->nombre : ($soloOferta ? 'Ofertas' : 'Catálogo');
        if ($q !== '') {
            $titulo = 'Resultados para “' . $q . '”';
        }

        $this->render('Catalogo.php', [
            'pageTitle'         => $titulo,
            'metaDescripcion'   => 'Compra ' . mb_strtolower($titulo, 'UTF-8') . ' en ' . APP_NAME . '. Envíos a todo Honduras.',
            'canonical'         => $urlBase,
            'titulo'            => $titulo,
            'categoria'         => $categoria,
            'productos'         => $resultado['items'],
            'total'             => $resultado['total'],
            'pagina'            => $resultado['pagina'],
            'paginas'           => $resultado['paginas'],
            'marcasDisponibles' => $marcasDisponibles,
            'marcas'            => $marcas,
            'q'                 => $q,
            'orden'             => $orden,
            'ordenes'           => ProductoModel::ORDENES,
            'soloDisponible'    => $soloDisponible,
            'soloOferta'        => $soloOferta,
            'hayFiltros'        => !empty($marcas) || $soloDisponible || $soloOferta || $q !== '',
            'urlBase'           => $urlBase,
            'query'             => $query,
        ]);
    }

    // ─────────────────────────────────────────────
    // DETALLE DE PRODUCTO
    // ─────────────────────────────────────────────
    public function producto(string $slug = ''): void
    {
        $producto = $slug !== '' ? $this->productoModel->findBySlug(strtolower($slug)) : null;

        // Inexistente, archivado o de temporada agotado → 404 real.
        // El Model ya solo devuelve productos visibles en tienda.
        if ($producto === null) {
            $this->noEncontrado();
            return;
        }

        $this->render('Producto.php', [
            'pageTitle'       => $producto->nombre . ' · ' . $producto->marca,
            'metaDescripcion' => mb_substr((string) $producto->descripcion, 0, 155, 'UTF-8'),
            'canonical'       => $producto->url(),
            'producto'        => $producto,
            'relacionados'    => $this->productoModel->findRelacionados($producto, 4),
        ]);
    }

    // ─────────────────────────────────────────────
    // CARRITO — cotización por WhatsApp
    // El contenido del carrito vive en el navegador (localStorage) y lo
    // pinta tienda.js. Esta acción solo entrega la página: no recibe datos,
    // no registra pedidos y no descuenta stock.
    // ─────────────────────────────────────────────
    public function carrito(): void
    {
        $this->render('Carrito.php', [
            'pageTitle'       => 'Tu carrito',
            'metaDescripcion' => 'Revisa tu carrito y pide tu cotización por WhatsApp.',
            'canonical'       => url('carrito'),
            'noIndexar'       => true,
        ]);
    }

    // ─────────────────────────────────────────────
    // 404 — con el mismo diseño de la tienda
    // ─────────────────────────────────────────────
    public function noEncontrado(): void
    {
        http_response_code(404);

        $this->render('NoEncontrado.php', [
            'pageTitle'       => 'Página no encontrada',
            'metaDescripcion' => '',
            'canonical'       => url(),
            'sugeridos'       => $this->productoModel->findNuevos(4),
            'noIndexar'       => true,
        ]);
    }

    // ─────────────────────────────────────────────
    // RENDER — vista dentro del template de la tienda
    //
    // 1) Ejecuta la vista y guarda su HTML en $content.
    // 2) Carga el template, que imprime $content entre cabecera y pie.
    //
    // Es private: el router solo ejecuta métodos públicos, así que no se
    // puede pedir /Tienda/render desde el navegador.
    // ─────────────────────────────────────────────
    private function render(string $vista, array $vars = []): void
    {
        // Datos que el template necesita en todas las páginas
        $vars['categoriasNav'] = $this->categoriaModel->findActivas();
        $vars['noIndexar']     = $vars['noIndexar'] ?? false;

        // EXTR_SKIP: una clave de $vars nunca pisa una variable de este método
        extract($vars, EXTR_SKIP);

        ob_start();
        require VIEWS_PATH . 'Tienda' . DS . $vista;
        $content = ob_get_clean();

        require TEMPLATE_PATH . 'Tienda' . DS . 'index.php';
    }
}
