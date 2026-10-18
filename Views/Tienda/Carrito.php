<?php
/**
 * Carrito — genera una COTIZACIÓN por WhatsApp.
 *
 * El cliente no llena ningún formulario: revisa sus productos y toca
 * "Cotizar por WhatsApp". Se abre el chat de la tienda con la lista ya
 * escrita; existencia, envío y forma de pago se acuerdan en esa conversación.
 *
 * El carrito vive en el navegador (localStorage) y lo pinta tienda.js
 * clonando el <template> de abajo. El servidor no recibe ni guarda nada:
 * la tienda web no registra pedidos ni descuenta stock. La venta se
 * registra en el POS cuando la tienda la confirma.
 *
 * Los precios que ve el cliente son una referencia tomada del catálogo al
 * momento de agregar; el monto final lo confirma la tienda en el chat.
 */
?>

<section class="contenedor encabezado-pagina">
    <nav class="migas" aria-label="Ruta de navegación">
        <a href="<?= e(url()) ?>">Inicio</a>
        <span aria-hidden="true">/</span>
        <span aria-current="page">Carrito</span>
    </nav>
    <h1 class="titulo titulo--lg">Tu carrito</h1>
</section>

<noscript>
    <div class="contenedor"><p class="producto__aviso">El carrito necesita JavaScript activado en tu navegador.</p></div>
</noscript>

<!-- Carrito vacío: tienda.js lo muestra cuando no hay artículos -->
<section class="contenedor" data-carrito-vacio hidden>
    <div class="vacio">
        <h2 class="titulo titulo--md">Tu carrito está vacío</h2>
        <p>Agrega tus productos favoritos y vuelve aquí para pedir tu cotización.</p>
        <a class="btn btn--vino" href="<?= e(url('catalogo')) ?>">Ir al catálogo</a>
    </div>
</section>

<section class="contenedor carrito" data-carrito hidden>

    <!-- Lista de artículos: la llena tienda.js -->
    <ul class="carrito__lista" data-carrito-lista aria-label="Artículos en tu carrito"></ul>

    <!-- ═════════ RESUMEN Y COTIZACIÓN ═════════ -->
    <aside class="resumen" aria-label="Resumen de tu cotización">
        <h2 class="titulo titulo--md">Resumen</h2>

        <dl class="resumen__lineas">
            <div><dt>Productos</dt><dd><span data-carrito-unidades>0</span> art.</dd></div>
            <div><dt>Envío</dt><dd>Se cotiza por WhatsApp</dd></div>
            <div class="resumen__total"><dt>Total estimado</dt><dd data-carrito-total>—</dd></div>
        </dl>

        <button class="btn btn--vino btn--grande" type="button" data-cotizar-whatsapp>
            <span class="icono icono--whatsapp" aria-hidden="true"></span>
            Cotizar por WhatsApp
        </button>

        <p class="resumen__nota">Te respondemos con existencia, costo de envío y forma de pago. No tienes que llenar ningún dato.</p>
        <a class="resumen__seguir" href="<?= e(url('catalogo')) ?>">← Seguir comprando</a>
    </aside>
</section>

<!-- Molde de un artículo del carrito. tienda.js lo clona y lo llena con
     textContent (nunca innerHTML): lo guardado en el navegador se trata
     como texto, no como HTML. -->
<template data-carrito-molde>
    <li class="articulo">
        <a class="articulo__foto" data-campo="enlace-foto" aria-hidden="true" tabindex="-1"></a>
        <div class="articulo__info">
            <span class="tarjeta__marca" data-campo="marca"></span>
            <a class="articulo__nombre" data-campo="nombre"></a>
            <span class="articulo__detalle" data-campo="detalle"></span>
        </div>
        <div class="cantidad cantidad--chica">
            <button type="button" data-accion="menos" aria-label="Quitar una unidad">−</button>
            <span data-campo="cantidad" aria-live="polite"></span>
            <button type="button" data-accion="mas" aria-label="Agregar una unidad">+</button>
        </div>
        <strong class="articulo__precio" data-campo="importe"></strong>
        <button class="articulo__quitar" type="button" data-accion="quitar">
            <span class="solo-lector">Quitar del carrito</span>
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18"></path></svg>
        </button>
    </li>
</template>
