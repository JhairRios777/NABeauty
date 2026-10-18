<?php

/**
 * AdminProductosController — Catálogo desde el panel (requiere web.productos).
 *
 *   GET  /admin/productos                          listado con filtros
 *   GET  /admin/productos/nuevo                    formulario vacío
 *   GET  /admin/productos/editar/{uuid}            formulario
 *   POST /admin/productos/guardar[/{uuid}]         crea o actualiza (+ fotos)
 *   POST /admin/productos/eliminarImagen/{uuid}/{img}
 *   POST /admin/productos/principalImagen/{uuid}/{img}
 */
class AdminProductosController extends AdminBaseController
{
    private ProductoModel $productos;
    private CategoriaModel $categorias;

    public function __construct()
    {
        parent::__construct();
        $this->exigir(PERM_PRODUCTOS);
        $this->categorias = new CategoriaModel();
        $this->productos  = new ProductoModel($this->categorias);
    }

    public function index(): void
    {
        $categorias = $this->categorias->findTodas();
        $uuidsValidos = array_map(fn(CategoriaEntity $c) => $c->uuid, $categorias);

        $filtros = [
            'q'              => $this->get('q', 60),
            'categoria_uuid' => in_array($this->get('categoria'), $uuidsValidos, true) ? $this->get('categoria') : '',
            'estado'         => in_array($this->get('estado'), ['activo', 'archivado'], true) ? $this->get('estado') : '',
            'stock'          => in_array($this->get('stock'), ['agotado', 'bajo'], true) ? $this->get('stock') : '',
        ];
        $pagina = max(1, (int) $this->get('pagina', 5));

        $resultado = $this->productos->listarAdmin($filtros, $pagina);

        $this->render('Productos/Index.php', [
            'pageTitle'  => 'Productos',
            'seccion'    => 'productos',
            'filtros'    => $filtros,
            'categorias' => $categorias,
            'resultado'  => $resultado,
            'query'      => array_filter([
                'q' => $filtros['q'], 'categoria' => $filtros['categoria_uuid'],
                'estado' => $filtros['estado'], 'stock' => $filtros['stock'],
            ]),
        ]);
    }

    public function nuevo(): void
    {
        $producto = new ProductoEntity();
        $producto->variantes = [new VarianteEntity()];
        $this->formulario($producto);
    }

    public function editar(string $uuid = ''): void
    {
        $producto = $this->productos->findAdmin($uuid);
        if ($producto === null) {
            $this->flash('error', 'Ese producto no existe.');
            $this->redirigir('admin/productos');
        }
        $this->formulario($producto);
    }

    public function guardar(string $uuid = ''): void
    {
        $existente = null;
        if ($uuid !== '') {
            $existente = $this->productos->findAdmin($uuid);
            if ($existente === null) {
                $this->flash('error', 'Ese producto no existe.');
                $this->redirigir('admin/productos');
            }
        }
        $this->exigirPost($existente ? 'admin/productos/editar/' . $existente->uuid : 'admin/productos/nuevo');

        $r = (new ProductoService($this->productos, $this->categorias))->guardar($_POST, $_FILES, $existente);

        if (!empty($r['errores'])) {
            http_response_code(422);
            $this->formulario($r['producto'], $r['errores']);
            return;
        }

        $this->flash('ok', $existente ? 'Cambios guardados.' : 'Producto creado.');
        if (!empty($r['avisos'])) {
            $this->flash('error', $r['avisos']);
        }
        $this->redirigir('admin/productos/editar/' . $r['producto']->uuid);
    }

    public function eliminarImagen(string $uuid = '', string $imagen = ''): void
    {
        $this->exigirPost('admin/productos/editar/' . $uuid);

        $ok = BaseModel::esUuid($uuid) && BaseModel::esUuid($imagen)
            && (new ProductoService($this->productos, $this->categorias))->eliminarFoto($uuid, $imagen);

        $this->flash($ok ? 'ok' : 'error', $ok ? 'Foto eliminada.' : 'No se encontró esa foto.');
        $this->redirigir(BaseModel::esUuid($uuid) ? 'admin/productos/editar/' . $uuid : 'admin/productos');
    }

    public function principalImagen(string $uuid = '', string $imagen = ''): void
    {
        $this->exigirPost('admin/productos/editar/' . $uuid);

        $ok = BaseModel::esUuid($uuid) && BaseModel::esUuid($imagen)
            && (new ProductoImagenModel())->hacerPrincipal($uuid, $imagen);

        $this->flash($ok ? 'ok' : 'error', $ok ? 'Foto principal actualizada.' : 'No se encontró esa foto.');
        $this->redirigir(BaseModel::esUuid($uuid) ? 'admin/productos/editar/' . $uuid : 'admin/productos');
    }

    private function formulario(ProductoEntity $producto, array $errores = []): void
    {
        $this->render('Productos/Formulario.php', [
            'pageTitle'  => $producto->uuid ? 'Editar producto' : 'Nuevo producto',
            'seccion'    => 'productos',
            'producto'   => $producto,
            'errores'    => $errores,
            'categorias' => $this->categorias->findTodas(),
            'marcas'     => $this->productos->marcasTodas(),
            'maxFotos'   => ProductoImagenModel::MAX_POR_PRODUCTO,
        ]);
    }
}
