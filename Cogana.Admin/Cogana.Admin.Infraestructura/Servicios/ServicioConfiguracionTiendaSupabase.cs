using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Infraestructura.Servicios;

public sealed class ServicioConfiguracionTiendaSupabase(ClienteSupabaseRest cliente)
    : IServicioConfiguracionTienda
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ConfiguracionTiendaDetalle?> ObtenerAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default)
    {
        var tiendaTask = ObtenerListaAsync<RespuestaTienda>(
            "/rest/v1/stores?select=id,name,address,district" +
            $"&id=eq.{tiendaId:D}&limit=1",
            cancellationToken);
        var ajustesTask = ObtenerListaAsync<RespuestaAjustes>(
            "/rest/v1/store_settings?select=*" +
            $"&store_id=eq.{tiendaId:D}&limit=1",
            cancellationToken);
        var horariosTask = ObtenerListaAsync<RespuestaHorario>(
            "/rest/v1/store_business_hours?select=id,weekday,opens_at,closes_at,is_closed" +
            $"&store_id=eq.{tiendaId:D}&order=weekday.asc",
            cancellationToken);
        var excepcionesTask = ObtenerListaAsync<RespuestaExcepcion>(
            "/rest/v1/store_schedule_exceptions?select=id,local_date,opens_at,closes_at,is_closed,public_message" +
            $"&store_id=eq.{tiendaId:D}&order=local_date.asc&limit=100",
            cancellationToken);

        await Task.WhenAll(tiendaTask, ajustesTask, horariosTask, excepcionesTask);
        var tienda = (await tiendaTask).FirstOrDefault();
        if (tienda is null)
        {
            return null;
        }

        var ajustes = (await ajustesTask).FirstOrDefault();
        var horariosGuardados = (await horariosTask)
            .ToDictionary(item => item.DiaSemana);
        var horarios = Enumerable.Range(1, 7)
            .Select(dia => horariosGuardados.TryGetValue(dia, out var horario)
                ? new HorarioAtencionDetalle(
                    dia,
                    LeerHora(horario.Apertura),
                    LeerHora(horario.Cierre),
                    horario.Cerrado)
                : new HorarioAtencionDetalle(dia, new TimeSpan(8, 0, 0), new TimeSpan(20, 0, 0), false))
            .ToList();
        var excepciones = (await excepcionesTask)
            .Where(item => DateOnly.TryParse(item.Fecha, out _))
            .Select(item => new ExcepcionHorarioDetalle(
                item.Id,
                DateOnly.Parse(item.Fecha),
                LeerHora(item.Apertura),
                LeerHora(item.Cierre),
                item.Cerrado,
                item.Mensaje))
            .ToList();

        return new ConfiguracionTiendaDetalle(
            tienda.Id,
            tienda.Nombre,
            tienda.Direccion,
            tienda.Distrito,
            ajustes?.NombreComercial ?? tienda.Nombre,
            ajustes?.RazonSocial,
            ajustes?.Ruc,
            ajustes?.Telefono,
            ajustes?.Whatsapp,
            ajustes?.AceptaRecojo ?? true,
            ajustes?.AceptaDelivery ?? true,
            ajustes?.AceptaEfectivoRecojo ?? true,
            ajustes?.AceptaYape ?? false,
            ajustes?.AceptaPlin ?? false,
            ajustes?.AceptaTarjeta ?? false,
            ajustes?.MinutosPreparacion ?? 30,
            horarios,
            excepciones);
    }

    public async Task<ResultadoOperacion> GuardarAsync(
        Guid tiendaId,
        EdicionConfiguracionTienda edicion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/rpc/save_store_configuration")
            {
                Content = JsonContent.Create(new
                {
                    p_store_id = tiendaId,
                    p_store_name = edicion.NombreTienda.Trim(),
                    p_address = Limpiar(edicion.Direccion),
                    p_district = Limpiar(edicion.Distrito),
                    p_display_name = edicion.NombreComercial.Trim(),
                    p_legal_name = Limpiar(edicion.RazonSocial),
                    p_tax_id = Limpiar(edicion.Ruc),
                    p_phone = Limpiar(edicion.Telefono),
                    p_whatsapp_phone = Limpiar(edicion.Whatsapp),
                    p_accepts_pickup = edicion.AceptaRecojo,
                    p_accepts_delivery = edicion.AceptaDelivery,
                    p_accepts_cash_pickup = edicion.AceptaEfectivoRecojo,
                    p_accepts_yape = edicion.AceptaYape,
                    p_accepts_plin = edicion.AceptaPlin,
                    p_accepts_card = edicion.AceptaTarjeta,
                    p_default_preparation_minutes = edicion.MinutosPreparacion,
                    p_business_hours = edicion.Horarios.Select(horario => new
                    {
                        weekday = horario.DiaSemana,
                        opens_at = FormatearHora(horario.Apertura),
                        closes_at = FormatearHora(horario.Cierre),
                        is_closed = horario.Cerrado
                    }),
                    p_schedule_exceptions = edicion.Excepciones.Select(excepcion => new
                    {
                        local_date = excepcion.Fecha.ToString("yyyy-MM-dd"),
                        opens_at = FormatearHora(excepcion.Apertura),
                        closes_at = FormatearHora(excepcion.Cierre),
                        is_closed = excepcion.Cerrado,
                        public_message = Limpiar(excepcion.Mensaje)
                    }),
                    p_operation_id = edicion.OperacionId
                })
            };
            using var respuesta = await cliente.EnviarAsync(solicitud, cancellationToken);
            return respuesta.IsSuccessStatusCode
                ? ResultadoOperacion.Correcta("Configuración de la tienda actualizada correctamente.")
                : ResultadoOperacion.Fallida(await LeerErrorAsync(respuesta, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return ResultadoOperacion.Fallida("La operación fue cancelada.");
        }
        catch (HttpRequestException)
        {
            return ResultadoOperacion.Fallida("No fue posible guardar la configuración en Supabase.");
        }
    }

    private async Task<List<T>> ObtenerListaAsync<T>(string ruta, CancellationToken cancellationToken)
    {
        using var respuesta = await cliente.ObtenerAsync(ruta, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<List<T>>(OpcionesJson, cancellationToken) ?? [];
    }

    private static TimeSpan? LeerHora(string? valor) =>
        TimeSpan.TryParse(valor, out var hora) ? hora : null;

    private static string? FormatearHora(TimeSpan? valor) =>
        valor?.ToString(@"hh\:mm\:ss");

    private static string? Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static async Task<string> LeerErrorAsync(
        HttpResponseMessage respuesta,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(OpcionesJson, cancellationToken);
            if (!string.IsNullOrWhiteSpace(error?.Mensaje)) return error.Mensaje;
        }
        catch (JsonException)
        {
        }

        return $"Supabase rechazó la operación ({(int)respuesta.StatusCode}).";
    }

    private sealed class RespuestaTienda
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("name")] public string Nombre { get; init; } = string.Empty;
        [JsonPropertyName("address")] public string? Direccion { get; init; }
        [JsonPropertyName("district")] public string? Distrito { get; init; }
    }

    private sealed class RespuestaAjustes
    {
        [JsonPropertyName("display_name")] public string NombreComercial { get; init; } = string.Empty;
        [JsonPropertyName("legal_name")] public string? RazonSocial { get; init; }
        [JsonPropertyName("tax_id")] public string? Ruc { get; init; }
        [JsonPropertyName("phone")] public string? Telefono { get; init; }
        [JsonPropertyName("whatsapp_phone")] public string? Whatsapp { get; init; }
        [JsonPropertyName("accepts_pickup")] public bool AceptaRecojo { get; init; }
        [JsonPropertyName("accepts_delivery")] public bool AceptaDelivery { get; init; }
        [JsonPropertyName("accepts_cash_pickup")] public bool AceptaEfectivoRecojo { get; init; }
        [JsonPropertyName("accepts_yape")] public bool AceptaYape { get; init; }
        [JsonPropertyName("accepts_plin")] public bool AceptaPlin { get; init; }
        [JsonPropertyName("accepts_card")] public bool AceptaTarjeta { get; init; }
        [JsonPropertyName("default_preparation_minutes")] public int MinutosPreparacion { get; init; }
    }

    private sealed class RespuestaHorario
    {
        [JsonPropertyName("weekday")] public int DiaSemana { get; init; }
        [JsonPropertyName("opens_at")] public string? Apertura { get; init; }
        [JsonPropertyName("closes_at")] public string? Cierre { get; init; }
        [JsonPropertyName("is_closed")] public bool Cerrado { get; init; }
    }

    private sealed class RespuestaExcepcion
    {
        [JsonPropertyName("id")] public Guid Id { get; init; }
        [JsonPropertyName("local_date")] public string Fecha { get; init; } = string.Empty;
        [JsonPropertyName("opens_at")] public string? Apertura { get; init; }
        [JsonPropertyName("closes_at")] public string? Cierre { get; init; }
        [JsonPropertyName("is_closed")] public bool Cerrado { get; init; }
        [JsonPropertyName("public_message")] public string? Mensaje { get; init; }
    }

    private sealed class RespuestaError
    {
        [JsonPropertyName("message")] public string? Mensaje { get; init; }
    }
}
