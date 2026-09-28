namespace Cogana.Admin.Dominio.Entidades;

public sealed class Cliente
{
    public Guid Id { get; init; }
    public Guid TiendaId { get; init; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
}
