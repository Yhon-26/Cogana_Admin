namespace Cogana.Admin.Aplicacion.Contratos;

/// <summary>Secciones (widgets de módulos) que el usuario colocó en el panel de inicio.</summary>
public interface IServicioPanelInicio
{
    /// <summary>Nombres de módulos guardados, en su orden. Vacío si nunca se guardó.</summary>
    IReadOnlyList<string> Cargar();

    void Guardar(IReadOnlyList<string> nombresModulos);
}
