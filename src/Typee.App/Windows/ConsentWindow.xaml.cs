using System.Windows;

namespace Typee.App.Windows;

public partial class ConsentWindow : Window
{
    public bool Accepted { get; private set; }

    public ConsentWindow()
    {
        InitializeComponent();
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        Accepted = true;
        DialogResult = true;
        Close();
    }

    private void OnLocalOnly(object sender, RoutedEventArgs e)
    {
        Accepted = false;
        DialogResult = false;
        Close();
    }
}
