namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record AjusteInventario(
    Guid OperacionId,
    long CantidadContada,
    string Motivo);
