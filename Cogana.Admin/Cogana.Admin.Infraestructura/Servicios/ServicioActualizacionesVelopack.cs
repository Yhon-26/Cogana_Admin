using System.Reflection;
using Cogana.Admin.Aplicacion.Contratos;
using Cogana.Admin.Aplicacion.Modelos;
using Velopack;
using Velopack.Sources;

namespace Cogana.Admin.Infraestructura.Servicios;

/// <summary>
/// Actualizaciones mediante Velopack. El canal de actualizaciones es, por defecto,
/// un repositorio de GitHub (publicado con `vpk publish github` desde
/// Publicar-Instalador.ps1, variable COGANA_GITHUB_REPO); si no está configurado,
/// usa el repositorio estático de Supabase Storage (COGANA_ACTUALIZACIONES_URL).
/// </summary>
public sealed class ServicioActualizacionesVelopack : IServicioActualizaciones
{
    public const string UrlPorDefecto =
        "https://mstcxtpjncozqztkawjg.supabase.co/storage/v1/object/public/actualizaciones";

    private const string VariableEntornoUrl = "COGANA_ACTUALIZACIONES_URL";
    private const string VariableEntornoRepositorio = "COGANA_GITHUB_REPO";

    private readonly string? _url;
    private readonly string? _repositorioGithub;
    private UpdateInfo? _ultimaBusqueda;

    public ServicioActualizacionesVelopack()
    {
        _repositorioGithub = Environment.GetEnvironmentVariable(VariableEntornoRepositorio);
        if (string.IsNullOrWhiteSpace(_repositorioGithub))
        {
            _repositorioGithub = null;
            var url = Environment.GetEnvironmentVariable(VariableEntornoUrl);
            _url = string.IsNullOrWhiteSpace(url) ? UrlPorDefecto : url;
        }

        VersionActual = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
    }

    public bool EstaInstaladoComoPaquete => CrearAdministrador().IsInstalled;

    public string VersionActual { get; }

    public async Task<InfoActualizacionDisponible?> BuscarNuevaVersionAsync(
        CancellationToken cancellationToken = default)
    {
        var administrador = CrearAdministrador();
        _ultimaBusqueda = await administrador.CheckForUpdatesAsync();

        if (_ultimaBusqueda is null)
        {
            return null;
        }

        return new InfoActualizacionDisponible(
            _ultimaBusqueda.TargetFullRelease.Version.ToString(),
            administrador.CurrentVersion?.ToString() ?? VersionActual);
    }

    public async Task DescargarAsync(
        InfoActualizacionDisponible version,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        var busqueda = _ultimaBusqueda ??
            throw new InvalidOperationException(
                "Descarga solicitada sin búsqueda previa de actualización.");

        var administrador = CrearAdministrador();
        await administrador.DownloadUpdatesAsync(
            busqueda,
            progreso is null ? null : valor => progreso.Report(valor));
    }

    private UpdateManager CrearAdministrador()
    {
        if (_repositorioGithub is not null)
        {
            var fuente = new GithubSource(
                $"https://github.com/{_repositorioGithub}",
                null,
                false);
            return new UpdateManager(fuente);
        }

        return new UpdateManager(_url!);
    }

    public void InstalarYReiniciar(InfoActualizacionDisponible version)
    {
        var busqueda = _ultimaBusqueda ??
            throw new InvalidOperationException(
                "Instalación solicitada sin búsqueda previa de actualización.");

        var administrador = CrearAdministrador();
        administrador.ApplyUpdatesAndRestart(busqueda);
    }
}
