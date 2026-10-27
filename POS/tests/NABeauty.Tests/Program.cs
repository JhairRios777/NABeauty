using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Tests;

// Pruebas de las reglas de negocio. Salida: código 1 si algo falla.
int ok = 0; var fallos = new List<string>();
void Check(string nombre, bool cond, string extra = "")
{
    if (cond) { ok++; Console.WriteLine($"  ✓ {nombre}"); }
    else { fallos.Add(nombre); Console.WriteLine($"  ✗ {nombre} {extra}"); }
}
void Lanza<T>(string nombre, Action a) where T : Exception
{
    try { a(); Check(nombre, false, "no lanzó"); } catch (T) { Check(nombre, true); }
}

Console.WriteLine("== Dinero ==");
Check("Formato L 1,250.50", Dinero.Formatear(125050) == "L 1,250.50");
Check("Formato cero", Dinero.Formatear(0) == "L 0.00");
Check("Parse con L y comas", Dinero.TryParse("L 1,500.5", out var c1) && c1 == 150050);
Check("Rechaza 3 decimales", !Dinero.TryParse("10.123", out _));
Check("Rechaza negativo", !Dinero.TryParse("-5", out _));

Console.WriteLine("== Permisos ==");
var admin = new Usuario { Rol = Permisos.RolAdmin };
var cajera = new Usuario { Rol = "cajera", PermisosRol = [.. Permisos.Predeterminados["cajera"].Permisos] };
var socio = new Usuario { Rol = "socio", PermisosRol = [.. Permisos.Predeterminados["socio"].Permisos] };
Check("Admin tiene todo, incluido administrar roles", Enum.GetValues<Permiso>().All(admin.Puede));
Check("Cajera vende pero no ve costos ni reportes", cajera.Puede(Permiso.Vender) && !cajera.Puede(Permiso.VerCostos) && !cajera.Puede(Permiso.VerReportes));
Check("Cajera no entra al panel web", !cajera.Puede(Permiso.WebAcceso));
Check("Socio ve reportes aquí y en la web, pero no vende", socio.Puede(Permiso.VerReportes) && socio.Puede(Permiso.WebReportes) && !socio.Puede(Permiso.Vender));
Check("Usuario inactivo no puede nada", !new Usuario { Rol = Permisos.RolAdmin, Activo = false }.Puede(Permiso.VerInicio));
Check("Administrar roles NO es asignable", !Permisos.EsAsignable(Permiso.GestionarRoles));
Check("Todos los demás permisos están en el catálogo",
    Enum.GetValues<Permiso>().Where(p => p != Permiso.GestionarRoles).All(Permisos.EsAsignable));
Check("Claves web iguales a las de la tienda en línea",
    Permisos.ClaveWeb(Permiso.WebAcceso) == "web.acceso" && Permisos.ClaveWeb(Permiso.WebProductos) == "web.productos"
    && Permisos.ClaveWeb(Permiso.WebReportes) == "web.reportes" && Permisos.ClaveWeb(Permiso.WebExportar) == "web.exportar"
    && Permisos.ClaveWeb(Permiso.Vender) is null);

Console.WriteLine("== Contraseñas ==");
var h = PasswordHasher.Hash("ClaveSegura2026");
Check("Verifica la correcta", PasswordHasher.Verificar("ClaveSegura2026", h));
Check("Rechaza la incorrecta", !PasswordHasher.Verificar("clavesegura2026", h));
Check("Rechaza hash corrupto sin excepción", !PasswordHasher.Verificar("x", "1000.@@@.###"));
Check("Clave inicial cumple la fortaleza mínima", PasswordHasher.ValidarFortaleza(NABeauty.Data.Infra.BaseDatos.ClaveInicial) is null);

// En Linux se corren además las pruebas contra SQLite real y las pantallas
if (OperatingSystem.IsLinux())
{
    Integracion.Correr(Check, Lanza<ReglaNegocioException>);
    PruebasPantallas.Correr(Check);
    PruebasCatalogoCaja.Correr(Check, Lanza<ReglaNegocioException>);
    PruebasOperaciones.Correr(Check, Lanza<ReglaNegocioException>);
}

Console.WriteLine($"\n{ok} pruebas OK, {fallos.Count} fallaron");
fallos.ForEach(f => Console.WriteLine($"  - {f}"));
return fallos.Count == 0 ? 0 : 1;
