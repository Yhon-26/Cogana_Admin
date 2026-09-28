using System.Collections.ObjectModel;
using Cogana.Admin.Aplicacion.Modelos;

namespace Cogana.Admin.Escritorio.ViewModels;

/// <summary>Tarjeta del panel de inicio que resume un módulo administrativo.</summary>
public sealed class WidgetInicioViewModel : BaseViewModel
{
    public event Action<WidgetInicioViewModel>? QuitarSolicitado;

    public WidgetInicioViewModel(string nombre, string inicial)
    {
        Nombre = nombre;
        Inicial = inicial;
    }

    public string Nombre { get; }
    public string Inicial { get; }
    public ObservableCollection<FilaModuloDatos> Filas { get; } = [];

    private int _total;
    public int Total
    {
        get => _total;
        private set => Establecer(ref _total, value);
    }

    private bool _estaCargando = true;
    public bool EstaCargando
    {
        get => _estaCargando;
        private set => Establecer(ref _estaCargando, value);
    }

    private string _mensaje = "Consultando Supabase...";
    public string Mensaje
    {
        get => _mensaje;
        private set => Establecer(ref _mensaje, value);
    }

    public bool TieneFilas => Filas.Count > 0;

    public void AplicarDatos(ModuloDatos datos)
    {
        EstaCargando = false;
        Mensaje = string.Empty;
        Filas.Clear();
        foreach (var fila in datos.Filas.Take(5))
        {
            Filas.Add(fila);
        }
        Total = datos.Filas.Count;
        Notificar(nameof(TieneFilas));
    }

    public void IndicarFallo(string mensaje)
    {
        EstaCargando = false;
        Mensaje = mensaje;
        Filas.Clear();
        Notificar(nameof(TieneFilas));
    }

    public void IndicarCarga()
    {
        EstaCargando = true;
        Mensaje = "Consultando Supabase...";
        Filas.Clear();
        Notificar(nameof(TieneFilas));
    }

    public void SolicitarQuitar() => QuitarSolicitado?.Invoke(this);
}
