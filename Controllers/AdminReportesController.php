<?php

/**
 * AdminReportesController — Ventas y entradas por día, semana, mes, año o rango.
 *
 *   GET /admin/reportes?periodo=semana&fecha=2026-10-12
 *   GET /admin/reportes/excel?…   (requiere además web.exportar)
 *   GET /admin/reportes/pdf?…
 *
 * Las descargas son GET a propósito: solo leen datos (no cambian nada),
 * y así el enlace se puede abrir directo desde el teléfono.
 */
class AdminReportesController extends AdminBaseController
{
    public function index(): void
    {
        $this->exigir(PERM_REPORTES);
        $periodo = PeriodoReporte::desdeQuery($_GET);

        $this->render('Reportes/Index.php', [
            'pageTitle' => 'Reportes',
            'seccion'   => 'reportes',
            'r'         => (new ReporteService())->generar($periodo),
            'puedeExportar' => $this->usuario->tienePermiso(PERM_EXPORTAR),
        ]);
    }

    public function excel(): void
    {
        [$reporte, $periodo] = $this->paraExportar();
        $ruta = (new ExportadorService())->excel($reporte, (string) $this->usuario->nombre);

        $this->descargar(
            $ruta,
            'reporte_' . $periodo->sufijoArchivo() . '.xlsx',
            'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
        );
        unlink($ruta);
    }

    public function pdf(): void
    {
        [$reporte, $periodo] = $this->paraExportar();
        $contenido = (new ExportadorService())->pdf($reporte, (string) $this->usuario->nombre);

        $ruta = tempnam(sys_get_temp_dir(), 'nab_pdf_');
        file_put_contents($ruta, $contenido);
        $this->descargar($ruta, 'reporte_' . $periodo->sufijoArchivo() . '.pdf', 'application/pdf');
        unlink($ruta);
    }

    /** @return array{0: array, 1: PeriodoReporte} */
    private function paraExportar(): array
    {
        $this->exigir(PERM_REPORTES);
        $this->exigir(PERM_EXPORTAR);

        // Exportar un año completo puede tardar unos segundos
        set_time_limit(120);

        $periodo = PeriodoReporte::desdeQuery($_GET);
        return [(new ReporteService())->generar($periodo, completo: true), $periodo];
    }

    private function descargar(string $ruta, string $nombre, string $mime): void
    {
        // El nombre lo arma el sistema (fechas), pero igual se limpia
        $nombre = preg_replace('/[^A-Za-z0-9._-]/', '_', $nombre);

        header('Content-Type: ' . $mime);
        header('Content-Disposition: attachment; filename="' . $nombre . '"');
        header('Content-Length: ' . filesize($ruta));
        readfile($ruta);
    }
}
