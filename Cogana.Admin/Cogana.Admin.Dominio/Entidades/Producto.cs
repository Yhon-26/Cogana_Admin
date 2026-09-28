namespace Cogana.Admin.Dominio.Entidades;

public sealed class Producto
{
    public Guid Id { get; init; }
    public Guid TiendaId { get; init; }
    public Guid CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? CodigoInterno { get; set; }
    public bool EstaActivo { get; set; } = true;
    public List<PresentacionProducto> Presentaciones { get; } = [];
}
