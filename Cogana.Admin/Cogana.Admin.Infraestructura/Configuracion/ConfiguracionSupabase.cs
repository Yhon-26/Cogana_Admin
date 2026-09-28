namespace Cogana.Admin.Infraestructura.Configuracion;

public sealed record ConfiguracionSupabase(Uri? Url, string ClavePublica)
{
    public bool EstaCompleta => Url is not null && !string.IsNullOrWhiteSpace(ClavePublica);

    public static ConfiguracionSupabase DesdeVariablesDeEntorno()
    {
        var urlTexto = ObtenerVariable("COGANA_SUPABASE_URL");
        var clave = ObtenerVariable("COGANA_SUPABASE_PUBLIC_KEY") ?? string.Empty;
        var urlValida = Uri.TryCreate(urlTexto, UriKind.Absolute, out var url) ? url : null;

        return new ConfiguracionSupabase(urlValida, clave);
    }

    private static string? ObtenerVariable(string nombre) =>
        Environment.GetEnvironmentVariable(nombre, EnvironmentVariableTarget.Process)
        ?? Environment.GetEnvironmentVariable(nombre, EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable(nombre, EnvironmentVariableTarget.Machine);
}
