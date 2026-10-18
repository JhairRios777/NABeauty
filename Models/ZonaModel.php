<?php

/**
 * ZonaModel — Zonas de envío.
 *
 * Por ahora solo expone los 18 departamentos de Honduras para el formulario
 * del carrito. Cuando se definan tarifas, este Model leerá la tabla
 * `zonas_envio` (departamento, costo, días) y el checkout calculará el envío.
 */
class ZonaModel extends BaseModel
{
    /** @return string[] */
    public function departamentos(): array
    {
        return [
            'Atlántida', 'Choluteca', 'Colón', 'Comayagua', 'Copán', 'Cortés',
            'El Paraíso', 'Francisco Morazán', 'Gracias a Dios', 'Intibucá',
            'Islas de la Bahía', 'La Paz', 'Lempira', 'Ocotepeque', 'Olancho',
            'Santa Bárbara', 'Valle', 'Yoro',
        ];
    }
}
