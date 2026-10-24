using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Cogana.Admin.Infraestructura.Configuracion;

namespace Cogana.Admin.Infraestructura.Servicios;

/// <summary>
/// Imágenes de producto sobre el bucket público product-images de Supabase Storage.
/// Ruta versionada dentro de {tiendaId}; la app móvil la
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
            if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
                return ResultadoOperacion.Fallida("Utiliza una imagen JPG, PNG o WebP.");
            if (new FileInfo(rutaArchivo).Length > 5 * 1024 * 1024)
                return ResultadoOperacion.Fallida("La imagen no puede superar 5 MiB.");

            using var consulta = await cliente.ObtenerAsync(
                $"/rest/v1/product_images?store_id=eq.{tiendaId:D}&product_id=eq.{productoId:D}" +
                "&select=id,storage_path,sort_order&order=sort_order.asc,id.asc&limit=1", cancellationToken);
            consulta.EnsureSuccessStatusCode();
            var anterior = (await consulta.Content.ReadFromJsonAsync<List<RespuestaRuta>>(cancellationToken))?.FirstOrDefault();
            var tipoContenido = extension switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
            var ruta = $"{tiendaId:D}/{productoId:D}-{Guid.NewGuid():N}{extension}";

            using var carga = new HttpRequestMessage(
                HttpMethod.Post, $"/storage/v1/object/{Bucket}/{ruta}")
            {
                Content = new ByteArrayContent(await File.ReadAllBytesAsync(rutaArchivo, cancellationToken))
            };
            carga.Content.Headers.ContentType = new MediaTypeHeaderValue(tipoContenido);
            using var respuestaCarga = await cliente.EnviarAsync(carga, cancellationToken);
            if (!respuestaCarga.IsSuccessStatusCode)
            {
                return ResultadoOperacion.Fallida(await LeerErrorAsync(respuestaCarga, cancellationToken));
            }

            // Una sola escritura de metadatos. La foto anterior sigue vigente hasta confirmarla.
            var id = anterior?.Id ?? productoId;
            var destino = anterior is null ? "/rest/v1/product_images" :
                $"/rest/v1/product_images?id=eq.{id:D}&store_id=eq.{tiendaId:D}&product_id=eq.{productoId:D}" +
                $"&storage_path=eq.{Uri.EscapeDataString(anterior.Ruta ?? string.Empty)}";
            using var fila = new HttpRequestMessage(anterior is null ? HttpMethod.Post : HttpMethod.Patch, destino)
            {
                Content = JsonContent.Create(new
                {
                    id,
                    store_id = tiendaId,
                    product_id = productoId,
                    storage_path = ruta,
                    sort_order = anterior?.Orden ?? 0
                })
            };
            fila.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            using var respuestaFila = await cliente.EnviarAsync(fila, cancellationToken);
            if (!respuestaFila.IsSuccessStatusCode)
            {
                await IntentarLimpiarObjetoAsync(ruta);
                return ResultadoOperacion.Fallida(await LeerErrorAsync(respuestaFila, cancellationToken));
            }
            var guardadas = await respuestaFila.Content.ReadFromJsonAsync<List<RespuestaRuta>>(cancellationToken);
            if (guardadas?.Any(x => x.Id == id && x.Ruta == ruta) != true)
            {
                await IntentarLimpiarObjetoAsync(ruta);
                return ResultadoOperacion.Fallida("La imagen cambió en otra sesión. Vuelve a abrir el producto antes de reintentar.");
            }
            if (!string.IsNullOrWhiteSpace(anterior?.Ruta)) await IntentarLimpiarObjetoAsync(anterior.Ruta);
            return ResultadoOperacion.Correcta("Imagen del producto actualizada.");
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
        catch (UnauthorizedAccessException)
        {
            return ResultadoOperacion.Fallida("Windows no permitió leer la imagen. Selecciona otro archivo.");
        }
        catch (JsonException)
        {
            return ResultadoOperacion.Fallida("No se pudo confirmar la imagen. Reabre el producto para comprobar qué foto quedó guardada.");
        }
    }

    private async Task IntentarLimpiarObjetoAsync(string ruta)
    {
        // No borrar archivos todavía referenciados, ni convertir una limpieza pendiente en fallo del guardado.
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            using var referencias = await cliente.ObtenerAsync(
                $"/rest/v1/product_images?storage_path=eq.{Uri.EscapeDataString(ruta)}&select=id&limit=1", limite.Token);
            if (!referencias.IsSuccessStatusCode) return;
            var filas = await referencias.Content.ReadFromJsonAsync<JsonElement>(limite.Token);
            if (filas.ValueKind != JsonValueKind.Array || filas.GetArrayLength() != 0) return;
            using var borrado = new HttpRequestMessage(HttpMethod.Delete, $"/storage/v1/object/{Bucket}")
                { Content = JsonContent.Create(new { prefixes = new[] { ruta } }) };
            using var resultado = await cliente.EnviarAsync(borrado, limite.Token);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException)
        { System.Diagnostics.Trace.TraceWarning("Limpieza de imagen pendiente ({0}).", error.GetType().Name); }
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
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public Guid Id { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("sort_order")]
        public int Orden { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("storage_path")]
        public string Ruta { get; init; } = string.Empty;
    }
}
