using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Cogana.Admin.Escritorio.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Establecer<T>(ref T campo, T valor, [CallerMemberName] string? propiedad = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
        {
            return false;
        }

        campo = valor;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
        return true;
    }

    protected void Notificar([CallerMemberName] string? propiedad = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}
