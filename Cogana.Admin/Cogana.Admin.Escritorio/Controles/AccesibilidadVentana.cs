using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Cogana.Admin.Escritorio.Controles;

/// <summary>Respeta el espacio lógico del monitor y la paleta de alto contraste de Windows.</summary>
public static class AccesibilidadVentana
{
    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, object?>> Originales = new();
    public static readonly DependencyProperty ActivaProperty=DependencyProperty.RegisterAttached(
        "Activa",typeof(bool),typeof(AccesibilidadVentana),new PropertyMetadata(false,AlActivar));
    public static bool GetActiva(DependencyObject objeto)=>(bool)objeto.GetValue(ActivaProperty);
    public static void SetActiva(DependencyObject objeto,bool valor)=>objeto.SetValue(ActivaProperty,valor);

    static AccesibilidadVentana()
    {
        EventManager.RegisterClassHandler(typeof(FrameworkElement),FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender,_)=> { if (SystemParameters.HighContrast && sender is DependencyObject objeto &&
                Window.GetWindow(objeto) is { } ventana && GetActiva(ventana)) Pintar(objeto); }));
    }

    private static void AlActivar(DependencyObject objeto,DependencyPropertyChangedEventArgs cambio)
    {
        if (objeto is not Window ventana || !(bool)cambio.NewValue) return;
        void Ajustar()
        {
            var area=SystemParameters.WorkArea;
            ventana.MinWidth=Math.Min(ventana.MinWidth,area.Width);
            ventana.MinHeight=Math.Min(ventana.MinHeight,area.Height);
            if (ventana.WindowState==WindowState.Normal)
            {
                ventana.Width=Math.Min(ventana.Width,area.Width);
                ventana.Height=Math.Min(ventana.Height,area.Height);
            }
            if (SystemParameters.HighContrast) PintarArbol(ventana);
        }
        ventana.SourceInitialized+=(_,_)=>Ajustar();
        ventana.Loaded+=(_,_)=>Ajustar();
        ventana.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
            new SelectionChangedEventHandler((_,_)=> { if(SystemParameters.HighContrast) PintarArbol(ventana); }));
        PropertyChangedEventHandler preferencias=(_,e)=>
        {
            if(e.PropertyName==nameof(SystemParameters.HighContrast))
                ventana.Dispatcher.BeginInvoke(()=>AplicarContraste(ventana,SystemParameters.HighContrast));
        };
        SystemParameters.StaticPropertyChanged+=preferencias;
        ventana.Closed+=(_,_)=>SystemParameters.StaticPropertyChanged-=preferencias;
    }

    private static void Pintar(DependencyObject objeto)
    {
        var seleccionado=false;
        for(var actual=objeto;actual is not null;actual=VisualTreeHelper.GetParent(actual))
            if(actual is ListBoxItem { IsSelected:true } or DataGridRow { IsSelected:true }) {seleccionado=true;break;}
        var fondo=seleccionado?SystemColors.HighlightBrush:SystemColors.WindowBrush;
        var texto=seleccionado?SystemColors.HighlightTextBrush:SystemColors.WindowTextBrush;
        if(objeto is TextBlock) Cambiar(objeto,TextBlock.ForegroundProperty,texto);
        if(objeto is Border){Cambiar(objeto,Border.BackgroundProperty,fondo);Cambiar(objeto,Border.BorderBrushProperty,texto);}
        if(objeto is Panel)Cambiar(objeto,Panel.BackgroundProperty,fondo);
        if(objeto is Control){Cambiar(objeto,Control.ForegroundProperty,texto);Cambiar(objeto,Control.BackgroundProperty,fondo);Cambiar(objeto,Control.BorderBrushProperty,texto);}
        if(objeto is System.Windows.Shapes.Shape forma) {Cambiar(objeto,System.Windows.Shapes.Shape.StrokeProperty,texto);if(forma.Fill is not null)Cambiar(objeto,System.Windows.Shapes.Shape.FillProperty,texto);}
    }

    private static void Cambiar(DependencyObject objeto,DependencyProperty propiedad,object valor)
    {
        var valores=Originales.GetOrCreateValue(objeto);
        if(!valores.ContainsKey(propiedad)) valores[propiedad]=objeto.GetValue(propiedad);
        objeto.SetCurrentValue(propiedad,valor);
    }

    internal static void AplicarContraste(Window ventana,bool activo)
    {
        if(activo) PintarArbol(ventana); else Restaurar(ventana);
    }

    private static void Restaurar(DependencyObject objeto)
    {
        if(Originales.TryGetValue(objeto,out var valores))
        {
            foreach(var valor in valores)objeto.SetCurrentValue(valor.Key,valor.Value);
            Originales.Remove(objeto);
        }
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(objeto);i++)Restaurar(VisualTreeHelper.GetChild(objeto,i));
    }

    private static void PintarArbol(DependencyObject objeto)
    {
        Pintar(objeto);
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(objeto);i++)PintarArbol(VisualTreeHelper.GetChild(objeto,i));
    }
}
