using Cogana.Admin.Aplicacion.Modelos;
namespace Cogana.Admin.Aplicacion.Contratos;
public sealed record PaqueteLote(Guid PresentacionId,string Nombre,long Sellados,long CantidadBase)
{ public string Descripcion=>$"{Nombre} · {Sellados:N0} paquetes · {CantidadBase:N0} por paquete"; }
public interface IServicioPaquetesInventario
{
    Task<IReadOnlyList<PaqueteLote>> ObtenerPaquetesAsync(Guid tienda,Guid lote,CancellationToken token=default);
    Task<ResultadoOperacion> AbrirPaquetesAsync(Guid tienda,Guid lote,Guid presentacion,long unidades,string motivo,Guid operacion,CancellationToken token=default);
}
