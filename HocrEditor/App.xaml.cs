using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HocrEditor.GlContexts;
using HocrEditor.GlContexts.Wgl;

namespace HocrEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly GlContext glContext = new WglContext();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            glContext.MakeCurrent();

            Icu.Wrapper.Init();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            Icu.Wrapper.Cleanup();
        }
    }
}
