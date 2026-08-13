using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MahmoudAI.WindowsIntegration.TestHost
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = $"Saved: {NameInput.Text}";
        }
    }
}
