using System.IO;
using System.Windows;
using Core.Models;
using Core.Models.DTOs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Task = Core.Interfaces.Task;

namespace Core.Services;

/// <summary>
/// Сервис для сохранения и загрузки проектов Gantt Chart в формате JSON.
/// Поддерживает сериализацию задач, сплитов, групп, ресурсов, интервалов участия, отсутствий и праздников.
/// </summary>
public class FileService
{
    /// <summary>
    /// Текущая версия формата файла.
    /// </summary>
    private const int CurrentFormatVersion = 3;

    /// <summary>
    /// Ссылка на ResourceService для сохранения/загрузки ресурсов.
    /// Может быть null — в этом случае ресурсы не сохраняются/не загружаются.
    /// </summary>
    public ResourceService? ResourceService { get; set; }

    /// <summary>
    /// Ссылка на ProductionCalendarService для сохранения/загрузки праздников.
    /// Может быть null — в этом случае праздники не сохраняются/не загружаются.
    /// </summary>
    public ProductionCalendarService? ProductionCalendarService { get; set; }

    /// <summary>
    /// Сохраняет проект в JSON файл.
    /// </summary>
    public void Save(ProjectManager manager, string filePath)
    {
        try
        {
            var managerData = new ProjectManagerData
            {
                FormatVersion = CurrentFormatVersion,
                Now = manager.Now.ToString(),
                Start = manager.Start,
                Tasks = new List<TaskData>(),
                SplitTasks = manager.GetSplitTaskRelations(),
                GroupTasks = manager.GetGroupRelations()
            };

            // Заполняем задачи
            foreach (var task in manager.Tasks)
            {
                var taskData = new TaskData
                {
                    Id = task.Id,
                    Name = task.Name,
                    Start = task.Start.ToString(),
                    End = task.End.ToString(),
                    Duration = task.Duration.ToString(),
                    Complete = task.Complete,
                    IsCollapsed = task.IsCollapsed,
                    Deadline = task.Deadline?.ToString(),
                    Note = task.Note
                };
                managerData.Tasks.Add(taskData);
            }

            // Добавляем ресурсы, назначения, интервалы и отсутствия
            if (ResourceService != null)
            {
                try
                {
                    var (resources, assignments, intervals, absences) = ResourceService.GetAllDataExtended();

                    // Ресурсы (с новым форматом Role как int)
                    managerData.Resources = resources.Select(r => r.ToData()).ToList();

                    // Назначения
                    managerData.ResourceAssignments = assignments.Select(a => a.ToData()).ToList();

                    // Интервалы участия
                    managerData.ParticipationIntervals = intervals
                        .Select(ParticipationIntervalData.FromDomain)
                        .ToList();

                    // Отсутствия
                    managerData.Absences = absences
                        .Select(AbsenceData.FromDomain)
                        .ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("ResourceService.GetAllDataExtended failed: " + ex.Message);
                }
            }

            // Добавляем праздники
            if (ProductionCalendarService != null)
            {
                try
                {
                    var holidays = ProductionCalendarService.GetAllHolidays();
                    managerData.Holidays = holidays
                        .Select(HolidayData.FromDomain)
                        .ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("ProductionCalendarService.GetAllHolidays failed: " + ex.Message);
                }
            }

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            var jsonString = JsonConvert.SerializeObject(managerData, settings);
            File.WriteAllText(filePath, jsonString);

            MessageBox.Show($"Сохранено {managerData.Tasks.Count} задач в файл.",
                "Gantt Chart", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения файла: {ex.Message}",
                "Gantt Chart", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    /// <summary>
    /// Загружает проект из JSON файла.
    /// </summary>
    public ProjectManager Load(string filePath)
    {
        try
        {
            var jsonString = File.ReadAllText(filePath);

            // Сначала парсим как JObject для определения версии
            var jsonObject = JObject.Parse(jsonString);
            var formatVersion = jsonObject["FormatVersion"]?.Value<int>() ?? 1;

            var managerData = JsonConvert.DeserializeObject<ProjectManagerData>(jsonString);

            // Миграция при необходимости
            if (formatVersion < CurrentFormatVersion)
            {
                MigrateData(managerData, jsonObject, formatVersion);
            }

            // Проверка старого формата без ID
            if (managerData.Tasks.All(t => t.Id == Guid.Empty))
            {
                MessageBox.Show(
                    "Это старый файл без поддержки ID задач.\n" +
                    "Система автоматически сгенерирует новые ID.",
                    "Миграция данных",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                foreach (var task in managerData.Tasks)
                    task.Id = Guid.NewGuid();

                foreach (var split in managerData.SplitTasks)
                {
                    split.PartId = Guid.NewGuid();
                    split.SplitTaskId = managerData.Tasks.First().Id;
                }

                managerData.GroupTasks.Clear();
            }

            var manager = new ProjectManager
            {
                Start = managerData.Start
            };

            if (TimeSpan.TryParse(managerData.Now, out var nowSpan))
                manager.Now = nowSpan;

            Dictionary<Guid, Task> tasksById = new Dictionary<Guid, Task>();

            // Восстанавливаем задачи
            foreach (var taskData in managerData.Tasks)
            {
                var task = new MyTask(manager)
                {
                    Id = taskData.Id,
                    Name = taskData.Name,
                    IsCollapsed = taskData.IsCollapsed,
                    Note = taskData.Note
                };

                manager.Add(task);

                if (TimeSpan.TryParse(taskData.Start, out var start) &&
                    TimeSpan.TryParse(taskData.Duration, out var duration))
                {
                    task.Start = start;
                    task.Duration = duration;
                    task.End = start + duration;
                }
                manager.SetComplete(task, taskData.Complete);
                
                if (!string.IsNullOrEmpty(taskData.Deadline) && TimeSpan.TryParse(taskData.Deadline, out var deadline))
                    task.Deadline = deadline;

                if (!tasksById.ContainsKey(task.Id))
                    tasksById[task.Id] = task;
            }

            // Восстанавливаем сплиты
            if (managerData.SplitTasks.Count > 0)
            {
                var splitTaskInfos = new Dictionary<Guid, List<SplitTaskRelation>>();

                foreach (var relation in managerData.SplitTasks)
                {
                    if (!splitTaskInfos.ContainsKey(relation.SplitTaskId))
                        splitTaskInfos[relation.SplitTaskId] = new List<SplitTaskRelation>();
                    splitTaskInfos[relation.SplitTaskId].Add(relation);
                }

                foreach (var kvp in splitTaskInfos)
                {
                    Guid splitTaskId = kvp.Key;
                    var partInfos = kvp.Value
                        .OrderBy(p =>
                        {
                            TimeSpan.TryParse(p.PartStart, out var time);
                            return time;
                        }).ToList();

                    if (tasksById.TryGetValue(splitTaskId, out Task splitTask) && partInfos.Count >= 2)
                    {
                        var firstPart = new MyTask(manager) { Id = partInfos[0].PartId };
                        var secondPart = new MyTask(manager) { Id = partInfos[1].PartId };

                        manager.Split(splitTask, firstPart, secondPart, TimeSpan.FromDays(1));

                        tasksById[firstPart.Id] = firstPart;
                        tasksById[secondPart.Id] = secondPart;

                        Task lastPart = secondPart;
                        var allParts = new List<Task> { firstPart, secondPart };

                        for (int j = 2; j < partInfos.Count; j++)
                        {
                            var newPart = new MyTask(manager) { Id = partInfos[j].PartId };
                            manager.Split(lastPart, newPart, TimeSpan.FromDays(1));
                            allParts.Add(newPart);
                            tasksById[newPart.Id] = newPart;
                            lastPart = newPart;
                        }

                        // Устанавливаем свойства частей
                        for (int i = 0; i < allParts.Count; i++)
                        {
                            var part = allParts[i];
                            var partInfo = partInfos[i];

                            part.Name = partInfo.PartName;
                            manager.SetComplete(part, partInfo.PartComplete);

                            if (TimeSpan.TryParse(partInfo.PartStart, out var st) &&
                                TimeSpan.TryParse(partInfo.PartDuration, out var dur))
                            {
                                part.Start = st;
                                part.Duration = dur;
                                part.End = st + dur;
                                
                            }
                        }
                    }
                }
            }

            // Восстанавливаем группы
            if (managerData.GroupTasks != null)
            {
                foreach (var relation in managerData.GroupTasks)
                {
                    if (tasksById.TryGetValue(relation.GroupId, out var group) &&
                        tasksById.TryGetValue(relation.MemberId, out var member))
                    {
                        manager.Group(group, member);
                    }
                }
            }

            // Загружаем ресурсы
            if (ResourceService != null && managerData.Resources != null)
            {
                try
                {
                    // Загружаем ресурсы (с учётом миграции роли)
                    var resources = managerData.Resources
                        .Select(r => Resource.FromData(r))
                        .Where(r => r != null)
                        .ToList()!;

                    ResourceService.LoadResources(resources);

                    // Загружаем назначения
                    if (managerData.ResourceAssignments != null)
                    {
                        var assignments = managerData.ResourceAssignments
                            .Select(a => ResourceAssignment.FromData(a))
                            .Where(a => a != null)
                            .ToList()!;

                        ResourceService.LoadAssignments(assignments);
                    }

                    // Загружаем интервалы участия
                    if (managerData.ParticipationIntervals != null && managerData.ParticipationIntervals.Count > 0)
                    {
                        var intervals = managerData.ParticipationIntervals
                            .Select(i => i.ToDomain())
                            .ToList();

                        ResourceService.LoadParticipationIntervals(intervals);
                    }
                    else
                    {
                        // Миграция: создаём дефолтные интервалы
                        ResourceService.EnsureDefaultParticipationIntervals();
                    }

                    // Загружаем отсутствия
                    if (managerData.Absences != null && managerData.Absences.Count > 0)
                    {
                        var absences = managerData.Absences
                            .Select(a => a.ToDomain())
                            .ToList();

                        ResourceService.LoadAbsences(absences);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Loading resources failed: " + ex.Message);
                }
            }

            // Загружаем праздники
            if (ProductionCalendarService != null && managerData.Holidays != null && managerData.Holidays.Count > 0)
            {
                try
                {
                    var holidays = managerData.Holidays
                        .Select(h => h.ToDomain())
                        .ToList();

                    ProductionCalendarService.LoadHolidays(holidays);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Loading holidays failed: " + ex.Message);
                }
            }

            return manager;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки файла: {ex.Message}",
                "Gantt Chart", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    /// <summary>
    /// Мигрирует данные из старого формата в новый.
    /// </summary>
    /// <param name="data">Десериализованные данные.</param>
    /// <param name="jsonObject">Исходный JSON для чтения legacy полей.</param>
    /// <param name="fromVersion">Версия исходного формата.</param>
    private void MigrateData(ProjectManagerData data, JObject jsonObject, int fromVersion)
    {
        if (fromVersion < 2)
        {
            // Миграция с версии 1: Role был string, MaxWorkload был в Resource
            var resourcesArray = jsonObject["Resources"] as JArray;
            if (resourcesArray != null)
            {
                for (int i = 0; i < resourcesArray.Count && i < data.Resources.Count; i++)
                {
                    var resourceJson = resourcesArray[i] as JObject;
                    if (resourceJson != null)
                    {
                        // Читаем старый string Role
                        var roleString = resourceJson["Role"]?.Value<string>();
                        if (!string.IsNullOrEmpty(roleString) && !int.TryParse(roleString, out _))
                        {
                            // Это строковая роль — конвертируем
                            data.Resources[i].Role = (int)ResourceRoleExtensions.ParseFromString(roleString);
                        }

                        // MaxWorkload из Resource уже не используется,
                        // но можем создать ParticipationInterval с этим значением
                        var maxWorkload = resourceJson["MaxWorkload"]?.Value<int>() ?? 100;

                        // Создаём интервал участия если его нет
                        var resourceId = data.Resources[i].Id;
                        if (!data.ParticipationIntervals.Any(p => p.ResourceId == resourceId))
                        {
                            data.ParticipationIntervals.Add(new ParticipationIntervalData
                            {
                                Id = Guid.NewGuid(),
                                ResourceId = resourceId,
                                StartDays = 0,
                                EndDays = null,
                                MaxWorkload = maxWorkload,
                                CreatedAt = DateTime.Now
                            });
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"Migrated data from version {fromVersion} to 2");
        }

        if (fromVersion < 3)
        {
            // Миграция с версии 2 на 3: добавлены праздники
            // Праздники — пустой список по умолчанию, миграция не требуется
            if (data.Holidays == null)
            {
                data.Holidays = new List<HolidayData>();
            }
            System.Diagnostics.Debug.WriteLine($"Migrated data from version {fromVersion} to 3");
        }
    }
}