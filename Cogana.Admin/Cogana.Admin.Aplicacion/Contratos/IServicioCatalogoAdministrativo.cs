using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioCatalogoAdministrativo
{
    Task<IReadOnlyList<OpcionCategoria>> ObtenerCategoriasAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpcionProducto>> ObtenerProductosAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpcionProveedor>> ObtenerProveedoresAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default);

    Task<ProductoDetalle?> ObtenerProductoAsync(
        Guid tiendaId,
        Guid productoId,
        CancellationToken cancellationToken = default);

    Task<CategoriaDetalle?> ObtenerCategoriaAsync(
        Guid tiendaId,
        Guid categoriaId,
        CancellationToken cancellationToken = default);

    Task<PresentacionDetalle?> ObtenerPresentacionAsync(
        Guid tiendaId,
        Guid presentacionId,
        CancellationToken cancellationToken = default);

    Task<ProveedorDetalle?> ObtenerProveedorAsync(
        Guid tiendaId,
        Guid proveedorId,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> CrearProductoAsync(
        Guid tiendaId,
        NuevoProducto producto,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> ActualizarProductoAsync(
        Guid tiendaId,
        Guid productoId,
        NuevoProducto producto,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> ActualizarCategoriaAsync(
        Guid tiendaId,
        Guid categoriaId,
        NuevaCategoria categoria,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> ActualizarPresentacionAsync(
        Guid tiendaId,
        Guid presentacionId,
        NuevaPresentacion presentacion,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> ActualizarProveedorAsync(
        Guid tiendaId,
        Guid proveedorId,
        NuevoProveedor proveedor,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> CrearCategoriaAsync(
        Guid tiendaId,
        NuevaCategoria categoria,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> CrearProveedorAsync(
        Guid tiendaId,
        NuevoProveedor proveedor,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> CrearPresentacionAsync(
        Guid tiendaId,
        NuevaPresentacion presentacion,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> CrearPromocionAsync(
        Guid tiendaId,
        NuevaPromocion promocion,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> RegistrarLoteAsync(
        Guid tiendaId,
        NuevoLote lote,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> CambiarEstadoAsync(
        Guid tiendaId,
        string modulo,
        Guid registroId,
        bool activo,
        CancellationToken cancellationToken = default);
}
