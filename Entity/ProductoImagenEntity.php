<?php

/**
 * ProductoImagenEntity — tabla `producto_imagenes`
 * El archivo vive en Content/Uploads/Productos/. orden 0 = foto principal.
 */
class ProductoImagenEntity extends BaseEntity
{
    public ?string $uuid          = null;
    public ?string $producto_uuid = null;
    public ?string $archivo       = null;
    public int     $orden         = 0;

    /**
     * URL pública de la foto, o null si el archivo no existe.
     * basename() + lista blanca de caracteres: aunque el dato viniera
     * manipulado, nunca arma una ruta fuera de la carpeta de productos.
     */
    public function url(): ?string
    {
        $archivo = basename((string) $this->archivo);

        if (!preg_match('/^[A-Za-z0-9_-]+\.(jpe?g|png|webp)$/i', $archivo)
            || !is_file(PRODUCT_IMAGE_DIR . $archivo)) {
            return null;
        }

        return APP_URL . PRODUCT_IMAGE_URL . rawurlencode($archivo);
    }
}
