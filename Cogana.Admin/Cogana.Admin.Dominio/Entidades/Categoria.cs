namespace Cogana.Admin.Dominio.Entidades;

public sealed class Categoria
{
    public Guid Id { get; init; }
    public Guid TiendaId { get; init; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EstaActiva { get; set; } = true;
}
