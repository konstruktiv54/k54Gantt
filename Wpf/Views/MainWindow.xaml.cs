using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Wpf.Controls;
using Wpf.Services.Export;
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
            vm.ExportToPdfAction = ExportDocument;
            vm.EditNoteAction = GanttChart.EditNote;
        }
        
        // Синхронизация скролла
        GanttChart.HorizontalScrollChanged += (_, offset) =>
        {
            EngagementStrip.HorizontalOffset = offset;
        };
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

    #region Document Export

    /// <summary>
    /// Экспортирует полный документ (GanttChart + EngagementStrip) в XPS.
    /// </summary>
    /// <param name="projectName">Имя проекта для имени файла по умолчанию.</param>
    /// <returns>true если экспорт успешен.</returns>
    public bool ExportDocument(string? projectName)
    {
        // 1. Диалог сохранения
        var saveDialog = new SaveFileDialog
        {
            Filter = "XPS документ (*.xps)|*.xps",
            DefaultExt = ".xps",
            FileName = $"{projectName ?? "Диаграмма Ганта"}_{DateTime.Now:yyyy-MM-dd}",
            Title = "Экспорт в XPS"
        };

        if (saveDialog.ShowDialog() != true)
            return false;

        try
        {
            // 2. Собираем данные от GanttChart
            var ganttData = GanttChart.GetExportData();
            if (ganttData == null)
            {
                MessageBox.Show(
                    "Нет данных диаграммы для экспорта.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // 3. Собираем данные от EngagementStrip
            var engagementData = EngagementStrip.GetExportDataForced();

            // 4. Синхронизируем ширину (если EngagementStrip уже, расширяем)
            if (engagementData != null)
            {
                SynchronizeExportWidth(ganttData, engagementData);
            }

            // 5. Создаём документ
            var documentData = new DocumentExportData
            {
                GanttChart = ganttData,
                EngagementStrip = engagementData,
                SectionGap = 10
            };

            // 6. Экспортируем
            DocumentExportService.ExportToXps(documentData, saveDialog.FileName);

            // 7. Предлагаем открыть файл
            var result = MessageBox.Show(
                "XPS документ успешно сохранён. Открыть файл?",
                "Экспорт завершён",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = saveDialog.FileName,
                    UseShellExecute = true
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка при экспорте:\n{ex.Message}",
                "Ошибка экспорта",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>
    /// Синхронизирует ширину GanttChart и EngagementStrip для экспорта.
    /// </summary>
    private void SynchronizeExportWidth(GanttChartExportData ganttData, EngagementStripExportData engagementData)
    {
        // Используем максимальную ширину из обоих компонентов
        var maxWidth = Math.Max(ganttData.TotalWidth, engagementData.Engagement.Width);

        // Обновляем ширину в данных (если нужно)
        // Примечание: Это не изменяет исходные Canvas, только метаданные для экспорта
        if (ganttData.TotalWidth < maxWidth)
        {
            // GanttChart уже, нужно будет расширить при клонировании
            // Но текущая реализация CloneCanvasAsVisual использует ActualWidth источника
            // Поэтому просто логируем
            System.Diagnostics.Debug.WriteLine(
                $"Export: GanttChart width ({ganttData.TotalWidth}) < EngagementStrip ({engagementData.Engagement.Width})");
        }
    }

    #endregion
}