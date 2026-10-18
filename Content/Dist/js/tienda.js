/**
 * tienda.js — Interacción de la tienda pública de N&A Beauty
 *
 * Sin librerías. Todo se engancha por atributos data-* (el HTML no lleva
 * onclick ni <script> en línea: la CSP del sitio los bloquea).
 *
 * Módulos:
 *   1. Utilidades       4. Menú móvil y filtros     7. Página del carrito
 *   2. Carrito (datos)  5. Tarjetas: agregar        8. Enlaces de WhatsApp
 *   3. Aviso flotante   6. Detalle de producto
 *
 * El carrito vive en localStorage y termina en una COTIZACIÓN por WhatsApp:
 * la web no registra pedidos ni pide datos al cliente. Los precios guardados
 * son una referencia; el monto final lo confirma la tienda en el chat.
 */
(function () {
    'use strict';

    // ─────────────────────────────────────────────
    // 1. UTILIDADES
    // ─────────────────────────────────────────────
    var cuerpo = document.body;
    var MONEDA = cuerpo.dataset.moneda || 'L';
    var WA     = (cuerpo.dataset.wa || '').replace(/\D/g, '');   // solo dígitos
    var TIENDA = cuerpo.dataset.tienda || '';
    var BASE   = cuerpo.dataset.base || '/';                        // URL raíz del sitio (APP_URL)
    var STOCK_BAJO = parseInt(cuerpo.dataset.stockBajo, 10) || 3;   // mismo umbral que usa PHP

    // 1234.5 → "L 1,234.50" (mismo formato que el helper precio() de PHP)
    function formatoPrecio(monto) {
        return MONEDA + ' ' + Number(monto).toLocaleString('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    // Entero dentro de un rango; cualquier valor raro cae en el mínimo
    function acotar(valor, minimo, maximo) {
        var n = parseInt(valor, 10);
        if (isNaN(n)) { n = minimo; }
        return Math.min(Math.max(n, minimo), maximo);
    }

    // ─────────────────────────────────────────────
    // 2. CARRITO — lectura y escritura en localStorage
    //
    // Cada artículo: { v, slug, nombre, marca, detalle, precio, cantidad, max, color }
    //   v   = uuid de la variante (identifica el artículo)
    //   max = existencia conocida al agregarlo (tope de cantidad)
    // ─────────────────────────────────────────────
    var CLAVE = 'na_carrito_v1';
    var MAX_ARTICULOS = 50;

    var Carrito = {
        leer: function () {
            try {
                var datos = JSON.parse(localStorage.getItem(CLAVE) || '[]');
                if (!Array.isArray(datos)) { return []; }

                // Lo guardado en el navegador no es de fiar: se normaliza
                // cada campo antes de usarlo.
                return datos.filter(function (a) {
                    return a && typeof a.v === 'string' && a.v !== '';
                }).slice(0, MAX_ARTICULOS).map(function (a) {
                    var max = acotar(a.max, 1, 999);
                    return {
                        v:        a.v,
                        slug:     String(a.slug || '').replace(/[^a-z0-9-]/g, ''),
                        nombre:   String(a.nombre || '').slice(0, 120),
                        marca:    String(a.marca || '').slice(0, 60),
                        detalle:  String(a.detalle || '').slice(0, 60),
                        precio:   Math.max(0, Number(a.precio) || 0),
                        cantidad: acotar(a.cantidad, 1, max),
                        max:      max,
                        color:    /^#[0-9A-Fa-f]{6}$/.test(a.color) ? a.color : '#F6D5DC'
                    };
                });
            } catch (error) {
                return [];   // JSON corrupto o localStorage bloqueado
            }
        },

        guardar: function (articulos) {
            try {
                localStorage.setItem(CLAVE, JSON.stringify(articulos));
            } catch (error) {
                // Modo privado o almacenamiento lleno: el carrito no persiste,
                // pero la página no se rompe.
            }
            document.dispatchEvent(new CustomEvent('carrito:cambio'));
        },

        // Agrega o suma cantidad. Retorna cuántas unidades entraron de verdad
        // (puede ser menos de lo pedido si se alcanza la existencia).
        agregar: function (articulo, cantidad) {
            var articulos = this.leer();
            var actual = articulos.find(function (a) { return a.v === articulo.v; });
            var antes  = actual ? actual.cantidad : 0;
            var max    = acotar(articulo.max, 1, 999);
            var nueva  = Math.min(antes + cantidad, max);

            if (actual) {
                actual.cantidad = nueva;
                actual.max = max;
                actual.precio = articulo.precio;   // refresca por si cambió
            } else if (articulos.length < MAX_ARTICULOS) {
                articulo.cantidad = nueva;
                articulos.push(articulo);
            }

            this.guardar(articulos);
            return nueva - antes;
        },

        cambiarCantidad: function (v, delta) {
            var articulos = this.leer();
            var actual = articulos.find(function (a) { return a.v === v; });
            if (!actual) { return; }
            actual.cantidad = acotar(actual.cantidad + delta, 1, actual.max);
            this.guardar(articulos);
        },

        quitar: function (v) {
            this.guardar(this.leer().filter(function (a) { return a.v !== v; }));
        },

        unidades: function (articulos) {
            return articulos.reduce(function (suma, a) { return suma + a.cantidad; }, 0);
        },

        subtotal: function (articulos) {
            return articulos.reduce(function (suma, a) { return suma + a.precio * a.cantidad; }, 0);
        }
    };

    // Número del carrito en la cabecera
    function pintarContador() {
        var unidades = Carrito.unidades(Carrito.leer());
        document.querySelectorAll('[data-carrito-contador]').forEach(function (el) {
            el.textContent = unidades;
        });
    }
    document.addEventListener('carrito:cambio', pintarContador);
    // Si el carrito cambia en otra pestaña, esta también se actualiza
    window.addEventListener('storage', function (evento) {
        if (evento.key === CLAVE) { document.dispatchEvent(new CustomEvent('carrito:cambio')); }
    });
    pintarContador();

    // ─────────────────────────────────────────────
    // 3. AVISO FLOTANTE
    // ─────────────────────────────────────────────
    var avisoEl = document.querySelector('[data-aviso]');
    var avisoReloj = null;

    function avisar(mensaje) {
        if (!avisoEl) { return; }
        avisoEl.textContent = mensaje;
        avisoEl.hidden = false;
        clearTimeout(avisoReloj);
        avisoReloj = setTimeout(function () { avisoEl.hidden = true; }, 3200);
    }

    function avisarAgregado(entraron, nombre) {
        avisar(entraron > 0
            ? 'Agregado al carrito: ' + nombre
            : 'Ya tienes en el carrito todas las unidades disponibles');
    }

    // ─────────────────────────────────────────────
    // 4. MENÚ MÓVIL Y FILTROS
    // ─────────────────────────────────────────────
    var botonMenu = document.querySelector('[data-menu-boton]');
    var cabecera  = document.querySelector('.cabecera');

    if (botonMenu && cabecera) {
        botonMenu.addEventListener('click', function () {
            var abierta = cabecera.classList.toggle('cabecera--abierta');
            botonMenu.setAttribute('aria-expanded', abierta ? 'true' : 'false');
        });
    }

    // Los filtros llegan abiertos (funcionan sin JS). En pantallas chicas se
    // pliegan al cargar para no empujar los productos, salvo que haya
    // filtros aplicados.
    var filtros = document.querySelector('[data-filtros]');
    if (filtros && filtros.dataset.activos !== '1' && window.matchMedia('(max-width: 1000px)').matches) {
        filtros.open = false;
    }

    // ─────────────────────────────────────────────
    // 5. TARJETAS — botón "+" de productos con variante única
    // Un solo listener en el documento atiende todas las tarjetas.
    // ─────────────────────────────────────────────
    document.addEventListener('click', function (evento) {
        var boton = evento.target.closest('[data-agregar]');
        if (!boton) { return; }

        var d = boton.dataset;
        var entraron = Carrito.agregar({
            v: d.variante, slug: d.slug, nombre: d.nombre, marca: d.marca,
            detalle: d.detalle, precio: Number(d.precio), max: Number(d.max), color: d.color
        }, 1);

        avisarAgregado(entraron, d.nombre);
    });

    // ─────────────────────────────────────────────
    // 6. DETALLE DE PRODUCTO
    // Al cambiar de variante se actualizan precio, existencia y tope de
    // cantidad. Los datos vienen en los data-* de cada radio.
    // ─────────────────────────────────────────────
    var producto = document.querySelector('[data-producto]');

    if (producto) {
        var precioEl   = producto.querySelector('[data-precio-actual]');
        var antesEl    = producto.querySelector('[data-precio-antes]');
        var stockEl    = producto.querySelector('[data-stock-texto]');
        var cantidadEl = producto.querySelector('[data-cantidad]');

        var varianteElegida = function () {
            return producto.querySelector('input[name="variante"]:checked');
        };

        var refrescar = function () {
            var v = varianteElegida();
            if (!v) { return; }
            var stock = Number(v.dataset.stock);

            precioEl.textContent = formatoPrecio(v.dataset.precio);
            antesEl.hidden = v.dataset.antes === '';
            if (v.dataset.antes !== '') { antesEl.textContent = formatoPrecio(v.dataset.antes); }

            stockEl.textContent = stock <= 0 ? 'Agotado'
                : (stock <= STOCK_BAJO ? 'Últimas ' + stock + ' unidades' : 'Disponible');
            stockEl.className = 'estado ' + (stock > 0 ? 'estado--ok' : 'estado--no');

            if (cantidadEl) {
                cantidadEl.max = Math.max(1, stock);
                cantidadEl.value = acotar(cantidadEl.value, 1, Math.max(1, stock));
            }
        };

        producto.addEventListener('change', function (evento) {
            if (evento.target.name === 'variante') { refrescar(); }
            if (evento.target === cantidadEl) {
                cantidadEl.value = acotar(cantidadEl.value, 1, Number(cantidadEl.max));
            }
        });

        producto.addEventListener('click', function (evento) {
            if (!cantidadEl) { return; }
            var paso = evento.target.closest('[data-cantidad-mas]') ? 1
                     : (evento.target.closest('[data-cantidad-menos]') ? -1 : 0);
            if (paso !== 0) {
                cantidadEl.value = acotar(Number(cantidadEl.value) + paso, 1, Number(cantidadEl.max));
            }

            if (evento.target.closest('[data-agregar-producto]')) {
                var v = varianteElegida();
                if (!v || Number(v.dataset.stock) <= 0) {
                    avisar('Elige una opción disponible');
                    return;
                }
                var d = producto.dataset;
                var entraron = Carrito.agregar({
                    v: v.value, slug: d.slug, nombre: d.nombre, marca: d.marca,
                    detalle: v.dataset.nombre, precio: Number(v.dataset.precio),
                    max: Number(v.dataset.stock), color: d.color
                }, acotar(cantidadEl.value, 1, Number(v.dataset.stock)));

                avisarAgregado(entraron, d.nombre);
            }
        });
    }

    // ─────────────────────────────────────────────
    // 7. PÁGINA DEL CARRITO
    // Se pinta clonando el <template> y llenándolo con textContent:
    // lo guardado en el navegador nunca se interpreta como HTML.
    // ─────────────────────────────────────────────
    var seccion = document.querySelector('[data-carrito]');

    if (seccion) {
        var vacio  = document.querySelector('[data-carrito-vacio]');
        var lista  = seccion.querySelector('[data-carrito-lista]');
        var molde  = document.querySelector('[data-carrito-molde]');

        var pintarCarrito = function () {
            var articulos = Carrito.leer();

            seccion.hidden = articulos.length === 0;
            vacio.hidden   = articulos.length > 0;

            lista.textContent = '';
            articulos.forEach(function (a) {
                var nodo = molde.content.cloneNode(true);
                var fila = nodo.querySelector('.articulo');
                var url  = BASE + 'producto/' + encodeURIComponent(a.slug);
                var campo = function (nombre) { return nodo.querySelector('[data-campo="' + nombre + '"]'); };

                fila.dataset.variante = a.v;
                campo('enlace-foto').href = url;
                campo('enlace-foto').style.background = a.color;
                campo('marca').textContent = a.marca;
                campo('nombre').textContent = a.nombre;
                campo('nombre').href = url;
                campo('detalle').textContent = a.detalle;
                campo('cantidad').textContent = a.cantidad;
                campo('importe').textContent = formatoPrecio(a.precio * a.cantidad);

                nodo.querySelector('[data-accion="menos"]').disabled = a.cantidad <= 1;
                nodo.querySelector('[data-accion="mas"]').disabled   = a.cantidad >= a.max;

                lista.appendChild(nodo);
            });

            var subtotal = Carrito.subtotal(articulos);
            seccion.querySelector('[data-carrito-unidades]').textContent = Carrito.unidades(articulos);
            // Total estimado = suma de productos. El envío se cotiza en el chat.
            seccion.querySelector('[data-carrito-total]').textContent = formatoPrecio(subtotal);
        };

        lista.addEventListener('click', function (evento) {
            var boton = evento.target.closest('[data-accion]');
            if (!boton) { return; }
            var v = boton.closest('.articulo').dataset.variante;

            if (boton.dataset.accion === 'mas')    { Carrito.cambiarCantidad(v, 1); }
            if (boton.dataset.accion === 'menos')  { Carrito.cambiarCantidad(v, -1); }
            if (boton.dataset.accion === 'quitar') { Carrito.quitar(v); }
        });

        document.addEventListener('carrito:cambio', pintarCarrito);
        pintarCarrito();

        // "Cotizar por WhatsApp": arma el mensaje con la lista del carrito y
        // abre el chat de la tienda. No pide ningún dato al cliente: nombre,
        // dirección y forma de pago se acuerdan en la conversación.
        seccion.querySelector('[data-cotizar-whatsapp]').addEventListener('click', function () {
            var articulos = Carrito.leer();
            if (articulos.length === 0) { return; }

            var lineas = ['Hola, quiero cotizar estos productos de ' + TIENDA + ':', ''];
            articulos.forEach(function (a) {
                lineas.push('• ' + a.cantidad + ' x ' + a.nombre
                    + (a.marca ? ' · ' + a.marca : '')
                    + (a.detalle ? ' (' + a.detalle + ')' : '')
                    + ' — ' + formatoPrecio(a.precio * a.cantidad));
            });
            lineas.push('', 'Total estimado: ' + formatoPrecio(Carrito.subtotal(articulos)),
                '', '¿Me confirman existencia y costo de envío?');

            abrirWhatsApp(lineas.join('\n'));
        });
    }

    // ─────────────────────────────────────────────
    // 8. ENLACES DE WHATSAPP
    // El número se configura en un solo lugar (WA_NUMBER en Define.php) y
    // llega por data-wa del <body>. Sin número, el enlace avisa en vez de
    // abrir un chat roto.
    // ─────────────────────────────────────────────
    function abrirWhatsApp(mensaje) {
        if (WA === '') {
            avisar('Falta configurar el número de WhatsApp de la tienda');
            return;
        }
        window.open('https://wa.me/' + WA + '?text=' + encodeURIComponent(mensaje), '_blank', 'noopener');
    }

    document.querySelectorAll('[data-wa-enlace]').forEach(function (enlace) {
        var mensaje = enlace.dataset.waMensaje || '';
        if (WA !== '') {
            // Con número configurado es un enlace real (funciona con clic medio, copiar, etc.)
            enlace.href = 'https://wa.me/' + WA + '?text=' + encodeURIComponent(mensaje);
            enlace.target = '_blank';
            enlace.rel = 'noopener';
        } else {
            enlace.addEventListener('click', function (evento) {
                evento.preventDefault();
                abrirWhatsApp(mensaje);
            });
        }
    });
})();
