using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioModulosAdministrativos
{
    Task<ResultadoModuloDatos> ObtenerAsync(
        string modulo,
        Guid tiendaId,
        CancellationToken cancellationToken = default);
}
