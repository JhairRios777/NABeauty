<?php

/**
 * BaseModel — Acceso a datos común a todos los Models.
 *
 * Todo SQL del sistema vive en clases que heredan de aquí y siempre
 * con sentencias preparadas: los valores viajan como parámetros, nunca
 * concatenados al SQL.
 */
abstract class BaseModel
{
    protected PDO $db;

    public function __construct()
    {
        $this->db = Conexion::obtener();
    }

    /** Ejecuta una sentencia preparada y devuelve el statement. */
    protected function ejecutar(string $sql, array $params = []): PDOStatement
    {
        $stmt = $this->db->prepare($sql);
        $stmt->execute($params);
        return $stmt;
    }

    /** @return array<int, array<string, mixed>> */
    protected function filas(string $sql, array $params = []): array
    {
        return $this->ejecutar($sql, $params)->fetchAll();
    }

    /** @return array<string, mixed>|null */
    protected function fila(string $sql, array $params = []): ?array
    {
        $fila = $this->ejecutar($sql, $params)->fetch();
        return $fila === false ? null : $fila;
    }

    protected function valor(string $sql, array $params = []): mixed
    {
        $v = $this->ejecutar($sql, $params)->fetchColumn();
        return $v === false ? null : $v;
    }

    /**
     * Ejecuta varias operaciones como una sola: o se guardan todas o
     * ninguna (si algo falla a mitad, la base queda como estaba).
     */
    protected function transaccion(callable $operaciones): mixed
    {
        $this->db->beginTransaction();
        try {
            $resultado = $operaciones();
            $this->db->commit();
            return $resultado;
        } catch (Throwable $e) {
            $this->db->rollBack();
            throw $e;
        }
    }

    /** UUID v4 aleatorio: identificador que no choca con los que genera el POS. */
    public static function uuid(): string
    {
        $b = random_bytes(16);
        $b[6] = chr((ord($b[6]) & 0x0f) | 0x40);
        $b[8] = chr((ord($b[8]) & 0x3f) | 0x80);

        return vsprintf('%s%s-%s-%s-%s-%s%s%s', str_split(bin2hex($b), 4));
    }

    public static function esUuid(?string $valor): bool
    {
        return is_string($valor)
            && preg_match('/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i', $valor) === 1;
    }
}
