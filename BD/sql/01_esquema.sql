-- ══════════════════════════════════════════════════════════════
-- N&A Beauty — Esquema de base de datos
-- Compatible con MariaDB 10.4+ (XAMPP 8.2) y MySQL 8.
--
-- Quién escribe cada tabla:
--   WEB (panel)  → categorias, productos, producto_imagenes, producto_variantes
--   POS (sync)   → usuarios, usuario_permisos, ventas, venta_detalle,
--                  entradas, entrada_detalle, inventario_movimientos
--   WEB (propio) → login_intentos
--
-- Todas las llaves de negocio son UUID (CHAR(36)): los genera quien crea
-- el registro (web o POS) y nunca chocan al sincronizar.
-- ══════════════════════════════════════════════════════════════

CREATE DATABASE IF NOT EXISTS nabeauty
    CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE nabeauty;

SET FOREIGN_KEY_CHECKS = 0;

-- ─────────────────────────────────────────────
-- USUARIOS (se crean en el POS y se sincronizan aquí)
-- La web no crea usuarios ni cambia contraseñas: solo los lee.
-- password_hash: bcrypt ($2y$), compatible con password_verify() de PHP.
-- ─────────────────────────────────────────────
DROP TABLE IF EXISTS usuarios;
CREATE TABLE usuarios (
    uuid              CHAR(36)     NOT NULL,
    nombre            VARCHAR(100) NOT NULL,
    usuario           VARCHAR(50)  NOT NULL,
    password_hash     VARCHAR(255) NOT NULL,
    activo            TINYINT(1)   NOT NULL DEFAULT 1,
    ultimo_acceso_web DATETIME     NULL,
    created_at        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (uuid),
    UNIQUE KEY uq_usuarios_usuario (usuario)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Permisos por usuario (los asigna el POS). Claves que usa la web:
--   web.acceso    entrar al panel web
--   web.productos crear y editar productos, fotos y categorías
--   web.reportes  ver ventas y entradas
--   web.exportar  descargar Excel y PDF
DROP TABLE IF EXISTS usuario_permisos;
CREATE TABLE usuario_permisos (
    usuario_uuid CHAR(36)    NOT NULL,
    permiso      VARCHAR(60) NOT NULL,
    PRIMARY KEY (usuario_uuid, permiso),
    CONSTRAINT fk_permisos_usuario FOREIGN KEY (usuario_uuid)
        REFERENCES usuarios (uuid) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Intentos de inicio de sesión: base del límite por IP y por usuario
DROP TABLE IF EXISTS login_intentos;
CREATE TABLE login_intentos (
    id       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    ip       VARCHAR(45)     NOT NULL,
    usuario  VARCHAR(50)     NOT NULL,
    exitoso  TINYINT(1)      NOT NULL DEFAULT 0,
    fecha    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_intentos_ip (ip, fecha),
    KEY idx_intentos_usuario (usuario, fecha)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ─────────────────────────────────────────────
-- CATÁLOGO (lo administra el panel web)
-- ─────────────────────────────────────────────
DROP TABLE IF EXISTS categorias;
CREATE TABLE categorias (
    uuid       CHAR(36)     NOT NULL,
    slug       VARCHAR(80)  NOT NULL,
    nombre     VARCHAR(80)  NOT NULL,
    color      CHAR(7)      NOT NULL DEFAULT '#F6D5DC',
    orden      INT          NOT NULL DEFAULT 0,
    activa     TINYINT(1)   NOT NULL DEFAULT 1,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (uuid),
    UNIQUE KEY uq_categorias_slug (slug)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS productos;
CREATE TABLE productos (
    uuid           CHAR(36)     NOT NULL,
    categoria_uuid CHAR(36)     NOT NULL,
    slug           VARCHAR(160) NOT NULL,
    nombre         VARCHAR(120) NOT NULL,
    marca          VARCHAR(80)  NOT NULL,
    descripcion    TEXT         NULL,
    -- permanente: siempre visible (sin stock dice "Agotado")
    -- temporada:  visible solo con stock
    ciclo          ENUM('permanente','temporada') NOT NULL DEFAULT 'permanente',
    -- archivado reemplaza a borrar: ventas viejas siguen apuntando aquí
    estado         ENUM('activo','archivado')     NOT NULL DEFAULT 'activo',
    destacado      TINYINT(1)   NOT NULL DEFAULT 0,
    created_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (uuid),
    UNIQUE KEY uq_productos_slug (slug),
    KEY idx_productos_categoria (categoria_uuid),
    KEY idx_productos_estado (estado, created_at),
    CONSTRAINT fk_productos_categoria FOREIGN KEY (categoria_uuid)
        REFERENCES categorias (uuid)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Fotos: el archivo vive en Content/Uploads/Productos/. orden 0 = principal.
DROP TABLE IF EXISTS producto_imagenes;
CREATE TABLE producto_imagenes (
    uuid          CHAR(36)     NOT NULL,
    producto_uuid CHAR(36)     NOT NULL,
    archivo       VARCHAR(100) NOT NULL,
    orden         INT          NOT NULL DEFAULT 0,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (uuid),
    KEY idx_imagenes_producto (producto_uuid, orden),
    CONSTRAINT fk_imagenes_producto FOREIGN KEY (producto_uuid)
        REFERENCES productos (uuid) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La variante es lo que se vende y se cuenta. Todo producto tiene al menos una.
-- stock es un acumulado de inventario_movimientos (lo mantiene el sistema,
-- nunca se edita a mano desde la web).
DROP TABLE IF EXISTS producto_variantes;
CREATE TABLE producto_variantes (
    uuid          CHAR(36)      NOT NULL,
    producto_uuid CHAR(36)      NOT NULL,
    sku           VARCHAR(40)   NULL,
    tipo          ENUM('unica','tono','talla','color','aroma') NOT NULL DEFAULT 'unica',
    nombre        VARCHAR(60)   NOT NULL,
    color_hex     CHAR(7)       NULL,
    precio        DECIMAL(10,2) NOT NULL,
    precio_oferta DECIMAL(10,2) NULL,
    stock         INT           NOT NULL DEFAULT 0,
    orden         INT           NOT NULL DEFAULT 0,
    activa        TINYINT(1)    NOT NULL DEFAULT 1,
    created_at    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (uuid),
    UNIQUE KEY uq_variantes_sku (sku),
    KEY idx_variantes_producto (producto_uuid, orden),
    CONSTRAINT fk_variantes_producto FOREIGN KEY (producto_uuid)
        REFERENCES productos (uuid)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ─────────────────────────────────────────────
-- VENTAS Y ENTRADAS (las registra el POS)
-- Los nombres de producto y variante se guardan también en el detalle:
-- el reporte de una venta vieja sigue diciendo lo que se vendió aunque
-- luego se cambie el nombre en el catálogo.
-- ─────────────────────────────────────────────
DROP TABLE IF EXISTS ventas;
CREATE TABLE ventas (
    uuid         CHAR(36)      NOT NULL,
    numero       VARCHAR(20)   NOT NULL,
    fecha        DATETIME      NOT NULL,
    usuario_uuid CHAR(36)      NULL,
    metodo_pago  ENUM('efectivo','tarjeta','transferencia','otro') NOT NULL DEFAULT 'efectivo',
    subtotal     DECIMAL(10,2) NOT NULL,
    descuento    DECIMAL(10,2) NOT NULL DEFAULT 0,
    total        DECIMAL(10,2) NOT NULL,
    estado       ENUM('completada','anulada') NOT NULL DEFAULT 'completada',
    origen       VARCHAR(10)   NOT NULL DEFAULT 'pos',
    created_at   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (uuid),
    UNIQUE KEY uq_ventas_numero (numero),
    KEY idx_ventas_fecha (estado, fecha)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS venta_detalle;
CREATE TABLE venta_detalle (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    venta_uuid      CHAR(36)      NOT NULL,
    variante_uuid   CHAR(36)      NOT NULL,
    producto_nombre VARCHAR(150)  NOT NULL,
    variante_nombre VARCHAR(60)   NOT NULL,
    cantidad        INT           NOT NULL,
    precio_unitario DECIMAL(10,2) NOT NULL,
    costo_unitario  DECIMAL(10,2) NULL,
    subtotal        DECIMAL(10,2) NOT NULL,
    PRIMARY KEY (id),
    KEY idx_vdetalle_venta (venta_uuid),
    KEY idx_vdetalle_variante (variante_uuid),
    CONSTRAINT fk_vdetalle_venta FOREIGN KEY (venta_uuid) REFERENCES ventas (uuid) ON DELETE CASCADE,
    CONSTRAINT fk_vdetalle_variante FOREIGN KEY (variante_uuid) REFERENCES producto_variantes (uuid)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS entradas;
CREATE TABLE entradas (
    uuid         CHAR(36)      NOT NULL,
    numero       VARCHAR(20)   NOT NULL,
    fecha        DATETIME      NOT NULL,
    proveedor    VARCHAR(120)  NULL,
    usuario_uuid CHAR(36)      NULL,
    total_costo  DECIMAL(10,2) NOT NULL DEFAULT 0,
    nota         VARCHAR(255)  NULL,
    origen       VARCHAR(10)   NOT NULL DEFAULT 'pos',
    created_at   DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (uuid),
    UNIQUE KEY uq_entradas_numero (numero),
    KEY idx_entradas_fecha (fecha)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TABLE IF EXISTS entrada_detalle;
CREATE TABLE entrada_detalle (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    entrada_uuid    CHAR(36)      NOT NULL,
    variante_uuid   CHAR(36)      NOT NULL,
    producto_nombre VARCHAR(150)  NOT NULL,
    variante_nombre VARCHAR(60)   NOT NULL,
    cantidad        INT           NOT NULL,
    costo_unitario  DECIMAL(10,2) NOT NULL,
    subtotal        DECIMAL(10,2) NOT NULL,
    PRIMARY KEY (id),
    KEY idx_edetalle_entrada (entrada_uuid),
    KEY idx_edetalle_variante (variante_uuid),
    CONSTRAINT fk_edetalle_entrada FOREIGN KEY (entrada_uuid) REFERENCES entradas (uuid) ON DELETE CASCADE,
    CONSTRAINT fk_edetalle_variante FOREIGN KEY (variante_uuid) REFERENCES producto_variantes (uuid)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Libro de inventario: cada cambio de stock es una fila (+ entra, − sale).
-- Web y POS solo AGREGAN filas, nunca editan: así no hay choques al sincronizar.
DROP TABLE IF EXISTS inventario_movimientos;
CREATE TABLE inventario_movimientos (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    variante_uuid   CHAR(36)    NOT NULL,
    tipo            ENUM('entrada','venta','ajuste','anulacion') NOT NULL,
    cantidad        INT         NOT NULL,
    referencia_uuid CHAR(36)    NULL,
    fecha           DATETIME    NOT NULL,
    origen          VARCHAR(10) NOT NULL DEFAULT 'pos',
    PRIMARY KEY (id),
    KEY idx_movimientos_variante (variante_uuid),
    KEY idx_movimientos_fecha (fecha),
    CONSTRAINT fk_movimientos_variante FOREIGN KEY (variante_uuid) REFERENCES producto_variantes (uuid)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET FOREIGN_KEY_CHECKS = 1;
