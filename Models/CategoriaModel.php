<?php

/**
 * CategoriaModel — tabla `categorias`.
 * Lectura para la tienda (solo activas) y mantenimiento desde el panel.
 */
class CategoriaModel extends BaseModel
{
    private const COLUMNAS = 'uuid, slug, nombre, color, orden, activa';

    /** @var CategoriaEntity[]|null caché por petición (la tienda la pide varias veces) */
    private ?array $activas = null;

    // ─────────────────────────────────────────────
    // TIENDA
    // ─────────────────────────────────────────────

    /** @return CategoriaEntity[] activas, por su campo `orden` */
    public function findActivas(): array
    {
        if ($this->activas === null) {
            $this->activas = array_map(
                fn(array $f) => CategoriaEntity::fromArray($f),
                $this->filas('SELECT ' . self::COLUMNAS . ' FROM categorias WHERE activa = 1 ORDER BY orden, nombre')
            );
        }
        return $this->activas;
    }

    public function findBySlug(string $slug): ?CategoriaEntity
    {
        foreach ($this->findActivas() as $c) {
            if ($c->slug === $slug) {
                return $c;
            }
        }
        return null;
    }

    /** Solo activas: la tienda no muestra productos de una categoría apagada. */
    public function findByUuid(string $uuid): ?CategoriaEntity
    {
        foreach ($this->findActivas() as $c) {
            if ($c->uuid === $uuid) {
                return $c;
            }
        }
        return null;
    }

    // ─────────────────────────────────────────────
    // PANEL
    // ─────────────────────────────────────────────

    /** @return CategoriaEntity[] todas, con cuántos productos activos tiene cada una */
    public function findTodas(): array
    {
        $filas = $this->filas(
            'SELECT c.uuid, c.slug, c.nombre, c.color, c.orden, c.activa,
                    COUNT(p.uuid) AS total_productos
               FROM categorias c
               LEFT JOIN productos p ON p.categoria_uuid = c.uuid AND p.estado = \'activo\'
              GROUP BY c.uuid, c.slug, c.nombre, c.color, c.orden, c.activa
              ORDER BY c.orden, c.nombre'
        );
        return array_map(fn(array $f) => CategoriaEntity::fromArray($f), $filas);
    }

    /** Cualquier estado (el panel también edita las inactivas). */
    public function find(string $uuid): ?CategoriaEntity
    {
        if (!self::esUuid($uuid)) {
            return null;
        }
        $fila = $this->fila('SELECT ' . self::COLUMNAS . ' FROM categorias WHERE uuid = ?', [$uuid]);
        return $fila ? CategoriaEntity::fromArray($fila) : null;
    }

    /**
     * Inserta o actualiza. El slug se genera del nombre y se vuelve único
     * (maquillaje, maquillaje-2…) para no romper URLs existentes de otra.
     * Al editar NO se cambia el slug: los enlaces compartidos siguen vivos.
     */
    public function guardar(CategoriaEntity $c): CategoriaEntity
    {
        if ($c->uuid === null) {
            $c->uuid = self::uuid();
            $c->slug = $this->slugUnico(slugify((string) $c->nombre));
            $this->ejecutar(
                'INSERT INTO categorias (uuid, slug, nombre, color, orden, activa) VALUES (?, ?, ?, ?, ?, ?)',
                [$c->uuid, $c->slug, trim((string) $c->nombre), strtoupper((string) $c->color), $c->orden, $c->activa]
            );
        } else {
            $this->ejecutar(
                'UPDATE categorias SET nombre = ?, color = ?, orden = ?, activa = ? WHERE uuid = ?',
                [trim((string) $c->nombre), strtoupper((string) $c->color), $c->orden, $c->activa, $c->uuid]
            );
        }
        $this->activas = null;
        return $c;
    }

    private function slugUnico(string $base): string
    {
        $base = $base !== '' ? mb_substr($base, 0, 70) : 'categoria';
        $slug = $base;
        for ($i = 2; $this->valor('SELECT 1 FROM categorias WHERE slug = ?', [$slug]); $i++) {
            $slug = $base . '-' . $i;
        }
        return $slug;
    }
}
