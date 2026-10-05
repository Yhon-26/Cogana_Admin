using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Infraestructura.Configuracion;

namespace Cogana.Admin.Infraestructura.Servicios;

/// <summary>
/// Imágenes de producto sobre el bucket público product-images de Supabase Storage.
/// Ruta de almacenamiento: {tiendaId}/{productoId}.{extensión} — la app móvil la
/// resuelve con la URL pública que expone el catálogo (image_path).
/// Requiere que existan el bucket y sus políticas (migración cogana_v2_storage.sql).
/// </summary>
public sealed class ServicioImagenesProducto(ClienteSupabaseRest cliente, ConfiguracionSupabase configuracion)
    : IServicioImagenesProducto
{
    private const string Bucket = "product-images";

    public async Task<ResultadoOperacion> SubirAsync(
        Guid tiendaId,
        Guid productoId,
        string rutaArchivo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(rutaArchivo))
            {
                return ResultadoOperacion.Fallida("El archivo de imagen ya no está disponible.");
            }

            var extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();
            var tipoContenido = extension switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
            var ruta = $"{tiendaId:D}/{productoId:D}{extension}";

            using var carga = new HttpRequestMessage(
                HttpMethod.Post, $"/storage/v1/object/{Bucket}/{ruta}")
            {
                Content = new ByteArrayContent(await File.ReadAllBytesAsync(rutaArchivo, cancellationToken))
            };
            carga.Content.Headers.ContentType = new MediaTypeHeaderValue(tipoContenido);
            carga.Headers.TryAddWithoutValidation("x-upsert", "true");
            using var respuestaCarga = await cliente.EnviarAsync(carga, cancellationToken);
            if (!respuestaCarga.IsSuccessStatusCode)
            {
                return ResultadoOperacion.Fallida(await LeerErrorAsync(respuestaCarga, cancellationToken));
            }

            // Reemplaza la fila del catálogo de imágenes para este producto
            using var limpieza = new HttpRequestMessage(
                HttpMethod.Delete,
                $"/rest/v1/product_images?store_id=eq.{tiendaId:D}&product_id=eq.{productoId:D}");
            await cliente.EnviarAsync(limpieza, cancellationToken);

            using var fila = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/product_images")
            {
                Content = JsonContent.Create(new
                {
                    store_id = tiendaId,
                    product_id = productoId,
                    storage_path = ruta,
                    sort_order = 1
                })
            };
            fila.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
            using var respuestaFila = await cliente.EnviarAsync(fila, cancellationToken);
            return respuestaFila.IsSuccessStatusCode
                ? ResultadoOperacion.Correcta("Imagen del producto actualizada.")
                : ResultadoOperacion.Fallida(await LeerErrorAsync(respuestaFila, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida("No fue posible subir la imagen a Supabase.");
        }
        catch (IOException)
        {
            return ResultadoOperacion.Fallida("No fue posible leer el archivo de imagen.");
        }
    }

    public async Task<string?> ObtenerRutaAsync(
        Guid tiendaId,
        Guid productoId,
        CancellationToken cancellationToken = default)
    {
        using var respuesta = await cliente.ObtenerAsync(
            $"/rest/v1/product_images?store_id=eq.{tiendaId:D}&product_id=eq.{productoId:D}" +
            "&select=storage_path&order=sort_order.asc,id.asc&limit=1",
            cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        var filas = await respuesta.Content
            .ReadFromJsonAsync<List<RespuestaRuta>>(cancellationToken: cancellationToken);
        return filas?.FirstOrDefault()?.Ruta;
    }

    public string? UrlPublica(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) || configuracion.Url is null)
        {
            return null;
        }

        return $"{configuracion.Url.ToString().TrimEnd('/')}/storage/v1/object/public/{Bucket}/{ruta}";
    }

    private static async Task<string> LeerErrorAsync(
        HttpResponseMessage respuesta,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await respuesta.Content
                .ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: cancellationToken);
            if (error.TryGetProperty("message", out var mensaje))
            {
                return mensaje.GetString() ?? $"Supabase rechazó la operación ({(int)respuesta.StatusCode}).";
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return $"Supabase rechazó la operación ({(int)respuesta.StatusCode}).";
    }

    private sealed class RespuestaRuta
    {
        [System.Text.Json.Serialization.JsonPropertyName("storage_path")]
        public string Ruta { get; init; } = string.Empty;
    }
}
