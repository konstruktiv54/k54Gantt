using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Wpf.Controls;
using Wpf.ViewModels;

namespace Wpf.Views;

/// <summary>
/// Главное окно приложения.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Загружаем последний файл после инициализации
        Loaded += OnLoaded;
        Closing += OnClosing;
        GanttChart.TaskDragged += OnGanttChartTaskDragged;
    }
    
    /// <summary>
    /// Обработчик изменения выделения в TreeView.
    /// </summary>
    private void TaskTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel vm && e.NewValue is TaskItemViewModel selectedItem)
        {
            vm.SelectedTaskItem = selectedItem;
        }
    }
    
    /// <summary>
    /// Обработчик двойного клика по элементу TreeView.
    /// </summary>
    private void TreeViewItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement element)
        {
            if (element.DataContext is TaskItemViewModel item && item.IsGroup)
            {
                item.IsExpanded = !item.IsExpanded;
                e.Handled = true;
            }
        }
    }
    
    /// <summary>
    /// Обработчик двойного клика по задаче на диаграмме.
    /// </summary>
    private void GanttChart_TaskDoubleClicked(object? sender, Core.Interfaces.Task task)
    {
        if (DataContext is MainViewModel vm && vm.ProjectManager != null)
        {
            // Если это группа — toggle collapse
            if (vm.ProjectManager.IsGroup(task))
            {
                task.IsCollapsed = !task.IsCollapsed;
            
                // Синхронизируем с TaskItemViewModel
                if (vm.RootTasks != null)
                {
                    var taskVm = FindTaskViewModel(vm.RootTasks, task.Id);
                    if (taskVm != null)
                    {
                        taskVm.IsExpanded = !task.IsCollapsed;
                    }
                }
            
                GanttChart.Refresh();
            }
        }
    }
    
    /// <summary>
    /// Обработчик завершения drag-операции на диаграмме.
    /// </summary>
    private void OnGanttChartTaskDragged(object? sender, TaskDragEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Синхронизируем изменения с sidebar
            vm.OnTaskDragged(e);
        }
    }

    /// <summary>
    /// Рекурсивный поиск TaskItemViewModel по ID.
    /// </summary>
    private TaskItemViewModel? FindTaskViewModel(
        System.Collections.ObjectModel.ObservableCollection<TaskItemViewModel> items, 
        Guid taskId)
    {
        foreach (var item in items)
        {
            if (item.Id == taskId)
                return item;
        
            if (item.Children.Count > 0)
            {
                var found = FindTaskViewModel(item.Children, taskId);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel?.LoadLastOpenedFile();
        if (DataContext is MainViewModel vm)
        {
            // Связываем callbacks
            vm.ExportToPdfAction = GanttChart.ExportToPdfWithDialog;
        }
    }
    
    public void RefreshChart()
    {
        GanttChart.InvalidateChart();
    }
    
    /// <summary>
    /// Принудительно перерисовывает диаграмму (полный сброс).
    /// </summary>
    public void ForceRefreshChart()
    {
        GanttChart.ForceFullRedraw();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (ViewModel?.HasUnsavedChanges == true)
        {
            var result = MessageBox.Show(
                "Сохранить изменения перед выходом?",
                "Несохранённые изменения",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                ViewModel.SaveProjectCommand.Execute(null);
            }
        }
    }
}