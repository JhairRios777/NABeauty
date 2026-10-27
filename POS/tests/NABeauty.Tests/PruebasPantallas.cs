using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;
using NABeauty.Presentacion.Mvvm;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.Tests;

/// <summary>Diálogos simulados: guardan los mensajes y responden "sí" a todo.</summary>
internal sealed class DialogosFalsos : IDialogos
{
    public readonly List<string> Errores = [], Infos = [];
    public void Info(string m) => Infos.Add(m);
    public void Error(string m) => Errores.Add(m);
    public bool Confirmar(string m) => true;
    /// <summary>Rutas que "elige" el usuario en el próximo selector de imágenes.</summary>
    public List<string> Imagenes = [];
    public IReadOnlyList<string> ElegirImagenes() { var r = Imagenes; Imagenes = []; return r; }
    /// <summary>Ruta que "elige" el usuario en el próximo Guardar como (null = cancela).</summary>
    public string? RutaGuardar;
    public string? GuardarComo(string nombre, string extension, string descripcion) => RutaGuardar;
    public readonly List<string> Abiertos = [];
    public void Abrir(string ruta) => Abiertos.Add(ruta);
}

/// <summary>Recorre login, menú y tablero por sus ViewModels, como lo haría un usuario.</summary>
internal static class PruebasPantallas
{
    public static void Correr(Action<string, bool, string> check)
    {
        void Check(string n, bool c, string e = "") => check(n, c, e);
        var archivo = Path.Combine(Path.GetTempPath(), $"nabeauty_ui_{Guid.NewGuid():N}.db");
        var bd = new BaseDatos(() => new ConexionMinima(archivo));
        bd.Inicializar();
        var s = new Sesion(bd, new DialogosFalsos());

        Console.WriteLine("== Pantallas: login ==");
        var login = new LoginViewModel(s) { Usuario = "admin" };
        Check("Contraseña mala muestra el error y cuántos intentos quedan",
            !login.Ingresar("mala") && login.HayError && login.Error.Contains("4 intentos"), login.Error);
        Check("Entrar con la clave inicial", login.Ingresar(BaseDatos.ClaveInicial) && !login.HayError && s.Usuario is not null);
        var cambio = new CambiarClaveViewModel(s);
        Check("Cambiar clave es obligatorio la primera vez", cambio.Obligatorio);
        Check("Nuevas que no coinciden", !cambio.Cambiar(BaseDatos.ClaveInicial, "Nataly2026pos", "Nataly2026po") && cambio.HayError);
        Check("Cambio correcto", cambio.Cambiar(BaseDatos.ClaveInicial, "Nataly2026pos", "Nataly2026pos"));

        Console.WriteLine("== Pantallas: menú e inicio (base vacía) ==");
        var shell = new ShellViewModel(s);
        Check("Admin ve todas las secciones", shell.Menu.Count == 10, shell.Menu.Count.ToString());
        Check("Abre en Inicio", shell.Vista is InicioViewModel && shell.Titulo == "Inicio" && shell.Menu.Single(m => m.Activo).Clave == "inicio");
        var inicio = (InicioViewModel)shell.Vista!;
        Check("Base vacía: guía de primeros pasos", inicio.MostrarPrimerosPasos && inicio.Pasos.Count == 4 && inicio.Pasos[0].Hecho && !inicio.Pasos[1].Hecho);
        Check("Base vacía: 4 indicadores de ventas en L 0.00", inicio.KpisVentas.Count == 4 && inicio.KpisVentas.All(k => k.Valor == "L 0.00"));
        Check("Sin ventas previas no inventa un porcentaje", inicio.KpisVentas.All(k => !k.HayVariacion));
        Check("Gráfico de 14 días en cero", inicio.Barras.Count == 14 && inicio.SinVentasRecientes && inicio.Barras[^1].Etiqueta == "Hoy");
        Check("Aviso de tienda en línea sin conectar", !inicio.SyncConfigurada && inicio.SyncTitulo.StartsWith("Sin conectar"));
        Check("Saludo con el primer nombre", inicio.Saludo.EndsWith("Administrador"), inicio.Saludo);

        Console.WriteLine("== Pantallas: navegación ==");
        shell.IrA("ventas");
        Check("Ventas abre su pantalla", shell.Vista is VentasViewModel && shell.Titulo == "Ventas");
        Check("Marca la opción activa", shell.Menu.Single(m => m.Activo).Clave == "ventas");
        Check("Ningún módulo queda como Pronto", shell.Menu.All(m => !m.Proximamente));
        shell.IrA("no-existe");
        Check("Sección desconocida cae en Inicio", shell.Vista is InicioViewModel);

        Console.WriteLine("== Pantallas: inicio con datos ==");
        using (var u = bd.Abrir(true))
        {
            var v = Datos.Producto(u, "Labial", 25000, 12000);
            Datos.Entrada(u, v, 5);
            Datos.Venta(u, "V-000001", v, 2, 25000, 12000);
            Datos.Venta(u, "V-000002", v, 1, 25000, 12000, DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd 12:00:00"));
            u.Confirmar();
        }
        inicio.Cargar();
        Check("Ya no muestra los primeros pasos", !inicio.MostrarPrimerosPasos);
        Check("Ventas de hoy L 500.00 con 2 unidades vendidas", inicio.KpisVentas[0].Valor == "L 500.00" && inicio.KpisVentas[0].Destacado, inicio.KpisVentas[0].Valor);
        Check("Compara contra ayer: +100%", inicio.KpisVentas[0].Variacion == "+100% vs ayer" && inicio.KpisVentas[0].Tendencia == "sube", inicio.KpisVentas[0].Variacion);
        Check("Admin ve la ganancia del mes", inicio.KpisVentas[3].Titulo == "Ganancia del mes");
        Check("Barra de hoy es la más alta", inicio.Barras[^1].Altura == 150 && inicio.Barras[^2].Altura == 75);
        Check("Más vendidos y últimas ventas", inicio.HayTop && inicio.Ultimas.Count == 2);
        Check("Resumen de hoy", inicio.Resumen.Contains("1 venta") || inicio.Resumen.Contains("venta"), inicio.Resumen);

        Console.WriteLine("== Pantallas: cajera ==");
        using (var u = bd.Abrir(true))
        {
            new UsuarioRepo(u).Crear("María López", "maria", NABeauty.Core.Seguridad.PasswordHasher.Hash("Maria2026xx"), "cajera");
            u.Confirmar();
        }
        s.Cerrar();
        var l2 = new LoginViewModel(s) { Usuario = "maria" };
        l2.Ingresar("Maria2026xx");
        var shellCajera = new ShellViewModel(s);
        var claves = shellCajera.Menu.Select(m => m.Clave).ToList();
        Check("Cajera: Inicio, Caja, Ventas y Productos", claves.SequenceEqual(["inicio", "caja", "ventas", "productos"]), string.Join(",", claves));
        shellCajera.IrA("usuarios");
        Check("Cajera no abre Usuarios (vuelve a Inicio)", shellCajera.Vista is InicioViewModel);
        var ic = (InicioViewModel)shellCajera.Vista!;
        Check("Cajera ve ticket promedio, no la ganancia", ic.KpisVentas[3].Titulo == "Ticket promedio" && !ic.VerCostos);
        Check("Pasos: la cajera no cambió su clave temporal", !ic.Pasos[0].Hecho);

        File.Delete(archivo);
    }
}
