// ═══════════════════════════════════════════════════════════════════════════════
// ДИАЛОГ ЭКСПОРТА В PDF - Code-behind (v4)
// Файл: Wpf/Views/PdfExportDialog.xaml.cs
// ═══════════════════════════════════════════════════════════════════════════════
//
// Изменения v4:
// - Добавлена обработка радиокнопки FitToPageRadioButton
// - Передача режима FitToSinglePage в настройки
//
// ═══════════════════════════════════════════════════════════════════════════════

using System;
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
        Settings = new PdfExportSettings
        {
            ProjectName = string.IsNullOrWhiteSpace(ProjectNameTextBox.Text) 
                ? "Диаграмма Ганта" 
                : ProjectNameTextBox.Text,
            ExportDate = DateTime.Now,
            ShowHeader = ShowHeaderCheckBox.IsChecked ?? true,
            ShowExportDate = ShowDateCheckBox.IsChecked ?? true,
            ShowPageNumbers = ShowPageNumbersCheckBox.IsChecked ?? true,
            Dpi = GetSelectedDpi(),
            FitToSinglePage = FitToPageRadioButton.IsChecked ?? true  // ← ДОБАВЛЕНО
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
}