using HeartMonitor.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;

namespace HeartMonitor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetService<MainViewModel>();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ((MainViewModel)DataContext).LoadedCommand.Execute(null);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ((MainViewModel)DataContext).UnloadedCommand.Execute(null);
        }
    }
}
