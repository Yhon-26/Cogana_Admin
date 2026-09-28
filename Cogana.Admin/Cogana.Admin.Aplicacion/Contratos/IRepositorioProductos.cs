using Cogana.Admin.Dominio.Entidades;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IRepositorioProductos
{
    Task<IReadOnlyList<Producto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<Producto?> ObtenerAsync(Guid productoId, CancellationToken cancellationToken = default);
}
