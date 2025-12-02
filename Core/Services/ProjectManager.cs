using Core.Interfaces;
using Core.Models.DTOs;
using Task = Core.Interfaces.Task;

namespace Core.Services;

/// <summary>
/// Wrapper ProjectManager class
/// </summary>
[Serializable]
public class ProjectManager : ProjectManager<Task, object>
{
}

/// <summary>
/// Concrete ProjectManager class for the IProjectManager interface
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TR"></typeparam>
[Serializable]
public class ProjectManager<T, TR> : IProjectManager<T, TR>
    where T : Task
    where TR : class
{
    private const double Tolerance = double.MinValue;
    HashSet<T> _mRegister = new();
    List<T> _mRootTasks = new();
    Dictionary<T, List<T>> _mMembersOfGroup = new Dictionary<T, List<T>>(); // Map group to list of members

    Dictionary<T, HashSet<T>>
        _mDependantsOfPrecedent = new Dictionary<T, HashSet<T>>(); // Map precendent to list of dependents

    Dictionary<T, HashSet<TR>> _mResourcesOfTask = new Dictionary<T, HashSet<TR>>(); // Map task to list of resources
    Dictionary<T, List<T>> _mPartsOfSplitTask = new Dictionary<T, List<T>>(); // Map split task to list of task parts
    Dictionary<T, T> _mSplitTaskOfPart = new Dictionary<T, T>(); // Map a task part to the original split task
    Dictionary<T, T> _mGroupOfMember = new Dictionary<T, T>(); // Map member task to parent group task
    Dictionary<T, int> _mTaskIndices = new Dictionary<T, int>(); // Map the task to its zero-based index order position

    #region Events

    /// <summary>
    /// Событие, возникающее при любом изменении расписания (задачи, связи, даты).
    /// </summary>
    public event EventHandler? ScheduleChanged;

    /// <summary>
    /// Флаг для временной приостановки событий (batch operations).
    /// </summary>
    private bool _suppressEvents;

    /// <summary>
    /// Вызывает событие ScheduleChanged, если события не подавлены.
    /// </summary>
    protected virtual void OnScheduleChanged()
    {
        if (!_suppressEvents)
        {
            ScheduleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Выполняет действие с подавлением событий, вызывая одно событие в конце.
    /// Используется для batch-операций.
    /// </summary>
    /// <param name="action">Действие для выполнения.</param>
    public void BatchUpdate(Action action)
    {
        _suppressEvents = true;
        try
        {
            action();
        }
        finally
        {
            _suppressEvents = false;
            OnScheduleChanged();
        }
    }

    #endregion

    /// <summary>
    /// Create a new Project
    /// </summary>
    public ProjectManager()
    {
        Now = TimeSpan.Zero;
        Start = DateTime.Now;
    }

    public Dictionary<T, List<T>> GetSplitTasks()
    {
        // Создаём новую копию, чтобы внешние изменения не влияли на внутреннее состояние
        return new Dictionary<T, List<T>>(_mPartsOfSplitTask);
    }

    public List<SplitTaskRelation> GetSplitTaskRelations()
    {
        return (from kvp in _mPartsOfSplitTask
            let splitTask = kvp.Key
            let parts = kvp.Value
            from part in parts
            select new SplitTaskRelation
            {
                PartId = part.Id,
                PartName = part.Name,
                PartStart = part.Start.ToString(),
                PartEnd = part.End.ToString(),
                PartDuration = part.Duration.ToString(),
                PartComplete = part.Complete,
                SplitTaskId = splitTask.Id,
            }).ToList();
    }

    public List<GroupRelation> GetGroupRelations()
    {
        return (from kvp in _mMembersOfGroup
            where kvp.Value.Any()
            from member in kvp.Value
            select new GroupRelation
            {
                GroupId = kvp.Key.Id,
                MemberId = member.Id
            }).ToList();
    }

    // Вспомогательный метод для форматирования TimeSpan с привязкой к дням
    private string FormatTimeSpanToDays(TimeSpan span)
    {
        // Округляем до целых дней
        int wholeDays = (int)Math.Round(span.TotalDays);
        return $"{wholeDays}.00:00:00";
    }

    /// <summary>
    /// Get or set the TimeSpan we are at now from Start DateTime
    /// </summary>
    public TimeSpan Now { get; set; }

    /// <summary>
    /// Get or set the starting date for this project
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// Get the date after the specified TimeSpan
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    public DateTime GetDateTime(TimeSpan span)
    {
        return Start.Add(span);
    }

    /// <summary>
    /// Create a new T for this Project and add it to the T tree
    /// </summary>
    /// <returns></returns>
    public void Add(T task)
    {
        if (!_mRegister.Contains(task))
        {
            _mRegister.Add(task);
            _mRootTasks.Add(task);
            _mMembersOfGroup[task] = new List<T>();
            _mDependantsOfPrecedent[task] = new HashSet<T>();
            _mResourcesOfTask[task] = new HashSet<TR>();
            _mGroupOfMember[task] = null;
            
            OnScheduleChanged();
        }
    }

    /// <summary>
    /// Удаляет задачу из этого проекта.
    /// </summary>
    /// <param name="task"></param>
    public void Delete(T task)
    {
        if (task != null
            && !_mSplitTaskOfPart.ContainsKey(task) // not a task part
           )
        {
            // Check if is group so can ungroup the task
            if (IsGroup(task))
                Ungroup(task);

            if (IsSplit(task))
                Merge(task);

            // Really delete all references
            _mRootTasks.Remove(task);
            _mMembersOfGroup.Remove(task);
            _mDependantsOfPrecedent.Remove(task);
            _mResourcesOfTask.Remove(task);
            _mGroupOfMember.Remove(task);
            _mPartsOfSplitTask.Remove(task);
            foreach (var g in _mMembersOfGroup) g.Value.Remove(task); // optimised: no need to check for contains
            foreach (var g in _mDependantsOfPrecedent) g.Value.Remove(task);
            _mRegister.Remove(task);
            
            OnScheduleChanged();
        }
        else if (task != null
                 && _mSplitTaskOfPart.ContainsKey(task) // must be existing part
                )
        {
            var split = _mSplitTaskOfPart[task];
            var parts = _mPartsOfSplitTask[split];
            if (parts.Count > 2)
            {
                parts.Remove(task); // remove the part from the split task
                _mRegister.Remove(task); // unregister the part
                _mResourcesOfTask.Remove(task);
                _mSplitTaskOfPart.Remove(task); // remove the reverse lookup

                split.Start = parts.First().Start; // recalculate the split task
                split.End = parts.Last().End;
                split.Duration = split.End - split.Start;
                
                OnScheduleChanged();
            }
            else
            {
                Merge(split);
            }
        }
    }

    /// <summary>
    /// Добавляет участника T в группу T.
    /// </summary>
    /// <param name="group"></param>
    /// <param name="member"></param>
    public void Group(T group, T member)
    {
        if (group != null
            && member != null
            && _mRegister.Contains(group)
           )
        {
            // if the member is a task part, assign the split task to the group instead
            if (_mSplitTaskOfPart.ContainsKey(member)) member = _mSplitTaskOfPart[member];

            if (_mRegister.Contains(member)
                && !group.Equals(member)
                && !_mPartsOfSplitTask.ContainsKey(group) // group cannot be split task
                && !_mSplitTaskOfPart.ContainsKey(group) // group cannot be parts
                && !MembersOf(member).Contains(group)
                && !HasRelations(group)
               )
            {
                _DetachTask(member);

                // add member to new group
                _mMembersOfGroup[group].Add(member);
                _mGroupOfMember[member] = group;

                _RecalculateAncestorsSchedule();
                _RecalculateSlack();
                // clear indices since positions changed
                _mTaskIndices.Clear();
                
                OnScheduleChanged();
            }
        }
    }

    /// <summary>
    /// Удалить задачу участника из его группы.
    /// </summary>
    public void Ungroup(T group, T member)
    {
        if (group != null
            && member != null
            && _mRegister.Contains(group)
           )
        {
            // change the member to become the split task is member is a task part
            if (_mSplitTaskOfPart.ContainsKey(member)) member = _mSplitTaskOfPart[member];
            if (_mRegister.Contains(member) && IsGroup(group))
            {
                var ancestor = GroupsOf(group).LastOrDefault();
                if (ancestor == null) // group is in root
                    _mRootTasks.Insert(_mRootTasks.IndexOf(group) + 1, member);
                else // group is not in root, we get the ancestor that is in root
                    _mRootTasks.Insert(_mRootTasks.IndexOf(ancestor) + 1, member);
                _mMembersOfGroup[group].Remove(member);
                _mGroupOfMember[member] = null;

                _RecalculateAncestorsSchedule();
                
                OnScheduleChanged();
            }
        }
    }

    /// <summary>
    /// Разгруппировывает все задачи участников в указанной групповой задаче.
    /// Указанная групповая задача станет обычной задачей.
    /// Если существует родительская группа, задачи участников станут частью родительской группы.
    /// </summary>
    /// <param name="group"></param>
    public void Ungroup(T group)
    {
        if (group != null
            //&& _mRegister.Contains(group)
            && _mMembersOfGroup.TryGetValue(group, out var members))
        {
            var newgroup = DirectGroupOf(group);
            if (newgroup == null)
            {
                foreach (var member in members)
                {
                    _mRootTasks.Add(member);
                    _mGroupOfMember[member] = null;
                }
            }
            else
            {
                foreach (var member in members)
                {
                    _mMembersOfGroup[newgroup].Add(member);
                    _mGroupOfMember[member] = null;
                }
            }

            members.Clear();

            _RecalculateAncestorsSchedule();
            
            OnScheduleChanged();
        }
    }

    /// <summary>
    /// Get the zero-based index of the task in this Project
    /// </summary>
    /// <param name="task"></param>
    /// <returns></returns>
    public int IndexOf(T task)
    {
        if (_mRegister.Contains(task))
        {
            if (_mTaskIndices.ContainsKey(task))
                return _mTaskIndices[task];

            var i = 0;
            foreach (var x in Tasks)
            {
                if (x.Equals(task))
                {
                    _mTaskIndices[task] = i;
                    return i;
                }

                i++;
            }
        }

        return -1;
    }

    /// <summary>
    /// Re-order position of the task by offset amount of places
    /// If task is moved between members, the task is added to the members' group
    /// If task is a member and it is moved above it's group or below last sibling member, then it is moved out of its group
    /// If task is a part, then its parent split-task will be move instead
    /// </summary>
    /// <param name="task"></param>
    /// <param name="offset"></param>
    public void Move(T task, int offset)
    {
        if (task != null && _mRegister.Contains(task) && offset != 0)
        {
            if (IsPart(task)) task = SplitTaskOf(task);

            var indexoftask = IndexOf(task);
            if (indexoftask > -1)
            {
                var newindexoftask = indexoftask + offset;
                // check for out of index bounds
                var taskcount = Tasks.Count();
                if (newindexoftask < 0) newindexoftask = 0;
                else if (newindexoftask > taskcount) newindexoftask = taskcount;
                // get the index of the task that will be displaced
                var displacedtask = Tasks.ElementAtOrDefault(newindexoftask);

                if (displacedtask == task)
                {
                    return;
                }

                if (displacedtask == null)
                {
                    // adding to the end of the task list
                    _DetachTask(task);
                    _mRootTasks.Add(task);
                }
                else if (!displacedtask.Equals(task))
                {
                    int indexofdestinationtask;
                    var displacedtaskparent = DirectGroupOf(displacedtask);
                    if (displacedtaskparent == null) // displacedtask is in root
                    {
                        indexofdestinationtask = _mRootTasks.IndexOf(displacedtask);
                        _DetachTask(task);
                        _mRootTasks.Insert(indexofdestinationtask, task);
                    }
                    else if (!displacedtaskparent.Equals(task)) // displaced task is not under the moving task
                    {
                        var memberlist = _mMembersOfGroup[displacedtaskparent];
                        indexofdestinationtask = memberlist.IndexOf(displacedtask);
                        _DetachTask(task);
                        memberlist.Insert(indexofdestinationtask, task);
                        _mGroupOfMember[task] = displacedtaskparent;
                    }
                }

                _RecalculateAncestorsSchedule();
                _RecalculateSlack();

                // clear indices since positions changed
                _mTaskIndices.Clear();
                
                OnScheduleChanged();
            }
        }
    }

    /// <summary>
    /// Get the T tree
    /// </summary>
    public List<T> Tasks
    {
        get
        {
            var result = new List<T>();
            var stack = new Stack<T>(1024);
            var rstack = new Stack<T>(30);
            foreach (var task in _mRootTasks)
            {
                stack.Push(task);
                while (stack.Count > 0)
                {
                    var visited = stack.Pop();
                    result.Add(visited);

                    foreach (var member in _mMembersOfGroup[visited])
                        rstack.Push(member);

                    while (rstack.Count > 0) stack.Push(rstack.Pop());
                }
            }

            return result;
        }
        set
        {
            // Очищаем существующие задачи
            _mRootTasks.Clear();
            _mMembersOfGroup.Clear();
            _mDependantsOfPrecedent.Clear();
            _mResourcesOfTask.Clear();
            _mPartsOfSplitTask.Clear();
            _mSplitTaskOfPart.Clear();
            _mGroupOfMember.Clear();
            _mTaskIndices.Clear();
            _mRegister.Clear();

            // Анализируем иерархию задач и восстанавливаем структуру
            var childrenMap = new Dictionary<T, List<T>>();

            // Сначала добавляем все задачи в реестр
            foreach (var task in value)
            {
                _mRegister.Add(task);
                _mMembersOfGroup[task] = new List<T>(); // Инициализируем пустой список для членов группы
            }

            // Определяем корневые задачи (те, которые не имеют родителя)
            var nonRootTasks = new HashSet<T>();

            // Предполагаем, что задачи либо имеют свойство Parent, либо имеют специальную структуру
            // Из-за отсутствия полной информации о структуре, придется сделать предположения
            // Вариант 1: Предполагаем, что корневые задачи идут первыми в списке
            foreach (var task in value)
            {
                var isRoot = true;

                // Проверяем, является ли задача частью группы
                // Для этой логики потребуется знать, как определить родителя задачи
                // Поскольку у нас нет этой информации, используем простую эвристику

                // В худшем случае, можно считать, что все задачи находятся на одном уровне (корневые)
                if (isRoot && !nonRootTasks.Contains(task))
                {
                    _mRootTasks.Add(task);
                }
            }

            // Если у задач есть определенная структура (например, они хранят ссылки на родителей или детей),
            // то нужно использовать эту информацию для построения правильной иерархии

            // Пример: если у задачи есть свойство Parent
            /*
            foreach (var task in value)
            {
                var parent = task.Parent;
                if (parent != null)
                {
                    _mMembersOfGroup[parent].Add(task);
                    _mGroupOfMember[task] = parent;
                    nonRootTasks.Add(task);
                }
                else if (!nonRootTasks.Contains(task))
                {
                    _mRootTasks.Add(task);
                }
            }
            */

            // Восстанавливаем зависимости и другие связи между задачами, если требуется
            // Для этого потребуется дополнительная информация о том, как хранятся эти связи

            // Если в задачах есть информация о зависимостях:
            /*
            foreach (var task in value)
            {
                foreach (var dependentTask in task.Dependencies)
                {
                    if (!_mDependantsOfPrecedent.ContainsKey(task))
                        _mDependantsOfPrecedent[task] = new HashSet<T>();

                    _mDependantsOfPrecedent[task].Add(dependentTask);
                }
            }
            */

            // Обновляем индексы задач
            var index = 0;
            foreach (var task in Tasks) // Используем геттер для обхода задач в нужном порядке
            {
                _mTaskIndices[task] = index++;
            }
            
            OnScheduleChanged();
        }
    }

    /// <summary>
    /// Enumerate upwards from member to and through all the parents and grandparents of the specified task
    /// </summary>
    public IEnumerable<T> GroupsOf(T member)
    {
        var parent = DirectGroupOf(member);
        while (parent != null)
        {
            yield return parent;
            parent = DirectGroupOf(parent);
        }
    }

    /// <summary>
    /// Enumerate through all the children and grandchildren of the specified group
    /// </summary>
    /// <param name="group"></param>
    /// <returns></returns>
    public IEnumerable<T> MembersOf(T group)
    {
        if (_mRegister.Contains(group))
        {
            var stack = new Stack<T>(20);
            var rstack = new Stack<T>(10);
            foreach (var child in _mMembersOfGroup[group])
            {
                stack.Push(child);
                while (stack.Count > 0)
                {
                    var visitedchild = stack.Pop();
                    yield return visitedchild;

                    // push the grandchild
                    rstack.Clear();
                    foreach (var grandchild in _mMembersOfGroup[visitedchild])
                        rstack.Push(grandchild);

                    // put in the right visiting order
                    while (rstack.Count > 0)
                        stack.Push(rstack.Pop());
                }
            }
        }
    }

    /// <summary>
    /// Get the parent group of the specified task
    /// </summary>
    /// <param name="member"></param>
    /// <returns></returns>
    public T DirectGroupOf(T member)
    {
        if (_mGroupOfMember.ContainsKey(member)) // _mRegister.Contains(task))
        {
            return _mGroupOfMember[member];
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Enumerate through all the direct children of the specified group
    /// </summary>
    /// <param name="group"></param>
    /// <returns></returns>
    public IEnumerable<T> DirectMembersOf(T group)
    {
        if (group == null) yield break;

        if (_mMembersOfGroup.TryGetValue(group, out var list))
        {
            var iter = list.GetEnumerator();
            while (iter.MoveNext()) yield return iter.Current;
        }
    }

    /// <summary>
    /// Enumerate through all the direct precedents and indirect precedents of the specified task
    /// </summary>
    /// <param name="dependant"></param>
    /// <returns></returns>
    public IEnumerable<T> PrecedentsOf(T dependant)
    {
        if (_mRegister.Contains(dependant))
        {
            var stack = new Stack<T>(20);
            foreach (var p in DirectPrecedentsOf(dependant))
            {
                stack.Push(p);
                while (stack.Count > 0)
                {
                    var visited = stack.Pop();
                    yield return visited;
                    foreach (var grandp in DirectPrecedentsOf(visited))
                        stack.Push(grandp);
                }
            }
        }
    }

    /// <summary>
    /// Enumerate through all the direct dependants and indirect dependants of the specified task
    /// </summary>
    /// <param name="precendent"></param>
    /// <returns></returns>
    public IEnumerable<T> DependantsOf(T precendent)
    {
        if (!_mDependantsOfPrecedent.ContainsKey(precendent)) yield break;

        var stack = new Stack<T>(20);
        foreach (var d in _mDependantsOfPrecedent[precendent])
        {
            stack.Push(d);
            while (stack.Count > 0)
            {
                var visited = stack.Pop();
                yield return visited;
                foreach (var grandd in _mDependantsOfPrecedent[visited])
                    stack.Push(grandd);
            }
        }
    }

    /// <summary>
    /// Enumerate through all the direct precedents of the specified task
    /// </summary>
    /// <param name="dependants"></param>
    /// <returns></returns>
    public IEnumerable<T> DirectPrecedentsOf(T dependants)
    {
        return _mDependantsOfPrecedent.Where(x => x.Value.Contains(dependants)).Select(x => x.Key);
    }

    /// <summary>
    /// /// Перечислить все прямые зависимости указанной задачи
    /// </summary>
    /// <param name="precedent"></param>
    /// <returns></returns>
    public IEnumerable<T> DirectDependantsOf(T precedent)
    {
        if (precedent == null) yield break;

        if (_mDependantsOfPrecedent.TryGetValue(precedent, out var dependants))
        {
            var iter = dependants.GetEnumerator();
            while (iter.MoveNext()) yield return iter.Current;
        }
    }

    /// <summary>
    /// Перечислить все задачи, которые являются предшественниками и имеют зависимые задачи.
    /// </summary>
    public List<T> Precedents
    {
        get
        {
            return _mDependantsOfPrecedent
                .Where(x => _mDependantsOfPrecedent[x.Key].Count > 0)
                .Select(x => x.Key)
                .ToList();
        }
        set => throw new NotImplementedException();
    }

    /// <summary>
    /// Enumerate list of critical paths in Project
    /// </summary>
    public List<IEnumerable<T>> CriticalPaths
    {
        get
        {
            var endtimelookup = new Dictionary<TimeSpan, List<T>>(1024);
            var maxEnd = TimeSpan.MinValue;
            foreach (var task in Tasks)
            {
                if (!endtimelookup.TryGetValue(task.End, out var list))
                    endtimelookup[task.End] = new List<T>(10);
                endtimelookup[task.End].Add(task);

                if (task.End > maxEnd) maxEnd = task.End;
            }

            var result = new List<IEnumerable<T>>();
            if (maxEnd != TimeSpan.MinValue)
            {
                foreach (var task in endtimelookup[maxEnd])
                {
                    result.Add(new T[] { task }.Concat(PrecedentsOf(task)));
                }
            }

            return result;
        }
        set => throw new NotImplementedException();
    }

    /// <summary>
    /// Get whether the specified task is a group
    /// </summary>
    /// <param name="task"></param>
    /// <returns></returns>
    public bool IsGroup(T task)
    {
        if (_mMembersOfGroup.TryGetValue(task, out var list))
            return list.Count > 0;
        else
            return false;
    }

    /// <summary>
    /// Get whether the specified task is a member
    /// </summary>
    /// <param name="task"></param>
    /// <returns></returns>
    public bool IsMember(T task)
    {
        return DirectGroupOf(task) != null;
    }

    /// <summary>
    /// Проверить, есть ли у указанной задачи связи: либо зависимые задачи, либо предшествующие задачи, связанные с ней.
    /// </summary>
    /// <param name="task"></param>
    /// <returns></returns>
    public bool HasRelations(T task)
    {
        if (_mRegister.Contains(task) && _mDependantsOfPrecedent.ContainsKey(task))
        {
            return _mDependantsOfPrecedent[task].Count > 0 || DirectPrecedentsOf(task).FirstOrDefault() != null;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Устанавливает связь между предшествующей и зависимой задачей
    /// </summary>
    /// <param name="precedent"></param>
    /// <param name="dependant"></param>
    public void Relate(T precedent, T dependant)
    {
        if (_mRegister.Contains(precedent)
            && _mRegister.Contains(dependant)
           )
        {
            if (_mSplitTaskOfPart.ContainsKey(precedent)) precedent = _mSplitTaskOfPart[precedent];
            if (_mSplitTaskOfPart.ContainsKey(dependant)) dependant = _mSplitTaskOfPart[dependant];

            if (!precedent.Equals(dependant)
                && !DependantsOf(dependant).Contains(precedent)
                && !IsGroup(precedent)
                && !IsGroup(dependant)
               )
            {
                _mDependantsOfPrecedent[precedent].Add(dependant);

                _RecalculateDependantsOf(precedent);
                _RecalculateAncestorsSchedule();
                _RecalculateSlack();
                
                OnScheduleChanged();
            }
        }
    }

    /// <summary>
    /// Unset the relation between the precedent and dependant task, if any.
    /// </summary>
    /// <param name="precedent"></param>
    /// <param name="dependant"></param>
    public void Unrelate(T precedent, T dependant)
    {
        if (_mRegister.Contains(precedent) && _mRegister.Contains(dependant))
        {
            if (_mSplitTaskOfPart.ContainsKey(precedent)) precedent = _mSplitTaskOfPart[precedent];
            if (_mSplitTaskOfPart.ContainsKey(dependant)) dependant = _mSplitTaskOfPart[dependant];

            _mDependantsOfPrecedent[precedent].Remove(dependant);

            _RecalculateSlack();
            
            OnScheduleChanged();
        }
    }

    /// <summary>
    /// Remove all dependant task from specified precedent task
    /// </summary>
    /// <param name="precedent"></param>
    public void Unrelate(T precedent)
    {
        if (_mRegister.Contains(precedent))
        {
            if (_mSplitTaskOfPart.ContainsKey(precedent))
                precedent = _mSplitTaskOfPart[precedent];

            _mDependantsOfPrecedent[precedent].Clear();

            _RecalculateSlack();
            
            OnScheduleChanged();
        }
    }

    /// <summary>
    /// Assign the specified resource to the specified task
    /// </summary>
    /// <param name="task"></param>
    /// <param name="resource"></param>
    public void Assign(T task, TR resource)
    {
        if (_mRegister.Contains(task) && !_mResourcesOfTask[task].Contains(resource))
        {
            _mResourcesOfTask[task].Add(resource);
            OnScheduleChanged();
        }
    }

    /// <summary>
    /// Unassign the specified resource from the specfied task
    /// </summary>
    /// <param name="task"></param>
    /// <param name="resource"></param>
    public void Unassign(T task, TR resource)
    {
        _mResourcesOfTask[task].Remove(resource);
        OnScheduleChanged();
    }

    /// <summary>
    /// Unassign the all resources from the specfied task
    /// </summary>
    /// <param name="task"></param>
    public void Unassign(T task)
    {
        if (_mRegister.Contains(task))
        {
            _mResourcesOfTask[task].Clear();
            OnScheduleChanged();
        }
    }

    /// <summary>
    /// Unassign the specified resource from all tasks that has this resource assigned
    /// </summary>
    /// <param name="resource"></param>
    public void Unassign(TR resource)
    {
        foreach (var r in _mResourcesOfTask.Where(x => x.Value.Contains(resource)))
            r.Value.Remove(resource);
        
        OnScheduleChanged();
    }

    /// <summary>
    /// Enumerate through all the resources that has been assigned to some task.
    /// </summary>
    public List<object> Resources
    {
        get
        {
            return _mResourcesOfTask
                .SelectMany(x => x.Value)
                .Distinct()
                .Cast<object>()
                .ToList();
        }
        set => throw new NotImplementedException();
    }

    /// <summary>
    /// Enumerate through all the resources that has been assigned to the specified task.
    /// </summary>
    /// <param name="task"></param>
    /// <returns></returns>
    public IEnumerable<TR> ResourcesOf(T task)
    {
        if (task == null || !_mRegister.Contains(task))
            yield break;

        if (_mResourcesOfTask.TryGetValue(task, out var list))
        {
            foreach (var item in list)
                yield return item;
        }
    }

    /// <summary>
    /// Перечислить все задачи, на которые назначен указанный ресурс.
    /// </summary>
    /// <param name="resource"></param>
    /// <returns></returns>
    public IEnumerable<T> TasksOf(TR resource)
    {
        return _mResourcesOfTask.Where(x => x.Value.Contains(resource)).Select(x => x.Key);
    }

    /// <summary>
    /// Set the start value. Affects group start/end and dependants start time.
    /// </summary>
    public void SetStart(T task, TimeSpan value)
    {
        if (_mRegister.Contains(task) && value != task.Start && !IsGroup(task))
        {
            _SetStartHelper(task, value);

            _RecalculateAncestorsSchedule();
            _RecalculateSlack();
            
            OnScheduleChanged();
        }
        // Set start for a group task
        else if (_mRegister.Contains(task) && value != task.Start && IsGroup(task))
        {
            _SetGroupStartHelper(task, value);

            _RecalculateAncestorsSchedule();
            _RecalculateSlack();
            
            OnScheduleChanged();
        }
    }

    /// <summary>
    /// Установите время окончания. Влияет на время завершения группы и время начала зависимых задач.
    /// </summary>
    public void SetEnd(T task, TimeSpan value)
    {
        if (!_mRegister.Contains(task) ||
            value == task.End ||
            IsGroup(task))
            return;
        _SetEndHelper(task, value);
        _RecalculateAncestorsSchedule();
        _RecalculateSlack();
        
        OnScheduleChanged();
    }

    /// <summary>
    /// Set the duration of the specified task from start to end.
    /// </summary>
    /// <param name="task"></param>
    /// <param name="duration">Number of timescale units between ProjectManager.Start</param>
    public void SetDuration(T task, TimeSpan duration)
    {
        // Округляем продолжительность до целых дней
        int wholeDays = (int)Math.Round(duration.TotalDays);
        TimeSpan roundedDuration = TimeSpan.FromDays(wholeDays);

        // Устанавливаем округленную продолжительность
        SetEnd(task, task.Start + roundedDuration);
    }

    /// <summary>
    /// Устанавливает процент выполнения указанной задачи от 0.0f до 1.0f.
    /// Не влияет на групповые задачи, так как они получают агрегированный процент выполнения всех дочерних задач.
    /// </summary>
    /// <param name="task"></param>
    /// <param name="complete"></param>
    public void SetComplete(T task, float complete)
    {
        if (!_mRegister.Contains(task)
            || !(Math.Abs(complete - task.Complete) > Tolerance)
            || IsGroup(task) || // not a group
            _mPartsOfSplitTask.ContainsKey(task)) return; // not a split task
        _SetCompleteHelper(task, complete);

        _RecalculateComplete();
        
        OnScheduleChanged();
    }

    /// <summary>
    /// Устанавливает, следует ли сворачивать указанную групповую задачу. На обычные задачи это не влияет.
    /// </summary>
    /// <param name="task"></param>
    /// <param name="collasped"></param>
    public void SetCollapse(T task, bool collasped)
    {
        if (_mRegister.Contains(task) && IsGroup(task))
        {
            task.IsCollapsed = collasped;
            OnScheduleChanged();
        }
    }

    /// <summary>
    /// Разделяе указанную задачу на последовательные части часть1 и часть2.
    /// </summary>
    /// <param name="task">The regular task to split which has duration of at least 2 to make two parts of 1 time unit duration each.</param>
    /// <param name="part1">New Task part (1) of the split task, with the start time of the original task and the specified duration value.</param>
    /// <param name="part2">New Task part (2) of the split task, starting 1 time unit after part (1) ends and having the remaining of the duration of the origina task.</param>
    /// <param name="duration">The duration of part (1) will be set to the specified duration value but will also be adjusted to approperiate value if necessary.</param>
    public void Split(T task, T part1, T part2, TimeSpan duration)
    {
        if (task == null
            || part1 == null
            || part2 == null
            || part1.Equals(part2) || ! // parts cannot be the same
                _mRegister.Contains(task) || // task must be registered
            _mPartsOfSplitTask.ContainsKey(task) || // task must not already be a split task
            _mSplitTaskOfPart.ContainsKey(task) || // task must not be a task part
            _mMembersOfGroup[task].Count != 0 || // task cannot be a group
            _mRegister.Contains(part1) || // part1 and part2 must have never existed
            _mRegister.Contains(part2))
            return;

        // Добавляем имена для частей задачи
        if (string.IsNullOrEmpty(part1.Name))
            part1.Name = $"{task.Name} (Part 1)";

        _mRegister.Add(part1); // register part1
        _mResourcesOfTask[part1] = new HashSet<TR>(); // create container for holding resource

        // add part1 to split task
        task.Complete = 0.0f; // reset the complete status
        var parts = _mPartsOfSplitTask[task] = new List<T>(2);
        parts.Add(part1);
        _mSplitTaskOfPart[part1] = task; // make a reverse lookup

        // allign the schedule
        if (duration <= TimeSpan.Zero || duration >= task.Duration)
            duration = TimeSpan.FromDays((int)(task.Duration.TotalDays / 2));
        part1.Start = task.Start;
        part1.End = task.End;
        part1.Duration = task.Duration;

        // Добавляем имя для part2 перед вызовом следующего Split
        if (string.IsNullOrEmpty(part2.Name))
            part2.Name = $"{task.Name} (Part 2)";

        // split part1 to give part2
        Split(part1, part2, duration);
    }

    /// <summary>
    /// Разделяет указанную часть и получает из нее другую часть.
    /// </summary>
    /// <param name="part">The task part to split which has duration of at least 2 to make two parts of 1 time unit duration each. Its duration will be set to the specified duration value.</param>
    /// <param name="other">New Task part of the original part, starting 1 time unit after it ends and having the remaining of the duration of the original part.</param>
    /// <param name="duration">The duration of part (1) will be set to the specified duration value but will also be adjusted to approperiate value if necessary.</param>
    public void Split(T part, T other, TimeSpan duration)
    {
        if (part == null
            || other == null
            || !_mSplitTaskOfPart.ContainsKey(part) || // part must be an existing part
            _mRegister.Contains(other)) return; // other must not have existed


        _mRegister.Add(other); // register other part
        _mResourcesOfTask[other] = new HashSet<TR>(); // create container for holding resource

        other.Name = part.Name + "spl1";

        var split = _mSplitTaskOfPart[part]; // get the split task
        var parts = _mPartsOfSplitTask[split]; // get the list of ordered parts

        parts.Insert(parts.IndexOf(part) + 1, other); // insert the other part after the existing part
        _mSplitTaskOfPart[other] = split; // set the reverse lookup

        System.Diagnostics.Debug.Write(
            "Project::Split(T part, T other, TimeSpan duration): Need to define minimum duration for splitting.");

        // limit the duration point within the split task duration
        if (duration <= TimeSpan.Zero || duration >= part.Duration)
            duration = TimeSpan.FromDays((int)(part.Duration.TotalDays / 2));

        // the real split
        var oneDuration = duration;
        var twoDuration = part.Duration - duration;
        part.Duration = oneDuration;
        part.End = part.Start + oneDuration;
        other.Duration = twoDuration;
        other.Start = part.End;
        other.End = other.Start + twoDuration;

        _PackPartsForward(parts);
        split.Start = parts.First().Start; // recalculate the split task
        split.End = parts.Last().End;
        split.Duration = split.End - split.Start;

        _RecalculateDependantsOf(split);
        _RecalculateAncestorsSchedule();
        
        OnScheduleChanged();
    }

    /// <summary>
    /// Объедините часть1 и часть2 в разделенной задаче в одну часть, представленную частью1, и часть2 будет удалена из ProjectManager.
    /// Результирующая часть будет иметь продолжительность, равную сумме продолжительностей двух частей.
    /// Часть1 и часть2 должны быть фактическими частями и должны быть последовательными частями в разделенной задаче.
    /// Если в результате объединения останется только одна часть, все части будут удалены, и разделенная задача станет обычной задачей.
    /// Расписание других частей не будет затронуто.
    /// TODO: Варианты объединения: EarlyStartLateEnd, EarlyStartEarlyEnd, LateStartLateEnd
    /// </summary>
    /// <param name="part1">The part to keep in the ProjectManager after the join completes successfully.</param>
    /// <param name="part2">The part to join into part1 and be deleted afterwards from the ProjectManager.</param>
    public void Join(T part1, T part2)
    {
        if (part1 == null
            || part2 == null
            || !_mSplitTaskOfPart.ContainsKey(part1) || ! // Часть1 и часть2 должны быть уже существующими частями
                _mSplitTaskOfPart.ContainsKey(part2)
            || _mSplitTaskOfPart[part1] != _mSplitTaskOfPart[part2])
            return; // Часть1 и часть2 должны принадлежать одной и той же разделенной задаче.
        var split = _mSplitTaskOfPart[part1];
        var parts = _mPartsOfSplitTask[split];
        if (parts.Count > 2)
        {
            //Объедините часть2 в часть1 и определите тип объединения.
            TimeSpan min;
            bool joinBackwards;
            if (part1.Start < part2.Start)
            {
                min = part1.Start;
                joinBackwards = true;
            }
            else
            {
                min = part2.Start;
                joinBackwards = false;
            }

            var duration = part1.Duration + part2.Duration;

            part1.Start = min;
            part1.Duration = duration;
            part1.End = min + duration;

            // объединить ресурсы
            // TODO: Спросить, нужно ли объединять ресурсы?
            foreach (var r in ResourcesOf(part2))
                Assign(part1, r);
            Unassign(part2);

            // remove all traces of part2
            // удалить все следы части2.
            parts.Remove(part2);
            _mResourcesOfTask.Remove(part2);
            _mSplitTaskOfPart.Remove(part2);
            _mRegister.Remove(part2);

            // pack the remaining parts
            if (joinBackwards) _PackPartsForward(parts);
            else _PackPartsBackwards(parts);

            // set the duration
            split.End = parts.Last().End;
            split.Duration = split.End - split.Start;
            split.Start = parts.First().Start;

            _RecalculateAncestorsSchedule();
            
            OnScheduleChanged();
        }
        else
        {
            Merge(split);
        }
    }

    /// <summary>
    ///Объедините все части разделенной задачи обратно в одну задачу, продолжительность которой будет равна сумме общей продолжительности отдельных частей задачи,
    /// и объедините ресурсы в результирующей задаче.        /// </summary>
    /// <param name="split">The split Task to merge</param>
    public void Merge(T split)
    {
        if (split != null
            && _mPartsOfSplitTask.ContainsKey(split) // must be existing split task
           )
        {
            var duration = TimeSpan.Zero;
            _mPartsOfSplitTask[split].ForEach(x =>
            {
                // sum durations
                duration += x.Duration;

                // merge resources onto split task
                foreach (var r in _mResourcesOfTask[x])
                    Assign(split, r);

                // remove traces of all parts
                _mSplitTaskOfPart.Remove(x);
                _mRegister.Remove(x);
                _mResourcesOfTask.Remove(x);
            });
            _mPartsOfSplitTask.Remove(split); // remove split as a split task

            // set the duration
            SetDuration(split, duration);
            
            // SetDuration вызовет OnScheduleChanged
        }
    }

    /// <summary>
    /// Получает части разделённой задачи
    /// </summary>
    /// <param name="split"></param>
    /// <returns></returns>
    public IEnumerable<T> PartsOf(T split)
    {
        if (split != null
            && _mPartsOfSplitTask.ContainsKey(split) // must be existing split task
           )
        {
            return _mPartsOfSplitTask[split].Select(x => x);
        }
        else
        {
            return new T[0];
        }
    }

    /// <summary>
    /// Получает разделенную задачу, к которой принадлежит указанная часть.
    /// </summary>
    /// <param name="part"></param>
    /// <returns></returns>
    public T SplitTaskOf(T part)
    {
        if (_mSplitTaskOfPart.ContainsKey(part))
            return _mSplitTaskOfPart[part];
        return null;
    }

    /// <summary>
    /// Определяет, является ли указанная задача разделенной задачей.
    /// </summary>
    /// <param name="task"></param>
    /// <returns></returns>
    public bool IsSplit(T task)
    {
        return task != null && _mPartsOfSplitTask.ContainsKey(task);
    }

    /// <summary>
    /// Get whether the specified task is a part of a split task
    /// </summary>
    /// <param name="task"></param>
    /// <returns></returns>
    public bool IsPart(T task)
    {
        return task != null && _mSplitTaskOfPart.ContainsKey(task);
    }

    /// <summary>
    ///Отсоедините указанную задачу от ProjectManager.Tasks (т.е. удалите из родительской группы, или, если у нее нет родительской группы, отмените регистрацию 
    ///статуса корневой задачи). Указанная задача останется зарегистрированной в ProjectManager.После выполнения этого вспомогательного метода ожидается,
    ///что задача будет повторно присоединена к ProjectManager.Tasks путем восстановления статуса корневой задачи или присоединения к новой группе.
    /// </summary>
    /// <param name="task"></param>
    private void _DetachTask(T task)
    {
        var group = DirectGroupOf(task);
        if (group == null) // member is actually not in any group, so it must be in _mRootTasks
            _mRootTasks.Remove(task);
        else
        {
            _mMembersOfGroup[group].Remove(task);
            _mGroupOfMember[task] = null;
        }
    }

    private void _SetStartHelper(T task, TimeSpan value)
    {
        if (task.Start == value) return;
        // Округляем значение до целых дней
        var wholeDays = (int)Math.Round(value.TotalDays);
        value = TimeSpan.FromDays(wholeDays);

        if (_mSplitTaskOfPart.ContainsKey(task))
        {
            // task part belonging to a split task needs special treatment
            _SetPartStartHelper(task, value);
        }
        else // regular task or a split task, which we will treat normally
        {
            // check out of bounds
            if (value < TimeSpan.Zero) value = TimeSpan.Zero;
            if (DirectPrecedentsOf(task).Any())
            {
                var maxEnd = DirectPrecedentsOf(task).Max(x => x.End);
                if (value <= maxEnd) value = maxEnd; // + One;
            }

            // save offset just in case we need to use for moving task parts
            var offset = value - task.Start;

            // cache value
            task.Start = value;
            // affect self
            task.End = task.Start + task.Duration;
            task.Duration = task.End - task.Start;

            // calculate dependants
            _RecalculateDependantsOf(task);

            // shift the task parts accordingly if task was a split task
            if (_mPartsOfSplitTask.ContainsKey(task))
            {
                _mPartsOfSplitTask[task].ForEach(x =>
                {
                    x.Start += offset;
                    x.End += offset;
                });
            }
        }
    }

    /// <summary>
    /// Set the start date for a group task. The relative dates between the tasks in the group will not be affected
    /// </summary>
    /// <param name="group"></param>
    /// <param name="value"></param>
    private void _SetGroupStartHelper(T group, TimeSpan value)
    {
        if (_mRegister.Contains(group) && value != group.Start && IsGroup(group))
        {
            var earlier = value < group.Start;
            var offset = value - group.Start;
            var decendants = earlier
                ? MembersOf(group).OrderBy((t) => t.Start)
                : MembersOf(group).OrderByDescending((t) => t.Start);

            foreach (var decendant in decendants)
            {
                if (IsGroup(decendant)) continue;

                decendant.Start += offset;
                decendant.End += offset;

                if (IsSplit(decendant))
                {
                    var parts = _mPartsOfSplitTask[decendant];
                    foreach (var part in parts)
                    {
                        part.Start += offset;
                        part.End += offset;
                    }
                }

                _RecalculateDependantsOf(decendant);
            }

            _RecalculateAncestorsSchedule();
            _RecalculateSlack();
        }
    }

    private void _SetEndHelper(T task, TimeSpan value)
    {
        if (task.End != value)
        {
            // Округляем значение до целых дней
            int wholeDays = (int)Math.Round(value.TotalDays);
            value = TimeSpan.FromDays(wholeDays);

            if (_mSplitTaskOfPart.ContainsKey(task))
            {
                // task part belonging to a split task needs special treatment
                _SetPartEndHelper(task, value);
            }
            else // regular task or a split task, which we will treat normally
            {
                // check bounds
                var isSplitTask = _mPartsOfSplitTask.ContainsKey(task);
                T lastPart = null;
                if (isSplitTask)
                {
                    lastPart = _mPartsOfSplitTask[task].Last();
                    if (value <= lastPart.Start) value = lastPart.Start + TimeSpan.FromDays(1); //тут было потеряно
                }
                
                if (value <= task.Start) value = task.Start + TimeSpan.FromDays(1);

                // УБРАНО: Логика сдвига Deadline
                // End теперь может свободно пересекать Deadline

                // assign end value
                task.End = value;
                task.Duration = task.End - task.Start;

                _RecalculateDependantsOf(task);

                if (isSplitTask)
                {
                    lastPart.End = value;
                    lastPart.Duration = lastPart.End - lastPart.Start;
                }
            }
        }
    }
    
    /// <summary>
    /// Устанавливает крайний срок (Deadline) для задачи.
    /// Deadline не может быть раньше End.
    /// </summary>
    public void SetDeadline(T task, TimeSpan? deadline)
    {
        if (!_mRegister.Contains(task)) return;
        
        // Группы не имеют собственного deadline
        if (IsGroup(task)) return;

        if (deadline.HasValue)
        {
            var wholeDays = (int)Math.Round(deadline.Value.TotalDays);
            var roundedDeadline = TimeSpan.FromDays(wholeDays);

            // Deadline не может быть раньше End
            // if (roundedDeadline < task.End)
            // {
            //     roundedDeadline = task.End;
            // }

            task.Deadline = roundedDeadline;
        }
        else
        {
            task.Deadline = null;
        }
        
        OnScheduleChanged();
    }
    
    /// <summary>
    /// Устанавливает заметку для задачи.
    /// </summary>
    public void SetNote(T task, string? note)
    {
        if (_mRegister.Contains(task))
        {
            task.Note = note;
            OnScheduleChanged();
        }
    }

    private void _SetPartStartHelper(T part, TimeSpan value)
    {
        var split = _mSplitTaskOfPart[part];
        var parts = _mPartsOfSplitTask[split];

        // check bounds
        if (DirectPrecedentsOf(split).Any())
        {
            var maxEnd = DirectPrecedentsOf(split).Max(x => x.End);
            if (value < maxEnd) value = maxEnd;
        }

        if (value < TimeSpan.Zero) value = TimeSpan.Zero;

        // flag whether we need to pack parts forward or backwards
        var backwards = value < part.Start;

        // assign start value, maintining duration and modifying end
        var duration = part.End - part.Start;
        part.Start = value;
        part.End = value + duration;

        // pack packs
        if (backwards) _PackPartsBackwards(parts);
        else _PackPartsForward(parts);

        // recalculate the split
        split.Start = parts.First().Start; // recalculate the split task
        split.End = parts.Last().End;
        split.Duration = split.End - split.Start;

        _RecalculateDependantsOf(split);
    }

    private void _SetPartEndHelper(T part, TimeSpan value)
    {
        var split = _mSplitTaskOfPart[part];
        var parts = _mPartsOfSplitTask[split];

        // check for bounds
        if (value <= part.Start) value = part.Start + TimeSpan.FromDays(1);

        // flag whether duration is increased or reduced
        var increased = value > part.End;

        // set end value and duration
        part.End = value;
        part.Duration = part.End - part.Start;

        // pack parts
        if (increased) _PackPartsForward(parts);

        // recalculate the split
        split.Start = parts.First().Start; // recalculate the split task
        split.End = parts.Last().End;
        split.Duration = split.End - split.Start;

        _RecalculateDependantsOf(split);
    }

    private void _PackPartsBackwards(List<T> parts)
    {
        // pack backwards first before packing forward again
        for (var i = parts.Count - 2; i > 0; i--) // Cannot pack beyond first part (i > 0)
        {
            var earlier = parts[i];
            var later = parts[i + 1];
            if (later.Start <= earlier.End)
            {
                earlier.End = later.Start;
                earlier.Start = earlier.End - earlier.Duration;
            }
        }

        _PackPartsForward(parts);
    }

    private void _PackPartsForward(List<T> parts)
    {
        for (var i = 1; i < parts.Count; i++)
        {
            var current = parts[i];
            var previous = parts[i - 1];
            if (previous.End >= current.Start)
            {
                current.Start = previous.End;
                current.End = current.Start + current.Duration;
            }
        }
    }

    private void _SetCompleteHelper(T task, float value)
    {
        if (Math.Abs(task.Complete - value) > TOLERANCE)
        {
            if (value > 1) value = 1;
            else if (value < 0) value = 0;
            task.Complete = value;

            if (_mSplitTaskOfPart.ContainsKey(task))
            {
                var split = _mSplitTaskOfPart[task];
                var parts = _mPartsOfSplitTask[split];
                float complete = 0;
                var duration = TimeSpan.Zero;
                foreach (var part in parts)
                {
                    complete += part.Complete * part.Duration.Ticks;
                    duration += part.Duration;
                }

                split.Complete = complete / duration.Ticks;
            }
        }
    }

    private const float TOLERANCE = float.MinValue;

    private void _RecalculateComplete()
    {
        var groups = new Stack<T>();
        foreach (var task in _mRootTasks.Where(x => IsGroup(x)))
        {
            _RecalculateCompletedHelper(task);
        }
    }

    private float _RecalculateCompletedHelper(T groupOrSplit)
    {
        float tComplete = 0;
        var tDuration = TimeSpan.Zero;

        if (_mPartsOfSplitTask.ContainsKey(groupOrSplit))
        {
            foreach (var part in _mPartsOfSplitTask[groupOrSplit])
            {
                tComplete += part.Complete * part.Duration.Ticks;
                tDuration += part.Duration;
            }
        }
        else
        {
            foreach (var member in DirectMembersOf(groupOrSplit))
            {
                tDuration += member.Duration;
                if (IsGroup(member)) tComplete += _RecalculateCompletedHelper(member) * member.Duration.Ticks;
                else tComplete += member.Complete * member.Duration.Ticks;
            }
        }

        groupOrSplit.Complete = tComplete / tDuration.Ticks;


        return groupOrSplit.Complete;
    }

    private void _RecalculateDependantsOf(T precedent)
    {
        // affect decendants
        foreach (var dependant in DirectDependantsOf(precedent))
        {
            if (dependant.Start < precedent.End)
                _SetStartHelper(dependant, precedent.End);
        }
    }

    private void _RecalculateAncestorsSchedule()
    {
        // affects parent group
        foreach (var group in _mRootTasks.Where(x => IsGroup(x)))
        {
            _RecalculateAncestorsScheduleHelper(group);
        }
    }

    private void _RecalculateAncestorsScheduleHelper(T group)
    {
        float tComplete = 0;
        var tDuration = TimeSpan.Zero;
        var start = TimeSpan.MaxValue;
        var end = TimeSpan.MinValue;
        foreach (var member in DirectMembersOf(group))
        {
            if (IsGroup(member))
                _RecalculateAncestorsScheduleHelper(member);

            tDuration += member.Duration;
            tComplete += member.Complete * member.Duration.Ticks;
            if (member.Start < start) start = member.Start;
            if (member.End > end) end = member.End;
        }

        _SetStartHelper(group, start);
        _SetEndHelper(group, end);
        _SetCompleteHelper(group, tComplete / tDuration.Ticks);
    }

    private void _RecalculateSlack()
    {
        var maxEnd = Tasks.Max(x => x.End);
        foreach (var task in Tasks)
        {
            // affects slack for current task
            if (DirectDependantsOf(task).Any())
            {
                // slack until the earliest dependant needs to start
                var min = DirectDependantsOf(task).Min(x => x.Start);
                task.Slack = min - task.End;
            }
            else
            {
                // no dependants, so we have all the time until the last task ends
                task.Slack = maxEnd - task.End;
            }
        }
    }
    
}