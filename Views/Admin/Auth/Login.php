<?php /** Login del panel. $ultimo: último usuario escrito (para no reescribirlo tras un error). */ ?>
<section class="login">
    <div class="login__marca">
        <img src="<?= e(asset('Demo/img/logo-blanco.png')) ?>" alt="<?= e(APP_NAME) ?>" width="180" height="72">
        <p>Panel administrativo</p>
    </div>

    <form class="login__form" method="post" action="<?= e(url('admin/auth/entrar')) ?>" data-un-envio>
        <input type="hidden" name="_csrf" value="<?= e($csrf) ?>">

        <h1 class="login__titulo">Bienvenida</h1>
        <p class="login__sub">Entra con el usuario que te dieron en el sistema de la tienda.</p>

        <label class="campo">
            <span class="campo__etiqueta">Usuario</span>
            <span class="campo__con-ico">
                <?= icono('usuario') ?>
                <input type="text" name="usuario" value="<?= e($ultimo) ?>" autocomplete="username"
                       autocapitalize="none" spellcheck="false" maxlength="50" required <?= $ultimo === '' ? 'autofocus' : '' ?>>
            </span>
        </label>

        <label class="campo">
            <span class="campo__etiqueta">Contraseña</span>
            <span class="campo__con-ico">
                <?= icono('candado') ?>
                <input type="password" name="password" autocomplete="current-password" maxlength="200" required <?= $ultimo !== '' ? 'autofocus' : '' ?>>
                <button class="campo__ver" type="button" data-ver-clave aria-label="Mostrar contraseña"><?= icono('ojo') ?></button>
            </span>
        </label>

        <button class="btn btn--primario btn--bloque" type="submit">Entrar</button>

        <p class="login__nota">
            ¿Olvidaste tu contraseña? Se cambia desde el sistema de la tienda (POS).
        </p>
    </form>

    <p class="login__credito">
        <a href="<?= e(url()) ?>">← Volver a la tienda</a>
    </p>
</section>
