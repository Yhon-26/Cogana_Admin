using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Aplicacion.Contratos;

public interface IServicioUsuariosAdministrativos
{
    Task<IReadOnlyList<UsuarioAdministrativoDetalle>> ObtenerUsuariosAsync(
        Guid tiendaId,
        CancellationToken cancellationToken = default);

    Task<UsuarioAdministrativoDetalle?> ObtenerUsuarioAsync(
        Guid tiendaId,
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> InvitarAsync(
        Guid tiendaId,
        InvitacionUsuarioAdministrativo invitacion,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacion> ActualizarAsync(
        Guid tiendaId,
        EdicionUsuarioAdministrativo edicion,
        CancellationToken cancellationToken = default);
}
