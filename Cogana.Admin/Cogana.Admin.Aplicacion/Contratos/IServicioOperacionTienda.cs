using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public sealed record PagosTienda(string? YapeNombre,string? YapeTelefono,string? PlinNombre,string? PlinTelefono,int MinutosPago);
public sealed record ReglaAtencion(string Modalidad,int MinutosFranja,int Cupo,int Anticipacion,int? Repartidores);
public sealed record OperacionTienda(PagosTienda Pagos,string Estado,string? Mensaje,IReadOnlyList<ReglaAtencion> Reglas);
public interface IServicioOperacionTienda
{
    Task<OperacionTienda> ObtenerAsync(Guid tienda,CancellationToken token=default);
    Task<ResultadoOperacion> GuardarPagosAsync(Guid tienda,PagosTienda pagos,CancellationToken token=default);
    Task<ResultadoOperacion> GuardarEstadoAsync(Guid tienda,string estado,string mensaje,CancellationToken token=default);
    Task<ResultadoOperacion> GuardarReglaAsync(Guid tienda,ReglaAtencion regla,CancellationToken token=default);
}
