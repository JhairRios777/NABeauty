/* ══════════════════════════════════════════════════════════════
   N&A Beauty — JS del panel administrativo
   Sin dependencias. Todo es mejora progresiva: sin JS el panel sigue
   funcionando (formularios normales); con JS es más cómodo en el teléfono.
   Se engancha por atributos data-*; no hay JS en línea (CSP).
   ══════════════════════════════════════════════════════════════ */
(function () {
    'use strict';

    document.documentElement.classList.add('js');

    var $  = function (sel, ctx) { return (ctx || document).querySelector(sel); };
    var $$ = function (sel, ctx) { return Array.prototype.slice.call((ctx || document).querySelectorAll(sel)); };

    // ─────────────────────────────────────────────
    // 1. FORMULARIOS: un solo envío, autoenvío de filtros, ver contraseña
    // ─────────────────────────────────────────────

    // Evita el doble envío (doble toque en el teléfono = producto duplicado)
    $$('form[data-un-envio]').forEach(function (form) {
        form.addEventListener('submit', function () {
            if (form.dataset.enviando) { return; }
            form.dataset.enviando = '1';
            $$('button[type="submit"], button:not([type])', form).forEach(function (b) {
                b.setAttribute('aria-busy', 'true');
            });
        });
    });

    // Filtros del listado: cambiar un select ya filtra
    $$('form[data-auto-enviar]').forEach(function (form) {
        $$('select', form).forEach(function (s) {
            s.addEventListener('change', function () {
                // URL limpia: los filtros vacíos no viajan
                $$('input, select', form).forEach(function (c) { if (!c.value) { c.disabled = true; } });
                form.submit();
            });
        });
    });

    $$('[data-ver-clave]').forEach(function (btn) {
        var input = btn.parentElement.querySelector('input');
        btn.addEventListener('click', function () {
            var ver = input.type === 'password';
            input.type = ver ? 'text' : 'password';
            btn.setAttribute('aria-label', ver ? 'Ocultar contraseña' : 'Mostrar contraseña');
            input.focus();
        });
    });

    // Resumen de errores: se lleva el foco (y el lector de pantalla) ahí
    var enfocar = $('[data-enfocar]');
    if (enfocar) { enfocar.focus(); }

    // ─────────────────────────────────────────────
    // 2. CONFIRMACIÓN EN DOS TOQUES (sin ventanas emergentes)
    // Primer toque: el botón cambia a "¿Seguro?". Segundo toque: se envía.
    // ─────────────────────────────────────────────
    $$('[data-confirmar]').forEach(function (btn) {
        var original = btn.innerHTML;
        var timer = null;
        btn.addEventListener('click', function (ev) {
            if (btn.classList.contains('btn--confirmando')) { return; }   // segundo toque: sigue el envío
            ev.preventDefault();
            btn.classList.add('btn--confirmando');
            btn.textContent = '¿Seguro?';
            timer = setTimeout(function () {
                btn.classList.remove('btn--confirmando');
                btn.innerHTML = original;
            }, 4000);
        });
        btn.addEventListener('blur', function () {
            if (timer) { clearTimeout(timer); timer = null; }
            setTimeout(function () {
                btn.classList.remove('btn--confirmando');
                btn.innerHTML = original;
            }, 150);
        });
    });

    // ─────────────────────────────────────────────
    // 3. AVISO DE CAMBIOS SIN GUARDAR
    // ─────────────────────────────────────────────
    $$('form[data-avisar-cambios]').forEach(function (form) {
        var sucio = false;
        form.addEventListener('input', function () { sucio = true; });
        form.addEventListener('change', function () { sucio = true; });
        form.addEventListener('submit', function () { sucio = false; });
        window.addEventListener('beforeunload', function (ev) {
            if (sucio) { ev.preventDefault(); ev.returnValue = ''; }
        });
    });

    // ─────────────────────────────────────────────
    // 4. VARIANTES (tono / talla / color / aroma)
    // ─────────────────────────────────────────────
    var contVariantes = $('[data-variantes]');
    if (contVariantes) {
        var plantilla = $('#tpl-variante');
        var siguiente = $$('[data-variante]', contVariantes).length;

        var actualizarSola = function () {
            var filas = $$('[data-variante]', contVariantes);
            filas.forEach(function (f) { f.classList.toggle('variante--sola', filas.length === 1); });
        };

        $$('[data-tipo-variante] input[type="radio"]').forEach(function (radio) {
            radio.addEventListener('change', function () {
                contVariantes.dataset.tipo = radio.value;
                $$('[data-etiqueta-nombre]', contVariantes).forEach(function (el) {
                    el.textContent = radio.dataset.etiqueta;
                });
                var primera = $('[data-variante] input[name$="[nombre]"]', contVariantes);
                if (radio.value !== 'unica' && primera && primera.value === '') { primera.focus(); }
            });
        });

        $('[data-agregar-variante]').addEventListener('click', function () {
            var html = plantilla.innerHTML.replace(/__i__/g, String(siguiente++));
            var tmp = document.createElement('div');
            tmp.innerHTML = html;
            var fila = tmp.firstElementChild;
            var tipo = $('[data-tipo-variante] input:checked');
            if (tipo) { $('[data-etiqueta-nombre]', fila).textContent = tipo.dataset.etiqueta; }
            contVariantes.appendChild(fila);
            actualizarSola();
            $('input[name$="[nombre]"]', fila).focus();
        });

        contVariantes.addEventListener('click', function (ev) {
            var btn = ev.target.closest('[data-quitar-variante]');
            if (!btn) { return; }
            var filas = $$('[data-variante]', contVariantes);
            if (filas.length > 1) {
                btn.closest('[data-variante]').remove();
                contVariantes.closest('form').dispatchEvent(new Event('change'));
                actualizarSola();
            }
        });

        actualizarSola();
    }

    // ─────────────────────────────────────────────
    // 5. FOTOS: vista previa + reducción ANTES de subir
    // Una foto del teléfono pesa 3–8 MB; reducida a 1200 px pesa ~200 KB.
    // Sube más rápido con datos móviles y no choca con el límite de PHP.
    // El servidor igual la valida y la re-codifica (esto es solo comodidad).
    // ─────────────────────────────────────────────
    $$('input[type="file"][data-max]').forEach(function (input) {
        var max   = parseInt(input.dataset.max, 10) || 8;
        var lado  = parseInt(input.dataset.lado, 10) || 1200;
        var previa = $('[data-previa]');
        var zona  = input.closest('[data-subir]');
        var urls  = [];

        ['dragenter', 'dragover'].forEach(function (t) {
            zona.addEventListener(t, function () { zona.classList.add('arrastrando'); });
        });
        ['dragleave', 'drop'].forEach(function (t) {
            zona.addEventListener(t, function () { zona.classList.remove('arrastrando'); });
        });

        var reducir = function (archivo) {
            if (!window.createImageBitmap || !/^image\/(jpeg|png|webp)$/.test(archivo.type)) {
                return Promise.resolve(archivo);
            }
            return createImageBitmap(archivo, { imageOrientation: 'from-image' }).then(function (bmp) {
                var escala = Math.min(1, lado / Math.max(bmp.width, bmp.height));
                if (escala === 1 && archivo.size < 1.5 * 1024 * 1024) { bmp.close && bmp.close(); return archivo; }
                var c = document.createElement('canvas');
                c.width = Math.round(bmp.width * escala);
                c.height = Math.round(bmp.height * escala);
                var ctx = c.getContext('2d');
                if (archivo.type !== 'image/jpeg') {   // PNG con transparencia → fondo blanco
                    ctx.fillStyle = '#fff';
                    ctx.fillRect(0, 0, c.width, c.height);
                }
                ctx.drawImage(bmp, 0, 0, c.width, c.height);
                bmp.close && bmp.close();
                return new Promise(function (ok) {
                    c.toBlob(function (blob) {
                        if (!blob) { ok(archivo); return; }
                        var nombre = archivo.name.replace(/\.[^.]+$/, '') + '.jpg';
                        ok(new File([blob], nombre, { type: 'image/jpeg', lastModified: Date.now() }));
                    }, 'image/jpeg', 0.88);
                });
            }).catch(function () { return archivo; });
        };

        input.addEventListener('change', function () {
            var archivos = Array.prototype.slice.call(input.files || []);
            urls.forEach(function (u) { URL.revokeObjectURL(u); });
            urls = [];
            previa.innerHTML = '';

            var aviso = archivos.length > max;
            archivos = archivos.slice(0, max);
            if (!archivos.length) { return; }

            zona.querySelector('.subir__texto').innerHTML = '<strong>Preparando ' + archivos.length + ' foto' + (archivos.length === 1 ? '' : 's') + '…</strong>';

            Promise.all(archivos.map(reducir)).then(function (listos) {
                // Reemplaza la selección por las versiones reducidas
                try {
                    var dt = new DataTransfer();
                    listos.forEach(function (f) { dt.items.add(f); });
                    input.files = dt.files;
                } catch (e) { /* navegador viejo: se suben las originales */ }

                listos.forEach(function (f, i) {
                    var url = URL.createObjectURL(f);
                    urls.push(url);
                    var li = document.createElement('li');
                    li.className = 'foto foto--previa';
                    var img = document.createElement('img');
                    img.src = url;
                    img.alt = 'Foto nueva ' + (i + 1);
                    var sello = document.createElement('span');
                    sello.className = 'foto__sello';
                    sello.textContent = 'Nueva';
                    li.appendChild(img);
                    li.appendChild(sello);
                    previa.appendChild(li);
                });

                zona.querySelector('.subir__texto').innerHTML =
                    '<strong>' + listos.length + ' foto' + (listos.length === 1 ? '' : 's') + ' lista' + (listos.length === 1 ? '' : 's') + '</strong> · se suben al guardar' +
                    (aviso ? ' (máximo ' + max + ')' : '');
            });
        });
    });

    // ─────────────────────────────────────────────
    // 6. GLOBO DE LOS GRÁFICOS (mouse, teclado y toque)
    // ─────────────────────────────────────────────
    var tip = $('.tip');
    if (tip) {
        var activa = null;
        var mostrar = function (barra) {
            if (activa) { activa.classList.remove('activa'); }
            activa = barra;
            barra.classList.add('activa');
            tip.innerHTML = '';
            var t = document.createElement('span'); t.textContent = barra.dataset.tipTitulo;
            var v = document.createElement('strong'); v.textContent = barra.dataset.tip;
            tip.appendChild(t); tip.appendChild(v);
            if (barra.dataset.tipDetalle) {
                var d = document.createElement('span'); d.textContent = barra.dataset.tipDetalle;
                tip.appendChild(d);
            }
            tip.hidden = false;
            var r = barra.getBoundingClientRect();
            var x = r.left + r.width / 2;
            var mitad = tip.offsetWidth / 2 + 8;
            x = Math.max(mitad, Math.min(window.innerWidth - mitad, x));
            tip.style.left = x + 'px';
            tip.style.top = Math.max(tip.offsetHeight + 16, r.top) + 'px';
        };
        var ocultar = function () {
            tip.hidden = true;
            if (activa) { activa.classList.remove('activa'); activa = null; }
        };

        $$('.grafico__barra').forEach(function (b) {
            b.addEventListener('pointerenter', function (ev) { if (ev.pointerType === 'mouse') { mostrar(b); } });
            b.addEventListener('pointerleave', function (ev) { if (ev.pointerType === 'mouse') { ocultar(); } });
            b.addEventListener('focus', function () { mostrar(b); });
            b.addEventListener('blur', ocultar);
            b.addEventListener('click', function () { activa === b ? ocultar() : mostrar(b); });
        });
        window.addEventListener('scroll', ocultar, { passive: true });
        document.addEventListener('keydown', function (ev) { if (ev.key === 'Escape') { ocultar(); } });
    }

    // Menú de cuenta: se cierra al tocar fuera
    var cuenta = $('.cuenta');
    if (cuenta) {
        document.addEventListener('click', function (ev) {
            if (cuenta.open && !cuenta.contains(ev.target)) { cuenta.open = false; }
        });
    }
})();
