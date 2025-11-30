// GanttChart.WPF/ViewModels/ResourceViewModel.cs
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Models;
using Core.Services;

namespace Wpf.ViewModels;

/// <summary>
/// ViewModel для управления ресурсами.
/// Используется в ResourceManagerDialog.
/// </summary>
public partial class ResourceViewModel : ObservableObject
{
    private readonly ResourceService _resourceService;

    #region Observable Properties

    /// <summary>
    /// Коллекция ресурсов для отображения.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Resource> _resources = new();

    /// <summary>
    /// Выбранный ресурс.
    /// </summary>
    [ObservableProperty]
    private Resource? _selectedResource;

    /// <summary>
    /// Имя нового/редактируемого ресурса.
    /// </summary>
    [ObservableProperty]
    private string _editName = string.Empty;

    /// <summary>
    /// Инициалы нового/редактируемого ресурса.
    /// </summary>
    [ObservableProperty]
    private string _editInitials = string.Empty;

    /// <summary>
    /// Роль нового/редактируемого ресурса.
    /// </summary>
    [ObservableProperty]
    private ResourceRole _editRole = ResourceRole.Constructor;

    /// <summary>
    /// Цвет нового/редактируемого ресурса (HEX).
    /// </summary>
    [ObservableProperty]
    private string _editColorHex = "#4682B4";

    /// <summary>
    /// Режим редактирования (true) или добавления (false).
    /// </summary>
    [ObservableProperty]
    private bool _isEditMode;

    /// <summary>
    /// Статусное сообщение.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// ID редактируемого ресурса (для сохранения ссылки).
    /// </summary>
    private Guid _editingResourceId;
    
    /// <summary>
    /// Доступные роли ресурсов для ComboBox.
    /// </summary>
    public ResourceRole[] AvailableRoles { get; } = Enum.GetValues<ResourceRole>();

    #endregion
    
    #region Observable Properties - Participation Intervals

    /// <summary>
    /// Интервалы участия выбранного ресурса.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ParticipationInterval> _participationIntervals = new();

    /// <summary>
    /// Выбранный интервал участия.
    /// </summary>
    [ObservableProperty]
    private ParticipationInterval? _selectedInterval;

    #endregion

    #region Observable Properties - Absences

    /// <summary>
    /// Отсутствия выбранного ресурса.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Absence> _absences = new();

    /// <summary>
    /// Выбранное отсутствие.
    /// </summary>
    [ObservableProperty]
    private Absence? _selectedAbsence;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Можно ли редактировать MaxWorkload (false для Constructor).
    /// </summary>
    public bool IsMaxWorkloadEditable => EditRole != ResourceRole.Constructor;

    #endregion

    #region Constructor

    public ResourceViewModel(ResourceService resourceService)
    {
        _resourceService = resourceService;
        LoadResources();
    }

    #endregion

    #region Commands

    /// <summary>
    /// Добавить новый ресурс.
    /// </summary>
    [RelayCommand]
    private void AddResource()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "Введите имя ресурса";
            return;
        }

        var resource = new Resource
        {
            Name = EditName.Trim(),
            Initials = string.IsNullOrWhiteSpace(EditInitials) 
                ? GenerateInitials(EditName) 
                : EditInitials.Trim().ToUpper(),
            Role = ResourceRole.Constructor,
            ColorHex = EditColorHex
        };

        try
        {
            _resourceService.AddResource(resource);
            Resources.Add(resource);
            ClearEditFields();
            StatusMessage = $"Ресурс '{resource.Name}' добавлен";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Начать редактирование выбранного ресурса.
    /// </summary>
    [RelayCommand]
    private void EditResource()
    {
        if (SelectedResource == null)
        {
            StatusMessage = "Выберите ресурс для редактирования";
            return;
        }

        // Сохраняем ID для последующего поиска
        _editingResourceId = SelectedResource.Id;
        
        EditName = SelectedResource.Name;
        EditInitials = SelectedResource.Initials;
        EditRole = SelectedResource.Role;
        EditColorHex = SelectedResource.ColorHex;
        IsEditMode = true;
        StatusMessage = "Режим редактирования";
    }

    /// <summary>
    /// Сохранить изменения ресурса.
    /// </summary>
    [RelayCommand]
    private void SaveResource()
    {
        if (!IsEditMode)
            return;

        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "Введите имя ресурса";
            return;
        }

        try
        {
            // Находим ресурс по сохранённому ID
            var resource = _resourceService.GetResource(_editingResourceId);
            
            if (resource == null)
            {
                StatusMessage = "Ресурс не найден";
                ClearEditFields();
                return;
            }

            // Обновляем свойства
            resource.Name = EditName.Trim();
            resource.Initials = string.IsNullOrWhiteSpace(EditInitials)
                ? GenerateInitials(EditName)
                : EditInitials.Trim().ToUpper();
            resource.Role = EditRole;
            resource.ColorHex = EditColorHex;

            _resourceService.UpdateResource(resource);
            
            // Перезагружаем список для обновления UI
            LoadResources();

            var name = resource.Name;
            ClearEditFields();
            StatusMessage = $"Ресурс '{name}' обновлён";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Удалить выбранный ресурс.
    /// </summary>
    [RelayCommand]
    private void DeleteResource()
    {
        if (SelectedResource == null)
        {
            StatusMessage = "Выберите ресурс для удаления";
            return;
        }

        var name = SelectedResource.Name;
        var id = SelectedResource.Id;

        var result = MessageBox.Show(
            $"Удалить ресурс '{name}'?\n\nВсе назначения этого ресурса также будут удалены.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _resourceService.RemoveResource(id);
            
            // Удаляем из ObservableCollection
            var resourceToRemove = Resources.FirstOrDefault(r => r.Id == id);
            if (resourceToRemove != null)
            {
                Resources.Remove(resourceToRemove);
            }
            
            SelectedResource = null;
            ClearEditFields();
            StatusMessage = $"Ресурс '{name}' удалён";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Отменить редактирование.
    /// </summary>
    [RelayCommand]
    private void CancelEdit()
    {
        ClearEditFields();
        StatusMessage = "Редактирование отменено";
    }

    #endregion

    #region Commands - Participation Intervals

    /// <summary>
    /// Добавить новый интервал участия.
    /// </summary>
    [RelayCommand]
    private void AddInterval()
    {
        if (SelectedResource == null)
        {
            StatusMessage = "Сначала выберите ресурс";
            return;
        }

        try
        {
            var interval = new ParticipationInterval(
                SelectedResource.Id,
                TimeSpan.Zero,
                null,
                SelectedResource.Role == ResourceRole.Constructor ? 100 : 100
            );

            _resourceService.AddParticipationInterval(interval);
            ParticipationIntervals.Add(interval);
            StatusMessage = "Интервал добавлен";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Удалить выбранный интервал участия.
    /// </summary>
    [RelayCommand]
    private void RemoveInterval()
    {
        if (SelectedInterval == null)
        {
            StatusMessage = "Выберите интервал для удаления";
            return;
        }

        try
        {
            _resourceService.RemoveParticipationInterval(SelectedInterval.Id);
            ParticipationIntervals.Remove(SelectedInterval);
            SelectedInterval = null;
            StatusMessage = "Интервал удалён";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    #endregion

    #region Commands - Absences

    /// <summary>
    /// Добавить новое отсутствие.
    /// </summary>
    [RelayCommand]
    private void AddAbsence()
    {
        if (SelectedResource == null)
        {
            StatusMessage = "Сначала выберите ресурс";
            return;
        }

        try
        {
            var absence = new Absence(
                SelectedResource.Id,
                TimeSpan.Zero,
                TimeSpan.FromDays(1),
                "Отпуск"
            );

            _resourceService.AddAbsence(absence);
            Absences.Add(absence);
            StatusMessage = "Отсутствие добавлено";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    /// <summary>
    /// Удалить выбранное отсутствие.
    /// </summary>
    [RelayCommand]
    private void RemoveAbsence()
    {
        if (SelectedAbsence == null)
        {
            StatusMessage = "Выберите отсутствие для удаления";
            return;
        }

        try
        {
            _resourceService.RemoveAbsence(SelectedAbsence.Id);
            Absences.Remove(SelectedAbsence);
            SelectedAbsence = null;
            StatusMessage = "Отсутствие удалено";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    #endregion
    
    #region Private Methods

    private void LoadResources()
    {
        Resources.Clear();
        foreach (var resource in _resourceService.Resources)
        {
            Resources.Add(resource);
        }
    }

    private void ClearEditFields()
    {
        EditName = string.Empty;
        EditInitials = string.Empty;
        EditRole = ResourceRole.Constructor;
        EditColorHex = "#4682B4";
        IsEditMode = false;
        _editingResourceId = Guid.Empty;
    }

    private static string GenerateInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "??";

        var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        
        if (parts.Length == 1 && parts[0].Length >= 2)
            return parts[0].Substring(0, 2).ToUpper();
        
        return name.Length > 0 ? name[0].ToString().ToUpper() : "?";
    }
    
    /// <summary>
    /// Вызывается при изменении выбранного ресурса.
    /// </summary>
    partial void OnSelectedResourceChanged(Resource? value)
    {
        LoadIntervalsAndAbsences();
    }
    
    private void LoadIntervalsAndAbsences()
    {
        ParticipationIntervals.Clear();
        Absences.Clear();

        if (SelectedResource == null)
            return;

        foreach (var interval in _resourceService.GetParticipationIntervalsForResource(SelectedResource.Id))
        {
            ParticipationIntervals.Add(interval);
        }

        foreach (var absence in _resourceService.GetAbsencesForResource(SelectedResource.Id))
        {
            Absences.Add(absence);
        }
    }

    /// <summary>
    /// Вызывается при изменении роли — обновляет IsMaxWorkloadEditable.
    /// </summary>
    partial void OnEditRoleChanged(ResourceRole value)
    {
        OnPropertyChanged(nameof(IsMaxWorkloadEditable));
    }

    #endregion
}