using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cogana.Admin.Infraestructura.Configuracion;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ClienteSupabaseRest : IDisposable
{
    private readonly ConfiguracionSupabase _configuracion;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _renovando = new(1, 1);
    private readonly object _estado = new();
    private Sesion _sesion = new(null, null, 0);
    private int _activas;
    private bool _retirado, _liberado;
    private sealed record Sesion(string? Acceso, string? Renovacion, long Version);

    public ClienteSupabaseRest(ConfiguracionSupabase configuracion, HttpMessageHandler? manejador = null)
    {
        _configuracion = configuracion;
        _httpClient = manejador is null ? new HttpClient() : new HttpClient(manejador);
        if (configuracion.Url is not null) _httpClient.BaseAddress = configuracion.Url;
        if (!string.IsNullOrWhiteSpace(configuracion.ClavePublica))
            _httpClient.DefaultRequestHeaders.Add("apikey", configuracion.ClavePublica);
    }

    public bool EstaConfigurado => _configuracion.EstaCompleta;
    public Uri? UrlProyecto => _configuracion.Url;

    public void EstablecerTokenAcceso(string tokenAcceso)
    {
        lock (_estado) _sesion = _sesion with { Acceso = tokenAcceso, Version = _sesion.Version + 1 };
    }

    public void EstablecerSesion(string tokenAcceso, string tokenRenovacion)
    {
        lock (_estado) _sesion = new(tokenAcceso,
            string.IsNullOrWhiteSpace(tokenRenovacion) ? null : tokenRenovacion, _sesion.Version + 1);
    }

    public void LimpiarTokenAcceso()
    {
        lock (_estado) _sesion = new(null, null, _sesion.Version + 1);
    }

    public async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage solicitud, CancellationToken cancellationToken = default)
    {
        lock (_estado)
        {
            if (_retirado) throw new OperationCanceledException("La sesión de este cliente ya terminó.");
            _activas++;
        }
        try { return await EnviarInternoAsync(solicitud, cancellationToken); }
        finally
        {
            lock (_estado) _activas--;
            LiberarSiInactivo();
        }
    }

    private async Task<HttpResponseMessage> EnviarInternoAsync(HttpRequestMessage solicitud, CancellationToken cancellationToken)
    {
        Sesion inicio;
        lock (_estado) inicio = _sesion;
        // Conservar los bytes antes de enviar: una imagen nunca se clona como texto.
        using var reintento = await ClonarAsync(solicitud, cancellationToken);
        Autorizar(solicitud, inicio.Acceso);
        var respuesta = await _httpClient.SendAsync(solicitud, cancellationToken);
        if (respuesta.StatusCode != HttpStatusCode.Unauthorized || inicio.Renovacion is null) return respuesta;
        try
        {
            var renovada = await RenovarSesionAsync(inicio, cancellationToken);
            if (renovada is null) return respuesta;
            respuesta.Dispose();
            Autorizar(reintento, renovada.Acceso);
            return await _httpClient.SendAsync(reintento, cancellationToken);
        }
        catch { respuesta.Dispose(); throw; }
    }

    public async Task<HttpResponseMessage> ObtenerAsync(string ruta, CancellationToken cancellationToken = default)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, ruta);
        return await EnviarAsync(solicitud, cancellationToken);
    }

    private static void Autorizar(HttpRequestMessage solicitud, string? token) =>
        solicitud.Headers.Authorization = string.IsNullOrWhiteSpace(token) ? null : new AuthenticationHeaderValue("Bearer", token);

    public async Task<IReadOnlyList<T>> ObtenerListaCompletaAsync<T>(string ruta,CancellationToken token=default)
    {
        const int pagina=200;
        var resultado=new List<T>();
        ruta=System.Text.RegularExpressions.Regex.Replace(ruta,"&limit=[0-9]+",string.Empty);
        ruta=System.Text.RegularExpressions.Regex.Replace(ruta,"order=([^&]+)",m=>m.Value+",id.asc");
        for(var offset=0;;offset+=pagina)
        {
            using var respuesta=await ObtenerAsync(ruta+$"&limit={pagina}&offset={offset}",token);
            respuesta.EnsureSuccessStatusCode();
            var filas=await respuesta.Content.ReadFromJsonAsync<List<T>>(token)??[];
            resultado.AddRange(filas);
            if(filas.Count<pagina)return resultado;
        }
    }

    private async Task<Sesion?> RenovarSesionAsync(Sesion inicio, CancellationToken cancellationToken)
    {
        await _renovando.WaitAsync(cancellationToken);
        try
        {
            Sesion actual;
            lock (_estado) actual = _sesion;
            // No reintentar con otra identidad ni restaurar una sesión cerrada.
            if (actual.Version != inicio.Version) return null;
            if (actual.Acceso != inicio.Acceso) return actual;
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token")
            { Content = JsonContent.Create(new { refresh_token = actual.Renovacion }) };
            using var respuesta = await _httpClient.SendAsync(solicitud, cancellationToken);
            if (!respuesta.IsSuccessStatusCode) return null;
            var datos = await respuesta.Content.ReadFromJsonAsync<RespuestaRenovacion>(cancellationToken);
            if (string.IsNullOrWhiteSpace(datos?.TokenAcceso)) return null;
            lock (_estado)
            {
                if (_sesion.Version != inicio.Version) return null;
                _sesion = new(datos.TokenAcceso, datos.TokenRenovacion ?? actual.Renovacion, actual.Version);
                return _sesion;
            }
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
        finally { _renovando.Release(); }
    }

    private static async Task<HttpRequestMessage> ClonarAsync(HttpRequestMessage original, CancellationToken token)
    {
        var clon = new HttpRequestMessage(original.Method, original.RequestUri)
        { Version = original.Version, VersionPolicy = original.VersionPolicy };
        if (original.Content is not null)
        {
            clon.Content = new ByteArrayContent(await original.Content.ReadAsByteArrayAsync(token));
            foreach (var encabezado in original.Content.Headers)
                clon.Content.Headers.TryAddWithoutValidation(encabezado.Key, encabezado.Value);
        }
        foreach (var encabezado in original.Headers)
            if (!encabezado.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                clon.Headers.TryAddWithoutValidation(encabezado.Key, encabezado.Value);
        foreach (var opcion in original.Options)
            clon.Options.Set(new HttpRequestOptionsKey<object?>(opcion.Key), opcion.Value);
        return clon;
    }

    public void Dispose()
    {
        LimpiarTokenAcceso();
        lock (_estado) _retirado=true;
        LiberarSiInactivo();
    }

    private void LiberarSiInactivo()
    {
        lock (_estado)
        {
            if (!_retirado || _activas!=0 || _liberado) return;
            _liberado=true;
        }
        _httpClient.Dispose();
        _renovando.Dispose();
    }

    private sealed class RespuestaRenovacion
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? TokenAcceso { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? TokenRenovacion { get; init; }
    }
}
