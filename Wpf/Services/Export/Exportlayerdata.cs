using System.Windows.Controls;

namespace Wpf.Services.Export;

/// <summary>
/// Данные одного слоя для экспорта.
/// Содержит Canvas и метаданные о его позиционировании.
/// </summary>
public class ExportLayerData
{
    /// <summary>
    /// Исходный Canvas для экспорта.
    /// </summary>
    public required Canvas Canvas { get; init; }
    
    /// <summary>
    /// Ширина содержимого в пикселях.
    /// </summary>
    public double Width { get; init; }
    
    /// <summary>
    /// Высота содержимого в пикселях.
    /// </summary>
    public double Height { get; init; }
    
    /// <summary>
    /// Название слоя (для отладки).
    /// </summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Данные GanttChart для экспорта.
/// </summary>
public class GanttChartExportData
{
    /// <summary>
    /// Слой заголовка (месяцы + дни).
    /// </summary>
    public required ExportLayerData Header { get; init; }
    
    /// <summary>
    /// Слой сетки (вертикальные линии, выходные).
    /// </summary>
    public required ExportLayerData Grid { get; init; }
    
    /// <summary>
    /// Слой задач (бары, имена, прогресс).
    /// </summary>
    public required ExportLayerData Tasks { get; init; }
    
    /// <summary>
    /// Слой линии "Сегодня".
    /// </summary>
    public required ExportLayerData TodayLine { get; init; }
    
    /// <summary>
    /// Ширина одной колонки (день) в пикселях.
    /// </summary>
    public double ColumnWidth { get; init; }
    
    /// <summary>
    /// Начальный день диаграммы (в днях от старта проекта).
    /// </summary>
    public double ChartStartDay { get; init; }
    
    /// <summary>
    /// Общая ширина контента.
    /// </summary>
    public double TotalWidth { get; init; }
    
    /// <summary>
    /// Общая высота контента (Header + Chart).
    /// </summary>
    public double TotalHeight => Header.Height + Math.Max(Grid.Height, Tasks.Height);
}

/// <summary>
/// Данные ResourceEngagementStrip для экспорта.
/// </summary>
public class EngagementStripExportData
{
    /// <summary>
    /// Canvas с ячейками загрузки.
    /// </summary>
    public required ExportLayerData Engagement { get; init; }
    
    /// <summary>
    /// Canvas с именами ресурсов (колонка слева).
    /// </summary>
    public ExportLayerData? ResourceNames { get; init; }
    
    /// <summary>
    /// Ширина одной колонки (день) в пикселях.
    /// Должна совпадать с GanttChart.ColumnWidth.
    /// </summary>
    public double ColumnWidth { get; init; }
    
    /// <summary>
    /// Высота одной строки ресурса.
    /// </summary>
    public double RowHeight { get; init; }
    
    /// <summary>
    /// Количество ресурсов.
    /// </summary>
    public int ResourceCount { get; init; }
    
    /// <summary>
    /// Ширина колонки с именами ресурсов.
    /// </summary>
    public double ResourceNamesWidth => ResourceNames?.Width ?? 0;
}

/// <summary>
/// Полные данные документа для экспорта.
/// </summary>
public class DocumentExportData
{
    public double TimelineOffsetX => EngagementStrip?.ResourceNamesWidth ?? 0;
    
    /// <summary>
    /// Данные диаграммы Ганта.
    /// </summary>
    public required GanttChartExportData GanttChart { get; init; }
    
    /// <summary>
    /// Данные полосы загрузки ресурсов.
    /// Может быть null, если ресурсы не настроены.
    /// </summary>
    public EngagementStripExportData? EngagementStrip { get; init; }
    
    /// <summary>
    /// Отступ между GanttChart и EngagementStrip.
    /// </summary>
    public double SectionGap { get; init; } = 10;
    
    /// <summary>
    /// Общая ширина документа.
    /// Включает колонку имён ресурсов, если она есть.
    /// </summary>
    public double TotalWidth
    {
        get
        {
            var namesWidth = EngagementStrip?.ResourceNamesWidth ?? 0;
            return Math.Max(GanttChart.TotalWidth, namesWidth + (EngagementStrip?.Engagement.Width ?? 0));
        }
    }
    
    /// <summary>
    /// Общая высота документа.
    /// </summary>
    public double TotalHeight
    {
        get
        {
            var height = GanttChart.TotalHeight;
            
            if (EngagementStrip != null)
            {
                height += SectionGap + EngagementStrip.Engagement.Height;
            }
            
            return height;
        }
    }
}