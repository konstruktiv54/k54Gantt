// GanttChart.WPF/Views/ResourceManagerDialog.xaml.cs

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Core.Services;
using Wpf.ViewModels;
using Wpf.Converters;

namespace Wpf.Views;

/// <summary>
/// Диалог управления ресурсами.
/// </summary>
public partial class ResourceManagerDialog : Window
{
    private ResourceViewModel ViewModel => (ResourceViewModel)DataContext;

    public ResourceManagerDialog(ResourceService resourceService, DateTime projectStart)
    {
        InitializeComponent();
        
        var viewModel = new ResourceViewModel(resourceService)
        {
            ProjectStart = projectStart
        };
        DataContext = viewModel;
        
        // Устанавливаем ProjectStart для статического конвертера
        TimeSpanDateConverter.ProjectStart = projectStart;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Background: SolidColorBrush brush })
        {
            ViewModel.EditColorHex = brush.Color.ToString();
        }
    }

    private void SaveOrAddButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsEditMode)
        {
            ViewModel.SaveResourceCommand.Execute(null);
        }
        else
        {
            ViewModel.AddResourceCommand.Execute(null);
        }
    }
}