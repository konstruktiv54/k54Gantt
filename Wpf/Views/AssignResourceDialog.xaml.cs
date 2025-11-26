using System.Windows;
using Core.Models;
using Core.Services;
using Task = Core.Interfaces.Task;

namespace Wpf.Views;

/// <summary>
/// Модель для отображения ресурса в списке назначения.
/// </summary>
public class ResourceAssignmentItem
{
    public Resource Resource { get; set; } = null!;
    public bool IsAssigned { get; set; }
}

/// <summary>
/// Диалог назначения ресурсов на задачу.
/// </summary>
public partial class AssignResourceDialog : Window
{
    private readonly ResourceService _resourceService;
    private readonly Task _task;
    private readonly List<ResourceAssignmentItem> _items = new();

    /// <summary>
    /// Результат: true если были изменения.
    /// </summary>
    public bool HasChanges { get; private set; }

    public AssignResourceDialog(ResourceService resourceService, Task task)
    {
        InitializeComponent();

        _resourceService = resourceService;
        _task = task;

        TaskNameText.Text = task.Name ?? "Без названия";

        LoadResources();
    }

    private void LoadResources()
    {
        _items.Clear();

        // Получаем уже назначенные ресурсы
        var assignedResources = _resourceService.GetResourcesForTask(_task.Id).ToHashSet();

        foreach (var resource in _resourceService.Resources)
        {
            _items.Add(new ResourceAssignmentItem
            {
                Resource = resource,
                IsAssigned = assignedResources.Contains(resource)
            });
        }

        ResourcesList.ItemsSource = _items;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        // Получаем текущие назначения
        var currentlyAssigned = _resourceService.GetResourcesForTask(_task.Id).Select(r => r.Id).ToHashSet();

        foreach (var item in _items)
        {
            var isCurrentlyAssigned = currentlyAssigned.Contains(item.Resource.Id);

            if (item.IsAssigned && !isCurrentlyAssigned)
            {
                // Новое назначение
                _resourceService.AssignResource(_task.Id, item.Resource.Id);
                HasChanges = true;
            }
            else if (!item.IsAssigned && isCurrentlyAssigned)
            {
                // Снятие назначения
                _resourceService.UnassignResource(_task.Id, item.Resource.Id);
                HasChanges = true;
            }
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}