using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioAutenticacion
{
    Task<ResultadoInicioSesion> IniciarSesionAsync(
        string correo,
        string contrasena,
        CancellationToken cancellationToken = default);

    Task CerrarSesionAsync(CancellationToken cancellationToken = default);

    Task<ResultadoOperacionAcceso> SolicitarRecuperacionAsync(string correo, CancellationToken cancellationToken = default);
    Task<ResultadoOperacionAcceso> VerificarRecuperacionAsync(string correo, string enlaceOCodigo, CancellationToken cancellationToken = default);
    Task<ResultadoOperacionAcceso> CambiarContrasenaAsync(string contrasena, CancellationToken cancellationToken = default);
    void CancelarCambioContrasena();
}
