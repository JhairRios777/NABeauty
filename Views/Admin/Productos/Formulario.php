<?php
/**
 * Alta / edición de producto.
 *   $producto (ProductoEntity), $errores (string[]), $categorias, $marcas, $maxFotos
 *
 * Funciona sin JS (todos los campos van en un solo POST). admin.js agrega:
 * vista previa y reducción de fotos antes de subir, filas de variantes
 * dinámicas y mostrar/ocultar campos según el tipo de variante.
 */
$esNuevo   = $producto->uuid === null;
$accion    = url('admin/productos/guardar' . ($esNuevo ? '' : '/' . $producto->uuid));
$tipo      = $producto->variantes[0]->tipo ?? 'unica';
$variantes = $producto->variantes ?: [new VarianteEntity()];
$libres    = $maxFotos - count($producto->imagenes);

$etiquetaNombre = [
    'unica' => 'Nombre', 'tono' => 'Nombre del tono', 'talla' => 'Talla',
    'color' => 'Nombre del color', 'aroma' => 'Aroma',
];

/** Una fila de variante. $i puede ser '__i__' (plantilla para admin.js). */
$fila = function (VarianteEntity $v, int|string $i) use ($etiquetaNombre, $tipo): void { ?>
    <fieldset class="variante" data-variante>
        <legend class="sr">Opción</legend>
        <input type="hidden" name="variantes[<?= $i ?>][uuid]" value="<?= e($v->uuid) ?>">

        <div class="variante__fila">
            <label class="campo variante__nombre">
                <span class="campo__etiqueta" data-etiqueta-nombre><?= e($etiquetaNombre[$tipo] ?? 'Nombre') ?></span>
                <input type="text" name="variantes[<?= $i ?>][nombre]" value="<?= e($v->esUnica() ? '' : $v->nombre) ?>"
                       maxlength="60" placeholder="Ej. Tono 2, M, Rosa">
            </label>
            <label class="campo variante__color">
                <span class="campo__etiqueta">Color</span>
                <input type="color" name="variantes[<?= $i ?>][color_hex]" value="<?= e($v->colorSeguro() ?? '#E8B4A0') ?>">
            </label>
        </div>

        <div class="variante__fila variante__fila--precios">
            <label class="campo">
                <span class="campo__etiqueta">Precio (<?= MONEDA_SIMBOLO ?>)</span>
                <input type="text" name="variantes[<?= $i ?>][precio]" value="<?= $v->precio > 0 ? e(number_format($v->precio, 2, '.', '')) : '' ?>"
                       inputmode="decimal" placeholder="0.00" autocomplete="off">
            </label>
            <label class="campo">
                <span class="campo__etiqueta">Oferta <small>(opcional)</small></span>
                <input type="text" name="variantes[<?= $i ?>][precio_oferta]" value="<?= $v->precio_oferta ? e(number_format($v->precio_oferta, 2, '.', '')) : '' ?>"
                       inputmode="decimal" placeholder="—" autocomplete="off">
            </label>
            <label class="campo">
                <span class="campo__etiqueta">SKU <small>(opcional)</small></span>
                <input type="text" name="variantes[<?= $i ?>][sku]" value="<?= e($v->sku) ?>" maxlength="40"
                       autocapitalize="characters" spellcheck="false" placeholder="MAQ-001">
            </label>
        </div>

        <div class="variante__pie">
            <span class="variante__stock">
                <?php if ($v->uuid): ?>
                    Stock: <strong><?= numero($v->stock) ?></strong>
                <?php else: ?>
                    Nueva · stock 0 hasta registrar una entrada en el POS
                <?php endif; ?>
            </span>
            <label class="interruptor variante__activa">
                <input type="checkbox" name="variantes[<?= $i ?>][activa]" value="1" <?= $v->activa ? 'checked' : '' ?>>
                <span>A la venta</span>
            </label>
            <button class="btn btn--icono btn--peligro variante__quitar" type="button" data-quitar-variante aria-label="Quitar opción">
                <?= icono('basura') ?>
            </button>
        </div>
    </fieldset>
<?php };
?>

<p class="miga"><a href="<?= e(url('admin/productos')) ?>"><?= icono('izq') ?> Productos</a></p>

<?php if (!empty($errores)): ?>
    <div class="aviso aviso--error aviso--lista" role="alert" tabindex="-1" data-enfocar>
        <?= icono('alerta') ?>
        <div>
            <strong>Revisa estos datos antes de guardar:</strong>
            <ul><?php foreach ($errores as $err): ?><li><?= e($err) ?></li><?php endforeach; ?></ul>
        </div>
    </div>
<?php endif; ?>

<form id="form-producto" class="formulario" method="post" action="<?= e($accion) ?>"
      enctype="multipart/form-data" data-un-envio data-avisar-cambios>
    <input type="hidden" name="_csrf" value="<?= e($csrf) ?>">

    <!-- ── FOTOS ── -->
    <section class="tarjeta">
        <div class="tarjeta__cabeza">
            <h2 class="tarjeta__titulo">Fotos</h2>
            <span class="tarjeta__nota"><?= count($producto->imagenes) ?> de <?= $maxFotos ?></span>
        </div>

        <?php if (!empty($producto->imagenes)): ?>
            <ul class="fotos">
                <?php foreach ($producto->imagenes as $n => $img): ?>
                    <li class="foto<?= $n === 0 ? ' foto--principal' : '' ?>">
                        <?php if ($url = $img->url()): ?>
                            <img src="<?= e($url) ?>" alt="Foto <?= $n + 1 ?> de <?= e($producto->nombre) ?>" loading="lazy">
                        <?php else: ?>
                            <span class="foto__falta"><?= icono('alerta') ?> Archivo no encontrado</span>
                        <?php endif; ?>
                        <?php if ($n === 0): ?>
                            <span class="foto__sello"><?= icono('estrella') ?> Principal</span>
                        <?php endif; ?>
                        <div class="foto__acciones">
                            <?php if ($n > 0): ?>
                                <button class="btn btn--mini" type="submit" form="f-principal-<?= e($img->uuid) ?>" data-sin-cambios>
                                    <?= icono('estrella') ?> Principal
                                </button>
                            <?php endif; ?>
                            <button class="btn btn--mini btn--peligro" type="submit" form="f-eliminar-<?= e($img->uuid) ?>"
                                    data-confirmar="¿Eliminar esta foto?" data-sin-cambios aria-label="Eliminar foto <?= $n + 1 ?>">
                                <?= icono('basura') ?>
                            </button>
                        </div>
                    </li>
                <?php endforeach; ?>
            </ul>
        <?php endif; ?>

        <?php if ($libres > 0): ?>
            <label class="subir" data-subir>
                <input type="file" name="fotos[]" accept="image/jpeg,image/png,image/webp" multiple
                       data-max="<?= $libres ?>" data-lado="<?= IMAGEN_MAX_LADO ?>">
                <?= icono('foto', 'ico--lg') ?>
                <span class="subir__texto"><strong>Agregar fotos</strong> desde la galería o la cámara</span>
                <span class="subir__ayuda">JPG, PNG o WebP · hasta <?= $libres ?> más · se ajustan solas al tamaño de la tienda</span>
            </label>
            <ul class="fotos fotos--previa" data-previa aria-live="polite"></ul>
        <?php else: ?>
            <p class="ayuda">Llegaste al máximo de <?= $maxFotos ?> fotos. Elimina una para subir otra.</p>
        <?php endif; ?>
    </section>

    <!-- ── INFORMACIÓN ── -->
    <section class="tarjeta">
        <h2 class="tarjeta__titulo">Información</h2>

        <label class="campo">
            <span class="campo__etiqueta">Nombre del producto</span>
            <input type="text" name="nombre" value="<?= e($producto->nombre) ?>" maxlength="120" required
                   placeholder="Ej. Buttermelt Blush">
        </label>

        <div class="campos-2">
            <label class="campo">
                <span class="campo__etiqueta">Marca</span>
                <input type="text" name="marca" value="<?= e($producto->marca) ?>" maxlength="80" required list="marcas"
                       placeholder="Ej. e.l.f.">
                <datalist id="marcas">
                    <?php foreach ($marcas as $m): ?><option value="<?= e($m) ?>"><?php endforeach; ?>
                </datalist>
            </label>

            <label class="campo">
                <span class="campo__etiqueta">Categoría</span>
                <select name="categoria_uuid" required>
                    <option value="">Elige una…</option>
                    <?php foreach ($categorias as $c): ?>
                        <option value="<?= e($c->uuid) ?>" <?= $producto->categoria_uuid === $c->uuid ? 'selected' : '' ?>>
                            <?= e($c->nombre) ?><?= $c->activa ? '' : ' (oculta)' ?>
                        </option>
                    <?php endforeach; ?>
                </select>
            </label>
        </div>

        <label class="campo">
            <span class="campo__etiqueta">Descripción <small>(opcional)</small></span>
            <textarea name="descripcion" rows="4" maxlength="2000"
                      placeholder="Qué es, cómo se usa, tamaño, ingredientes…"><?= e($producto->descripcion) ?></textarea>
        </label>
    </section>

    <!-- ── PRECIO Y OPCIONES ── -->
    <section class="tarjeta">
        <h2 class="tarjeta__titulo">Precio y opciones</h2>

        <fieldset class="pastillas" data-tipo-variante>
            <legend class="campo__etiqueta">¿El producto viene en varias opciones?</legend>
            <?php foreach (VarianteEntity::TIPOS as $clave => $texto): ?>
                <label class="pastilla">
                    <input type="radio" name="tipo_variante" value="<?= e($clave) ?>" <?= $tipo === $clave ? 'checked' : '' ?>
                           data-etiqueta="<?= e($etiquetaNombre[$clave]) ?>">
                    <span><?= $clave === 'unica' ? 'No, un solo precio' : 'Por ' . e(mb_strtolower($texto)) ?></span>
                </label>
            <?php endforeach; ?>
        </fieldset>

        <div class="variantes" data-variantes data-tipo="<?= e($tipo) ?>">
            <?php foreach ($variantes as $i => $v) { $fila($v, $i); } ?>
        </div>

        <button class="btn btn--secundario variantes__agregar" type="button" data-agregar-variante>
            <?= icono('mas') ?> Agregar otra opción
        </button>

        <template id="tpl-variante"><?php $fila(new VarianteEntity(), '__i__'); ?></template>

        <p class="ayuda">
            El <strong>stock</strong> no se edita aquí: sube con las entradas de mercadería y baja con las ventas que se registran en el POS.
        </p>
    </section>

    <!-- ── VISIBILIDAD ── -->
    <section class="tarjeta">
        <h2 class="tarjeta__titulo">En la tienda</h2>

        <fieldset class="opciones">
            <legend class="campo__etiqueta">Tipo de producto</legend>
            <label class="opcion">
                <input type="radio" name="ciclo" value="permanente" <?= $producto->ciclo === 'permanente' ? 'checked' : '' ?>>
                <span><strong>Permanente</strong>Se reabastece (maquillaje, skincare). Siempre visible; si se agota dice “Agotado”.</span>
            </label>
            <label class="opcion">
                <input type="radio" name="ciclo" value="temporada" <?= $producto->ciclo === 'temporada' ? 'checked' : '' ?>>
                <span><strong>De temporada</strong>Ropa, zapatos. Solo se ve mientras haya stock; al agotarse sale sola de la tienda.</span>
            </label>
        </fieldset>

        <fieldset class="opciones">
            <legend class="campo__etiqueta">Estado</legend>
            <label class="opcion">
                <input type="radio" name="estado" value="activo" <?= $producto->estado === 'activo' ? 'checked' : '' ?>>
                <span><strong>Activo</strong>Se vende en la tienda y en el POS.</span>
            </label>
            <label class="opcion">
                <input type="radio" name="estado" value="archivado" <?= $producto->estado === 'archivado' ? 'checked' : '' ?>>
                <span><strong>Archivado</strong>Ya no se vende. Se oculta, pero su historial de ventas se conserva.</span>
            </label>
        </fieldset>

        <label class="interruptor interruptor--grande">
            <input type="checkbox" name="destacado" value="1" <?= $producto->destacado ? 'checked' : '' ?>>
            <span><strong>Mostrar en “Lo más top”</strong> de la portada (si tiene stock)</span>
        </label>
    </section>

    <div class="acciones-fijas">
        <?php if (!$esNuevo && $producto->esVisibleEnTienda()): ?>
            <a class="enlace" href="<?= e($producto->url()) ?>" target="_blank" rel="noopener"><?= icono('ojo') ?> Ver en tienda</a>
        <?php endif; ?>
        <a class="btn btn--secundario" href="<?= e(url('admin/productos')) ?>">Cancelar</a>
        <button class="btn btn--primario" type="submit"><?= icono('check') ?> <?= $esNuevo ? 'Crear producto' : 'Guardar cambios' ?></button>
    </div>
</form>

<?php /* Formularios de cada foto: fuera del principal (HTML no permite anidar
         formularios); sus botones los apuntan con el atributo form="". */ ?>
<?php foreach ($producto->imagenes as $img): ?>
    <form id="f-principal-<?= e($img->uuid) ?>" method="post" hidden
          action="<?= e(url('admin/productos/principalImagen/' . $producto->uuid . '/' . $img->uuid)) ?>">
        <input type="hidden" name="_csrf" value="<?= e($csrf) ?>">
    </form>
    <form id="f-eliminar-<?= e($img->uuid) ?>" method="post" hidden
          action="<?= e(url('admin/productos/eliminarImagen/' . $producto->uuid . '/' . $img->uuid)) ?>">
        <input type="hidden" name="_csrf" value="<?= e($csrf) ?>">
    </form>
<?php endforeach; ?>
