using System.Collections.ObjectModel;
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
        if (NewHolidayDate < ProjectStart)
        {
            StatusMessage = "Дата праздника не может быть раньше начала проекта";
            return;
        }

        // Проверяем, не существует ли уже праздник на эту дату
        var dayOffset = (NewHolidayDate.Date - ProjectStart.Date).Days;
        if (_calendarService.IsHoliday(TimeSpan.FromDays(dayOffset)))
        {
            StatusMessage = "Праздник на эту дату уже существует";
            return;
        }

        var holiday = _calendarService.AddHoliday(
            NewHolidayDate, 
            ProjectStart, 
            string.IsNullOrWhiteSpace(NewHolidayName) ? null : NewHolidayName.Trim());

        RefreshHolidays();

        // Выбираем добавленный праздник
        SelectedHoliday = Holidays.FirstOrDefault(h => h.Holiday.Id == holiday.Id);

        // Сбрасываем поля ввода
        NewHolidayName = string.Empty;
        NewHolidayDate = DateTime.Today;

        StatusMessage = $"Добавлен праздник: {holiday.Name ?? holiday.GetDate(ProjectStart).ToShortDateString()}";
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
        var year = ProjectStart.Year;
        var russianHolidays = new List<(DateTime Date, string Name)>
        {
            (new DateTime(year, 1, 1), "Новый год"),
            (new DateTime(year, 1, 2), "Новогодние каникулы"),
            (new DateTime(year, 1, 3), "Новогодние каникулы"),
            (new DateTime(year, 1, 4), "Новогодние каникулы"),
            (new DateTime(year, 1, 5), "Новогодние каникулы"),
            (new DateTime(year, 1, 6), "Новогодние каникулы"),
            (new DateTime(year, 1, 7), "Рождество Христово"),
            (new DateTime(year, 1, 8), "Новогодние каникулы"),
            (new DateTime(year, 2, 23), "День защитника Отечества"),
            (new DateTime(year, 3, 8), "Международный женский день"),
            (new DateTime(year, 5, 1), "Праздник Весны и Труда"),
            (new DateTime(year, 5, 9), "День Победы"),
            (new DateTime(year, 6, 12), "День России"),
            (new DateTime(year, 11, 4), "День народного единства"),
        };

        int addedCount = 0;
        foreach (var (date, name) in russianHolidays)
        {
            // Проверяем, что дата в пределах проекта и праздник ещё не добавлен
            if (date >= ProjectStart)
            {
                var dayOffset = (date.Date - ProjectStart.Date).Days;
                if (!_calendarService.IsHoliday(TimeSpan.FromDays(dayOffset)))
                {
                    _calendarService.AddHoliday(date, ProjectStart, name);
                    addedCount++;
                }
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