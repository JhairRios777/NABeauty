<?php

/**
 * catalogo.php — DATOS DE MUESTRA (no son datos reales de la tienda)
 *
 * Precios, existencias y descripciones son inventados para poder diseñar
 * y probar la tienda. Las marcas y tipos de producto salen del Instagram
 * de N&A Beauty. Todo esto se reemplaza por el catálogo que envíe el POS.
 *
 * El archivo devuelve tres "tablas" con la MISMA forma que tendrán en la
 * base de datos (categorias, productos, producto_variantes). Por eso los
 * Models ya trabajan con la estructura final y no hay que tocarlos por
 * fuera cuando se conecte la BD.
 */

// UUID estable a partir de una clave legible. En producción el UUID lo
// genera el POS; aquí solo importa que no cambie entre peticiones (el
// carrito del navegador guarda el uuid de la variante).
$uuid = static function (string $clave): string {
    $h = md5('na-beauty:' . $clave);
    return sprintf(
        '%s-%s-4%s-a%s-%s',
        substr($h, 0, 8), substr($h, 8, 4), substr($h, 13, 3), substr($h, 17, 3), substr($h, 20, 12)
    );
};

// Fecha de alta relativa a hoy, para que la etiqueta "Nuevo" siga
// teniendo sentido cada vez que se abra la muestra.
$hace = static fn(int $dias): string => date('Y-m-d H:i:s', strtotime("-{$dias} days"));

// ─────────────────────────────────────────────
// CATEGORÍAS
// ─────────────────────────────────────────────
$categorias = [];
foreach ([
    ['maquillaje', 'Maquillaje', '#F6D5DC'],
    ['skincare',   'Skincare',   '#E7EEF1'],
    ['aromas',     'Aromas',     '#E9E3F0'],
    ['ropa',       'Ropa',       '#1A1416'],
    ['zapatos',    'Zapatos',    '#F4E6D6'],
    ['joyeria',    'Joyería',    '#EFE2C8'],
    ['termos',     'Termos',     '#DDE8DC'],
    ['accesorios', 'Accesorios', '#F9E4E8'],
] as $i => [$slug, $nombre, $color]) {
    $categorias[] = [
        'uuid'   => $uuid('cat:' . $slug),
        'slug'   => $slug,
        'nombre' => $nombre,
        'color'  => $color,
        'orden'  => $i + 1,
        'activa' => 1,
    ];
}

// ─────────────────────────────────────────────
// PRODUCTOS + VARIANTES
// Cada entrada: datos del producto y su lista de variantes
//   [tipo, nombre, color_hex|null, precio, precio_oferta|null, stock]
// ─────────────────────────────────────────────
$definicion = [
    // ── Maquillaje (permanente) ──────────────────
    ['maquillaje', 'NYX Professional Makeup', 'Buttermelt Blush', 'permanente', 1, 5,
        'Rubor en polvo de textura mantecosa que se difumina sin dejar parches. Acabado natural que dura todo el día.',
        [['tono', 'Tono 1', '#E7A3A4', 320, null, 6], ['tono', 'Tono 2', '#D9787F', 320, null, 4],
         ['tono', 'Tono 3', '#F0B79E', 320, null, 0], ['tono', 'Tono 4', '#C8656B', 320, null, 2]]],
    ['maquillaje', 'Huda Beauty', 'Easy Bake Loose Powder', 'permanente', 1, 12,
        'Polvo suelto para sellar el maquillaje. Controla el brillo y deja un acabado aterciopelado.',
        [['tono', 'Tono claro', '#F3DCC6', 1150, null, 3], ['tono', 'Tono medio', '#E6C3A0', 1150, null, 2],
         ['tono', 'Tono banana', '#F2DFA8', 1150, null, 1]]],
    ['maquillaje', 'e.l.f.', 'Power Grip Primer', 'permanente', 1, 40,
        'Prebase en gel que agarra el maquillaje y deja la piel hidratada. Transparente, para todo tono de piel.',
        [['unica', 'Única', null, 390, null, 12]]],
    ['maquillaje', 'Beauty Creations', 'Flawless Stay Foundation', 'permanente', 0, 55,
        'Base líquida de cobertura media a alta y larga duración, con acabado mate natural.',
        [['tono', 'Tono 1', '#F1D3B8', 290, null, 5], ['tono', 'Tono 2', '#E3BC98', 290, null, 5],
         ['tono', 'Tono 3', '#CFA07A', 290, null, 3], ['tono', 'Tono 4', '#B4835E', 290, null, 0]]],
    ['maquillaje', 'e.l.f.', 'Glow Reviver Lip Oil', 'permanente', 0, 20,
        'Aceite labial con color translúcido. Hidrata y deja brillo sin sensación pegajosa.',
        [['tono', 'Rosa', '#E58CA0', 260, 220, 7], ['tono', 'Coral', '#EE8E76', 260, 220, 4],
         ['tono', 'Vino', '#9E3B4C', 260, 220, 5]]],
    ['maquillaje', 'Beauty Creations', 'Banana Setting Powder', 'permanente', 0, 70,
        'Polvo compacto de tono banana para fijar el corrector e iluminar la zona de la ojera.',
        [['unica', 'Única', null, 210, null, 0]]],
    ['maquillaje', 'N&A', 'Kit de brochas básico', 'permanente', 0, 8,
        'Juego de brochas esenciales para rostro y ojos, con estuche. Ideal para empezar tu kit.',
        [['unica', 'Única', null, 350, null, 2]]],

    // ── Skincare (permanente) ────────────────────
    ['skincare', 'CeraVe', 'Crema hidratante', 'permanente', 1, 90,
        'Crema para rostro y cuerpo con ceramidas y ácido hialurónico. Para piel normal a seca.',
        [['unica', 'Única', null, 480, null, 9]]],
    ['skincare', 'CeraVe', 'Limpiador espumoso', 'permanente', 0, 90,
        'Gel limpiador que retira el exceso de grasa sin resecar. Para piel normal a grasa.',
        [['unica', 'Única', null, 430, null, 7]]],
    ['skincare', 'La Roche-Posay', 'Anthelios protector solar SPF 50', 'permanente', 0, 35,
        'Protector solar de amplio espectro y textura ligera, de rápida absorción.',
        [['unica', 'Única', null, 890, null, 5]]],
    ['skincare', 'Tocobo', 'Cotton Soft Sun Stick', 'permanente', 0, 6,
        'Protector solar en barra de acabado mate. Práctico para reaplicar durante el día.',
        [['unica', 'Única', null, 520, null, 4]]],

    // ── Aromas (permanente) ──────────────────────
    ['aromas', 'Bath & Body Works', 'Gel antibacterial PocketBac', 'permanente', 0, 25,
        'Gel antibacterial de bolsillo con aroma. Tamaño perfecto para la cartera.',
        [['aroma', 'Strawberry Pound Cake', '#F2B6C0', 95, null, 10], ['aroma', 'Lavender Vanilla', '#C9BCE3', 95, null, 8],
         ['aroma', 'Rose', '#EFA7B8', 95, null, 0]]],
    ['aromas', 'eos', 'Loción corporal Shea Better', 'permanente', 0, 18,
        'Loción corporal con manteca de karité. Hidratación de 24 horas y aroma suave.',
        [['aroma', 'Vainilla', '#F3E3C2', 340, null, 6], ['aroma', 'Coco', '#E4EEF0', 340, null, 3]]],

    // ── Termos (permanente) ──────────────────────
    ['termos', 'Owala', 'Termo FreeSip 24 oz', 'permanente', 1, 10,
        'Termo de acero inoxidable con boquilla doble: pajilla integrada o trago directo. A prueba de derrames.',
        [['color', 'Rosa', '#F2B6C0', 1250, null, 3], ['color', 'Verde', '#A9C4AE', 1250, null, 2],
         ['color', 'Azul', '#8FB7C9', 1250, null, 0], ['color', 'Lila', '#C9BCE3', 1250, null, 1]]],

    // ── Joyería (permanente) ─────────────────────
    ['joyeria', 'N&A Joyería', 'Aretes corazón', 'permanente', 0, 15,
        'Aretes pequeños con dije de corazón. Livianos, para uso diario.',
        [['color', 'Dorado', '#D9B45A', 180, null, 5], ['color', 'Plateado', '#C9CDD2', 180, null, 4]]],
    ['joyeria', 'N&A Joyería', 'Argollas trenzadas', 'permanente', 0, 15,
        'Argollas medianas de diseño trenzado.',
        [['color', 'Dorado', '#D9B45A', 160, null, 1], ['color', 'Plateado', '#C9CDD2', 160, null, 0]]],

    // ── Ropa (temporada) ─────────────────────────
    ['ropa', 'N&A', 'Blusa básica manga corta', 'temporada', 0, 4,
        'Blusa de tela fresca y corte recto. Combina con jeans, faldas y pantalón de vestir.',
        [['talla', 'S', null, 380, null, 2], ['talla', 'M', null, 380, null, 1], ['talla', 'L', null, 380, null, 0]]],
    ['ropa', 'N&A', 'Pantalón palazzo', 'temporada', 0, 9,
        'Pantalón de pierna ancha y tiro alto. Cómodo y elegante.',
        [['talla', 'S', null, 520, null, 1], ['talla', 'M', null, 520, null, 1]]],
    // Temporada y sin stock: NO debe aparecer en la tienda (prueba la regla)
    ['ropa', 'N&A', 'Blazer oversize', 'temporada', 0, 60,
        'Blazer de corte amplio.',
        [['talla', 'M', null, 890, null, 0]]],

    // ── Zapatos (temporada) ──────────────────────
    ['zapatos', 'N&A', 'Sandalia de tiras', 'temporada', 0, 14,
        'Sandalia baja de tiras finas. Liviana y fácil de combinar.',
        [['talla', '36', null, 690, null, 1], ['talla', '37', null, 690, null, 0], ['talla', '38', null, 690, null, 2]]],
    ['zapatos', 'N&A', 'Mary Jane bicolor', 'temporada', 0, 3,
        'Zapato tipo Mary Jane con punta en contraste y tacón bajo.',
        [['talla', '36', null, 790, null, 1], ['talla', '37', null, 790, null, 1]]],

    // ── Accesorios (temporada) ───────────────────
    ['accesorios', 'N&A', 'Bandana de satín estampada', 'temporada', 0, 2,
        'Bandana de satín suave. Úsala al cuello, en el cabello, como top o en la cartera.',
        [['color', 'Floral', '#F0B79E', 120, null, 6], ['color', 'Puntos', '#1A1416', 120, null, 4],
         ['color', 'Café', '#8A5A3C', 120, null, 3]]],
    ['accesorios', 'N&A', 'Lentes de sol rectangulares', 'temporada', 0, 30,
        'Lentes de sol de marco rectangular.',
        [['unica', 'Única', null, 250, null, 4]]],
];

$productos = [];
$variantes = [];

foreach ($definicion as [$catSlug, $marca, $nombre, $ciclo, $destacado, $dias, $descripcion, $listaVariantes]) {
    $slug         = slugify($nombre . ' ' . $marca);
    $productoUuid = $uuid('prod:' . $slug);

    $productos[] = [
        'uuid'           => $productoUuid,
        'categoria_uuid' => $uuid('cat:' . $catSlug),
        'slug'           => $slug,
        'nombre'         => $nombre,
        'marca'          => $marca,
        'descripcion'    => $descripcion,
        'image_url'      => null,          // sin fotos todavía
        'ciclo'          => $ciclo,
        'estado'         => 'activo',
        'destacado'      => $destacado,
        'created_at'     => $hace($dias),
    ];

    foreach ($listaVariantes as $n => [$tipo, $nombreVar, $colorHex, $precio, $oferta, $stock]) {
        $variantes[] = [
            'uuid'          => $uuid('var:' . $slug . ':' . $nombreVar),
            'producto_uuid' => $productoUuid,
            'sku'           => strtoupper(substr($catSlug, 0, 3)) . '-' . str_pad((string) (count($productos)), 3, '0', STR_PAD_LEFT) . '-' . ($n + 1),
            'tipo'          => $tipo,
            'nombre'        => $nombreVar,
            'color_hex'     => $colorHex,
            'precio'        => (float) $precio,
            'precio_oferta' => $oferta !== null ? (float) $oferta : null,
            'stock'         => $stock,
            'activa'        => 1,
        ];
    }
}

// Producto archivado: tampoco debe aparecer aunque tenga stock (prueba la regla)
$archivadoUuid = $uuid('prod:archivado');
$productos[] = [
    'uuid' => $archivadoUuid, 'categoria_uuid' => $uuid('cat:maquillaje'), 'slug' => 'producto-descontinuado',
    'nombre' => 'Producto descontinuado', 'marca' => 'N&A', 'descripcion' => 'Ya no se vende.',
    'image_url' => null, 'ciclo' => 'permanente', 'estado' => 'archivado', 'destacado' => 0, 'created_at' => $hace(200),
];
$variantes[] = [
    'uuid' => $uuid('var:archivado'), 'producto_uuid' => $archivadoUuid, 'sku' => 'MAQ-999-1', 'tipo' => 'unica',
    'nombre' => 'Única', 'color_hex' => null, 'precio' => 100.0, 'precio_oferta' => null, 'stock' => 5, 'activa' => 1,
];

return [
    'categorias'         => $categorias,
    'productos'          => $productos,
    'producto_variantes' => $variantes,
];
