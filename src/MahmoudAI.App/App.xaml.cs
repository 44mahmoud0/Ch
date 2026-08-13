using Microsoft.UI.Xaml;

using Microsoft.UI.Dispatching;

namespace MahmoudAI.App
{
    public partial class App : Application
    {
        private Window? m_window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            AppHost.Initialize(DispatcherQueue.GetForCurrentThread());
            m_window = AppHost.GetRequiredService<MainWindow>();
            m_window.Activate();
        }
    }
}
