namespace Cogana.Admin.Aplicacion.Modelos;

public sealed class ProductoPromocionSeleccion(
    Guid id,
    string nombre,
    bool estaActivo,
    bool estaSeleccionado = false)
{
    public Guid Id { get; } = id;
    public string Nombre { get; } = nombre;
    public bool EstaActivo { get; } = estaActivo;
    public string Estado => EstaActivo ? "Activo" : "Inactivo";
    public bool EstaSeleccionado { get; set; } = estaSeleccionado;
}
