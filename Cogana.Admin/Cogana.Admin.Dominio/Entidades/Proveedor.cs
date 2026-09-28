namespace Cogana.Admin.Dominio.Entidades;

public sealed class Proveedor
{
    public Guid Id { get; init; }
    public Guid TiendaId { get; init; }
    public string RazonSocial { get; set; } = string.Empty;
    public string? Documento { get; set; }
    public string? Telefono { get; set; }
    public bool EstaActivo { get; set; } = true;
}
