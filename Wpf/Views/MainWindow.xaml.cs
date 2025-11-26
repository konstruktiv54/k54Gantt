using System.ComponentModel;
using System.Windows;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel?.LoadLastOpenedFile();
    }
    
    public void RefreshChart()
    {
        GanttChart.InvalidateChart();
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