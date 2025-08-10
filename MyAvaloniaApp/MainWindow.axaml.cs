using Avalonia.Controls;
using MyAvaloniaApp.ViewModels;

namespace MyAvaloniaApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    protected override void OnClosed(System.EventArgs e)
    {
        (DataContext as MainWindowViewModel)?.Dispose();
        base.OnClosed(e);
    }
}