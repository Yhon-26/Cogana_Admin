using System.Net.Http.Headers;
using Cogana.Admin.Infraestructura.Configuracion;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ClienteSupabaseRest : IDisposable
{
    private readonly ConfiguracionSupabase _configuracion;
    private readonly HttpClient _httpClient;

    public ClienteSupabaseRest(ConfiguracionSupabase configuracion)
    {
        _configuracion = configuracion;
        _httpClient = new HttpClient();

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

    public void EstablecerTokenAcceso(string tokenAcceso)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenAcceso);
    }

    public void LimpiarTokenAcceso()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public Task<HttpResponseMessage> EnviarAsync(
        HttpRequestMessage solicitud,
        CancellationToken cancellationToken = default) =>
        _httpClient.SendAsync(solicitud, cancellationToken);

    public Task<HttpResponseMessage> ObtenerAsync(
        string ruta,
        CancellationToken cancellationToken = default) =>
        _httpClient.GetAsync(ruta, cancellationToken);

    public void Dispose() => _httpClient.Dispose();
}
