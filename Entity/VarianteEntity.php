<?php

/**
 * VarianteEntity — tabla `producto_variantes`
 *
 * La variante es la unidad que realmente se vende y se cuenta: un tono,
 * una talla, un color. TODO producto tiene al menos una (tipo "unica").
 * Por eso el precio y el stock viven aquí y no en el producto: el sistema
 * tiene un solo camino para calcular existencias, con o sin variantes.
 */
class VarianteEntity extends BaseEntity
{
    public ?string $uuid          = null;
    public ?string $producto_uuid = null;
    public ?string $sku           = null;
    public ?string $tipo          = 'unica';   // unica | tono | talla | color | aroma
    public ?string $nombre        = null;      // "Tono 2", "M", "Rosa"
    public ?string $color_hex     = null;      // muestra de color (tono / color)
    public float   $precio        = 0.0;
    public ?float  $precio_oferta = null;      // null = sin oferta
    public int     $stock         = 0;         // acumulado del libro de inventario (solo lectura en web)
    public int     $orden         = 0;
    public int     $activa        = 1;

    public const TIPOS = [
        'unica' => 'Única',
        'tono'  => 'Tono',
        'talla' => 'Talla',
        'color' => 'Color',
        'aroma' => 'Aroma',
    ];

    /** @return string[] errores de esta variante; vacío = válida */
    public function validar(): array
    {
        $errores = [];

        if (!isset(self::TIPOS[(string) $this->tipo])) {
            $errores[] = 'tipo no válido.';
        }
        if (trim((string) $this->nombre) === '' || mb_strlen((string) $this->nombre) > 60) {
            $errores[] = 'el nombre es obligatorio (máximo 60 caracteres).';
        }
        if ($this->precio <= 0 || $this->precio > 99999999) {
            $errores[] = 'el precio debe ser mayor que 0.';
        }
        if ($this->precio_oferta !== null && ($this->precio_oferta <= 0 || $this->precio_oferta >= $this->precio)) {
            $errores[] = 'el precio de oferta debe ser menor que el precio normal.';
        }
        if ($this->sku !== null && !preg_match('/^[A-Za-z0-9._-]{1,40}$/', $this->sku)) {
            $errores[] = 'el SKU solo admite letras, números, punto, guion y guion bajo (máx. 40).';
        }
        if ($this->color_hex !== null && $this->colorSeguro() === null) {
            $errores[] = 'color no válido.';
        }

        return $errores;
    }

    public function tieneOferta(): bool
    {
        return $this->precio_oferta !== null
            && $this->precio_oferta > 0
            && $this->precio_oferta < $this->precio;
    }

    // Precio que paga el cliente hoy
    public function precioFinal(): float
    {
        return $this->tieneOferta() ? (float) $this->precio_oferta : $this->precio;
    }

    public function hayStock(): bool
    {
        return $this->stock > 0;
    }

    public function esUnica(): bool
    {
        return $this->tipo === 'unica';
    }

    // Muestra de color validada, o null si la variante no tiene (ej. tallas)
    public function colorSeguro(): ?string
    {
        return preg_match('/^#[0-9A-Fa-f]{6}$/', (string) $this->color_hex)
            ? (string) $this->color_hex
            : null;
    }
}
