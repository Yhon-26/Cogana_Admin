using System.Windows.Input;

namespace Cogana.Admin.Escritorio.ViewModels;

public sealed class ComandoAsincrono(Func<Task> ejecutar, Func<bool>? puedeEjecutar = null) : ICommand
{
    private bool _estaEjecutando;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_estaEjecutando && (puedeEjecutar?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _estaEjecutando = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            await ejecutar();
        }
        finally
        {
            _estaEjecutando = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
