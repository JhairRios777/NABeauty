<?php

/**
 * AdminDashboardController — Portada del panel (/admin).
 * Las cifras de ventas solo se muestran a quien tiene permiso de reportes.
 */
class AdminDashboardController extends AdminBaseController
{
    public function index(): void
    {
        $usuario = $this->exigir();
        $vars = ['pageTitle' => 'Inicio', 'seccion' => 'inicio'];

        if ($usuario->tienePermiso(PERM_REPORTES)) {
            $modelo   = new ReporteModel();
            $servicio = new ReporteService($modelo);
            $hoy      = new DateTimeImmutable('today');

            $tarjetas = [];
            foreach (['dia' => 'Hoy', 'semana' => 'Esta semana', 'mes' => 'Este mes'] as $tipo => $titulo) {
                $p      = PeriodoReporte::de($tipo, $hoy);
                $previo = $p->anterior();
                $actual = $modelo->resumenVentas($p->desde, $p->hasta);
                $antes  = $modelo->resumenVentas($previo->desde, $previo->hasta);
                $tarjetas[] = [
                    'titulo'    => $titulo,
                    'resumen'   => $actual,
                    'variacion' => ReporteService::variacion($actual['total'], $antes['total']),
                    'vs'        => ['dia' => 'ayer', 'semana' => 'la semana pasada', 'mes' => 'el mes pasado'][$tipo],
                    'query'     => $p->query(),
                ];
            }

            $ultimos14 = PeriodoReporte::rango($hoy->modify('-13 days'), $hoy);
            $mes       = PeriodoReporte::de('mes', $hoy);

            $vars += [
                'tarjetas' => $tarjetas,
                'serie'    => $servicio->serie($ultimos14),
                'top'      => $modelo->topProductos($mes->desde, $mes->hasta, 5),
            ];
        }

        if ($usuario->tienePermiso(PERM_PRODUCTOS) || $usuario->tienePermiso(PERM_REPORTES)) {
            $vars['stockBajo']       = (new VarianteModel())->stockBajo(8);
            $vars['productosActivos'] = (new ProductoModel())->contarActivos();
        }

        $this->render('Dashboard/Index.php', $vars);
    }
}
