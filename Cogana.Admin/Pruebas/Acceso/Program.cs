using System.Net;
using System.Text.Json;
using Cogana.Admin.Infraestructura.Configuracion;
using Cogana.Admin.Infraestructura.Servicios;
using Cogana.Admin.Escritorio.ViewModels;

var total = 0;
void Comprobar(bool condicion, string caso)
{
    if (!condicion) throw new Exception(caso);
    Console.WriteLine($"Correcto: {caso}");
    total++;
}

const string enlace = "https://proyecto.supabase.co/auth/v1/verify?token=hash-de-prueba&type=recovery";
var manejador = new ServidorSimulado();
using var cliente = new ClienteSupabaseRest(new(new Uri("https://proyecto.supabase.co"), "clave-publica-de-prueba"), manejador);
var servicio = new ServicioAutenticacionSupabase(cliente);

Comprobar(!(await servicio.CambiarContrasenaAsync("NuevaClaveSegura1!")).EsExitoso && manejador.Solicitudes.Count == 0,
    "No permite cambiar sin verificar acceso");
foreach (var invalido in new[] {
    "https://ajeno.test/auth/v1/verify?token=hash&type=recovery",
    "http://proyecto.supabase.co/auth/v1/verify?token=hash&type=recovery",
    "https://proyecto.supabase.co/auth/v1/verify?token=hash&type=signup",
    "https://proyecto.supabase.co/otro?token=hash&type=recovery",
    "https://proyecto.supabase.co/auth/v1/verify?token=hash&type=recovery#access_token=secreto" })
    Comprobar(!(await servicio.VerificarRecuperacionAsync("admin@example.test", invalido)).EsExitoso && manejador.Solicitudes.Count == 0,
        "Rechaza enlace no autorizado sin enviar solicitudes");

Comprobar(!(await servicio.SolicitarRecuperacionAsync("correo-invalido")).EsExitoso, "Valida correo antes de solicitar recuperación");
Comprobar((await servicio.SolicitarRecuperacionAsync("admin@example.test")).EsExitoso, "Solicitud de correo genérica sin enumerar usuarios");
manejador.Expirado = true;
Comprobar(!(await servicio.VerificarRecuperacionAsync("admin@example.test", enlace)).EsExitoso, "Rechaza recuperación vencida");
manejador.Expirado = false;
manejador.MembresiaActiva = false;
Comprobar(!(await servicio.VerificarRecuperacionAsync("admin@example.test", enlace)).EsExitoso, "Rechaza cuenta sin membresía administrativa");
await cliente.ObtenerAsync("/comprobar");
Comprobar(manejador.Solicitudes.Last().Autorizacion is null, "Limpia la sesión de una cuenta sin membresía");
manejador.MembresiaActiva = true;
Comprobar((await servicio.VerificarRecuperacionAsync("admin@example.test", enlace)).EsExitoso, "Verifica enlace contra Auth");
Comprobar(manejador.Solicitudes.Any(s => s.Ruta == "/auth/v1/verify" && s.Cuerpo.Contains("token_hash")), "Intercambia hash mediante POST, sin abrir el enlace");
Comprobar(!(await servicio.CambiarContrasenaAsync("corta")).EsExitoso, "Rechaza contraseña corta");
manejador.FalloCambio = true;
Comprobar(!(await servicio.CambiarContrasenaAsync("NuevaClaveSegura1!")).EsExitoso, "No anuncia éxito cuando Auth rechaza el cambio");
manejador.FalloCambio = false;
Comprobar((await servicio.CambiarContrasenaAsync("NuevaClaveSegura1!")).EsExitoso, "Guarda contraseña y exige nuevo ingreso");
Comprobar(manejador.Solicitudes.Any(s => s.Metodo == "PUT" && s.Ruta == "/auth/v1/user"), "Recuperación normal usa Auth del usuario");
Comprobar(!(await servicio.CambiarContrasenaAsync("NuevaClaveSegura1!")).EsExitoso, "La sesión de recuperación no se reutiliza después del cambio");

manejador.Pendiente = true;
var ingreso = await servicio.IniciarSesionAsync("admin@example.test", "TemporalSegura1!");
Comprobar(ingreso.EsExitoso && ingreso.RequiereCambioContrasena && ingreso.Sesion is null, "El ingreso temporal no entrega una sesión al panel");
var vm = new InicioSesionViewModel(servicio) { Correo = "admin@example.test" };
var panelAbierto = false;
var cambioMostrado = false;
vm.SesionIniciada += _ => panelAbierto = true;
vm.CambioContrasenaRequerido += () => { cambioMostrado = true; return Task.CompletedTask; };
await vm.IniciarSesionAsync("TemporalSegura1!");
Comprobar(cambioMostrado && !panelAbierto, "El ViewModel solicita cambio y bloquea el panel");
Comprobar((await servicio.CambiarContrasenaAsync("NuevaClaveSegura1!")).EsExitoso, "Cambio inicial usa la operación protegida del servidor");
Comprobar(manejador.Solicitudes.Any(s => s.Ruta == "/functions/v1/admin-users" && s.Cuerpo.Contains("change-password")), "Envía contrato de cambio inicial con tienda");
await servicio.IniciarSesionAsync("admin@example.test", "TemporalSegura1!");
servicio.CancelarCambioContrasena();
Comprobar(!(await servicio.CambiarContrasenaAsync("NuevaClaveSegura1!")).EsExitoso, "Cancelar elimina la sesión pendiente");
manejador.Pendiente = false;
manejador.MarcaEnUsuario = true;
ingreso = await servicio.IniciarSesionAsync("admin@example.test", "Segura1!");
Comprobar(ingreso.Sesion is not null && !ingreso.RequiereCambioContrasena, "user_metadata no controla la obligación");
servicio.CancelarCambioContrasena();
Comprobar((await servicio.VerificarRecuperacionAsync("admin@example.test", "123456")).EsExitoso, "Acepta código de recuperación con correo");
Comprobar(manejador.Solicitudes.Any(s => s.Ruta == "/auth/v1/verify" && s.Cuerpo.Contains("123456") && s.Cuerpo.Contains("recovery")), "El código se verifica con tipo recovery");
manejador.ErrorRedMembresia = true;
ingreso = await servicio.IniciarSesionAsync("admin@example.test", "Segura1!");
await cliente.ObtenerAsync("/comprobar");
Comprobar(!ingreso.EsExitoso && manejador.Solicitudes.Last().Autorizacion is null, "Error de red al comprobar permisos elimina tokens");
Console.WriteLine($"{total} comprobaciones correctas; ninguna usa producción ni envía correos.");

sealed class ServidorSimulado : HttpMessageHandler
{
    public List<(string Metodo, string Ruta, string Cuerpo, string? Autorizacion)> Solicitudes { get; } = [];
    public bool Pendiente { get; set; }
    public bool MarcaEnUsuario { get; set; }
    public bool Expirado { get; set; }
    public bool MembresiaActiva { get; set; } = true;
    public bool FalloCambio { get; set; }
    public bool ErrorRedMembresia { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancellationToken)
    {
        var ruta = solicitud.RequestUri!.AbsolutePath;
        var cuerpo = solicitud.Content is null ? "" : await solicitud.Content.ReadAsStringAsync(cancellationToken);
        Solicitudes.Add((solicitud.Method.Method, ruta, cuerpo, solicitud.Headers.Authorization?.Parameter));
        object contenido = new { };
        var estado = HttpStatusCode.OK;
        if (ruta is "/auth/v1/token" or "/auth/v1/verify")
        {
            if (Expirado) estado = HttpStatusCode.Forbidden;
            contenido = new {
                access_token = "token-de-prueba", refresh_token = "renovacion-de-prueba",
                user = new {
                    id = "10000000-0000-0000-0000-000000000001", email = "admin@example.test", is_anonymous = false,
                    app_metadata = new { cogana_admin_cambio_contrasena_pendiente = Pendiente },
                    user_metadata = new { cogana_admin_cambio_contrasena_pendiente = MarcaEnUsuario }
                }
            };
        }
        if (ruta == "/rest/v1/store_memberships")
        {
            if (ErrorRedMembresia) throw new HttpRequestException("Error simulado");
            contenido = MembresiaActiva ? new[] { new { store_id = "20000000-0000-0000-0000-000000000002", role = "admin", is_active = true } } : Array.Empty<object>();
        }
        if ((ruta == "/auth/v1/user" || ruta == "/functions/v1/admin-users") && FalloCambio) estado = HttpStatusCode.BadRequest;
        return new(estado) { Content = new StringContent(JsonSerializer.Serialize(contenido), System.Text.Encoding.UTF8, "application/json") };
    }
}
