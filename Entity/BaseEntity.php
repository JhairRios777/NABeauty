<?php

/**
 * BaseEntity — Clase base de todas las entidades.
 *
 * Una entidad representa una fila de una tabla. Sus propiedades públicas
 * se llaman igual que las columnas, así el Model la llena con el array que
 * devuelve PDO (o, por ahora, con el array de datos de muestra).
 */
abstract class BaseEntity
{
    // true cuando la entidad se construyó con datos (el registro existe)
    public bool $Found = false;

    public function __construct(array $data = [])
    {
        if (!empty($data)) {
            $this->fill($data);
            $this->Found = true;
        }
    }

    // Fábrica: ProductoEntity::fromArray($fila)
    public static function fromArray(array $data): static
    {
        return new static($data);
    }

    // Copia cada clave del array a la propiedad del mismo nombre, si existe.
    // Las claves que la entidad no declara se ignoran (no crea propiedades).
    protected function fill(array $data): void
    {
        foreach ($data as $key => $value) {
            if (is_string($key) && $key !== 'Found' && property_exists($this, $key)) {
                $this->$key = $value;
            }
        }
    }
}
