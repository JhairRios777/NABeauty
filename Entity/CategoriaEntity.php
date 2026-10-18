<?php

/**
 * CategoriaEntity — tabla `categorias`
 */
class CategoriaEntity extends BaseEntity
{
    public ?string $uuid   = null;   // identificador que comparte con el POS
    public ?string $slug   = null;   // parte de la URL: /catalogo/{slug}
    public ?string $nombre = null;
    public ?string $color  = null;   // color de su tarjeta en la portada (#RRGGBB)
    public int     $orden  = 0;
    public int     $activa = 1;

    // No son columnas: las calcula CategoriaModel::findTodas() para el panel
    public int $total_productos = 0;

    /** @return string[] errores; vacío = válida */
    public function validar(): array
    {
        $errores = [];
        if (trim((string) $this->nombre) === '' || mb_strlen((string) $this->nombre) > 80) {
            $errores[] = 'El nombre es obligatorio (máximo 80 caracteres).';
        }
        if (!preg_match('/^#[0-9A-Fa-f]{6}$/', (string) $this->color)) {
            $errores[] = 'El color debe tener el formato #RRGGBB.';
        }
        if ($this->orden < 0 || $this->orden > 999) {
            $errores[] = 'El orden debe estar entre 0 y 999.';
        }
        return $errores;
    }

    public function url(): string
    {
        return url('catalogo/' . rawurlencode((string) $this->slug));
    }

    /**
     * Color seguro para imprimir dentro de un atributo style.
     * Si el dato no es un hexadecimal válido se usa el rosa de la marca:
     * así un valor corrupto nunca llega al HTML.
     */
    public function colorSeguro(): string
    {
        return preg_match('/^#[0-9A-Fa-f]{6}$/', (string) $this->color)
            ? (string) $this->color
            : '#F6D5DC';
    }

    /**
     * ¿El color de la categoría es oscuro? La vista lo usa para decidir
     * si el texto encima va en blanco o en negro (contraste legible).
     */
    public function esOscura(): bool
    {
        [$r, $g, $b] = sscanf($this->colorSeguro(), '#%02x%02x%02x');

        // Luminancia percibida (fórmula estándar Rec. 601)
        return (0.299 * $r + 0.587 * $g + 0.114 * $b) < 140;
    }
}
