namespace Cogana.Admin.Dominio.Entidades;

public sealed class LoteInventario
{
    public Guid Id { get; init; }
    public Guid TiendaId { get; init; }
    public Guid ProductoId { get; init; }
    public Guid? ProveedorId { get; set; }
    public string CodigoLote { get; set; } = string.Empty;
    public int CantidadDisponible { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public bool EstaBloqueado { get; set; }
}
