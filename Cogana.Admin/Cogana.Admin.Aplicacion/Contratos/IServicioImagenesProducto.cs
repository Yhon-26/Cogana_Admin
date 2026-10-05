using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

/// <summary>Imágenes de producto en el bucket product-images de Supabase Storage.</summary>
public interface IServicioImagenesProducto
{
    /// <summary>Sube la imagen y la deja asignada al producto (reemplaza la anterior).</summary>
    Task<ResultadoOperacion> SubirAsync(
        Guid tiendaId,
        Guid productoId,
        string rutaArchivo,
        CancellationToken cancellationToken = default);

    /// <summary>Ruta de almacenamiento de la imagen actual del producto, o null si no tiene.</summary>
    Task<string?> ObtenerRutaAsync(
        Guid tiendaId,
        Guid productoId,
        CancellationToken cancellationToken = default);

    /// <summary>URL pública de una ruta del bucket product-images.</summary>
    string? UrlPublica(string? ruta);
}
