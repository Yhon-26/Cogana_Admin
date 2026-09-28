using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioConfiguracionTienda
{
    Task<ConfiguracionTiendaDetalle?> ObtenerAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> GuardarAsync(
        Guid tiendaId,
        EdicionConfiguracionTienda edicion,
        CancellationToken cancellationToken = default);
}
