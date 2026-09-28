namespace Cogana.Admin.Aplicacion.Modelos;

public sealed record CategoriaDetalle(
    Guid Id,
    string Nombre,
    string Identificador,
    int Orden,
    bool EstaActiva);
