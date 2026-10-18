<?php

/**
 * UsuarioEntity — tabla `usuarios` (+ sus permisos de `usuario_permisos`).
 * Los usuarios los crea el POS; la web solo los lee.
 */
class UsuarioEntity extends BaseEntity
{
    public ?string $uuid              = null;
    public ?string $nombre            = null;
    public ?string $usuario           = null;
    public ?string $password_hash     = null;
    public int     $activo            = 1;
    public ?string $ultimo_acceso_web = null;

    /** @var string[] claves de permiso (web.acceso, web.productos…) */
    public array $permisos = [];

    protected function fill(array $data): void
    {
        unset($data['permisos']);
        parent::fill($data);
    }

    public function tienePermiso(string $permiso): bool
    {
        return in_array($permiso, $this->permisos, true);
    }

    /** Nombre corto para el saludo: "Nataly Fernández" → "Nataly" */
    public function primerNombre(): string
    {
        return explode(' ', trim((string) $this->nombre))[0] ?: (string) $this->usuario;
    }

    /** Iniciales para el avatar: "Nataly Fernández" → "NF" */
    public function iniciales(): string
    {
        $partes = preg_split('/\s+/', trim((string) $this->nombre)) ?: [];
        $ini = '';
        foreach (array_slice($partes, 0, 2) as $p) {
            $ini .= mb_strtoupper(mb_substr($p, 0, 1, 'UTF-8'), 'UTF-8');
        }
        return $ini !== '' ? $ini : 'NA';
    }
}
