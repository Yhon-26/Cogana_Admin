namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record PreferenciasClienteDetalle(
    bool NotificacionesPedido,
    bool NotificacionesPromociones,
    bool ConsentimientoMarketing,
    string SustitucionPredeterminada);
