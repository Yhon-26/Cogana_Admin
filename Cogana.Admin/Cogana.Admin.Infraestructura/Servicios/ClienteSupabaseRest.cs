using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Cogana.Admin.Infraestructura.Configuracion;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ClienteSupabaseRest : IDisposable
{
    private readonly ConfiguracionSupabase _configuracion;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _renovando = new(1, 1);
    private string? _tokenRenovacion;

    public ClienteSupabaseRest(ConfiguracionSupabase configuracion, HttpMessageHandler? manejador = null)
    {
        _configuracion = configuracion;
        _httpClient = manejador is null ? new HttpClient() : new HttpClient(manejador);

        if (_configuracion.Url is not null)
        {
            _httpClient.BaseAddress = _configuracion.Url;
        }

        if (!string.IsNullOrWhiteSpace(_configuracion.ClavePublica))
        {
            _httpClient.DefaultRequestHeaders.Add("apikey", _configuracion.ClavePublica);
        }
    }

    public bool EstaConfigurado => _configuracion.EstaCompleta;
    public Uri? UrlProyecto => _configuracion.Url;

    public void EstablecerTokenAcceso(string tokenAcceso)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenAcceso);
    }

    /// <summary>Guarda el par de tokens para poder renovar la sesión al expirar.</summary>
    public void EstablecerSesion(string tokenAcceso, string tokenRenovacion)
    {
        _tokenRenovacion = string.IsNullOrWhiteSpace(tokenRenovacion) ? null : tokenRenovacion;
        EstablecerTokenAcceso(tokenAcceso);
    }

    public void LimpiarTokenAcceso()
    {
        _tokenRenovacion = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<HttpResponseMessage> EnviarAsync(
        HttpRequestMessage solicitud,
        CancellationToken cancellationToken = default)
    {
        var respuesta = await _httpClient.SendAsync(solicitud, cancellationToken);
        if (respuesta.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
            await RenovarSesionAsync(cancellationToken))
        {
            respuesta.Dispose();
            using var reintento = await ClonarAsync(solicitud);
            respuesta = await _httpClient.SendAsync(reintento, cancellationToken);
        }

        return respuesta;
    }

    public async Task<HttpResponseMessage> ObtenerAsync(
        string ruta,
        CancellationToken cancellationToken = default)
    {
        var respuesta = await _httpClient.GetAsync(ruta, cancellationToken);
        if (respuesta.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
            await RenovarSesionAsync(cancellationToken))
        {
            respuesta.Dispose();
            respuesta = await _httpClient.GetAsync(ruta, cancellationToken);
        }

        return respuesta;
    }

    /// <summary>Cambia el token expirado por uno nuevo usando el token de renovación.</summary>
    private async Task<bool> RenovarSesionAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tokenRenovacion))
        {
            return false;
        }

        await _renovando.WaitAsync(cancellationToken);
        try
        {
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token");
            solicitud.Content = JsonContent.Create(new { refresh_token = _tokenRenovacion });
            using var respuesta = await _httpClient.SendAsync(solicitud, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
            {
                return false;
            }

            var sesion = await respuesta.Content
                .ReadFromJsonAsync<RespuestaRenovacion>(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(sesion?.TokenAcceso))
            {
                return false;
            }

            _tokenRenovacion = string.IsNullOrWhiteSpace(sesion.TokenRenovacion)
                ? _tokenRenovacion
                : sesion.TokenRenovacion;
            EstablecerTokenAcceso(sesion.TokenAcceso);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        finally
        {
            _renovando.Release();
        }
    }

    private static async Task<HttpRequestMessage> ClonarAsync(HttpRequestMessage original)
    {
        var clon = new HttpRequestMessage(original.Method, original.RequestUri);
        if (original.Content is not null)
        {
            var cuerpo = await original.Content.ReadAsStringAsync();
            clon.Content = new StringContent(
                cuerpo,
                Encoding.UTF8,
                original.Content.Headers.ContentType?.MediaType ?? "application/json");
        }

        foreach (var encabezado in original.Headers)
        {
            clon.Headers.TryAddWithoutValidation(encabezado.Key, encabezado.Value);
        }

        return clon;
    }

    public void Dispose()
    {
        _renovando.Dispose();
        _httpClient.Dispose();
    }

    private sealed class RespuestaRenovacion
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? TokenAcceso { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? TokenRenovacion { get; init; }
    }
}
