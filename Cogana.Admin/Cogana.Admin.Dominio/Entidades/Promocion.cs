namespace Cogana.Admin.Dominio.Entidades;

public sealed class Promocion
{
    public Guid Id { get; init; }
    public Guid TiendaId { get; init; }
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
    public bool EstaActiva { get; set; }
}
