using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Cogana.Admin.Escritorio.Controles;
using Cogana.Admin.Escritorio.Vistas;
using Cogana.Admin.Infraestructura.Servicios;

internal static class PruebasMarco
{
    public static void Probar(Action<bool,string> verificar,bool visual=false)
    {
        var contenido=new Canvas{Width=1800,Height=2400,Background=Brushes.White};
        contenido.Children.Add(new TextBlock{Text="Prueba aislada de desplazamiento",FontSize=24,Margin=new Thickness(24)});
        var scroll=new ScrollViewer{Content=contenido,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
        var ventana=new Window{Title="Prueba aislada · Marco Cogana",Width=680,Height=700,Content=scroll,Background=new SolidColorBrush(Color.FromRgb(247,247,245)),WindowStartupLocation=WindowStartupLocation.CenterScreen};
        ventana.Show();Esperar();ventana.UpdateLayout();
        verificar(WindowChrome.GetWindowChrome(ventana)?.CaptionHeight==40&&MarcoVentana.GetActivo(ventana),"La cabecera común conserva las zonas nativas de arrastre y redimensionamiento");
        var cabecera=(Grid)ventana.Template.FindName("CabeceraVentana",ventana);
        verificar(cabecera.Background==ventana.Background,"La cabecera adopta el fondo de la ventana sin una franja blanca separada");
        var barras=Descendientes(scroll).OfType<ScrollBar>().Where(b=>b.IsVisible).ToList();
        verificar(barras.Count==2&&barras.All(b=>b.Template.FindName("PART_Track",b) is Track),"Las barras vertical y horizontal mantienen su Track funcional");
        var vertical=barras.Single(b=>b.Orientation==Orientation.Vertical);
        var horizontal=barras.Single(b=>b.Orientation==Orientation.Horizontal);
        verificar(Descendientes(vertical).OfType<Border>().Any(b=>b.Name=="Pulgar"&&b.ActualWidth<=6)&&Descendientes(horizontal).OfType<Border>().Any(b=>b.Name=="Pulgar"&&b.ActualHeight<=6),"El desplazamiento usa carriles finos en ambas orientaciones");
        scroll.PageDown();Esperar();verificar(scroll.VerticalOffset>0,"El desplazamiento por página sigue funcionando");
        scroll.ScrollToTop();Esperar();
        var track=(Track)vertical.Template.FindName("PART_Track",vertical);
        track.Thumb.RaiseEvent(new DragDeltaEventArgs(0,40));Esperar();
        verificar(scroll.VerticalOffset>0,"Arrastrar el pulgar vertical mueve el contenido");
        ((Track)horizontal.Template.FindName("PART_Track",horizontal)).Thumb.RaiseEvent(new DragDeltaEventArgs(40,0));Esperar();
        verificar(scroll.HorizontalOffset>0,"Arrastrar el pulgar horizontal mueve el contenido");
        Invocar(ventana,"AlternarVentana");Esperar();
        verificar(ventana.WindowState==WindowState.Maximized,"El botón integrado maximiza la ventana");
        verificar(ventana.ActualHeight<=SystemParameters.WorkArea.Height+20,"Maximizar respeta el área de trabajo y la barra de tareas");
        Invocar(ventana,"AlternarVentana");Esperar();
        verificar(ventana.WindowState==WindowState.Normal&&Math.Abs(ventana.Width-680)<2,"Restaurar recupera el tamaño anterior");
        Invocar(ventana,"MinimizarVentana");Esperar();
        verificar(ventana.WindowState==WindowState.Minimized,"El botón integrado minimiza la ventana");
        SystemCommands.RestoreWindow(ventana);Esperar();
        var cerrada=false;ventana.Closed+=(_,_)=>cerrada=true;Invocar(ventana,"CerrarVentana");Esperar();
        verificar(cerrada,"Cerrar usa el ciclo normal de Window y conserva sus eventos");
        var fija=new Window{Width=400,Height=300,ResizeMode=ResizeMode.NoResize,Title="Ventana fija"};fija.Show();Esperar();
        verificar(!((Button)fija.Template.FindName("AlternarVentana",fija)).IsEnabled&&!((Button)fija.Template.FindName("MinimizarVentana",fija)).IsEnabled,"Las ventanas fijas no habilitan comandos de tamaño incompatibles");fija.Close();
        using var cliente=new ClienteSupabaseRest(new(new Uri("https://prueba.example.test"),"publica-prueba"),new CatalogoSimulado());
        var formulario=new VentanaNuevoRegistro(new ServicioCatalogoAdministrativoSupabase(cliente),Guid.NewGuid(),"Productos");
        formulario.Show();Esperar();formulario.UpdateLayout();
        var raiz=(Grid)formulario.Content;var superficie=(Border)raiz.Children[0];
        verificar(raiz.Margin==new Thickness(0)&&superficie.BorderThickness==new Thickness(0)&&superficie.CornerRadius==new CornerRadius(0),"Nuevo producto usa una sola superficie sin el contorno exterior duplicado");
        verificar(formulario.Background==Application.Current.Resources["BrochaSuperficie"],"El formulario y su marco comparten el color de superficie");
        var guardar=(Button)formulario.FindName("BotonGuardar");
        verificar(guardar.IsVisible&&guardar.TransformToAncestor(formulario).Transform(new Point()).Y+guardar.ActualHeight<formulario.ActualHeight,"Guardar permanece visible con la cabecera integrada");
        Capturar(formulario,"marco-nuevo-producto.png");
        var desplazamiento=Descendientes(formulario).OfType<ScrollViewer>().First(s=>s.IsVisible&&s.ScrollableHeight>0);
        desplazamiento.ScrollToBottom();Esperar();Capturar(formulario,"marco-formulario-final.png");
        verificar(desplazamiento.VerticalOffset>0,"El formulario permite llegar a los campos inferiores con las barras comunes");
        if(visual){formulario.Closed+=(_,_)=>Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);Dispatcher.Run();}
        else formulario.Close();
    }
    private static void Invocar(Window ventana,string nombre)=>((IInvokeProvider)new ButtonAutomationPeer((Button)ventana.Template.FindName(nombre,ventana)).GetPattern(PatternInterface.Invoke)).Invoke();
    private sealed class CatalogoSimulado:System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage solicitud,CancellationToken token)
            =>Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.StringContent("[]",System.Text.Encoding.UTF8,"application/json")});
    }
    private static void Esperar(){var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};t.Tick+=(_,_)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
    private static IEnumerable<DependencyObject> Descendientes(DependencyObject raiz)
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(raiz);i++){var hijo=VisualTreeHelper.GetChild(raiz,i);yield return hijo;foreach(var nieto in Descendientes(hijo))yield return nieto;}}
    private static void Capturar(Window ventana,string nombre)
    {var bitmap=new RenderTargetBitmap((int)ventana.ActualWidth,(int)ventana.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(ventana);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var archivo=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,nombre));png.Save(archivo);}
}
