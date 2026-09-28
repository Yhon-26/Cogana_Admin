using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioAutenticacion
{
    Task<ResultadoInicioSesion> IniciarSesionAsync(
        string correo,
        string contrasena,
        CancellationToken cancellationToken = default);

    Task CerrarSesionAsync(CancellationToken cancellationToken = default);
}
