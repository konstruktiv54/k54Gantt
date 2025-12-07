using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Core.Services;
using Wpf.ViewModels;

namespace Wpf.Views;

/// <summary>
/// Диалог управления ресурсами и производственным календарём.
/// </summary>
public partial class ResourceManagerDialog : Window
{
    private ResourceManagerDialogViewModel ViewModel => (ResourceManagerDialogViewModel)DataContext;

    public ResourceManagerDialog(
        ResourceService resourceService, 
        ProductionCalendarService calendarService,
        ProjectManager projectManager)
    {
        InitializeComponent();
        var viewModel = new ResourceManagerDialogViewModel(resourceService, calendarService, projectManager);
        DataContext = viewModel;
    
        // УДАЛЕНО: TimeSpanDateConverter.ProjectStart = projectManager.Start;
        // ProjectStart теперь передаётся через binding в MultiBinding конвертерах
    }

    /// <summary>
    /// Конструктор для обратной совместимости (без ProductionCalendarService).
    /// </summary>
    public ResourceManagerDialog(ResourceService resourceService, ProjectManager projectManager)
        : this(resourceService, new ProductionCalendarService(), projectManager)
    {
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
            ViewModel.ResourceViewModel.EditColorHex = brush.Color.ToString();
        }
    }

    private void SaveOrAddButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ResourceViewModel.IsEditMode)
        {
            ViewModel.ResourceViewModel.SaveResourceCommand.Execute(null);
        }
        else
        {
            ViewModel.ResourceViewModel.AddResourceCommand.Execute(null);
        }
    }
}

/// <summary>
/// Составной ViewModel для диалога управления ресурсами и календарём.
/// </summary>
public class ResourceManagerDialogViewModel
{
    /// <summary>
    /// ViewModel для управления ресурсами.
    /// </summary>
    public ResourceViewModel ResourceViewModel { get; }

    /// <summary>
    /// ViewModel для управления праздниками.
    /// </summary>
    public HolidayViewModel HolidayViewModel { get; }

    public ResourceManagerDialogViewModel(
        ResourceService resourceService,
        ProductionCalendarService calendarService,
        ProjectManager projectManager)
    {
        ResourceViewModel = new ResourceViewModel(resourceService)
        {
            ProjectStart = projectManager.Start
        };

        HolidayViewModel = new HolidayViewModel(calendarService, projectManager);
    }
}