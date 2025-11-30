using Core.Models;
using Core.Services;

namespace Core.Models.DTOs;

[Serializable]
public class ProjectManagerData
{
    public string Now { get; set; } = "0.00:00:00";
    public DateTime Start { get; set; }
    public List<TaskData> Tasks { get; set; } = new();
    public List<SplitTaskRelation> SplitTasks { get; set; } = new();
    public List<GroupRelation> GroupTasks { get; set; } = new();
    public List<ResourceData> Resources { get; set; } = new();
    public List<ResourceAssignmentData> ResourceAssignments { get; set; } = new();

    /// <summary>
    /// Интервалы участия ресурсов (новое поле).
    /// </summary>
    public List<ParticipationIntervalData> ParticipationIntervals { get; set; } = new();

    /// <summary>
    /// Отсутствия ресурсов (новое поле).
    /// </summary>
    public List<AbsenceData> Absences { get; set; } = new();

    /// <summary>
    /// Версия формата данных для миграции.
    /// </summary>
    public int FormatVersion { get; set; } = 2;

    // Метод для преобразования в ProjectManager
    public ProjectManager<MyTask, object> ToProjectManager()
    {
        var manager = new ProjectManager<MyTask, object>
        {
            // Установка базовых свойств
            Start = Start
        };

        // Попытка установить Now через рефлексию, если в оригинальном классе это поле
        if (Now != null)
        {
            var nowValue = Now;
            if (nowValue.StartsWith("-"))
                nowValue = nowValue.Substring(1);

            if (TimeSpan.TryParse(nowValue, out var nowTimeSpan))
            {
                manager.Now = nowTimeSpan;
            }
        }

        // Словарь для связывания имени задачи с объектом задачи
        Dictionary<Guid, MyTask> tasksById = new Dictionary<Guid, MyTask>();


        // Преобразование задач из JSON
        foreach (var taskData in Tasks)
        {
            var task = new MyTask
            {
                Id = taskData.Id, // Устанавливаем ID из данных!
                Name = taskData.Name,
                Complete = taskData.Complete,
                IsCollapsed = taskData.IsCollapsed
            };

            // Преобразование временных интервалов
            ParseTimeSpan(taskData.Start, out var startTime);
            ParseTimeSpan(taskData.End, out var endTime);
            ParseTimeSpan(taskData.Duration, out var duration);

            task.Start = startTime;
            task.End = endTime;
            task.Duration = duration;

            // Добавление задачи через метод Add (важно!)
            manager.Add(task);

            // Сохраняем ссылку на задачу по имени
            tasksById[task.Id] = task;
        }

        // Восстановление отношений разделенных задач
        if (SplitTasks is { Count: > 0 })
        {
            // Создаем временные структуры для хранения информации о задачах
            var splitTaskInfos = new Dictionary<Guid, List<SplitTaskRelation>>();

            // Группируем разделенные задачи по ID основной задачи
            foreach (var relation in SplitTasks)
            {
                if (!splitTaskInfos.ContainsKey(relation.SplitTaskId))
                    splitTaskInfos[relation.SplitTaskId] = new List<SplitTaskRelation>();

                splitTaskInfos[relation.SplitTaskId].Add(relation);
            }

            // Обрабатываем каждую разделенную задачу
            foreach (var kvp in splitTaskInfos)
            {
                Guid splitTaskId = kvp.Key;
                var partInfos = kvp.Value.OrderBy(p =>
                {
                    ParseTimeSpan(p.PartStart, out var time);
                    return time;
                }).ToList();

                // Находим основную задачу
                if (tasksById.TryGetValue(splitTaskId, out MyTask splitTask))
                {
                    if (partInfos.Count >= 2)
                    {
                        // Получаем информацию о первых двух частях
                        var firstPartInfo = partInfos[0];
                        var secondPartInfo = partInfos[1];

                        // Создаем первые две части
                        var firstPart = new MyTask();
                        var secondPart = new MyTask();

                        // Используем метод Split для создания разделенной задачи
                        ParseTimeSpan(firstPartInfo.PartDuration, out var firstDuration);
                        manager.Split(splitTask, firstPart, secondPart, firstDuration);

                        // Устанавливаем имена и процент выполнения для частей
                        firstPart.Name = firstPartInfo.PartName;
                        firstPart.Complete = firstPartInfo.PartComplete;

                        secondPart.Name = secondPartInfo.PartName;
                        secondPart.Complete = secondPartInfo.PartComplete;

                        // Если есть больше двух частей, создаем их последовательно
                        MyTask lastPart = secondPart;
                        for (int i = 2; i < partInfos.Count; i++)
                        {
                            var partInfo = partInfos[i];
                            var newPart = new MyTask();

                            // Определяем продолжительность части
                            ParseTimeSpan(partInfo.PartDuration, out var partDuration);

                            // Разделяем последнюю часть
                            manager.Split(lastPart, newPart, partDuration);

                            // Устанавливаем имя и процент выполнения
                            newPart.Name = partInfo.PartName;
                            newPart.Complete = partInfo.PartComplete;

                            // Переходим к следующей части
                            lastPart = newPart;
                        }
                    }
                }
            }
        }

        return manager;
    }

    private bool ParseTimeSpan(string timeString, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        // Проверка на пустую строку
        if (string.IsNullOrEmpty(timeString))
            return false;

        try
        {
            // Обработка формата с днями (например, "3.12:34:56")
            if (timeString.Contains("."))
            {
                var parts = timeString.Split(new[] { '.' }, 2);

                // Парсим дни как целое число
                if (int.TryParse(parts[0], out var days))
                {
                    // Создаем TimeSpan на основе дней
                    result = TimeSpan.FromDays(days);
                    return true;
                }
            }

            // Пробуем стандартный TimeSpan.Parse, но округляем до дней
            if (TimeSpan.TryParse(timeString, out var parsedTs))
            {
                int days = (int)Math.Round(parsedTs.TotalDays);
                result = TimeSpan.FromDays(days);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
