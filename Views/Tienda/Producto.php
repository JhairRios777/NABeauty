<?php
/**
 * Detalle de producto.
 *
 * Espera:
 *   $producto      ProductoEntity (con variantes y categoría cargadas)
 *   $relacionados  ProductoEntity[]
 *
 * La página se imprime ya con la primera variante disponible elegida,
 * así que precio y existencia son correctos aunque el JS no cargue.
 * tienda.js solo actualiza esos textos cuando el cliente cambia de variante.
 */
$variantes = $producto->variantesActivas();
$foto      = $producto->getImageUrl();
$agotado   = $producto->estaAgotado();
$elegir    = $producto->requiereEleccion();

// Variante preseleccionada: la primera con existencia (o la primera, si no hay)
$inicial = $variantes[0];
foreach ($variantes as $v) {
    if ($v->hayStock()) {
        $inicial = $v;
        break;
    }
}

$tituloSelector = [
    'tono'  => 'Elige tu tono',
    'talla' => 'Elige tu talla',
    'color' => 'Elige el color',
    'aroma' => 'Elige el aroma',
][$producto->tipoVariante()] ?? 'Elige una opción';

$mensajeWa = $agotado
    ? 'Hola, quiero saber cuándo vuelve a estar disponible: ' . $producto->nombre . ' (' . $producto->marca . ').'
    : 'Hola, tengo una consulta sobre: ' . $producto->nombre . ' (' . $producto->marca . ').';
?>

<section class="contenedor producto" data-producto
         data-slug="<?= e($producto->slug) ?>"
         data-nombre="<?= e($producto->nombre) ?>"
         data-marca="<?= e($producto->marca) ?>"
         data-color="<?= e($producto->colorFondo()) ?>">

    <!-- ═════════ GALERÍA ═════════ -->
    <div class="producto__galeria" style="--fondo: <?= e($producto->colorFondo()) ?>">
        <?php if ($foto): ?>
            <img src="<?= e($foto) ?>" alt="<?= e($producto->nombre . ' de ' . $producto->marca) ?>" width="800" height="800">
        <?php else: ?>
            <span class="producto__inicial" aria-hidden="true"><?= e(mb_strtoupper(mb_substr((string) $producto->marca, 0, 1, 'UTF-8'), 'UTF-8')) ?></span>
            <span class="producto__sin-foto">Foto pendiente</span>
        <?php endif; ?>

        <?php if ($etiqueta = $producto->etiqueta()): ?>
            <span class="etiqueta etiqueta--<?= e($etiqueta['tipo']) ?>"><?= e($etiqueta['texto']) ?></span>
        <?php endif; ?>
    </div>

    <!-- ═════════ INFORMACIÓN Y COMPRA ═════════ -->
    <div class="producto__info">
        <nav class="migas" aria-label="Ruta de navegación">
            <a href="<?= e(url()) ?>">Inicio</a>
            <span aria-hidden="true">/</span>
            <a href="<?= e($producto->categoria->url()) ?>"><?= e($producto->categoria->nombre) ?></a>
        </nav>

        <div>
            <span class="tarjeta__marca"><?= e($producto->marca) ?></span>
            <h1 class="titulo titulo--lg"><?= e($producto->nombre) ?></h1>
        </div>

        <div class="producto__precio">
            <strong data-precio-actual><?= e(precio($inicial->precioFinal())) ?></strong>
            <s data-precio-antes <?= $inicial->tieneOferta() ? '' : 'hidden' ?>><?= e(precio($inicial->precio)) ?></s>
            <span class="estado <?= $inicial->hayStock() ? 'estado--ok' : 'estado--no' ?>" data-stock-texto>
                <?php if (!$inicial->hayStock()): ?>Agotado
                <?php elseif ($inicial->stock <= STOCK_BAJO): ?>Últimas <?= (int) $inicial->stock ?> unidades
                <?php else: ?>Disponible<?php endif; ?>
            </span>
        </div>

        <?php if ($elegir): ?>
            <!-- Radios reales: se eligen con teclado y los lee un lector de pantalla.
                 Cada uno lleva en data-* lo que cambia al seleccionarlo. -->
            <fieldset class="variantes">
                <legend><?= e($tituloSelector) ?></legend>
                <div class="variantes__lista">
                    <?php foreach ($variantes as $v): ?>
                        <label class="variante<?= $v->hayStock() ? '' : ' variante--agotada' ?>">
                            <input type="radio" name="variante" value="<?= e($v->uuid) ?>"
                                   data-nombre="<?= e($v->nombre) ?>"
                                   data-precio="<?= e(number_format($v->precioFinal(), 2, '.', '')) ?>"
                                   data-antes="<?= $v->tieneOferta() ? e(number_format($v->precio, 2, '.', '')) : '' ?>"
                                   data-stock="<?= (int) $v->stock ?>"
                                   <?= $v->uuid === $inicial->uuid ? 'checked' : '' ?>
                                   <?= $v->hayStock() ? '' : 'disabled' ?>>
                            <span class="variante__cuerpo">
                                <?php if ($v->colorSeguro()): ?>
                                    <span class="variante__muestra" style="--muestra: <?= e($v->colorSeguro()) ?>"></span>
                                <?php endif; ?>
                                <?= e($v->nombre) ?>
                                <?php if (!$v->hayStock()): ?><span class="solo-lector"> (agotado)</span><?php endif; ?>
                            </span>
                        </label>
                    <?php endforeach; ?>
                </div>
            </fieldset>
        <?php else: ?>
            <!-- Variante única: viaja oculta para que el JS la lea igual que un radio -->
            <input type="radio" name="variante" value="<?= e($inicial->uuid) ?>" checked hidden
                   data-nombre=""
                   data-precio="<?= e(number_format($inicial->precioFinal(), 2, '.', '')) ?>"
                   data-antes="<?= $inicial->tieneOferta() ? e(number_format($inicial->precio, 2, '.', '')) : '' ?>"
                   data-stock="<?= (int) $inicial->stock ?>">
        <?php endif; ?>

        <?php if (!$agotado): ?>
            <div class="producto__compra">
                <div class="cantidad">
                    <button type="button" data-cantidad-menos aria-label="Quitar una unidad">−</button>
                    <label class="solo-lector" for="cantidad">Cantidad</label>
                    <input id="cantidad" type="number" inputmode="numeric" value="1" min="1" max="<?= (int) max(1, $inicial->stock) ?>" data-cantidad>
                    <button type="button" data-cantidad-mas aria-label="Agregar una unidad">+</button>
                </div>
                <button class="btn btn--vino btn--grande producto__agregar" type="button" data-agregar-producto>Agregar al carrito</button>
            </div>
        <?php else: ?>
            <p class="producto__aviso">Este producto está agotado por ahora. Escríbenos y te avisamos cuando vuelva.</p>
        <?php endif; ?>

        <a class="btn btn--borde btn--grande" href="#" data-wa-enlace data-wa-mensaje="<?= e($mensajeWa) ?>">
            <span class="icono icono--whatsapp" aria-hidden="true"></span>
            <?= $agotado ? 'Avísame cuando llegue' : 'Pregúntanos por WhatsApp' ?>
        </a>

        <ul class="garantias">
            <li><strong>Producto original</strong><span>Marcas garantizadas</span></li>
            <li><strong>Envío nacional</strong><span>A todo Honduras</span></li>
            <li><strong>Pago fácil</strong><span>Transferencia o contra entrega</span></li>
        </ul>

        <?php if (!empty($producto->descripcion)): ?>
            <div class="producto__descripcion">
                <h2>Descripción</h2>
                <p><?= nl2br(e($producto->descripcion)) ?></p>
            </div>
        <?php endif; ?>
    </div>
</section>

<?php if (!empty($relacionados)): ?>
<section class="contenedor seccion">
    <div class="seccion__cabeza">
        <h2 class="titulo titulo--lg">Completa tu <em>look</em></h2>
    </div>
    <div class="rejilla rejilla--4">
        <?php foreach ($relacionados as $p): ?>
            <?php require __DIR__ . '/Parciales/TarjetaProducto.php'; ?>
        <?php endforeach; ?>
    </div>
</section>
<?php endif; ?>
