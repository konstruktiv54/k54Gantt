using System.Collections.ObjectModel;
using Core.Services;
using Task = Core.Interfaces.Task;

namespace Wpf.Services;

/// <summary>
/// Сервис для построения иерархии TaskItemViewModel из ProjectManager.
/// Поддерживает группы и split-части.
/// </summary>
public class TaskHierarchyBuilder
{
    #region Fields

    private readonly ProjectManager _manager;
    private readonly Action _onExpandChanged;
    private readonly Action<ViewModels.TaskItemViewModel>? _onShowPartsChanged;
    private readonly Action? _onTaskModified;
    private readonly WorkingDaysCalculator _workingDaysCalculator;

    #endregion

    #region Constructor

    /// <summary>
    /// Создаёт построитель иерархии.
    /// </summary>
    /// <param name="manager">ProjectManager с задачами.</param>
    /// <param name="calculator"></param>
    /// <param name="onExpandChanged">Callback при изменении expand/collapse.</param>
    /// <param name="onShowPartsChanged">Callback при изменении ShowParts.</param>
    /// <param name="onTaskModified"></param>
    public TaskHierarchyBuilder(
        ProjectManager manager,
        WorkingDaysCalculator calculator,
        Action onExpandChanged,
        Action<ViewModels.TaskItemViewModel>? onShowPartsChanged = null,
        Action? onTaskModified = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _onExpandChanged = onExpandChanged;
        _onShowPartsChanged = onShowPartsChanged;
        _onTaskModified = onTaskModified;
        _workingDaysCalculator = calculator;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Строит полную иерархию TaskItemViewModel из ProjectManager.
    /// Возвращает только корневые элементы (дети уже добавлены в Children).
    /// </summary>
    public ObservableCollection<ViewModels.TaskItemViewModel> BuildHierarchy()
    {
        var result = new ObservableCollection<ViewModels.TaskItemViewModel>();
        var projectStart = _manager.Start;

        // Получаем все задачи в правильном порядке
        var allTasks = _manager.Tasks;

        // Словарь для быстрого поиска ViewModel по Task
        var viewModelMap = new Dictionary<Guid, ViewModels.TaskItemViewModel>();

        // Первый проход: создаём ViewModel для каждой задачи
        foreach (var task in allTasks)
        {
            // Пропускаем части split-задач - они будут добавлены отдельно
            if (_manager.IsPart(task))
                continue;

            var parentTask = _manager.DirectGroupOf(task);
            ViewModels.TaskItemViewModel? parentVm = null;
            int level = 0;

            if (parentTask != null && viewModelMap.TryGetValue(parentTask.Id, out var foundParent))
            {
                parentVm = foundParent;
                level = parentVm.Level + 1;
            }

            var vm = new ViewModels.TaskItemViewModel(
                task,
                _manager,
                projectStart,
                _workingDaysCalculator,
                level,
                parentVm,
                _onExpandChanged,
                _onShowPartsChanged,
                _onTaskModified);

            viewModelMap[task.Id] = vm;

            // Добавляем к родителю или в корень
            if (parentVm != null)
            {
                parentVm.Children.Add(vm);
            }
            else
            {
                result.Add(vm);
            }

            // Если это split-задача, добавляем части как дочерние (если ShowParts = true)
            if (_manager.IsSplit(task))
            {
                BuildSplitParts(vm, task, projectStart);
            }
        }

        return result;
    }

    /// <summary>
    /// Строит плоский список видимых задач с учётом expand/collapse.
    /// </summary>
    /// <param name="rootTasks">Корневые элементы иерархии.</param>
    /// <returns>Плоский список видимых TaskItemViewModel.</returns>
    public ObservableCollection<ViewModels.TaskItemViewModel> BuildFlatList(
        ObservableCollection<ViewModels.TaskItemViewModel> rootTasks)
    {
        var result = new ObservableCollection<ViewModels.TaskItemViewModel>();
        
        foreach (var root in rootTasks)
        {
            AddToFlatList(root, result);
        }

        return result;
    }

    /// <summary>
    /// Перестраивает Children для split-задачи при изменении ShowParts.
    /// </summary>
    public void RebuildSplitChildren(ViewModels.TaskItemViewModel splitVm)
    {
        if (!splitVm.IsSplitRoot)
            return;

        // Сохраняем обычные дочерние элементы (не части)
        var normalChildren = splitVm.Children
            .Where(c => !c.IsPart)
            .ToList();

        splitVm.Children.Clear();

        if (splitVm.ShowParts)
        {
            // Добавляем split-части
            var parts = _manager.PartsOf(splitVm.Task);
            int partIndex = 0;
            foreach (var part in parts)
            {
                var partVm = new ViewModels.TaskItemViewModel(
                    part,
                    _manager,
                    _manager.Start,
                    _workingDaysCalculator,
                    splitVm.Level + 1,
                    splitVm,
                    _onExpandChanged,
                    null,
                    _onTaskModified);

                splitVm.Children.Add(partVm);
                partIndex++;
            }
        }

        // Возвращаем обычные дочерние элементы
        foreach (var child in normalChildren)
        {
            splitVm.Children.Add(child);
        }
    }

    /// <summary>
    /// Находит TaskItemViewModel по Task.Id в иерархии.
    /// </summary>
    public ViewModels.TaskItemViewModel? FindByTaskId(
        ObservableCollection<ViewModels.TaskItemViewModel> rootTasks,
        Guid taskId)
    {
        foreach (var root in rootTasks)
        {
            var found = FindByTaskIdRecursive(root, taskId);
            if (found != null)
                return found;
        }
        return null;
    }

    #endregion

    #region Private Methods

    private void BuildSplitParts(
        ViewModels.TaskItemViewModel splitVm,
        Task splitTask,
        DateTime projectStart)
    {
        // По умолчанию ShowParts = false, части не добавляются
        // Они будут добавлены при установке ShowParts = true через RebuildSplitChildren
    }

    private void AddToFlatList(
        ViewModels.TaskItemViewModel item,
        ObservableCollection<ViewModels.TaskItemViewModel> flatList)
    {
        flatList.Add(item);

        // Если элемент развёрнут, добавляем его детей
        if (item.IsExpanded || (!item.IsGroup && !item.IsSplitRoot))
        {
            // Для split-задач учитываем ShowParts
            if (item.IsSplitRoot && !item.ShowParts)
            {
                // Показываем только обычные дети, не части
                foreach (var child in item.Children.Where(c => !c.IsPart))
                {
                    AddToFlatList(child, flatList);
                }
            }
            else if (item.IsGroup || (item.IsSplitRoot && item.ShowParts))
            {
                // Показываем все дочерние элементы
                foreach (var child in item.Children)
                {
                    AddToFlatList(child, flatList);
                }
            }
        }
    }

    private ViewModels.TaskItemViewModel? FindByTaskIdRecursive(
        ViewModels.TaskItemViewModel current,
        Guid taskId)
    {
        if (current.Id == taskId)
            return current;

        foreach (var child in current.Children)
        {
            var found = FindByTaskIdRecursive(child, taskId);
            if (found != null)
                return found;
        }

        return null;
    }

    #endregion
}