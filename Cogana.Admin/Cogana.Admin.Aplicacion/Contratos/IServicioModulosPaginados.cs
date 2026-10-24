using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioModulosPaginados
{
    Task<ResultadoModuloDatos> ObtenerPaginaAsync(string modulo, Guid tienda, int desplazamiento, int cantidad, string busqueda, CancellationToken token = default);
}
