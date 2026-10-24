using System.IO;
using System.Text.Json;

namespace Cogana.Admin.Escritorio.Servicios;

/// <summary>
/// Marca local (por equipo) de que la introducción animada ya se mostró.
/// Es una preferencia de esta instalación: no vive en la base de datos.
/// Acepta una ruta alternativa para que las pruebas no toquen el archivo real.
/// </summary>
public sealed class EstadoPrimerIngreso
{
    private readonly string _ruta;

    public EstadoPrimerIngreso(string? ruta = null)
    {
        _ruta = ruta ?? RutaPredeterminada();
    }

    public static string RutaPredeterminada() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CoganaAdmin",
        "onboarding.json");

    private sealed class Datos
    {
        public bool Visto { get; init; }
        public string? Fecha { get; init; }
        public string? Version { get; init; }
    }

    public bool Visto { get; private set; }

    public void Cargar()
    {
        try
        {
            if (!File.Exists(_ruta))
            {
                Visto = false;
                return;
            }

            Visto = JsonSerializer.Deserialize<Datos>(File.ReadAllText(_ruta))?.Visto ?? false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Un archivo ilegible no debe castigar al usuario con la introducción otra vez... ni tampoco
            // privarlo de ella: se asume pendiente y se intenta sobrescribir al completar.
            Visto = false;
        }
    }

    public void MarcarVisto(string version)
    {
        Visto = true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_ruta)!);
            File.WriteAllText(_ruta, JsonSerializer.Serialize(new Datos
            {
                Visto = true,
                Fecha = DateTimeOffset.Now.ToString("O"),
                Version = version
            }));
            Visto = true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Se permite continuar en esta sesión aunque la preferencia no persista.
        }
    }
}
