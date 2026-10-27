-- =====================================================================
-- N&A Beauty POS — Esquema SQLite (base local de la tienda)
--
-- Convenciones (las mismas de DeskCod):
--   * Dinero en CENTAVOS (INTEGER). L 1,250.50 se guarda como 125050.
--   * Fechas como TEXT ISO 'YYYY-MM-DD HH:MM:SS' en hora local de la tienda.
--   * "Hoy" en SQL = date('now','localtime'): el POS corre en una sola PC.
--   * Catálogo, ventas y entradas usan UUID (TEXT): son los mismos identificadores
--     que la tienda en línea (MySQL), así la sincronización nunca choca.
--   * El stock de cada variante es un ACUMULADO del libro inventario_movimientos:
--     lo mantiene un trigger; nadie lo escribe a mano.
--
-- Se aplica completo en cada arranque: todo es idempotente (IF NOT EXISTS).
-- =====================================================================

PRAGMA foreign_keys = ON;

-- ── Seguridad ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS roles (
    id           INTEGER PRIMARY KEY,
    clave        TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    nombre       TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    descripcion  TEXT,
    sistema      INTEGER NOT NULL DEFAULT 0 CHECK (sistema IN (0,1)),
    creado       TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

-- Permiso = nombre del enum NABeauty.Core.Seguridad.Permiso
CREATE TABLE IF NOT EXISTS rol_permisos (
    rol_id   INTEGER NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permiso  TEXT    NOT NULL,
    PRIMARY KEY (rol_id, permiso)
);

DROP TRIGGER IF EXISTS trg_roles_sistema_borrar;
CREATE TRIGGER trg_roles_sistema_borrar BEFORE DELETE ON roles WHEN OLD.sistema = 1
BEGIN SELECT RAISE(ABORT, 'El rol Administrador no se puede eliminar.'); END;
DROP TRIGGER IF EXISTS trg_roles_sistema_editar;
CREATE TRIGGER trg_roles_sistema_editar BEFORE UPDATE OF clave, sistema ON roles WHEN OLD.sistema = 1
BEGIN SELECT RAISE(ABORT, 'El rol Administrador no se puede modificar.'); END;
DROP TRIGGER IF EXISTS trg_roles_en_uso;
CREATE TRIGGER trg_roles_en_uso BEFORE DELETE ON roles
WHEN EXISTS (SELECT 1 FROM usuarios WHERE rol = OLD.clave)
BEGIN SELECT RAISE(ABORT, 'El rol tiene usuarios asignados: cámbialos de rol antes de eliminarlo.'); END;

-- Los usuarios se crean AQUÍ (el POS es la fuente) y viajan a la web con su uuid y permisos web.
-- password_hash: PBKDF2-SHA256 (el mismo formato de AnaMarcolPOS y DeskCod).
CREATE TABLE IF NOT EXISTS usuarios (
    id                     INTEGER PRIMARY KEY,
    uuid                   TEXT    NOT NULL UNIQUE,
    nombre                 TEXT    NOT NULL,
    usuario                TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    password_hash          TEXT    NOT NULL,
    rol                    TEXT    NOT NULL REFERENCES roles(clave),
    activo                 INTEGER NOT NULL DEFAULT 1 CHECK (activo IN (0,1)),
    debe_cambiar_password  INTEGER NOT NULL DEFAULT 1 CHECK (debe_cambiar_password IN (0,1)),
    ultimo_acceso          TEXT,
    creado                 TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    actualizado            TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE IF NOT EXISTS login_intentos (
    id       INTEGER PRIMARY KEY,
    usuario  TEXT NOT NULL COLLATE NOCASE,
    creado   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS ix_login_usuario ON login_intentos (usuario, creado);

-- Registro de acciones sensibles. Solo admite inserciones.
CREATE TABLE IF NOT EXISTS bitacora (
    id          INTEGER PRIMARY KEY,
    usuario_id  INTEGER REFERENCES usuarios(id),
    accion      TEXT NOT NULL,
    entidad     TEXT NOT NULL,
    entidad_id  INTEGER,
    detalle     TEXT,
    creado      TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS ix_bitacora_entidad ON bitacora (entidad, entidad_id);
DROP TRIGGER IF EXISTS trg_bitacora_sin_update;
CREATE TRIGGER trg_bitacora_sin_update BEFORE UPDATE ON bitacora
BEGIN SELECT RAISE(ABORT, 'La bitácora no se modifica.'); END;
DROP TRIGGER IF EXISTS trg_bitacora_sin_delete;
CREATE TRIGGER trg_bitacora_sin_delete BEFORE DELETE ON bitacora
BEGIN SELECT RAISE(ABORT, 'La bitácora no se borra.'); END;

-- Ajustes clave/valor (datos del negocio, conexión con la web)
CREATE TABLE IF NOT EXISTS config (
    clave  TEXT PRIMARY KEY,
    valor  TEXT
);

-- Numeraciones sin huecos (V-000001, E-0001)
CREATE TABLE IF NOT EXISTS secuencias (
    clave   TEXT PRIMARY KEY,
    ultimo  INTEGER NOT NULL DEFAULT 0
);

-- ── Catálogo (misma forma que la tienda en línea) ────────────────────
CREATE TABLE IF NOT EXISTS categorias (
    uuid        TEXT    PRIMARY KEY,
    slug        TEXT    NOT NULL UNIQUE,
    nombre      TEXT    NOT NULL,
    color       TEXT    NOT NULL DEFAULT '#F6D5DC',
    orden       INTEGER NOT NULL DEFAULT 0,
    activa      INTEGER NOT NULL DEFAULT 1 CHECK (activa IN (0,1)),
    actualizado TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE IF NOT EXISTS productos (
    uuid            TEXT    PRIMARY KEY,
    categoria_uuid  TEXT    NOT NULL REFERENCES categorias(uuid),
    slug            TEXT    NOT NULL UNIQUE,
    nombre          TEXT    NOT NULL,
    marca           TEXT    NOT NULL,
    descripcion     TEXT,
    ciclo           TEXT    NOT NULL DEFAULT 'permanente' CHECK (ciclo IN ('permanente','temporada')),
    estado          TEXT    NOT NULL DEFAULT 'activo'     CHECK (estado IN ('activo','archivado')),
    destacado       INTEGER NOT NULL DEFAULT 0 CHECK (destacado IN (0,1)),
    creado          TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    actualizado     TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS ix_productos_categoria ON productos (categoria_uuid);

-- Fotos del producto (misma forma que producto_imagenes de la web). El archivo vive en
-- %LOCALAPPDATA%\NABeautyPOS\Fotos\ en dos tamaños: <archivo> (máx. 1200 px) y mini_<archivo> (máx. 200 px).
-- orden 0 = foto principal. Se sincronizan con la tienda en línea.
CREATE TABLE IF NOT EXISTS producto_imagenes (
    uuid           TEXT    PRIMARY KEY,
    producto_uuid  TEXT    NOT NULL REFERENCES productos(uuid) ON DELETE CASCADE,
    archivo        TEXT    NOT NULL,
    orden          INTEGER NOT NULL DEFAULT 0,
    creado         TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS ix_imagenes_producto ON producto_imagenes (producto_uuid, orden);

-- La variante es lo que se vende y se cuenta. Todo producto tiene al menos una (tipo 'unica').
CREATE TABLE IF NOT EXISTS producto_variantes (
    uuid           TEXT    PRIMARY KEY,
    producto_uuid  TEXT    NOT NULL REFERENCES productos(uuid),
    sku            TEXT    UNIQUE,
    codigo_barras  TEXT    UNIQUE,
    tipo           TEXT    NOT NULL DEFAULT 'unica' CHECK (tipo IN ('unica','tono','talla','color','aroma')),
    nombre         TEXT    NOT NULL,
    color_hex      TEXT,
    precio         INTEGER NOT NULL CHECK (precio > 0),
    precio_oferta  INTEGER CHECK (precio_oferta IS NULL OR (precio_oferta > 0 AND precio_oferta < precio)),
    costo          INTEGER CHECK (costo IS NULL OR costo >= 0),
    stock          INTEGER NOT NULL DEFAULT 0,
    orden          INTEGER NOT NULL DEFAULT 0,
    activa         INTEGER NOT NULL DEFAULT 1 CHECK (activa IN (0,1)),
    actualizado    TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS ix_variantes_producto ON producto_variantes (producto_uuid, orden);

-- Una variante con movimientos (vendida o con stock) no se borra: se desactiva
DROP TRIGGER IF EXISTS trg_variantes_con_historial;
CREATE TRIGGER trg_variantes_con_historial BEFORE DELETE ON producto_variantes
WHEN EXISTS (SELECT 1 FROM inventario_movimientos WHERE variante_uuid = OLD.uuid)
BEGIN SELECT RAISE(ABORT, 'Esta opción ya tiene movimientos de inventario: desactívala en lugar de quitarla.'); END;
-- Un producto no se borra: se archiva (las ventas viejas apuntan a él)
DROP TRIGGER IF EXISTS trg_productos_sin_delete;
CREATE TRIGGER trg_productos_sin_delete BEFORE DELETE ON productos
BEGIN SELECT RAISE(ABORT, 'Los productos no se borran: se archivan.'); END;

-- ── Ventas y entradas ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ventas (
    uuid          TEXT    PRIMARY KEY,
    numero        TEXT    NOT NULL UNIQUE,
    fecha         TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    usuario_id    INTEGER REFERENCES usuarios(id),
    metodo_pago   TEXT    NOT NULL DEFAULT 'efectivo' CHECK (metodo_pago IN ('efectivo','tarjeta','transferencia','otro')),
    subtotal      INTEGER NOT NULL CHECK (subtotal >= 0),
    descuento     INTEGER NOT NULL DEFAULT 0 CHECK (descuento >= 0),
    total         INTEGER NOT NULL CHECK (total >= 0),
    estado        TEXT    NOT NULL DEFAULT 'completada' CHECK (estado IN ('completada','anulada')),
    motivo_anulacion TEXT,
    recibido      INTEGER,                 -- efectivo entregado (para reimprimir el recibo con el cambio)
    anulada_por   INTEGER REFERENCES usuarios(id),
    anulada_fecha TEXT,
    CHECK (total = subtotal - descuento)
);
CREATE INDEX IF NOT EXISTS ix_ventas_fecha ON ventas (estado, fecha);
CREATE INDEX IF NOT EXISTS ix_ventas_fecha_sola ON ventas (fecha);

-- Nombre de producto y variante copiados: una venta vieja dice lo que se vendió aunque cambie el catálogo
CREATE TABLE IF NOT EXISTS venta_detalle (
    id               INTEGER PRIMARY KEY,
    venta_uuid       TEXT    NOT NULL REFERENCES ventas(uuid) ON DELETE CASCADE,
    variante_uuid    TEXT    NOT NULL REFERENCES producto_variantes(uuid),
    producto_nombre  TEXT    NOT NULL,
    variante_nombre  TEXT    NOT NULL,
    cantidad         INTEGER NOT NULL CHECK (cantidad > 0),
    precio_unitario  INTEGER NOT NULL CHECK (precio_unitario >= 0),
    costo_unitario   INTEGER,
    subtotal         INTEGER NOT NULL,
    CHECK (subtotal = cantidad * precio_unitario)
);
CREATE INDEX IF NOT EXISTS ix_vdetalle_venta ON venta_detalle (venta_uuid);
CREATE INDEX IF NOT EXISTS ix_vdetalle_variante ON venta_detalle (variante_uuid);

CREATE TABLE IF NOT EXISTS entradas (
    uuid         TEXT    PRIMARY KEY,
    numero       TEXT    NOT NULL UNIQUE,
    fecha        TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    proveedor    TEXT,
    usuario_id   INTEGER REFERENCES usuarios(id),
    total_costo  INTEGER NOT NULL DEFAULT 0,
    nota         TEXT
);

CREATE INDEX IF NOT EXISTS ix_entradas_fecha ON entradas (fecha);

CREATE TABLE IF NOT EXISTS entrada_detalle (
    id               INTEGER PRIMARY KEY,
    entrada_uuid     TEXT    NOT NULL REFERENCES entradas(uuid) ON DELETE CASCADE,
    variante_uuid    TEXT    NOT NULL REFERENCES producto_variantes(uuid),
    producto_nombre  TEXT    NOT NULL,
    variante_nombre  TEXT    NOT NULL,
    cantidad         INTEGER NOT NULL CHECK (cantidad > 0),
    costo_unitario   INTEGER NOT NULL CHECK (costo_unitario >= 0),
    subtotal         INTEGER NOT NULL
);

-- Libro de inventario: cada cambio de stock es una fila (+ entra, − sale). Solo INSERT.
CREATE TABLE IF NOT EXISTS inventario_movimientos (
    id               INTEGER PRIMARY KEY,
    variante_uuid    TEXT    NOT NULL REFERENCES producto_variantes(uuid),
    tipo             TEXT    NOT NULL CHECK (tipo IN ('entrada','venta','ajuste','anulacion')),
    cantidad         INTEGER NOT NULL CHECK (cantidad <> 0),
    referencia_uuid  TEXT,
    fecha            TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    usuario_id       INTEGER REFERENCES usuarios(id),
    nota             TEXT
);
CREATE INDEX IF NOT EXISTS ix_movimientos_variante ON inventario_movimientos (variante_uuid);
CREATE INDEX IF NOT EXISTS ix_movimientos_tipo ON inventario_movimientos (tipo, fecha);

-- El stock lo mantiene la base: cada movimiento suma (o resta) a su variante
DROP TRIGGER IF EXISTS trg_movimiento_stock;
CREATE TRIGGER trg_movimiento_stock AFTER INSERT ON inventario_movimientos
BEGIN
    UPDATE producto_variantes SET stock = stock + NEW.cantidad, actualizado = datetime('now','localtime')
     WHERE uuid = NEW.variante_uuid;
END;
DROP TRIGGER IF EXISTS trg_movimientos_sin_update;
CREATE TRIGGER trg_movimientos_sin_update BEFORE UPDATE ON inventario_movimientos
BEGIN SELECT RAISE(ABORT, 'Los movimientos de inventario no se modifican: registra un ajuste.'); END;
DROP TRIGGER IF EXISTS trg_movimientos_sin_delete;
CREATE TRIGGER trg_movimientos_sin_delete BEFORE DELETE ON inventario_movimientos
BEGIN SELECT RAISE(ABORT, 'Los movimientos de inventario no se borran: registra un ajuste.'); END;
-- El stock solo cambia por movimientos (el trigger de arriba usa este mismo UPDATE, por eso se compara)
DROP TRIGGER IF EXISTS trg_stock_solo_por_movimientos;
CREATE TRIGGER trg_stock_solo_por_movimientos BEFORE UPDATE OF stock ON producto_variantes
WHEN NEW.stock <> OLD.stock + COALESCE((SELECT cantidad FROM inventario_movimientos WHERE variante_uuid = NEW.uuid ORDER BY id DESC LIMIT 1), 0)
BEGIN SELECT RAISE(ABORT, 'El stock solo cambia con entradas, ventas o ajustes.'); END;

-- Una venta completada no se borra ni se edita: se anula
DROP TRIGGER IF EXISTS trg_ventas_sin_delete;
CREATE TRIGGER trg_ventas_sin_delete BEFORE DELETE ON ventas
BEGIN SELECT RAISE(ABORT, 'Las ventas no se borran: se anulan.'); END;
DROP TRIGGER IF EXISTS trg_ventas_inmutables;
CREATE TRIGGER trg_ventas_inmutables BEFORE UPDATE OF numero, fecha, subtotal, descuento, total, metodo_pago ON ventas
BEGIN SELECT RAISE(ABORT, 'Una venta registrada no se modifica: anúlala y registra otra.'); END;
-- Una entrada registrada tampoco se borra ni se edita (el stock ya se movió): se corrige con un ajuste
DROP TRIGGER IF EXISTS trg_entradas_sin_delete;
CREATE TRIGGER trg_entradas_sin_delete BEFORE DELETE ON entradas
BEGIN SELECT RAISE(ABORT, 'Las entradas no se borran: corrige el stock con un ajuste de inventario.'); END;
DROP TRIGGER IF EXISTS trg_ventas_anular;
CREATE TRIGGER trg_ventas_anular BEFORE UPDATE OF estado ON ventas
WHEN OLD.estado = 'anulada' OR NEW.motivo_anulacion IS NULL OR trim(NEW.motivo_anulacion) = ''
BEGIN SELECT RAISE(ABORT, 'Para anular una venta se requiere un motivo, y una venta anulada no vuelve atrás.'); END;

-- ── Sincronización con la tienda en línea (cola de salida) ───────────
-- Cada cambio que la web debe conocer deja aquí una fila; el sincronizador la envía y la marca.
CREATE TABLE IF NOT EXISTS sync_cola (
    id         INTEGER PRIMARY KEY,
    entidad    TEXT NOT NULL,
    uuid       TEXT NOT NULL,
    operacion  TEXT NOT NULL CHECK (operacion IN ('crear','actualizar','anular')),
    creado     TEXT NOT NULL DEFAULT (datetime('now','localtime')),
    enviado    TEXT,
    intentos   INTEGER NOT NULL DEFAULT 0,
    error      TEXT
);
CREATE INDEX IF NOT EXISTS ix_sync_pendientes ON sync_cola (enviado, id);
