<?php

use Dompdf\Dompdf;
use Dompdf\Options;
use PhpOffice\PhpSpreadsheet\Cell\Coordinate;
use PhpOffice\PhpSpreadsheet\Shared\Date as FechaExcel;
use PhpOffice\PhpSpreadsheet\Spreadsheet;
use PhpOffice\PhpSpreadsheet\Style\Alignment;
use PhpOffice\PhpSpreadsheet\Style\Fill;
use PhpOffice\PhpSpreadsheet\Worksheet\Worksheet;
use PhpOffice\PhpSpreadsheet\Writer\Xlsx;

/**
 * ExportadorService — Excel (PhpSpreadsheet) y PDF (Dompdf) de un reporte.
 *
 * Recibe el arreglo que arma ReporteService::generar(..., completo: true):
 * los archivos muestran exactamente lo mismo que la pantalla.
 *
 * Las librerías se cargan aquí y solo aquí (AutoLoad::vendor): si a XAMPP
 * le faltara una extensión, falla la descarga, no todo el panel.
 */
final class ExportadorService
{
    private const VINO   = '9E1B2A';
    private const ROSA   = 'F6D5DC';
    private const MONEDA = '"L" #,##0.00';

    public function __construct()
    {
        AutoLoad::vendor();
    }

    // ═════════════════════════════════════════════
    // EXCEL
    // ═════════════════════════════════════════════

    /** Genera el .xlsx en un archivo temporal y devuelve su ruta. */
    public function excel(array $r, string $generadoPor): string
    {
        /** @var PeriodoReporte $p */
        $p     = $r['periodo'];
        $libro = new Spreadsheet();
        $libro->getProperties()
            ->setCreator(APP_NAME . ' — ' . $generadoPor)
            ->setTitle('Reporte ' . $p->etiqueta())
            ->setCompany(APP_NAME);
        $libro->getDefaultStyle()->getFont()->setName('Calibri')->setSize(11);

        $this->hojaResumen($libro->getActiveSheet(), $r, $generadoPor);

        $this->hojaTabla($libro->createSheet(), 'Por ' . mb_strtolower($p->etiquetaAgrupacion()),
            [$p->etiquetaAgrupacion(), 'Ventas', 'Ingresos', 'Entradas (costo)', 'Unid. ingresadas'],
            array_map(fn($f) => [$f['etiqueta'], $f['ventas'], $f['total'], $f['entradas'], $f['unidades_entrada']], $r['serie']),
            ['C' => self::MONEDA, 'D' => self::MONEDA], totales: ['B', 'C', 'D', 'E']);

        $this->hojaTabla($libro->createSheet(), 'Productos',
            ['Producto', 'Marca', 'Unidades', 'Vendido (antes de descuentos)', 'Costo', 'Ganancia'],
            array_map(fn($f) => [$f['producto'], $f['marca'], $f['unidades'], $f['total'], $f['costo'], $f['total'] - $f['costo']], $r['top']),
            ['D' => self::MONEDA, 'E' => self::MONEDA, 'F' => self::MONEDA], totales: ['C', 'D', 'E', 'F']);

        $this->hojaTabla($libro->createSheet(), 'Categorías',
            ['Categoría', 'Unidades', 'Vendido (antes de descuentos)', '% del total'],
            array_map(fn($f) => [$f['categoria'], $f['unidades'], $f['total'], $f['porcentaje'] / 100], $r['categorias']),
            ['C' => self::MONEDA, 'D' => '0.0%'], totales: ['B', 'C']);

        $this->hojaTabla($libro->createSheet(), 'Ventas',
            ['Número', 'Fecha', 'Vendedor', 'Pago', 'Unidades', 'Subtotal', 'Descuento', 'Total', 'Estado'],
            array_map(fn($v) => [
                $v['numero'], $this->fechaExcel($v['fecha']), $v['vendedor'], ReporteService::metodoPago($v['metodo_pago']),
                (int) $v['unidades'], (float) $v['subtotal'], (float) $v['descuento'], (float) $v['total'], ucfirst($v['estado']),
            ], $r['ventas']),
            ['B' => 'dd/mm/yyyy hh:mm', 'F' => self::MONEDA, 'G' => self::MONEDA, 'H' => self::MONEDA]);

        $this->hojaTabla($libro->createSheet(), 'Detalle de ventas',
            ['Venta', 'Fecha', 'Producto', 'Variante', 'SKU', 'Cantidad', 'Precio', 'Costo', 'Subtotal', 'Estado'],
            array_map(fn($d) => [
                $d['numero'], $this->fechaExcel($d['fecha']), $d['producto_nombre'], $d['variante_nombre'], $d['sku'],
                (int) $d['cantidad'], (float) $d['precio_unitario'],
                $d['costo_unitario'] !== null ? (float) $d['costo_unitario'] : null, (float) $d['subtotal'], ucfirst($d['estado']),
            ], $r['ventasDetalle'] ?? []),
            ['B' => 'dd/mm/yyyy hh:mm', 'G' => self::MONEDA, 'H' => self::MONEDA, 'I' => self::MONEDA]);

        $this->hojaTabla($libro->createSheet(), 'Entradas',
            ['Entrada', 'Fecha', 'Proveedor', 'Producto', 'Variante', 'SKU', 'Cantidad', 'Costo unit.', 'Subtotal'],
            array_map(fn($d) => [
                $d['numero'], $this->fechaExcel($d['fecha']), $d['proveedor'], $d['producto_nombre'], $d['variante_nombre'],
                $d['sku'], (int) $d['cantidad'], (float) $d['costo_unitario'], (float) $d['subtotal'],
            ], $r['entradasDetalle'] ?? []),
            ['B' => 'dd/mm/yyyy hh:mm', 'H' => self::MONEDA, 'I' => self::MONEDA], totales: ['G', 'I']);

        $libro->setActiveSheetIndex(0);

        $ruta = tempnam(sys_get_temp_dir(), 'nab_xlsx_');
        (new Xlsx($libro))->save($ruta);
        $libro->disconnectWorksheets();

        return $ruta;
    }

    private function hojaResumen(Worksheet $h, array $r, string $generadoPor): void
    {
        $p = $r['periodo'];
        $s = $r['resumen'];
        $e = $r['entradasResumen'];

        $h->setTitle('Resumen');
        $h->setCellValue('A1', APP_NAME . ' — Reporte de ventas y entradas');
        $h->getStyle('A1')->getFont()->setBold(true)->setSize(15)->getColor()->setRGB(self::VINO);
        $h->setCellValue('A2', $p->etiqueta());
        $h->getStyle('A2')->getFont()->setBold(true)->setSize(12);
        $h->setCellValue('A3', 'Generado el ' . date('d/m/Y H:i') . ' por ' . $generadoPor);
        $h->getStyle('A3')->getFont()->setItalic(true)->getColor()->setRGB('6B6B6B');

        $filas = [
            ['VENTAS', null, null],
            ['Ventas completadas', $s['ventas'], null],
            ['Unidades vendidas', $s['unidades'], null],
            ['Subtotal', $s['subtotal'], self::MONEDA],
            ['Descuentos', $s['descuento'], self::MONEDA],
            ['Ingresos (total cobrado)', $s['total'], self::MONEDA],
            ['Costo de lo vendido', $s['costo'], self::MONEDA],
            ['Ganancia bruta', $s['ganancia'], self::MONEDA],
            ['Ticket promedio', $s['ticket'], self::MONEDA],
            ['Ventas anuladas', $s['anuladas'], null],
            ['Monto anulado', $s['total_anuladas'], self::MONEDA],
            [null, null, null],
            ['ENTRADAS DE MERCADERÍA', null, null],
            ['Entradas registradas', $e['entradas'], null],
            ['Unidades ingresadas', $e['unidades'], null],
            ['Costo de lo ingresado', $e['costo'], self::MONEDA],
        ];
        if (!empty($r['metodos'])) {
            $filas[] = [null, null, null];
            $filas[] = ['POR MÉTODO DE PAGO', null, null];
            foreach ($r['metodos'] as $m) {
                $filas[] = [ReporteService::metodoPago($m['metodo']) . ' (' . $m['ventas'] . ')', $m['total'], self::MONEDA];
            }
        }

        $fila = 5;
        foreach ($filas as [$etiqueta, $valor, $formato]) {
            if ($etiqueta !== null) {
                $h->setCellValue('A' . $fila, $etiqueta);
            }
            if ($valor !== null) {
                $h->setCellValue('B' . $fila, $valor);
                if ($formato) {
                    $h->getStyle('B' . $fila)->getNumberFormat()->setFormatCode($formato);
                }
            } elseif ($etiqueta !== null) {
                // Título de sección
                $h->getStyle("A{$fila}:B{$fila}")->getFont()->setBold(true)->getColor()->setRGB('FFFFFF');
                $h->getStyle("A{$fila}:B{$fila}")->getFill()->setFillType(Fill::FILL_SOLID)->getStartColor()->setRGB(self::VINO);
            }
            $fila++;
        }
        $h->getColumnDimension('A')->setWidth(34);
        $h->getColumnDimension('B')->setWidth(20);
        $h->getStyle('B5:B' . $fila)->getAlignment()->setHorizontal(Alignment::HORIZONTAL_RIGHT);
    }

    /**
     * Hoja con encabezado de color, filtros, primera fila fija y, si se
     * pide, una fila TOTAL con fórmulas SUM (se recalculan si editan el Excel).
     */
    private function hojaTabla(Worksheet $h, string $titulo, array $encabezados, array $filas, array $formatos = [], array $totales = []): void
    {
        // Excel no admite : \ / ? * [ ] en el nombre ni más de 31 caracteres
        $h->setTitle(mb_substr(str_replace([':', '\\', '/', '?', '*', '[', ']'], '', $titulo), 0, 31));

        $h->fromArray($encabezados, null, 'A1');
        $ultimaCol = Coordinate::stringFromColumnIndex(count($encabezados));
        $h->getStyle("A1:{$ultimaCol}1")->getFont()->setBold(true)->getColor()->setRGB('FFFFFF');
        $h->getStyle("A1:{$ultimaCol}1")->getFill()->setFillType(Fill::FILL_SOLID)->getStartColor()->setRGB(self::VINO);

        if (empty($filas)) {
            $h->setCellValue('A2', 'Sin movimientos en este período');
            $h->getStyle('A2')->getFont()->setItalic(true)->getColor()->setRGB('6B6B6B');
        } else {
            $h->fromArray($filas, null, 'A2', true);
        }
        $ultimaFila = max(2, count($filas) + 1);

        foreach ($formatos as $col => $formato) {
            $h->getStyle("{$col}2:{$col}" . ($ultimaFila + 1))->getNumberFormat()->setFormatCode($formato);
        }

        if (!empty($totales) && !empty($filas)) {
            $t = $ultimaFila + 1;
            $h->setCellValue("A{$t}", 'TOTAL');
            foreach ($totales as $col) {
                $h->setCellValue("{$col}{$t}", "=SUM({$col}2:{$col}{$ultimaFila})");
            }
            $h->getStyle("A{$t}:{$ultimaCol}{$t}")->getFont()->setBold(true);
            $h->getStyle("A{$t}:{$ultimaCol}{$t}")->getFill()->setFillType(Fill::FILL_SOLID)->getStartColor()->setRGB(self::ROSA);
        }

        if (!empty($filas)) {
            $h->setAutoFilter("A1:{$ultimaCol}{$ultimaFila}");
        }
        $h->freezePane('A2');
        for ($i = 1; $i <= count($encabezados); $i++) {
            $h->getColumnDimension(Coordinate::stringFromColumnIndex($i))->setAutoSize(true);
        }
    }

    private function fechaExcel(string $fecha): float
    {
        return FechaExcel::PHPToExcel(new DateTimeImmutable($fecha));
    }

    // ═════════════════════════════════════════════
    // PDF
    // ═════════════════════════════════════════════

    /** Genera el PDF y devuelve su contenido binario. */
    public function pdf(array $r, string $generadoPor): string
    {
        $html = $this->vistaPdf($r, $generadoPor);

        $opciones = new Options();
        $opciones->set('isRemoteEnabled', false);        // no descarga nada de internet
        $opciones->set('isPhpEnabled', false);           // no ejecuta PHP embebido en el HTML
        $opciones->set('isJavascriptEnabled', false);
        $opciones->set('chroot', ROOT);                  // solo lee archivos del proyecto
        $opciones->set('defaultFont', 'DejaVu Sans');    // trae tildes, ñ y el símbolo de lempira
        $opciones->set('tempDir', sys_get_temp_dir());

        $pdf = new Dompdf($opciones);
        $pdf->loadHtml($html, 'UTF-8');
        $pdf->setPaper('letter', 'portrait');
        $pdf->render();

        // Número de página en el pie
        $canvas = $pdf->getCanvas();
        $fuente = $pdf->getFontMetrics()->getFont('DejaVu Sans');
        $canvas->page_text(510, 771, 'Página {PAGE_NUM} de {PAGE_COUNT}', $fuente, 7, [0.42, 0.42, 0.42]);

        return (string) $pdf->output();
    }

    private function vistaPdf(array $r, string $generadoPor): string
    {
        $logo = CONTENT_PATH . 'Demo' . DS . 'img' . DS . 'logo.png';
        $logoDataUri = is_file($logo) ? 'data:image/png;base64,' . base64_encode((string) file_get_contents($logo)) : null;

        extract(['r' => $r, 'generadoPor' => $generadoPor, 'logoDataUri' => $logoDataUri], EXTR_SKIP);
        ob_start();
        require VIEWS_PATH . 'Admin' . DS . 'Reportes' . DS . 'Pdf.php';
        return (string) ob_get_clean();
    }
}
