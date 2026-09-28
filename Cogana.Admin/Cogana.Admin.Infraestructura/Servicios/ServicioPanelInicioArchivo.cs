using System.IO;
using System.Text.Json;
using Cogana.Admin.Aplicacion.Contratos;

namespace Cogana.Admin.Infraestructura.Servicios;

/// <summary>
/// Guarda la disposición del panel de inicio en un archivo JSON local del equipo.
/// No usa la base de datos: es una preferencia de interfaz de este escritorio.
/// </summary>
public sealed class ServicioPanelInicioArchivo : IServicioPanelInicio
{
    private static readonly string RutaArchivo = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CoganaAdmin",
        "panel_inicio.json");

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = true
    };

    public IReadOnlyList<string> Cargar()
    {
        try
        {
            if (!File.Exists(RutaArchivo))
            {
                return [];
            }

            var nombres = JsonSerializer.Deserialize<List<string>>(
                File.ReadAllText(RutaArchivo));

            return nombres is null ? [] : nombres.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public void Guardar(IReadOnlyList<string> nombresModulos)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(nombresModulos, OpcionesJson));
        }
        catch (Exception)
        {
            // Si el disco no permite guardar, el panel simplemente no persiste la disposición.
        }
    }
}
