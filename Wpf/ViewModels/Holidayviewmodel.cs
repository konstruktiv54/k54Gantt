using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Models;
using Core.Services;

namespace Wpf.ViewModels;

/// <summary>
/// ViewModel для управления праздниками в производственном календаре.
/// </summary>
public partial class HolidayViewModel : ObservableObject
{
    private readonly ProductionCalendarService _calendarService;
    private readonly ProjectManager _projectManager;

    /// <summary>
    /// Дата начала проекта (для конвертации TimeSpan в DateTime).
    /// </summary>
    public DateTime ProjectStart { get; set; } = DateTime.Today;

    /// <summary>
    /// Список праздников для отображения.
    /// </summary>
    public ObservableCollection<HolidayItemViewModel> Holidays { get; } = new();

    /// <summary>
    /// Выбранный праздник.
    /// </summary>
    [ObservableProperty]
    private HolidayItemViewModel? _selectedHoliday;

    /// <summary>
    /// Дата для добавления нового праздника.
    /// </summary>
    [ObservableProperty]
    private DateTime _newHolidayDate = DateTime.Today;

    /// <summary>
    /// Название для нового праздника.
    /// </summary>
    [ObservableProperty]
    private string _newHolidayName = string.Empty;

    /// <summary>
    /// Сообщение статуса.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public HolidayViewModel(ProductionCalendarService calendarService, 
        ProjectManager projectManager)
    {
        _calendarService = calendarService;
        _projectManager = projectManager;
        RefreshHolidays();
    }

    /// <summary>
    /// Обновляет список праздников из сервиса.
    /// </summary>
    public void RefreshHolidays()
    {
        Holidays.Clear();
        
        foreach (var holiday in _calendarService.Holidays.OrderBy(h => h.Day))
        {
            Holidays.Add(new HolidayItemViewModel(holiday, _projectManager));
        }

        StatusMessage = $"Праздников: {Holidays.Count}";
    }

    /// <summary>
    /// Команда: Добавить праздник.
    /// </summary>
    [RelayCommand]
    private void AddHoliday()
    {
        var projectStart = _projectManager.Start;
        if (NewHolidayDate < projectStart)
        {
            StatusMessage = "Дата праздника не может быть раньше начала проекта";
            return;
        }

        // Проверяем, не существует ли уже праздник на эту дату
        var dayOffset = (NewHolidayDate.Date - projectStart.Date).Days;
        if (_calendarService.IsHoliday(TimeSpan.FromDays(dayOffset)))
        {
            StatusMessage = "Праздник на эту дату уже существует";
            return;
        }

        var holiday = _calendarService.AddHoliday(
            NewHolidayDate, 
            projectStart, 
            string.IsNullOrWhiteSpace(NewHolidayName) ? null : NewHolidayName.Trim());

        RefreshHolidays();

        // Выбираем добавленный праздник
        SelectedHoliday = Holidays.FirstOrDefault(h => h.Holiday.Id == holiday.Id);

        // Сбрасываем поля ввода
        NewHolidayName = string.Empty;
        NewHolidayDate = DateTime.Today;

        StatusMessage = $"Добавлен праздник: {holiday.Name ?? holiday.GetDate(_projectManager.Start).ToShortDateString()}";
    }

    /// <summary>
    /// Команда: Удалить выбранный праздник.
    /// </summary>
    [RelayCommand]
    private void RemoveHoliday()
    {
        if (SelectedHoliday == null)
            return;

        var holidayName = SelectedHoliday.DisplayName;
        _calendarService.RemoveHoliday(SelectedHoliday.Holiday.Id);

        RefreshHolidays();
        SelectedHoliday = null;

        StatusMessage = $"Удалён праздник: {holidayName}";
    }

/// <summary>
/// Команда: Добавить стандартные российские праздники.
/// </summary>
[RelayCommand]
private void AddRussianHolidays()
{
    // Базовый год — старт проекта (НИЖНЯЯ ГРАНИЦА)
    var projectStart = _projectManager.Start;
    int startYear = projectStart.Year;
    int endYear = startYear;

    // Расширяем диапазон до задач, но НЕ опускаемся ниже года проекта
    if (_projectManager.Tasks.Any())
    {
        var taskYears = _projectManager.Tasks
            .Select(t => (projectStart + t.Start).Year)
            .Concat(_projectManager.Tasks.Select(t => (projectStart + t.End).Year))
            .Distinct();

        var enumerable = taskYears.ToList();
        if (enumerable.Count != 0)
        {
            // Гарантируем: не раньше года старта проекта
            startYear = Math.Max(startYear, enumerable.Min());
            endYear = Math.Max(endYear, enumerable.Max());
        }
    }

    // Базовый список праздников (шаблон: год не важен)
    var russianHolidays = new List<(DateTime Date, string Name)>
    {
        (new DateTime(2000, 1, 1), "Новый год"),
        (new DateTime(2000, 1, 2), "Новогодние каникулы"),
        (new DateTime(2000, 1, 3), "Новогодние каникулы"),
        (new DateTime(2000, 1, 4), "Новогодние каникулы"),
        (new DateTime(2000, 1, 5), "Новогодние каникулы"),
        (new DateTime(2000, 1, 6), "Новогодние каникулы"),
        (new DateTime(2000, 1, 7), "Рождество Христово"),
        (new DateTime(2000, 1, 8), "Новогодние каникулы"),
        (new DateTime(2000, 2, 23), "День защитника Отечества"),
        (new DateTime(2000, 3, 8), "Международный женский день"),
        (new DateTime(2000, 5, 1), "Праздник Весны и Труда"),
        (new DateTime(2000, 5, 9), "День Победы"),
        (new DateTime(2000, 6, 12), "День России"),
        (new DateTime(2000, 11, 4), "День народного единства"),
    };

    int addedCount = 0;

    // Добавляем праздники для каждого года в диапазоне
    for (int year = startYear; year <= endYear; year++)
    {
        foreach (var (baseDate, name) in russianHolidays)
        {
            var date = new DateTime(year, baseDate.Month, baseDate.Day);

            // НЕ добавляем праздники, которые датированы раньше реальной даты старта проекта
            if (date.Date < projectStart.Date) continue;

            // Проверяем только на дубликаты (используем projectStart для согласованности)
            if (_calendarService.IsHoliday(date, projectStart)) continue;
            
            // Пропустить, если праздник попадает на выходной
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            _calendarService.AddHoliday(date, projectStart, name);
            addedCount++;
        }
    }

    RefreshHolidays();
    StatusMessage = $"Добавлено праздников: {addedCount}";
}


    /// <summary>
    /// Команда: Очистить все праздники.
    /// </summary>
    [RelayCommand]
    private void ClearAllHolidays()
    {
        var count = Holidays.Count;
        _calendarService.Clear();
        RefreshHolidays();
        StatusMessage = $"Удалено праздников: {count}";
    }
}

/// <summary>
/// ViewModel для отдельного праздника в списке.
/// </summary>
public class HolidayItemViewModel : ObservableObject
{
    public Holiday Holiday { get; }
    private readonly ProjectManager _projectManager;
    
    public HolidayItemViewModel(Holiday holiday, ProjectManager projectManager)
    {
        Holiday = holiday;
        _projectManager = projectManager;
    }

    /// <summary>
    /// Абсолютная дата праздника.
    /// </summary>
    public DateTime Date => _projectManager.Start.AddDays(Holiday.Day.Days);

    /// <summary>
    /// Форматированная дата для отображения.
    /// </summary>
    public string DateDisplay => Date.ToString("d MMMM yyyy");

    /// <summary>
    /// День недели.
    /// </summary>
    public string DayOfWeekDisplay => Date.ToString("dddd");

    /// <summary>
    /// Название праздника.
    /// </summary>
    public string? Name => Holiday.Name;

    /// <summary>
    /// Отображаемое название (или дата, если название не задано).
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Holiday.Name) 
        ? DateDisplay 
        : Holiday.Name;
}