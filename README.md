# N&A Beauty

Sistema de N&A Beauty (@nya_beautystore_hn), desarrollado por DeskCod. Un solo repositorio con dos partes:

| Carpeta | Qué es | Tecnología |
|---|---|---|
| `/` (raíz) | Tienda en línea + panel administrativo `/admin` | PHP MVC puro, MySQL/MariaDB, Apache |
| `POS/` | Punto de venta de la tienda física (funciona sin internet) | WPF .NET 10 + SQLite |

```
NABeauty/
├── index.php, .htaccess        Front Controller de la web
├── Config/ Controllers/ Entity/ Models/ Services/ Views/ Template/
├── Content/                    CSS, JS, imágenes y fotos subidas (lo único público)
├── BD/sql/                     Esquema MySQL y datos de prueba
├── vendor/                     PhpSpreadsheet y Dompdf
└── POS/                        Punto de venta (ver POS/README.md)
    ├── NABeautyPOS.slnx
    ├── src/  tests/
    └── run-dev.bat, instalar.bat, publicar.bat
```

## Tienda en línea
- XAMPP con un VirtualHost cuyo `DocumentRoot` es esta carpeta.
- Base de datos: importar `BD/sql/01_esquema.sql` y después `02_datos_prueba.sql` (por ejemplo con SQLyog).
- `php.ini`: activar `extension=gd` y `extension=zip`.

## POS
- Requiere el **.NET 10 SDK**.
- Desarrollo: `POS\run-dev.bat` corre las pruebas, compila y abre la app.
- Uso diario: `POS\instalar.bat`.
- Detalle completo en [POS/README.md](POS/README.md).

## Seguridad
- `.htaccess` bloquea por HTTP todas las carpetas de código y datos, **incluida `POS/`**: Apache publica la raíz, pero el código del POS nunca se sirve.
- Al desplegar la tienda en un servidor, **no subas `POS/`**: no se usa allá.
- El POS guarda sus datos fuera del repositorio, en `%LOCALAPPDATA%\NABeautyPOS` (base, fotos y respaldos).
- `POS/.gitignore` excluye `bin/`, `obj/`, logs y bases `.db`.

## Cómo se comunican
El POS será la fuente de las ventas, las entradas y los usuarios. El catálogo se edita en los dos lados.
La sincronización todavía no está construida: el POS ya guarda en una cola (`sync_cola`) todo lo que la web debe recibir,
y la dirección y el token de la tienda se configuran en POS → Ajustes.
