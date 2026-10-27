using System.IO.Compression;
using System.Xml.Linq;
using NABeauty.Core.Dominio;
using NABeauty.Core.Entidades;
using NABeauty.Core.Seguridad;
using NABeauty.Data.Infra;
using NABeauty.Data.Repositorios;
using NABeauty.Data.Servicios;
using NABeauty.Presentacion.Mvvm;
using NABeauty.Presentacion.ViewModels;

namespace NABeauty.Tests;

/// <summary>Impresora falsa: guarda lo que se "imprimió".</summary>
internal sealed class ImpresionFalsa : IImpresion
{
    public readonly List<(Recibo Recibo, bool Pregunto)> Recibos = [];
    public readonly List<Reporte> Reportes = [];
    public bool ImprimirRecibo(Recibo r, bool preguntar = true) { Recibos.Add((r, preguntar)); return true; }
    public bool ImprimirReporte(Reporte r) { Reportes.Add(r); return true; }
}

/// <summary>Ventas, entradas, ajustes, reportes, usuarios, roles y ajustes: servicios y pantallas.</summary>
internal static class PruebasOperaciones
{
    public static void Correr(Action<string, bool, string> check, Action<string, Action> lanza)
    {
        void Check(string n, bool c, string e = "") => check(n, c, e);
        var archivo = Path.Combine(Path.GetTempPath(), $"nabeauty_ops_{Guid.NewGuid():N}.db");
        var bd = new BaseDatos(() => new ConexionMinima(archivo));
        bd.Inicializar();
        long Esc(string sql) { using var u = bd.Abrir(); return u.Escalar<long>(sql); }

        var login = new LoginService(bd);
        var adm = login.Ingresar("admin", BaseDatos.ClaveInicial);
        var ctxAdm = new Contexto(bd, adm);

        // ═══════════════ Período ═══════════════
        Console.WriteLine("== Período ==");
        var hoy = new DateOnly(2026, 10, 26);   // lunes
        Check("Hoy = un día", Periodo.DePreset("hoy", hoy) == new Periodo(hoy, hoy.AddDays(1)));
        Check("Semana empieza el lunes", Periodo.DePreset("semana", new DateOnly(2026, 10, 29)).Desde == hoy);
        var oct = Periodo.DePreset("mes", hoy);
        Check("Mes completo y su anterior es el mes anterior", oct.Dias == 31 && oct.Anterior() == new Periodo(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)));
        Check("Rango incluye el último día", Periodo.Rango(hoy, hoy.AddDays(2)).Hasta == hoy.AddDays(3));
        Check("Rango invertido se corrige", Periodo.Rango(hoy.AddDays(2), hoy).Desde == hoy);
        Check("Año se grafica por mes", Periodo.DePreset("anio", hoy).PorMes && !oct.PorMes);
        Check("Etiqueta de mes", oct.Etiqueta.StartsWith("Octubre"), oct.Etiqueta);

        // ═══════════════ Catálogo base ═══════════════
        var cat = new CatalogoService(ctxAdm);
        var maq = cat.GuardarCategoria(new Categoria { Nombre = "Maquillaje", Color = "#F6D5DC" });
        var labial = cat.GuardarProducto(new Producto
        {
            CategoriaUuid = maq, Nombre = "Labial", Marca = "Ruby",
            Variantes = [new Variante { Tipo = "tono", Nombre = "Rojo", Precio = 25000, Costo = 10000, CodigoBarras = "111" },
                         new Variante { Tipo = "tono", Nombre = "Nude", Precio = 25000, Costo = 10000 }],
        });
        var rubor = cat.GuardarProducto(new Producto
        {
            CategoriaUuid = maq, Nombre = "Rubor", Marca = "Bissú",
            Variantes = [new Variante { Precio = 18000, Costo = 9000 }],
        });
        var vRojo = cat.Producto(labial).Variantes[0].Uuid;
        var vNude = cat.Producto(labial).Variantes[1].Uuid;
        var vRubor = cat.Producto(rubor).Variantes[0].Uuid;
        long Stock(string v) => Esc($"SELECT stock FROM producto_variantes WHERE uuid = '{v}'");

        using (var u = bd.Abrir(true))
        {
            var repo = new UsuarioRepo(u);
            repo.Crear("María Cajera", "maria", PasswordHasher.Hash("Maria2026xx"), "cajera");
            repo.Crear("Sofía Socia", "sofia", PasswordHasher.Hash("Sofia2026xx"), "socio");
            u.Confirmar();
        }
        var caj = login.Ingresar("maria", "Maria2026xx");
        var soc = login.Ingresar("sofia", "Sofia2026xx");

        // ═══════════════ Entradas ═══════════════
        Console.WriteLine("== Entradas de mercadería ==");
        var ent = new EntradaService(ctxAdm);
        var e1 = ent.Registrar("Distribuidora Ruby", "Factura 123", [new(vRojo, 10, 11000), new(vRubor, 4, 9500)], actualizarCostos: true);
        Check("Entrada E-0001 suma stock", e1 == "E-0001" && Stock(vRojo) == 10 && Stock(vRubor) == 4);
        Check("Actualiza el costo del producto", Esc($"SELECT costo FROM producto_variantes WHERE uuid = '{vRojo}'") == 11000);
        var e2 = ent.Registrar(null, null, [new(vNude, 3, 12000)], actualizarCostos: false);
        Check("Sin actualizar costo lo deja igual", e2 == "E-0002" && Esc($"SELECT costo FROM producto_variantes WHERE uuid = '{vNude}'") == 10000);
        var listaE = ent.Listar(Periodo.Dia(Formato.Hoy));
        Check("Lista de entradas con totales", listaE.Count == 2 && listaE.First(x => x.Numero == "E-0001").TotalCosto == 10 * 11000 + 4 * 9500
            && listaE.First(x => x.Numero == "E-0001").Unidades == 14);
        Check("Buscar entrada por proveedor", ent.Listar(Periodo.Dia(Formato.Hoy), "ruby").Single().Numero == "E-0001");
        Check("Detalle con nombres", ent.Detalle(listaE.First(x => x.Numero == "E-0001").Uuid).Lineas.Count == 2);
        lanza("Producto repetido en una entrada", () => ent.Registrar(null, null, [new(vRojo, 1, 1), new(vRojo, 1, 1)], false));
        lanza("Cantidad cero", () => ent.Registrar(null, null, [new(vRojo, 0, 1)], false));
        lanza("Entrada vacía", () => ent.Registrar(null, null, [], false));
        lanza("Cajera no registra entradas", () => new EntradaService(new Contexto(bd, caj)).Registrar(null, null, [new(vRojo, 1, 1)], false));
        lanza("Una entrada no se borra", () => { using var u = bd.Abrir(); u.Ejecutar("DELETE FROM entradas"); });
        Check("Buscar para entrada por código exacto", ent.Buscar("111").Single().VarianteUuid == vRojo);

        Console.WriteLine("== Ajustes de inventario ==");
        Check("Conteo físico registra la diferencia", ent.Ajustar(vRojo, 8, conteo: true, "Conteo de fin de mes") == -2 && Stock(vRojo) == 8);
        Check("Sumar unidades", ent.Ajustar(vNude, 2, conteo: false, "Encontradas en bodega") == 2 && Stock(vNude) == 5);
        lanza("Ajuste sin motivo", () => ent.Ajustar(vNude, 1, false, ""));
        lanza("No deja el stock negativo", () => ent.Ajustar(vNude, -50, false, "Dañados"));
        lanza("Conteo igual al stock: nada que ajustar", () => ent.Ajustar(vNude, 5, true, "Conteo"));
        lanza("Cajera no ajusta", () => new EntradaService(new Contexto(bd, caj)).Ajustar(vNude, 1, false, "Prueba"));
        var ajustes = ent.Ajustes(Periodo.Dia(Formato.Hoy));
        Check("Historial de ajustes con motivo", ajustes.Count == 2 && ajustes.Any(a => a.Cantidad == -2 && a.Nota == "Conteo de fin de mes" && a.CantidadTexto == "-2"));

        // ═══════════════ Ventas ═══════════════
        Console.WriteLine("== Ventas: historial y anulación ==");
        var caja = new CajaService(new Contexto(bd, caj));
        Carrito Carro(string v, int n) { var c = new Carrito(); c.Agregar(caja.Buscar(null).First(a => a.VarianteUuid == v), n); return c; }
        var venta1 = caja.Cobrar(Carro(vRojo, 2), "efectivo", 60000);
        var venta2 = caja.Cobrar(Carro(vRubor, 1), "tarjeta", 0);
        var venta3 = caja.Cobrar(Carro(vNude, 1), "transferencia", 0);
        Check("El efectivo recibido queda guardado", Esc($"SELECT recibido FROM ventas WHERE uuid = '{venta1.Uuid}'") == 60000
            && Esc($"SELECT COUNT(*) FROM ventas WHERE recibido IS NULL") == 2);

        var vs = new VentaService(ctxAdm);
        var hoyF = new FiltroVentas(Periodo.Dia(Formato.Hoy));
        Check("Historial de hoy", vs.Listar(hoyF).Count == 3);
        Check("Filtro por método", vs.Listar(hoyF with { MetodoPago = "tarjeta" }).Single().Uuid == venta2.Uuid);
        Check("Filtro por número", vs.Listar(hoyF with { Numero = "0002" }).Single().Numero == "V-000002");
        Check("Filtro por vendedora", vs.Listar(hoyF with { UsuarioId = caj.Id }).Count == 3 && vs.Listar(hoyF with { UsuarioId = adm.Id }).Count == 0);
        Check("Vendedoras con ventas", vs.Vendedoras().Single().Nombre == "María Cajera");
        var res = vs.Resumen(hoyF);
        Check("Resumen del filtro", res.Ventas == 3 && res.Total == 50000 + 18000 + 25000 && res.Unidades == 4);
        var det = vs.Detalle(venta1.Uuid);
        Check("Detalle: líneas y cambio", det.Lineas.Single().Cantidad == 2 && det.Venta.Cambio == 10000 && det.Venta.Vendedora == "María Cajera");
        Check("Cajera ve el detalle sin costo", new VentaService(new Contexto(bd, caj)).Detalle(venta1.Uuid).Lineas.All(l => l.CostoUnitario is null));

        lanza("Cajera no anula", () => new VentaService(new Contexto(bd, caj)).Anular(venta1.Uuid, "Error de cobro"));
        lanza("Anular sin motivo suficiente", () => vs.Anular(venta1.Uuid, "x"));
        var antes = Stock(vRojo);
        vs.Anular(venta1.Uuid, "La clienta lo devolvió");
        var anulada = vs.Detalle(venta1.Uuid).Venta;
        Check("Anular devuelve el stock", Stock(vRojo) == antes + 2);
        Check("Queda quién y cuándo anuló", anulada.Anulada && anulada.AnuladaPor == "Administrador" && anulada.AnuladaFecha is not null && anulada.MotivoAnulacion == "La clienta lo devolvió");
        lanza("No se anula dos veces", () => vs.Anular(venta1.Uuid, "Otra vez por error"));
        Check("La anulación queda para la tienda en línea", Esc($"SELECT COUNT(*) FROM sync_cola WHERE uuid = '{venta1.Uuid}' AND operacion = 'anular'") == 1);
        res = vs.Resumen(hoyF);
        Check("Resumen: anuladas aparte", res.Ventas == 2 && res.Total == 43000 && res.Anuladas == 1 && res.TotalAnulado == 50000);
        Check("Filtro de anuladas", vs.Listar(hoyF with { Estado = "anulada" }).Single().Uuid == venta1.Uuid);

        var recibo = vs.Recibo(venta1.Uuid);
        Check("Recibo: negocio, líneas, cambio y marca de anulada", recibo.Negocio.Nombre == "N&A Beauty" && recibo.Lineas.Count == 1 && recibo.Cambio == 10000 && recibo.Anulada);
        Check("La cajera imprime el recibo aunque no anule", new VentaService(new Contexto(bd, caj)).Recibo(venta2.Uuid).Numero == "V-000002");

        // ═══════════════ Reportes ═══════════════
        Console.WriteLine("== Reportes ==");
        var rep = new ReporteService(ctxAdm).Generar(Periodo.Dia(Formato.Hoy));
        Check("Total del período sin la anulada", rep.Resumen.Ventas == 2 && rep.Resumen.Total == 43000 && rep.Resumen.Unidades == 2);
        Check("Costo y ganancia con el costo del momento", rep.Resumen.Costo == 9500 + 10000 && rep.Resumen.Ganancia == 43000 - 19500, rep.Resumen.Costo.ToString());
        Check("Anuladas y entradas del período", rep.Resumen.Anuladas == 1 && rep.Resumen.Entradas == 2 && rep.Resumen.UnidadesEntrada == 17);
        Check("Por método con nombre legible", rep.Metodos.Select(m => m.Nombre).OrderBy(x => x).SequenceEqual(["Tarjeta", "Transferencia"]));
        Check("Por vendedora", rep.Vendedoras.Single().Nombre == "María Cajera" && rep.Vendedoras[0].Porcentaje == 100);
        Check("Por categoría", rep.Categorias.Single().Nombre == "Maquillaje" && rep.Categorias[0].Total == 43000);
        Check("Productos con opción", rep.Productos.Any(p => p.Nombre == "Labial" && p.Detalle == "Nude") && rep.Productos.Any(p => p.Nombre == "Rubor" && p.Detalle is null));
        Check("Serie de un día", rep.Serie.Count == 1 && rep.Serie[0].Total == 43000);
        var mes = new ReporteService(ctxAdm).Generar(Periodo.DePreset("mes", Formato.Hoy));
        Check("Serie del mes: todos los días, en cero los que no hubo ventas", mes.Serie.Count == mes.Periodo.Dias && mes.Serie.Sum(s => s.Total) == 43000);
        Check("Año agrupa por mes", new ReporteService(ctxAdm).Generar(Periodo.DePreset("anio", Formato.Hoy)).Serie.Count == 12);
        lanza("Cajera no ve reportes", () => new ReporteService(new Contexto(bd, caj)).Generar(Periodo.Dia(Formato.Hoy)));
        Check("Socio ve reportes con costos", new ReporteService(new Contexto(bd, soc)).Generar(Periodo.Dia(Formato.Hoy)).Resumen.Costo == 19500);
        var rolSinCostos = new RolService(ctxAdm).Guardar(0, "Supervisora", "Reportes sin costos", [Permiso.VerInicio, Permiso.VerReportes]);
        using (var u = bd.Abrir(true)) { new UsuarioRepo(u).Crear("Ana Supervisora", "ana", PasswordHasher.Hash("Ana2026xxxx"), "supervisora"); u.Confirmar(); }
        var sup = login.Ingresar("ana", "Ana2026xxxx");
        var repSup = new ReporteService(new Contexto(bd, sup)).Generar(Periodo.Dia(Formato.Hoy));
        Check("Sin permiso de costos: costo en cero y sin ganancia", !repSup.IncluyeCostos && repSup.Resumen.Costo == 0 && repSup.Productos.All(p => p.Costo == 0));
        lanza("Sin permiso de exportar no descarga", () => new ReporteService(new Contexto(bd, sup)).ExportarExcel(Periodo.Dia(Formato.Hoy), Path.GetTempFileName()));

        Console.WriteLine("== Reportes: Excel ==");
        var xlsx = Path.Combine(Path.GetTempPath(), $"reporte_{Guid.NewGuid():N}.xlsx");
        new ReporteService(ctxAdm).ExportarExcel(Periodo.Dia(Formato.Hoy), xlsx);
        using (var zip = ZipFile.OpenRead(xlsx))
        {
            var nombres = zip.Entries.Select(e => e.FullName).ToList();
            Check("El .xlsx tiene libro, estilos y 5 hojas", nombres.Contains("xl/workbook.xml") && nombres.Contains("xl/styles.xml")
                && nombres.Count(n => n.StartsWith("xl/worksheets/sheet")) == 5, string.Join(",", nombres));
            var todoXmlValido = zip.Entries.All(e => { using var s = e.Open(); XDocument.Load(s); return true; });
            Check("Todo el XML del .xlsx es válido", todoXmlValido);
            string Leer(string n) { using var r = new StreamReader(zip.GetEntry(n)!.Open()); return r.ReadToEnd(); }
            var hojas = XDocument.Parse(Leer("xl/workbook.xml")).Descendants().Where(x => x.Name.LocalName == "sheet").Select(x => (string)x.Attribute("name")!).ToList();
            Check("Nombres de hojas", hojas.SequenceEqual(["Resumen", "Productos", "Categorías", "Pagos y vendedoras", "Detalle"]), string.Join(",", hojas));
            var resumen = Leer("xl/worksheets/sheet1.xml");
            Check("Resumen: total como número (430 lempiras)", resumen.Contains("Total vendido") && resumen.Contains("<v>430</v>"));
            var detalle = Leer("xl/worksheets/sheet5.xml");
            Check("Detalle: una fila por línea vendida (sin la anulada)", detalle.Split("<row ").Length - 1 == 1 + 2 && detalle.Contains("V-000002") && !detalle.Contains("V-000001"));
            Check("Caracteres especiales escapados", Leer("xl/worksheets/sheet1.xml").Contains("N&amp;A Beauty"));
        }
        Check("Columnas de Excel", LibroExcel_Columna(0) == "A" && LibroExcel_Columna(25) == "Z" && LibroExcel_Columna(26) == "AA" && LibroExcel_Columna(701) == "ZZ");

        // ═══════════════ Usuarios ═══════════════
        Console.WriteLine("== Usuarios ==");
        var us = new UsuarioService(ctxAdm);
        var temporal = us.Crear("Laura Pérez", "Laura.P", "cajera");
        var laura = login.Ingresar("laura.p", temporal);
        Check("Crear con contraseña temporal (usuario en minúsculas)", laura.DebeCambiarPassword && laura.Rol == "cajera" && temporal.Length == 12);
        Check("El usuario nuevo queda para la web", Esc($"SELECT COUNT(*) FROM sync_cola WHERE entidad = 'usuario' AND uuid = '{laura.Uuid}'") == 1);
        lanza("Usuario con espacios", () => us.Crear("Otra Persona", "otra persona", "cajera"));
        lanza("Usuario repetido", () => us.Crear("Laura Dos", "LAURA.P", "cajera"));
        lanza("Rol inexistente", () => us.Crear("Otra Persona", "otra", "gerente"));
        lanza("No te desactivas a ti mismo", () => us.Actualizar(adm.Id, "Administrador", "admin", "admin", activo: false));
        lanza("No te quitas el rol de administrador", () => us.Actualizar(adm.Id, "Administrador", "admin", "cajera", activo: true));
        lanza("Cajera no gestiona usuarios", () => new UsuarioService(new Contexto(bd, caj)).Listar());
        us.Actualizar(laura.Id, "Laura Pérez", "laura.p", "socio", activo: true);
        Check("Cambiar rol", us.Listar().Single(x => x.Id == laura.Id).Rol == "socio");
        for (var i = 0; i < 5; i++) { try { login.Ingresar("laura.p", "mala"); } catch (ReglaNegocioException) { } }
        Check("Lista marca el bloqueo", us.Listar().Single(x => x.Id == laura.Id).Bloqueado);
        us.Desbloquear(laura.Id);
        Check("Desbloquear", !us.Listar().Single(x => x.Id == laura.Id).Bloqueado && login.Ingresar("laura.p", temporal).Id == laura.Id);
        var nueva = us.RestablecerPassword(laura.Id);
        lanza("La contraseña anterior deja de servir", () => login.Ingresar("laura.p", temporal));
        Check("Restablecer: nueva temporal", login.Ingresar("laura.p", nueva).DebeCambiarPassword);
        us.Actualizar(laura.Id, "Laura Pérez", "laura.p", "socio", activo: false);
        lanza("Usuario inactivo no entra", () => login.Ingresar("laura.p", nueva));
        Check("La lista no expone hashes", us.Listar().All(x => x.PasswordHash == ""));
        Check("Roles para asignar incluyen al administrador (eres admin)", us.Roles().Any(r => r.Sistema));

        // ═══════════════ Roles ═══════════════
        Console.WriteLine("== Roles ==");
        var rs = new RolService(ctxAdm);
        var bodega = rs.Guardar(0, "Bodega y entradas", "Recibe mercadería", [Permiso.VerInicio, Permiso.VerProductos, Permiso.RegistrarEntradas]);
        var rb = rs.Listar().Single(r => r.Id == bodega);
        Check("Crear rol: clave sin espacios y sus permisos", rb.Clave == "bodega_y_entradas" && rb.Permisos.Count == 3);
        lanza("Gestionar roles no se asigna", () => rs.Guardar(0, "Jefa", null, [Permiso.GestionarRoles]));
        lanza("Rol sin permisos", () => rs.Guardar(0, "Vacío", null, []));
        lanza("Nombre repetido", () => rs.Guardar(0, "bodega y entradas", null, [Permiso.VerInicio]));
        lanza("El Administrador no se edita", () => rs.Guardar(rs.Listar().Single(r => r.Sistema).Id, "Admin", null, [Permiso.VerInicio]));
        rs.Guardar(bodega, "Bodega y entradas", "Recibe y ajusta", [Permiso.VerInicio, Permiso.RegistrarEntradas, Permiso.AjustarInventario]);
        Check("Editar permisos", rs.Listar().Single(r => r.Id == bodega).Permisos.Contains(Permiso.AjustarInventario));
        lanza("No se elimina un rol con usuarios", () => rs.Eliminar(rs.Listar().Single(r => r.Clave == "cajera").Id));
        rs.Eliminar(bodega);
        Check("Eliminar rol sin usuarios", rs.Listar().All(r => r.Id != bodega));
        lanza("Solo el administrador gestiona roles", () => new RolService(new Contexto(bd, soc)).Listar());

        // ═══════════════ Ajustes ═══════════════
        Console.WriteLine("== Ajustes ==");
        var aj = new AjustesService(ctxAdm);
        lanza("RTN inválido", () => aj.GuardarNegocio(new Negocio { Nombre = "N&A Beauty", Rtn = "123" }));
        aj.GuardarNegocio(new Negocio { Nombre = "N&A Beauty Store", Rtn = "0501-1999-123456", Telefono = "9342-9641", Direccion = "Santa Bárbara", MensajeRecibo = "Vuelve pronto", AnchoRecibo = 58, ImprimirAlCobrar = true });
        var neg = aj.Negocio();
        Check("Guardar datos del negocio y del recibo", neg.Nombre == "N&A Beauty Store" && neg.AnchoRecibo == 58 && neg.ImprimirAlCobrar && neg.Rtn == "0501-1999-123456");
        Check("El recibo usa los datos nuevos", vs.Recibo(venta2.Uuid).Negocio.MensajeRecibo == "Vuelve pronto");
        lanza("Intentos fuera de rango", () => aj.GuardarSeguridad(1, 15));
        aj.GuardarSeguridad(3, 30);
        Check("Seguridad del ingreso", aj.Seguridad() == new AjustesSeguridad(3, 30));
        lanza("Tienda en línea sin https", () => aj.GuardarWeb("http://nyabeauty.com", null));
        lanza("Token corto", () => aj.GuardarWeb("https://nyabeauty.com", "abc"));
        aj.GuardarWeb("https://nyabeauty.com/", "token-de-prueba-0123456789");
        aj.GuardarWeb("https://nyabeauty.com", null);
        var web = aj.Web();
        Check("Conexión web: URL limpia y el token se conserva", web.Url == "https://nyabeauty.com" && web.TieneToken && web.Pendientes > 0);
        lanza("Cajera no entra a Ajustes", () => new AjustesService(new Contexto(bd, caj)).Seguridad());
        var carpetaResp = Directory.CreateTempSubdirectory("nabeauty_resp_").FullName;
        var r1 = aj.Respaldar(carpetaResp);
        Check("Respaldo manual con hora", File.Exists(r1) && Path.GetFileName(r1).Length == "nabeauty-2026-10-26-153000.db".Length);

        Console.WriteLine("== Migración de una base anterior ==");
        using (var u = bd.Abrir()) u.Ejecutar("ALTER TABLE ventas DROP COLUMN recibido");
        bd.Inicializar();
        Check("Agrega la columna que falta sin perder ventas", Esc("SELECT COUNT(*) FROM pragma_table_info('ventas') WHERE name = 'recibido'") == 1
            && Esc("SELECT COUNT(*) FROM ventas") == 3);

        // ═══════════════ Pantallas ═══════════════
        Console.WriteLine("== Pantallas: ventas, entradas, reportes, usuarios, roles y ajustes ==");
        var dlg = new DialogosFalsos();
        var imp = new ImpresionFalsa();
        var s = new Sesion(bd, dlg) { Impresion = imp, CarpetaRespaldos = carpetaResp };
        s.Iniciar(login.Ingresar("admin", BaseDatos.ClaveInicial));
        var shell = new ShellViewModel(s);

        shell.IrA("ventas");
        var vv = (VentasViewModel)shell.Vista!;
        Check("Ventas: hoy por defecto con total", vv.Lista.Count == 3 && vv.TotalTexto == "L 430.00" && vv.HayAnuladas, vv.TotalTexto);
        vv.Seleccionada = vv.Lista.First(x => x.Uuid == venta2.Uuid);
        Check("Detalle de la venta elegida", vv.HaySeleccion && vv.Lineas.Count == 1 && vv.DetallePago == "Tarjeta" && vv.PuedeAnular);
        vv.Motivo = "abc";
        dlg.Errores.Clear();
        Check("Anular pide motivo", !vv.Anular() && dlg.Errores.Count == 1);
        vv.Motivo = "Cobro duplicado por error";
        Check("Anular desde la pantalla", vv.Anular() && vv.Lista.First(x => x.Uuid == venta2.Uuid).Anulada && !vv.PuedeAnular);
        Check("Reimprimir recibo", vv.Imprimir() && imp.Recibos[^1].Recibo.Numero == "V-000002" && imp.Recibos[^1].Pregunto);
        vv.Metodo = vv.Metodos.First(m => m.Clave == "transferencia");
        Check("Filtrar por método desde la pantalla", vv.Lista.Count == 1);
        vv.Periodo.Elegir("ayer");
        Check("Cambiar el período recarga", vv.Lista.Count == 0 && !vv.HayVentas);

        shell.IrA("entradas");
        var ve = (EntradasViewModel)shell.Vista!;
        Check("Entradas del mes", ve.Lista.Count == 2 && ve.VerEntradas && ve.Resumen.StartsWith("2 entradas"));
        ve.Seleccionada = ve.Lista.First(x => x.Numero == "E-0001");
        Check("Detalle de la entrada", ve.Lineas.Count == 2);
        ve.NuevaCommand.Execute(null);
        var ne = (NuevaEntradaViewModel)shell.Vista!;
        ne.Texto = "111";
        ne.Enter();
        Check("Nueva entrada: escáner agrega con el costo actual", ne.Lineas.Count == 1 && ne.Lineas[0].Costo == "110.00" && ne.Texto == "");
        ne.Agregar(ne.Lineas[0].Articulo);
        Check("Agregar de nuevo suma cantidad", ne.Lineas[0].Cantidad == "2" && ne.Total == "L 220.00");
        ne.Lineas[0].Cantidad = "x";
        dlg.Errores.Clear();
        Check("Cantidad inválida no guarda", !ne.Guardar() && dlg.Errores.Count == 1);
        ne.Lineas[0].Cantidad = "6";
        ne.Proveedor = "Ruby";
        var stockAntes = Stock(vRojo);
        Check("Guardar entrada vuelve a la lista", ne.Guardar() && shell.Vista is EntradasViewModel && Stock(vRojo) == stockAntes + 6);
        ve = (EntradasViewModel)shell.Vista!;
        ve.TabCommand.Execute("ajustes");
        ve.BuscarAjuste = "rubor";
        ve.ElegirCommand.Execute(ve.Resultados.Single());
        Check("Ajuste: conteo propone el stock actual", ve.HayElegido && ve.Cantidad == Stock(vRubor).ToString() && ve.Conteo);
        ve.Cantidad = "0";
        ve.Motivo = "Se dañaron en el traslado";
        Check("Vista previa del ajuste (4 en stock: la anulación devolvió 1)", ve.Vista == "4 → 0 (−4)", ve.Vista);
        Check("Registrar ajuste", ve.Ajustar() && Stock(vRubor) == 0 && ve.Ajustes.Count == 3);

        shell.IrA("reportes");
        var vr = (ReportesViewModel)shell.Vista!;
        Check("Reportes del mes: 4 indicadores y gráfico por día", vr.Kpis.Count == 4 && vr.Kpis[2].Titulo == "Ganancia" && vr.Barras.Count == Periodo.DePreset("mes", Formato.Hoy).Dias);
        Check("Total del mes sin anuladas", vr.Kpis[0].Valor == "L 250.00", vr.Kpis[0].Valor);
        dlg.RutaGuardar = Path.Combine(Path.GetTempPath(), $"rep_{Guid.NewGuid():N}.xlsx");
        Check("Exportar a Excel y abrirlo", vr.ExportarExcel() && File.Exists(dlg.RutaGuardar) && dlg.Abiertos.Contains(dlg.RutaGuardar));
        dlg.RutaGuardar = null;
        Check("Cancelar Guardar como no exporta", !vr.ExportarExcel());
        Check("PDF: manda el reporte completo a imprimir", vr.ImprimirPdf() && imp.Reportes.Count == 1);
        vr.Periodo.Elegir("anio");
        Check("Año: barras por mes", vr.Barras.Count == 12 && vr.TituloGrafico == "Ventas por mes");

        shell.IrA("usuarios");
        var vu = (UsuariosViewModel)shell.Vista!;
        Check("Usuarios: formulario nuevo con rol cajera", vu.EsNuevo && vu.Rol?.Clave == "cajera" && vu.Lista.Count >= 5);
        vu.Nombre = "Karla Mejía"; vu.Login = "karla";
        Check("Crear muestra la contraseña temporal", vu.Guardar() && vu.HayClaveTemporal && vu.ClaveTemporal.Length == 12 && vu.Seleccionado?.UsuarioLogin == "karla");
        Check("La temporal sirve para entrar", login.Ingresar("karla", vu.ClaveTemporal).DebeCambiarPassword);
        vu.CerrarTemporalCommand.Execute(null);
        vu.Seleccionado = vu.Lista.First(x => x.UsuarioLogin == "admin");
        Check("Elegirte a ti mismo", vu.EsYo && !vu.EsNuevo);
        vu.Activo = false;
        dlg.Errores.Clear();
        Check("No te desactivas desde la pantalla", !vu.Guardar() && dlg.Errores.Count == 1);

        shell.IrA("roles");
        var vro = (RolesViewModel)shell.Vista!;
        Check("Roles: grupos de permisos (incluye Panel web)", vro.Grupos.Any(g => g.Nombre == "Panel web") && vro.EsNuevo);
        vro.Seleccionado = vro.Lista.Single(r => r.Sistema);
        Check("Administrador: todo marcado y sin editar", vro.EsSistema && vro.Grupos.All(g => g.Permisos.All(p => p.Marcado)) && !vro.GuardarCommand.CanExecute(null));
        vro.Nuevo();
        vro.Nombre = "Vendedora web";
        vro.TodosCommand.Execute(vro.Grupos.Single(g => g.Nombre == "Panel web"));
        Check("Marcar todo un grupo", vro.Marcados == "5 de 17 permisos", vro.Marcados);
        Check("Crear rol desde la pantalla", vro.Guardar() && vro.Seleccionado?.Nombre == "Vendedora web" && vro.Seleccionado.Permisos.Contains(Permiso.WebProductos));

        shell.IrA("ajustes");
        var va = (AjustesViewModel)shell.Vista!;
        Check("Ajustes cargados", va.NegocioNombre == "N&A Beauty Store" && va.Papel58 && va.Intentos == "3" && va.Url == "https://nyabeauty.com" && va.Token == "");
        va.Papel80 = true;
        va.ImprimirAlCobrar = false;
        Check("Guardar negocio desde la pantalla", va.GuardarNegocio() && new AjustesService(ctxAdm).Negocio().AnchoRecibo == 80);
        Check("Respaldar desde la pantalla", va.Respaldar() && va.Respaldos.Count >= 1);
        va.Intentos = "cinco";
        dlg.Errores.Clear();
        Check("Seguridad: número inválido", !va.GuardarSeguridad() && dlg.Errores.Count == 1);
        va.PruebaReciboCommand.Execute(null);
        Check("Recibo de prueba", imp.Recibos[^1].Recibo.Numero == "PRUEBA");

        // Caja: impresión automática al cobrar
        new AjustesService(ctxAdm).GuardarNegocio(new Negocio { Nombre = "N&A Beauty", ImprimirAlCobrar = true });
        shell.IrA("caja");
        var vk = (CajaViewModel)shell.Vista!;
        vk.Texto = "111";
        vk.Enter();
        var antesImp = imp.Recibos.Count;
        Check("Cobrar imprime solo (sin preguntar) si está activado", vk.Cobrar() && imp.Recibos.Count == antesImp + 1 && !imp.Recibos[^1].Pregunto);
        Check("Botón imprimir recibo de la última venta", vk.ImprimirReciboCommand.CanExecute(null) && vk.ImprimirRecibo(preguntar: true) && imp.Recibos[^1].Pregunto);

        // Un rol solo con ajustes ve la sección Entradas (en la pestaña Ajustes)
        rs.Guardar(0, "Inventario", null, [Permiso.VerInicio, Permiso.AjustarInventario]);
        using (var u = bd.Abrir(true)) { new UsuarioRepo(u).Crear("Inv", "inv1", PasswordHasher.Hash("Inv2026xxxx"), "inventario"); u.Confirmar(); }
        var si = new Sesion(bd, new DialogosFalsos());
        si.Iniciar(login.Ingresar("inv1", "Inv2026xxxx"));
        var shI = new ShellViewModel(si);
        shI.IrA("entradas");
        Check("Solo ajustes: ve Entradas en la pestaña Ajustes", shI.Vista is EntradasViewModel { VerAjustes: true, PuedeEntradas: false });

        File.Delete(archivo);
    }

    private static string LibroExcel_Columna(int i) => NABeauty.Data.Exportar.LibroExcel.Columna(i);
}
