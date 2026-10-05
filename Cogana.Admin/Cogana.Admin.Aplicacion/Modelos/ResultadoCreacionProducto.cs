namespace Cogana.Admin.Aplicacion.Modelos;

/// <summary>Resultado de un alta que expone además el identificador creado.</summary>
public sealed record ResultadoCreacionProducto(
    bool EsExitoso,
    string Mensaje,
    Guid? ProductoId) : ResultadoOperacion(EsExitoso, Mensaje);
