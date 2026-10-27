# N&A Beauty POS — punto de venta local

Sistema de escritorio (WPF .NET 10 + SQLite) para la tienda física de N&A Beauty.
Funciona **sin internet**. Vive en la carpeta `POS/` del mismo repositorio que la tienda en línea (la raíz):
el POS envía usuarios con sus permisos web, ventas, entradas y movimientos de inventario; la web le
envía el catálogo y las fotos.

Arquitectura tomada de **DeskCod** (capas, pruebas, seguridad) y login con el diseño de **AnaMarcolPOS**.
Colores y tipografía del logo de N&A Beauty (vino `#9E1B2A`, rosa `#F6D5DC`, negro `#1A1416`;
Anton + DM Sans), en **modo claro y oscuro**.

## Estado (fase 3: todas las pantallas)

| Módulo | Qué hace |
|---|---|
| **Inicio** | Cifras del día, semana y mes; gráfico de 14 días; más vendidos; por agotarse |
| **Caja** | Escáner/búsqueda, carrito, descuento, efectivo con cambio, tarjeta, transferencia; recibo impreso (manual o automático) |
| **Ventas** | Historial con filtros (período, pago, vendedora, estado, número); detalle; reimprimir recibo; anular con motivo (devuelve el stock) |
| **Productos / Categorías** | Tonos, tallas, colores o aromas; precio, oferta, costo, SKU, código de barras, stock inicial, fotos |
| **Entradas** | Mercadería que llega (proveedor, cantidad, costo; actualiza el costo del producto) y **ajustes** de inventario con motivo |
| **Reportes** | Por período con comparación; gráfico por día o mes; más vendidos, categorías, métodos de pago y vendedoras; **Excel** y **PDF** |
| **Usuarios** | Crear con contraseña temporal, editar, desactivar, restablecer contraseña, desbloquear |
| **Roles y permisos** | Roles propios con permisos del POS y del panel web |
| **Ajustes** | Negocio y recibo (58/80 mm), seguridad del ingreso, conexión con la tienda en línea, respaldos |

**Pendiente:** la sincronización automática con la tienda en línea (la conexión ya se configura en Ajustes y
todos los cambios esperan en la cola `sync_cola`) y la apertura/cierre de caja por turno.

### Excel y PDF
- **Excel:** se genera un `.xlsx` propio (sin paquetes) con 5 hojas: Resumen, Productos, Categorías, Pagos y vendedoras, Detalle.
- **PDF:** botón "PDF / Imprimir" → en la impresora elige **Microsoft Print to PDF** (viene con Windows).
- Los dos salen del mismo cálculo que la pantalla: los números siempre coinciden.

### Recibo
Ajustes → Negocio y recibo: nombre, RTN, teléfono, dirección, mensaje al pie y papel de 58 u 80 mm.
Con "Imprimir automáticamente al cobrar" va directo a la impresora **predeterminada** de Windows
(pon la térmica como predeterminada). Si la impresora falla, la venta queda registrada igual.

### Caja — teclado
| Tecla | Acción |
|---|---|
| Escribir / escanear (en cualquier parte de la caja) | Va a la búsqueda |
| `Enter` en la búsqueda | Agrega el código exacto (escáner) o el único resultado |
| `Esc` | Limpia la búsqueda |
| `Enter` en "Efectivo recibido" o `F12` | Cobra |

Al cobrar, el sistema vuelve a leer precio y stock dentro de la transacción: si otra venta se llevó lo último,
el precio cambió o el producto se archivó, la venta **no** se registra y se avisa qué pasó.
La venta en curso no se pierde al ir a otra sección del menú.

### Fotos
Se guardan en `%LOCALAPPDATA%\NABeautyPOS\Fotos\` en dos tamaños: `<uuid>.jpg` (máx. 1200 px, la que irá a la
tienda en línea) y `mini_<uuid>.jpg` (máx. 200 px). El POS **siempre** muestra la miniatura y en tamaño pequeño
(34–48 px en caja y listados, 76 px en la ficha). Se corrige la orientación de las fotos de teléfono. Máximo 8 por producto.

## 1. Cómo correrlo

1. **.NET 10 SDK** (el mismo de DeskCod y AnaMarcolPOS).
2. **Desarrollo:** doble clic en `run-dev.bat` → corre las pruebas, compila y abre la app. Todo queda en `build.log`.
3. **Instalar para uso diario:** `instalar.bat` (pruebas → publica → copia a `%LOCALAPPDATA%\Programs\NABeautyPOS` → accesos directos).
4. **Primer ingreso:** usuario `admin`, contraseña `NAbeauty2026*`. La app obliga a cambiarla al entrar.

## 2. Dónde quedan los datos

| Archivo | Ubicación |
|---|---|
| Base de datos | `%LOCALAPPDATA%\NABeautyPOS\nabeauty.db` |
| Respaldos | `%LOCALAPPDATA%\NABeautyPOS\Respaldos\` (uno por día, se conservan 30) |
| Errores | `%LOCALAPPDATA%\NABeautyPOS\error.log` |
| Tema elegido | `%LOCALAPPDATA%\NABeautyPOS\tema.config` |
| Fotos de productos | `%LOCALAPPDATA%\NABeautyPOS\Fotos\` |

La base **no** va en OneDrive: puede sincronizar a medias un SQLite abierto y corromperlo.
Para empezar de cero, cierra la app y borra `nabeauty.db`.

## 3. Arquitectura

```
src/
  NABeauty.Core          Reglas puras: dinero en centavos, roles y permisos (incluye los del panel web),
                         contraseñas PBKDF2. Sin dependencias.
  NABeauty.Data          SQL + servicios (login, tablero, catálogo, caja, ventas, entradas, reportes,
                         usuarios, roles, ajustes). Exportar/LibroExcel.cs: .xlsx sin dependencias. ADO.NET base: la app le entrega la conexión SQLite.
    Sql/schema.sql       Tablas, índices y TRIGGERS (se aplica en cada arranque, idempotente).
  NABeauty.Presentacion  ViewModels (MVVM) sin WPF: se prueban en cualquier PC.
  NABeauty.App           WPF: ventanas, vistas, estilos, temas, fuentes y logos.
tests/
  NABeauty.Tests         Pruebas de dominio, integración contra SQLite y pantallas (ViewModels).
  xaml_lint.py           Revisa el XAML antes de compilar (recursos, temas, eventos).
```

MVC/MVVM estricto: las vistas no tienen lógica, los ViewModels no tienen SQL y todo el SQL vive en
`Repositorios/`, siempre con parámetros. Los permisos se validan en los **servicios**, no solo ocultando botones.

## 4. Reglas que cuida la base de datos

- Dinero en **centavos** (enteros): sin errores de redondeo.
- Catálogo, ventas y entradas con **UUID**: los mismos identificadores que la tienda en línea.
- El **stock** es el acumulado del libro `inventario_movimientos`; un trigger lo actualiza y nadie lo escribe a mano.
- Movimientos, ventas y bitácora **no se borran ni se editan**; una venta se **anula** con motivo.
- `sync_cola` guarda lo que falta enviar a la web (para la fase de sincronización).

## 5. Roles iniciales

| Rol | Puede |
|---|---|
| Administrador | Todo (rol del sistema, protegido) |
| Cajera | Vender, ver ventas y productos. No ve costos ni administra |
| Socio | Ver ventas, reportes y ganancias aquí y en el panel web. No vende ni edita |

## 6. Pruebas

```bash
dotnet run --project tests/NABeauty.Tests     # 343 pruebas (en Windows corren las de dominio)
python tests/xaml_lint.py
```
