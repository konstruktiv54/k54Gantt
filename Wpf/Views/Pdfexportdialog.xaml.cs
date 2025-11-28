// ═══════════════════════════════════════════════════════════════════════════════
// ДИАЛОГ НАСТРОЕК ЭКСПОРТА В PDF - Code-behind
// Файл: Wpf/Views/PdfExportDialog.xaml.cs
// ═══════════════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using Wpf.Services;

namespace Wpf.Views;

public partial class PdfExportDialog : Window
{
    /// <summary>
    /// Настройки экспорта (результат диалога).
    /// </summary>
    public PdfExportSettings? Settings { get; private set; }
    
    /// <summary>
    /// Открывать PDF после создания.
    /// </summary>
    public bool OpenAfterExport { get; private set; }

    public PdfExportDialog(string defaultProjectName = "Диаграмма Ганта")
    {
        InitializeComponent();
        ProjectNameTextBox.Text = defaultProjectName;
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        // Собираем настройки
        Settings = new PdfExportSettings
        {
            ProjectName = string.IsNullOrWhiteSpace(ProjectNameTextBox.Text) 
                ? "Диаграмма Ганта" 
                : ProjectNameTextBox.Text,
            ExportDate = DateTime.Now,
            ShowHeader = ShowHeaderCheckBox.IsChecked ?? true,
            ShowExportDate = ShowDateCheckBox.IsChecked ?? true,
            ShowPageNumbers = ShowPageNumbersCheckBox.IsChecked ?? true,
            Scale = ScaleSlider.Value / 100.0,
            Dpi = GetSelectedDpi(),
            Orientation = GetSelectedOrientation()
        };

        OpenAfterExport = OpenAfterExportCheckBox.IsChecked ?? true;

        DialogResult = true;
        Close();
    }

    private double GetSelectedDpi()
    {
        if (QualityComboBox.SelectedItem is ComboBoxItem item && item.Tag is string dpiStr)
        {
            return double.TryParse(dpiStr, out var dpi) ? dpi : 150;
        }
        return 150;
    }

    private PageOrientation GetSelectedOrientation()
    {
        if (OrientationComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag == "Portrait" ? PageOrientation.Portrait : PageOrientation.Landscape;
        }
        return PageOrientation.Landscape;
    }
}


// ═══════════════════════════════════════════════════════════════════════════════
// МЕТОД ДЛЯ ВЫЗОВА ДИАЛОГА И ЭКСПОРТА
// Добавьте в GanttChartControl или MainWindowViewModel
// ═══════════════════════════════════════════════════════════════════════════════

/*
    /// <summary>
    /// Показывает диалог и экспортирует диаграмму в PDF.
    /// </summary>
    public bool ExportToPdfWithDialog(string? defaultProjectName = null)
    {
        var dialog = new PdfExportDialog(defaultProjectName ?? "Диаграмма Ганта")
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Settings == null)
            return false;

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF документ (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = $"{dialog.Settings.ProjectName}_{DateTime.Now:yyyy-MM-dd}"
        };

        if (saveDialog.ShowDialog() != true)
            return false;

        try
        {
            var exportService = new GanttPdfExportService();
            exportService.ExportToPdfFile(ChartCanvas, HeaderCanvas, saveDialog.FileName, dialog.Settings);

            if (dialog.OpenAfterExport)
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
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }
*/
