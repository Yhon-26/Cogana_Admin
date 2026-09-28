using Cogana.Admin.Dominio.Enumeraciones;

namespace Cogana.Admin.Dominio.Entidades;

public sealed class PresentacionProducto
{
    public Guid Id { get; init; }
    public Guid TiendaId { get; init; }
    public Guid ProductoId { get; init; }
    public string Nombre { get; set; } = string.Empty;
    public TipoPresentacion Tipo { get; set; }
    public int CantidadBase { get; set; }
    public int PrecioCentimos { get; set; }
    public bool EstaActiva { get; set; } = true;

    public decimal PrecioEnSoles => PrecioCentimos / 100m;
}
