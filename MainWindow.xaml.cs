using System.Windows;
using WindowMagnet.ViewModels;
using Microsoft.Win32;
using System.Windows.Media;
using System.Diagnostics;
using WindowMagnet.Helpers;

namespace WindowMagnet
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private const string GitHubUrl = "https://github.com/voidksa/WindowMagnet";

        public MainWindow()
        {
            InitializeComponent();
            ApplyTheme();
            SystemEvents.UserPreferenceChanged += (s, e) => ApplyTheme();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            Loaded += async (s, e) => await UpdateChecker.CheckForUpdatesAsync();
        }

        private void ApplyTheme()
        {
            bool isDark = true; // Default
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val != null)
                        {
                            isDark = (int)val == 0;
                        }
                    }
                }
            }
            catch { }

            if (isDark)
            {
                Resources["AppBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
                Resources["SurfaceColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202020"));
                Resources["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
                Resources["PrimaryText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1E1E1"));
                Resources["SecondaryText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));
                Resources["TitleBarBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
            }
            else
            {
                Resources["AppBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F3"));
                Resources["SurfaceColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                Resources["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1E1E1"));
                Resources["PrimaryText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
                Resources["SecondaryText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
                Resources["TitleBarBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F3"));
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            _viewModel.Dispose();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
