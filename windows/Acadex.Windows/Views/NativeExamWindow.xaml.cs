using System.Windows;
using Acadex.Windows.Services;
using Acadex.Windows.ViewModels;
using Acadex.Windows.Models;

namespace Acadex.Windows.Views;

public partial class NativeExamWindow : Window
{
    private readonly NativeExamViewModel _viewModel;

    public NativeExamWindow(NativeExamService service, string examId, ExamSessionResponse session)
    {
        InitializeComponent();
        _viewModel = new NativeExamViewModel(service, examId, session);
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }
}
