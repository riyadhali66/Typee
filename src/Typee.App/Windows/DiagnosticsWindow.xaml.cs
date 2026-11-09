using System.Windows;
using Typee.App.ViewModels;

namespace Typee.App.Windows;

public partial class DiagnosticsWindow : Window
{
    private readonly DiagnosticsViewModel _viewModel;

    public DiagnosticsWindow(DiagnosticsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }
}
