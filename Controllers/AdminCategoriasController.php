<?php

/**
 * AdminCategoriasController — Categorías de la tienda (requiere web.productos).
 *
 *   GET  /admin/categorias
 *   POST /admin/categorias/guardar[/{uuid}]
 */
class AdminCategoriasController extends AdminBaseController
{
    private CategoriaModel $categorias;

    public function __construct()
    {
        parent::__construct();
        $this->exigir(PERM_PRODUCTOS);
        $this->categorias = new CategoriaModel();
    }

    public function index(): void
    {
        $this->render('Categorias/Index.php', [
            'pageTitle'  => 'Categorías',
            'seccion'    => 'categorias',
            'categorias' => $this->categorias->findTodas(),
        ]);
    }

    public function guardar(string $uuid = ''): void
    {
        $this->exigirPost('admin/categorias');

        $categoria = $uuid !== '' ? $this->categorias->find($uuid) : new CategoriaEntity();
        if ($categoria === null) {
            $this->flash('error', 'Esa categoría no existe.');
            $this->redirigir('admin/categorias');
        }

        $categoria->nombre = is_string($_POST['nombre'] ?? null) ? mb_substr(trim(preg_replace('/\s+/u', ' ', $_POST['nombre'])), 0, 80) : '';
        $categoria->color  = is_string($_POST['color'] ?? null) ? trim($_POST['color']) : '';
        $categoria->orden  = (int) ($_POST['orden'] ?? 0);
        $categoria->activa = !empty($_POST['activa']) ? 1 : 0;

        $errores = $categoria->validar();
        if (!empty($errores)) {
            $this->flash('error', $errores);
            $this->redirigir('admin/categorias');
        }

        $nueva = $categoria->uuid === null;
        $this->categorias->guardar($categoria);
        $this->flash('ok', $nueva ? 'Categoría «' . $categoria->nombre . '» creada.' : 'Categoría actualizada.');
        $this->redirigir('admin/categorias');
    }
}
