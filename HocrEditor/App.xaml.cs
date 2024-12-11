using System.Windows;
using Application = System.Windows.Application;

namespace HocrEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Icu.Wrapper.Init();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            Icu.Wrapper.Cleanup();
        }
    }
}
