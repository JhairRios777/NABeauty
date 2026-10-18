<?php

/**
 * ImagenService — Fotos de producto subidas desde el panel.
 *
 * Toda foto se RE-CODIFICA con GD antes de guardarse:
 *   1. El tipo real se detecta por el contenido (finfo), no por la
 *      extensión ni por el Content-Type que manda el navegador.
 *   2. Se decodifica a píxeles y se vuelve a escribir como WebP. Cualquier
 *      cosa escondida en el archivo original (PHP pegado a un JPG, metadatos
 *      EXIF con la ubicación del teléfono) desaparece.
 *   3. Se reduce a IMAGEN_MAX_LADO px: una foto de 12 MP del teléfono pasa
 *      a pesar ~100–200 KB, y la tienda carga rápido.
 *   4. El nombre lo genera el sistema (UUID): nunca se usa el del usuario.
 */
final class ImagenService
{
    private const TIPOS = [
        'image/jpeg' => 'jpeg',
        'image/png'  => 'png',
        'image/webp' => 'webp',
    ];
    private const CALIDAD = 82;
    private const MAX_PIXELES = 40_000_000;   // ~40 MP: más que eso agotaría la memoria al decodificar

    /**
     * Normaliza $_FILES['campo'] de un <input multiple> a una lista de archivos.
     * @return array<int, array{name:string, tmp_name:string, error:int, size:int}>
     */
    public static function archivosSubidos(mixed $campo): array
    {
        if (!is_array($campo) || !isset($campo['tmp_name'])) {
            return [];
        }
        if (!is_array($campo['tmp_name'])) {
            return [$campo];
        }

        $lista = [];
        foreach (array_keys($campo['tmp_name']) as $i) {
            if (($campo['error'][$i] ?? UPLOAD_ERR_NO_FILE) === UPLOAD_ERR_NO_FILE) {
                continue;
            }
            $lista[] = [
                'name'     => (string) $campo['name'][$i],
                'tmp_name' => (string) $campo['tmp_name'][$i],
                'error'    => (int) $campo['error'][$i],
                'size'     => (int) $campo['size'][$i],
            ];
        }
        return $lista;
    }

    /**
     * Valida, re-codifica y guarda. Devuelve el nombre del archivo creado.
     * @throws DomainException con un mensaje apto para mostrarle a la usuaria
     */
    public function guardar(array $archivo): string
    {
        $this->validarSubida($archivo);

        $mime = (new finfo(FILEINFO_MIME_TYPE))->file($archivo['tmp_name']);
        if (!isset(self::TIPOS[$mime])) {
            throw new DomainException('«' . self::nombreCorto($archivo['name']) . '» no es una foto JPG, PNG o WebP.');
        }

        $info = @getimagesize($archivo['tmp_name']);
        if ($info === false || $info[0] < 1 || $info[1] < 1) {
            throw new DomainException('«' . self::nombreCorto($archivo['name']) . '» está dañada o no se puede leer.');
        }
        if ($info[0] * $info[1] > self::MAX_PIXELES) {
            throw new DomainException('«' . self::nombreCorto($archivo['name']) . '» tiene una resolución demasiado grande.');
        }

        $origen = match (self::TIPOS[$mime]) {
            'jpeg' => @imagecreatefromjpeg($archivo['tmp_name']),
            'png'  => @imagecreatefrompng($archivo['tmp_name']),
            'webp' => @imagecreatefromwebp($archivo['tmp_name']),
        };
        if (!$origen) {
            throw new DomainException('«' . self::nombreCorto($archivo['name']) . '» está dañada o no se puede leer.');
        }

        if (self::TIPOS[$mime] === 'jpeg') {
            $origen = $this->corregirOrientacion($origen, $archivo['tmp_name']);
        }

        $destino = $this->redimensionar($origen);
        $nombre  = BaseModel::uuid() . '.webp';

        $this->asegurarCarpeta();
        $ok = imagewebp($destino, PRODUCT_IMAGE_DIR . $nombre, self::CALIDAD);
        imagedestroy($destino);

        if (!$ok) {
            throw new RuntimeException('No se pudo escribir la imagen en ' . PRODUCT_IMAGE_DIR);
        }
        return $nombre;
    }

    /** Borra el archivo del disco. Solo acepta nombres que el propio sistema genera. */
    public function eliminar(?string $archivo): void
    {
        $archivo = basename((string) $archivo);
        if (preg_match('/^[0-9a-f-]{36}\.webp$/', $archivo) && is_file(PRODUCT_IMAGE_DIR . $archivo)) {
            unlink(PRODUCT_IMAGE_DIR . $archivo);
        }
    }

    // ─────────────────────────────────────────────

    private function validarSubida(array $archivo): void
    {
        $nombre = self::nombreCorto($archivo['name'] ?? '');

        switch ($archivo['error'] ?? UPLOAD_ERR_NO_FILE) {
            case UPLOAD_ERR_OK:
                break;
            case UPLOAD_ERR_INI_SIZE:
            case UPLOAD_ERR_FORM_SIZE:
                throw new DomainException('«' . $nombre . '» pesa más de lo permitido.');
            case UPLOAD_ERR_PARTIAL:
                throw new DomainException('«' . $nombre . '» no terminó de subir. Inténtalo de nuevo.');
            default:
                throw new DomainException('No se pudo recibir «' . $nombre . '».');
        }

        // is_uploaded_file: el archivo debe venir de ESTA petición, no ser
        // una ruta del servidor colada en el arreglo
        if (!is_uploaded_file($archivo['tmp_name'])) {
            throw new DomainException('Archivo no válido.');
        }
        if ($archivo['size'] > IMAGEN_MAX_BYTES) {
            throw new DomainException('«' . $nombre . '» pesa más de ' . (IMAGEN_MAX_BYTES / 1024 / 1024) . ' MB.');
        }
    }

    /** Escala para que el lado mayor no pase de IMAGEN_MAX_LADO, conservando transparencia. */
    private function redimensionar(GdImage $origen): GdImage
    {
        $w = imagesx($origen);
        $h = imagesy($origen);
        $escala = min(1, IMAGEN_MAX_LADO / max($w, $h));
        $nw = max(1, (int) round($w * $escala));
        $nh = max(1, (int) round($h * $escala));

        $destino = imagecreatetruecolor($nw, $nh);
        imagealphablending($destino, false);
        imagesavealpha($destino, true);
        imagefill($destino, 0, 0, imagecolorallocatealpha($destino, 0, 0, 0, 127));
        imagecopyresampled($destino, $origen, 0, 0, 0, 0, $nw, $nh, $w, $h);
        imagedestroy($origen);

        return $destino;
    }

    /** Las fotos del teléfono vienen "acostadas" con una marca EXIF: se enderezan antes de borrar el EXIF. */
    private function corregirOrientacion(GdImage $img, string $ruta): GdImage
    {
        if (!function_exists('exif_read_data')) {
            return $img;
        }
        $exif = @exif_read_data($ruta);
        $angulo = match ((int) ($exif['Orientation'] ?? 1)) {
            3 => 180,
            6 => -90,
            8 => 90,
            default => 0,
        };
        if ($angulo === 0) {
            return $img;
        }
        $rotada = imagerotate($img, $angulo, 0);
        if ($rotada === false) {
            return $img;
        }
        imagedestroy($img);
        return $rotada;
    }

    private function asegurarCarpeta(): void
    {
        if (!is_dir(PRODUCT_IMAGE_DIR) && !mkdir(PRODUCT_IMAGE_DIR, 0755, true) && !is_dir(PRODUCT_IMAGE_DIR)) {
            throw new RuntimeException('No se pudo crear ' . PRODUCT_IMAGE_DIR);
        }
    }

    private static function nombreCorto(string $nombre): string
    {
        $nombre = basename($nombre);
        return mb_strlen($nombre) > 40 ? mb_substr($nombre, 0, 37) . '…' : $nombre;
    }
}
