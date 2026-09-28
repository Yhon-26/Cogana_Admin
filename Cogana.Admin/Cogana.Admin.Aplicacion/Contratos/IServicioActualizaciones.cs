using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioActualizaciones
{
    /// <summary>Indica si la app corre instalada como paquete Velopack (no en desarrollo).</summary>
    bool EstaInstaladoComoPaquete { get; }

    /// <summary>Versión en ejecución, mostrada en la interfaz.</summary>
    string VersionActual { get; }

    /// <summary>Devuelve la versión disponible o null si ya se tiene la más reciente.</summary>
    Task<InfoActualizacionDisponible?> BuscarNuevaVersionAsync(CancellationToken cancellationToken = default);

    Task DescargarAsync(
        InfoActualizacionDisponible version,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default);

    /// <summary>Aplica la versión descargada y reinicia la aplicación.</summary>
    void InstalarYReiniciar(InfoActualizacionDisponible version);
}
