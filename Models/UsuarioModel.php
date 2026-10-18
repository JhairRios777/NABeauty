<?php

/**
 * UsuarioModel — Lectura de usuarios para el inicio de sesión del panel.
 * La web no crea ni modifica usuarios: eso llega sincronizado desde el POS.
 */
class UsuarioModel extends BaseModel
{
    /** Usuario activo por nombre de usuario (incluye el hash, solo para el login). */
    public function findParaLogin(string $usuario): ?UsuarioEntity
    {
        $fila = $this->fila(
            'SELECT uuid, nombre, usuario, password_hash, activo
               FROM usuarios
              WHERE usuario = ? AND activo = 1',
            [$usuario]
        );

        return $fila ? $this->conPermisos(UsuarioEntity::fromArray($fila)) : null;
    }

    /** Usuario activo por UUID, sin el hash (para cada petición con sesión). */
    public function findActivoConPermisos(string $uuid): ?UsuarioEntity
    {
        $fila = $this->fila(
            'SELECT uuid, nombre, usuario, activo, ultimo_acceso_web
               FROM usuarios
              WHERE uuid = ? AND activo = 1',
            [$uuid]
        );

        return $fila ? $this->conPermisos(UsuarioEntity::fromArray($fila)) : null;
    }

    public function registrarAcceso(string $uuid): void
    {
        $this->ejecutar('UPDATE usuarios SET ultimo_acceso_web = NOW() WHERE uuid = ?', [$uuid]);
    }

    private function conPermisos(UsuarioEntity $usuario): UsuarioEntity
    {
        $usuario->permisos = $this->ejecutar(
            'SELECT permiso FROM usuario_permisos WHERE usuario_uuid = ?',
            [$usuario->uuid]
        )->fetchAll(PDO::FETCH_COLUMN);

        return $usuario;
    }
}
