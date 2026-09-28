namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record ConfiguracionTiendaDetalle(
    Guid TiendaId,
    string NombreTienda,
    string? Direccion,
    string? Distrito,
    string NombreComercial,
    string? RazonSocial,
    string? Ruc,
    string? Telefono,
    string? Whatsapp,
    bool AceptaRecojo,
    bool AceptaDelivery,
    bool AceptaEfectivoRecojo,
    bool AceptaYape,
    bool AceptaPlin,
    bool AceptaTarjeta,
    int MinutosPreparacion,
    IReadOnlyList<HorarioAtencionDetalle> Horarios,
    IReadOnlyList<ExcepcionHorarioDetalle> Excepciones);
