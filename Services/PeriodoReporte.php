<?php

/**
 * PeriodoReporte — Rango de fechas de un reporte (valor inmutable).
 *
 *   dia    → un día, desglosado por hora
 *   semana → lunes a domingo, desglosado por día
 *   mes    → mes calendario, desglosado por día
 *   anio   → año calendario, desglosado por mes
 *   rango  → fechas libres; el desglose se elige según su largo
 *
 * $hasta es EXCLUSIVO (fecha >= desde AND fecha < hasta): así un día
 * termina a las 23:59:59.999 sin perder ventas del último segundo.
 */
final class PeriodoReporte
{
    public const TIPOS = ['dia' => 'Día', 'semana' => 'Semana', 'mes' => 'Mes', 'anio' => 'Año', 'rango' => 'Rango'];

    private const MAX_DIAS_RANGO = 366;

    private const MESES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio',
                           'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
    private const MESES_CORTOS = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];
    private const DIAS_CORTOS  = ['lun', 'mar', 'mié', 'jue', 'vie', 'sáb', 'dom'];

    private function __construct(
        public readonly string $tipo,
        public readonly DateTimeImmutable $desde,
        public readonly DateTimeImmutable $hasta,
        public readonly string $agrupacion,   // hora | dia | semana | mes
    ) {}

    // ─────────────────────────────────────────────
    // CONSTRUCCIÓN
    // ─────────────────────────────────────────────

    /**
     * Desde los parámetros de la URL. Todo valor inválido cae al valor
     * por defecto (semana actual): nunca lanza error por una URL tocada.
     */
    public static function desdeQuery(array $q): self
    {
        $tipo  = is_string($q['periodo'] ?? null) && isset(self::TIPOS[$q['periodo']]) ? $q['periodo'] : 'semana';
        $fecha = self::fecha($q['fecha'] ?? null) ?? new DateTimeImmutable('today');

        if ($tipo === 'rango') {
            $desde = self::fecha($q['desde'] ?? null);
            $hasta = self::fecha($q['hasta'] ?? null);
            if ($desde !== null && $hasta !== null) {
                return self::rango($desde, $hasta);
            }
            $tipo = 'semana';
        }

        return self::de($tipo, $fecha);
    }

    /** Período de tipo dia|semana|mes|anio que contiene la fecha. */
    public static function de(string $tipo, DateTimeImmutable $fecha): self
    {
        $f = $fecha->setTime(0, 0);

        return match ($tipo) {
            'dia'   => new self('dia', $f, $f->modify('+1 day'), 'hora'),
            'mes'   => new self('mes', $f->modify('first day of this month'), $f->modify('first day of next month'), 'dia'),
            'anio'  => new self('anio', $f->setDate((int) $f->format('Y'), 1, 1), $f->setDate((int) $f->format('Y') + 1, 1, 1), 'mes'),
            default => new self('semana', $f->modify('monday this week'), $f->modify('monday this week')->modify('+7 days'), 'dia'),
        };
    }

    /** Rango libre, ambos días incluidos. Se ordena y se limita a un año. */
    public static function rango(DateTimeImmutable $desde, DateTimeImmutable $hasta): self
    {
        $desde = $desde->setTime(0, 0);
        $hasta = $hasta->setTime(0, 0);
        if ($hasta < $desde) {
            [$desde, $hasta] = [$hasta, $desde];
        }
        if ($desde->diff($hasta)->days > self::MAX_DIAS_RANGO) {
            $desde = $hasta->modify('-' . self::MAX_DIAS_RANGO . ' days');
        }

        $dias = $desde->diff($hasta)->days + 1;
        $agrupacion = match (true) {
            $dias <= 1  => 'hora',
            $dias <= 62 => 'dia',
            $dias <= 190 => 'semana',
            default     => 'mes',
        };

        return new self('rango', $desde, $hasta->modify('+1 day'), $agrupacion);
    }

    private static function fecha(mixed $valor): ?DateTimeImmutable
    {
        if (!is_string($valor) || !preg_match('/^\d{4}-\d{2}-\d{2}$/', $valor)) {
            return null;
        }
        $f = DateTimeImmutable::createFromFormat('!Y-m-d', $valor);
        // createFromFormat acepta 2026-02-31 (lo corre a marzo): se rechaza
        return ($f && $f->format('Y-m-d') === $valor) ? $f : null;
    }

    // ─────────────────────────────────────────────
    // NAVEGACIÓN
    // ─────────────────────────────────────────────

    public function anterior(): self
    {
        if ($this->tipo === 'rango') {
            $dias = $this->desde->diff($this->hasta)->days;
            return self::rango($this->desde->modify("-{$dias} days"), $this->desde->modify('-1 day'));
        }
        return self::de($this->tipo, $this->desde->modify('-1 day'));
    }

    public function siguiente(): self
    {
        if ($this->tipo === 'rango') {
            $dias = $this->desde->diff($this->hasta)->days;
            return self::rango($this->hasta, $this->hasta->modify('+' . ($dias - 1) . ' days'));
        }
        return self::de($this->tipo, $this->hasta);
    }

    /** ¿El período ya empezó? (no se navega hacia fechas futuras) */
    public function tieneSiguiente(): bool
    {
        return $this->hasta <= new DateTimeImmutable('today');
    }

    /** Parámetros de URL que reconstruyen este período. */
    public function query(): array
    {
        return $this->tipo === 'rango'
            ? ['periodo' => 'rango', 'desde' => $this->desde->format('Y-m-d'), 'hasta' => $this->ultimoDia()->format('Y-m-d')]
            : ['periodo' => $this->tipo, 'fecha' => $this->desde->format('Y-m-d')];
    }

    public function ultimoDia(): DateTimeImmutable
    {
        return $this->hasta->modify('-1 day');
    }

    // ─────────────────────────────────────────────
    // TEXTOS
    // ─────────────────────────────────────────────

    /** "Hoy", "Semana del 12 al 18 oct 2026", "Octubre 2026"… */
    public function etiqueta(): string
    {
        $hoy = new DateTimeImmutable('today');
        $d = $this->desde;
        $h = $this->ultimoDia();

        return match ($this->tipo) {
            'dia'    => $d == $hoy ? 'Hoy, ' . $this->fechaLarga($d)
                      : ($d == $hoy->modify('-1 day') ? 'Ayer, ' . $this->fechaLarga($d) : ucfirst($this->fechaLarga($d))),
            'semana' => 'Semana del ' . $this->fechaCorta($d, $d->format('Y') !== $h->format('Y')) . ' al ' . $this->fechaCorta($h, true),
            'mes'    => ucfirst(self::MESES[(int) $d->format('n') - 1]) . ' ' . $d->format('Y'),
            'anio'   => 'Año ' . $d->format('Y'),
            default  => 'Del ' . $this->fechaCorta($d, true) . ' al ' . $this->fechaCorta($h, true),
        };
    }

    /** Nombre del archivo exportado: ventas_2026-10-12_a_2026-10-18 */
    public function sufijoArchivo(): string
    {
        $d = $this->desde->format('Y-m-d');
        $h = $this->ultimoDia()->format('Y-m-d');
        return $d === $h ? $d : $d . '_a_' . $h;
    }

    // ─────────────────────────────────────────────
    // CUBETAS DEL DESGLOSE
    // ─────────────────────────────────────────────

    /** Expresión SQL que agrupa `fecha` según el desglose (lista cerrada, sin datos del usuario). */
    public function sqlGrupo(string $columna = 'fecha'): string
    {
        return match ($this->agrupacion) {
            'hora'   => "DATE_FORMAT($columna, '%Y-%m-%d %H:00')",
            // Lunes de la semana (WEEKDAY: lunes = 0)
            'semana' => "DATE_FORMAT(DATE_SUB(DATE($columna), INTERVAL WEEKDAY($columna) DAY), '%Y-%m-%d')",
            'mes'    => "DATE_FORMAT($columna, '%Y-%m')",
            default  => "DATE_FORMAT($columna, '%Y-%m-%d')",
        };
    }

    /**
     * Todas las cubetas del período, incluidas las vacías: un día sin
     * ventas debe verse como barra en cero, no desaparecer del gráfico.
     * @return array<string, string> clave (la misma que sqlGrupo) => etiqueta
     */
    public function cubetas(): array
    {
        [$paso, $formato] = match ($this->agrupacion) {
            'hora'   => ['+1 hour',  'Y-m-d H:00'],
            'semana' => ['+1 week',  'Y-m-d'],
            'mes'    => ['+1 month', 'Y-m'],
            default  => ['+1 day',   'Y-m-d'],
        };

        $inicio = $this->agrupacion === 'semana' ? $this->desde->modify('monday this week') : $this->desde;
        $inicio = $this->agrupacion === 'mes' ? $inicio->modify('first day of this month') : $inicio;

        $cubetas = [];
        for ($t = $inicio; $t < $this->hasta; $t = $t->modify($paso)) {
            $cubetas[$t->format($formato)] = $this->etiquetaCubeta($t);
        }
        return $cubetas;
    }

    private function etiquetaCubeta(DateTimeImmutable $t): string
    {
        return match ($this->agrupacion) {
            'hora'   => $t->format('H') . ':00',
            'semana' => 'Sem. ' . $this->fechaCorta($t, false),
            'mes'    => ucfirst(self::MESES_CORTOS[(int) $t->format('n') - 1]) . ' ' . $t->format('y'),
            default  => $this->agrupacion === 'dia' && $this->tipo === 'semana'
                        ? ucfirst(self::DIAS_CORTOS[(int) $t->format('N') - 1]) . ' ' . $t->format('j')
                        : $t->format('j') . ' ' . self::MESES_CORTOS[(int) $t->format('n') - 1],
        };
    }

    public function etiquetaAgrupacion(): string
    {
        return ['hora' => 'Hora', 'dia' => 'Día', 'semana' => 'Semana', 'mes' => 'Mes'][$this->agrupacion];
    }

    private function fechaCorta(DateTimeImmutable $f, bool $conAnio): string
    {
        return $f->format('j') . ' ' . self::MESES_CORTOS[(int) $f->format('n') - 1] . ($conAnio ? ' ' . $f->format('Y') : '');
    }

    private function fechaLarga(DateTimeImmutable $f): string
    {
        return $f->format('j') . ' de ' . self::MESES[(int) $f->format('n') - 1] . ' de ' . $f->format('Y');
    }
}
