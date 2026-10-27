using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;
using NABeauty.Data.Servicios;

namespace NABeauty.Tests;

/// <summary>Servicios reales (NABeauty.Data) contra SQLite, con el proveedor mínimo de SqliteMinimo.cs (solo Linux).</summary>
internal static class Integracion
{
    public static void Correr(Action<string, bool, string> check, Action<string, Action> lanza)
    {
        void Check(string n, bool c, string e = "") => check(n, c, e);
        var archivo = Path.Combine(Path.GetTempPath(), $"nabeauty_test_{Guid.NewGuid():N}.db");
        var bd = new BaseDatos(() => new ConexionMinima(archivo));
        long Esc(string sql) { using var u = bd.Abrir(); return u.Escalar<long>(sql); }

        Console.WriteLine("== Integración: base de datos nueva ==");
        bd.Inicializar();
        bd.Inicializar();   // idempotente: se corre en cada arranque
        Check("Un solo administrador inicial", Esc("SELECT COUNT(*) FROM usuarios") == 1);
        Check("Roles Administrador, Cajera y Socio", Esc("SELECT COUNT(*) FROM roles") == 3);
        Check("Administrador con uuid para sincronizar", Esc("SELECT length(uuid) FROM usuarios WHERE usuario = 'admin'") == 36);
        Check("Base vacía: sin productos ni ventas", Esc("SELECT (SELECT COUNT(*) FROM productos) + (SELECT COUNT(*) FROM ventas)") == 0);

        Console.WriteLine("== Integración: login ==");
        var login = new LoginService(bd);
        lanza("Usuario vacío pide los datos", () => login.Ingresar("", "x"));
        Check("Campos vacíos no cuentan como intento", Esc("SELECT COUNT(*) FROM login_intentos") == 0);
        lanza("Contraseña incorrecta", () => login.Ingresar("admin", "mala"));
        var adm = login.Ingresar("ADMIN", BaseDatos.ClaveInicial);
        Check("Ingreso sin distinguir mayúsculas en el usuario", adm.EsAdmin && adm.DebeCambiarPassword);
        Check("La sesión no guarda el hash", adm.PasswordHash == "");
        Check("Ingreso exitoso limpia los intentos", Esc("SELECT COUNT(*) FROM login_intentos") == 0);
        lanza("Rechaza contraseña débil", () => login.CambiarPassword(adm, BaseDatos.ClaveInicial, "corta", "corta"));
        lanza("Rechaza repetir la misma", () => login.CambiarPassword(adm, BaseDatos.ClaveInicial, BaseDatos.ClaveInicial, BaseDatos.ClaveInicial));
        login.CambiarPassword(adm, BaseDatos.ClaveInicial, "Nataly2026pos", "Nataly2026pos");
        Check("Cambio de contraseña", !adm.DebeCambiarPassword && login.Ingresar("admin", "Nataly2026pos").Id == adm.Id);
        string? mensaje = null;
        for (var i = 0; i < 5; i++) { try { login.Ingresar("admin", "x"); } catch (ReglaNegocioException e) { mensaje = e.Message; } }
        Check("El quinto fallo avisa el bloqueo", mensaje?.Contains("bloqueado") == true, mensaje ?? "");
        try { login.Ingresar("admin", "Nataly2026pos"); Check("Bloqueo aunque la clave sea correcta", false); }
        catch (ReglaNegocioException e) { Check("Bloqueo aunque la clave sea correcta", e.Message.Contains("bloqueado"), e.Message); }
        using (var u = bd.Abrir(true)) { new UsuarioRepo(u).LimpiarFallos("admin"); u.Confirmar(); }
        Check("Bitácora registra el ingreso", Esc("SELECT COUNT(*) FROM bitacora WHERE accion = 'sesion.entrar'") >= 2);

        // Cajera para probar permisos en los servicios
        using (var u = bd.Abrir(true))
        {
            new UsuarioRepo(u).Crear("Cajera Uno", "cajera1", PasswordHasher.Hash("Cajera2026x"), "cajera");
            u.Confirmar();
        }
        var caj = login.Ingresar("cajera1", "Cajera2026x");
        Check("Cajera carga los permisos de su rol", caj.Puede(Permiso.Vender) && !caj.Puede(Permiso.VerCostos));

        Console.WriteLine("== Integración: tablero con la base vacía ==");
        var ctx = new Contexto(bd, adm);
        var panel = new PanelService(ctx);
        var m = panel.Metricas();
        Check("Todo en cero", m.VentasHoy == 0 && m.VentasMes == 0 && m.ProductosActivos == 0 && m.SinCatalogo);
        var dias = panel.VentasPorDia(14);
        Check("14 días, terminando hoy, aunque no haya ventas", dias.Count == 14 && dias[^1].Dia == Formato.ATexto(Formato.Hoy) && dias.All(d => d.Total == 0));
        Check("Listas vacías sin error", panel.TopDelMes().Count == 0 && panel.PorAgotarse().Count == 0 && panel.UltimasVentas().Count == 0);
        Check("Sin conexión con la web configurada", !panel.EstadoSync().Configurada);

        Console.WriteLine("== Integración: inventario y ventas ==");
        string labial, blush;
        using (var u = bd.Abrir(true))
        {
            labial = Datos.Producto(u, "Labial", precio: 25000, costo: 12000, tipo: "tono", variante: "Rojo");
            blush = Datos.Producto(u, "Blush", precio: 32000, costo: 17000);
            Datos.Entrada(u, labial, 10);
            Datos.Entrada(u, blush, 2);
            u.Confirmar();
        }
        Check("La entrada suma stock (trigger)", Esc($"SELECT stock FROM producto_variantes WHERE uuid = '{labial}'") == 10);
        lanza("El stock no se escribe a mano", () => { using var u = bd.Abrir(); u.Ejecutar($"UPDATE producto_variantes SET stock = 99 WHERE uuid = '{labial}'"); });
        lanza("Los movimientos no se borran", () => { using var u = bd.Abrir(); u.Ejecutar("DELETE FROM inventario_movimientos"); });
        lanza("Precio de oferta mayor al precio", () => { using var u = bd.Abrir(); u.Ejecutar($"UPDATE producto_variantes SET precio_oferta = 99999 WHERE uuid = '{labial}'"); });

        var ayer = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd 15:00:00");
        string anulada;
        using (var u = bd.Abrir(true))
        {
            Datos.Venta(u, "V-000001", labial, 2, 25000, 12000);            // hoy: L 500
            Datos.Venta(u, "V-000002", blush, 1, 32000, 17000);             // hoy: L 320
            Datos.Venta(u, "V-000003", labial, 1, 25000, 12000, ayer);      // ayer: L 250
            anulada = Datos.Venta(u, "V-000004", labial, 1, 25000, 12000, estado: "anulada");
            u.Confirmar();
        }
        Check("Venta y anulación mueven el stock", Esc($"SELECT stock FROM producto_variantes WHERE uuid = '{labial}'") == 7
                                                   && Esc($"SELECT stock FROM producto_variantes WHERE uuid = '{blush}'") == 1);
        Check("Stock = suma del libro", Esc("""
            SELECT COUNT(*) FROM producto_variantes v
             WHERE v.stock <> (SELECT COALESCE(SUM(cantidad),0) FROM inventario_movimientos m WHERE m.variante_uuid = v.uuid)
            """) == 0);
        lanza("Una venta no se borra", () => { using var u = bd.Abrir(); u.Ejecutar("DELETE FROM ventas WHERE numero = 'V-000001'"); });
        lanza("Una venta no se edita", () => { using var u = bd.Abrir(); u.Ejecutar("UPDATE ventas SET total = 1 WHERE numero = 'V-000001'"); });
        lanza("Anular exige motivo", () => { using var u = bd.Abrir(); u.Ejecutar("UPDATE ventas SET estado = 'anulada' WHERE numero = 'V-000001'"); });
        lanza("Una anulada no vuelve atrás", () => { using var u = bd.Abrir(); u.Ejecutar($"UPDATE ventas SET estado = 'completada', motivo_anulacion = 'x' WHERE uuid = '{anulada}'"); });

        Console.WriteLine("== Integración: tablero con datos ==");
        m = panel.Metricas();
        Check("Ventas de hoy (sin la anulada)", m.VentasHoy == 82000 && m.NumVentasHoy == 2, $"{m.VentasHoy} {m.NumVentasHoy}");
        Check("Ventas de ayer", m.VentasAyer == 25000, m.VentasAyer.ToString());
        var esteMes = DateTime.Now.Day > 1;
        Check("Mes: ventas, unidades y costo", !esteMes || (m.VentasMes == 107000 && m.UnidadesMes == 4 && m.CostoMes == 53000),
            $"{m.VentasMes} {m.UnidadesMes} {m.CostoMes}");
        Check("Ganancia = ventas − costo", m.GananciaMes == m.VentasMes - m.CostoMes);
        Check("Anuladas del mes se cuentan aparte", m.AnuladasMes == 1);
        Check("Inventario: 2 activos, 1 por agotarse", m.ProductosActivos == 2 && m.PorAgotarse == 1 && m.Agotados == 0 && m.ConStock == 2);
        dias = panel.VentasPorDia(14);
        Check("Gráfico: hoy y ayer con su total", dias[^1].Total == 82000 && dias[^2].Total == 25000 && dias[^1].Ventas == 2);
        var top = panel.TopDelMes();
        Check("Más vendido: Labial", top.Count >= 1 && top[0].Producto == "Labial" && top[0].Unidades >= 2, top.FirstOrDefault()?.Producto ?? "");
        var stock = panel.PorAgotarse();
        Check("Por agotarse: Blush con 1", stock.Count == 1 && stock[0].Producto == "Blush" && stock[0].EstadoTexto == "1 en stock");
        var ultimas = panel.UltimasVentas();
        Check("Últimas ventas incluyen la anulada marcada", ultimas.Count == 4 && ultimas.Any(v => v.Estado == "anulada"));

        Console.WriteLine("== Integración: permisos en el servicio ==");
        var panelCajera = new PanelService(new Contexto(bd, caj));
        var mc = panelCajera.Metricas();
        Check("Cajera ve ventas pero no el costo", mc.VentasHoy == 82000 && mc.CostoMes == 0);
        using (var u = bd.Abrir(true))
        {
            new UsuarioRepo(u).Crear("Bodega", "bodega", PasswordHasher.Hash("Bodega2026x"), "socio");
            u.Ejecutar("DELETE FROM rol_permisos WHERE rol_id = (SELECT id FROM roles WHERE clave = 'socio') AND permiso IN ('VerVentas','VerCostos')");
            u.Confirmar();
        }
        var sinVentas = login.Ingresar("bodega", "Bodega2026x");
        var mb = new PanelService(new Contexto(bd, sinVentas)).Metricas();
        Check("Sin permiso de ventas: los montos llegan en cero", mb.VentasHoy == 0 && mb.VentasMes == 0 && mb.ProductosActivos == 2);
        lanza("Sin permiso de ventas no recibe el gráfico", () => new PanelService(new Contexto(bd, sinVentas)).VentasPorDia());

        Console.WriteLine("== Integración: respaldo ==");
        var carpeta = Path.Combine(Path.GetTempPath(), $"nabeauty_resp_{Guid.NewGuid():N}");
        var r1 = bd.Respaldar(carpeta);
        Check("Respaldo diario creado", r1 is not null && File.Exists(r1));
        Check("Un solo respaldo por día", bd.Respaldar(carpeta) is null);

        File.Delete(archivo);
    }
}
