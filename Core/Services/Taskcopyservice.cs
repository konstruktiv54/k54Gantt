// using Core.Interfaces;
// using Core.Models;
// using Task = Core.Interfaces.Task;
//
// // Extension методы (GetTaskById и др.)
//
// namespace Core.Services;
//
// /// <summary>
// /// Сервис копирования и вставки задач.
// /// Поддерживает глубокое копирование групп и split-задач.
// /// </summary>
// public class TaskCopyService
// {
//     #region Constants
//
//     /// <summary>
//     /// Суффикс для имени копии.
//     /// </summary>
//     private const string CopySuffix = "1";
//
//     #endregion
//
//     #region Public Methods - Copy
//
//     /// <summary>
//     /// Создаёт снимок задачи для буфера обмена.
//     /// Рекурсивно копирует дочерние задачи для групп.
//     /// </summary>
//     /// <param name="task">Задача для копирования.</param>
//     /// <param name="manager">ProjectManager для получения иерархии.</param>
//     /// <returns>Буфер обмена с одним снимком.</returns>
//     public TaskClipboard Copy(Task task, ProjectManager manager)
//     {
//         if (task == null)
//             throw new ArgumentNullException(nameof(task));
//         if (manager == null)
//             throw new ArgumentNullException(nameof(manager));
//
//         var snapshot = CreateSnapshot(task, manager);
//         var parentId = manager.DirectGroupOf(task)?.Id;
//
//         return TaskClipboard.Single(snapshot, parentId, task.Id);
//     }
//
//     /// <summary>
//     /// Создаёт снимки нескольких задач для буфера обмена.
//     /// </summary>
//     /// <param name="tasks">Задачи для копирования.</param>
//     /// <param name="manager">ProjectManager для получения иерархии.</param>
//     /// <returns>Буфер обмена с несколькими снимками.</returns>
//     public TaskClipboard CopyMultiple(IEnumerable<Task> tasks, ProjectManager manager)
//     {
//         if (tasks == null)
//             throw new ArgumentNullException(nameof(tasks));
//         if (manager == null)
//             throw new ArgumentNullException(nameof(manager));
//
//         var items = tasks.Select(task =>
//         {
//             var snapshot = CreateSnapshot(task, manager);
//             var parentId = manager.DirectGroupOf(task)?.Id;
//             return (snapshot, parentId, task.Id);
//         });
//
//         return TaskClipboard.Multiple(items);
//     }
//
//     #endregion
//
//     #region Public Methods - Paste
//
//     /// <summary>
//     /// Вставляет задачи из буфера обмена.
//     /// </summary>
//     /// <param name="clipboard">Буфер обмена.</param>
//     /// <param name="manager">ProjectManager для добавления задач.</param>
//     /// <param name="insertAfterTask">Задача, после которой вставить (null = в конец).</param>
//     /// <returns>Список созданных корневых задач.</returns>
//     public List<Task> Paste(TaskClipboard clipboard, ProjectManager manager, Task? insertAfterTask = null)
//     {
//         if (clipboard == null || !clipboard.HasData)
//             throw new ArgumentException("Clipboard is empty", nameof(clipboard));
//         if (manager == null)
//             throw new ArgumentNullException(nameof(manager));
//
//         var createdTasks = new List<Task>();
//
//         // Используем batch update для оптимизации
//         manager.BatchUpdate(() =>
//         {
//             for (var i = 0; i < clipboard.Snapshots.Count; i++)
//             {
//                 var snapshot = clipboard.Snapshots[i];
//                 var sourceParentId = clipboard.GetSourceParentId(i);
//
//                 // Определяем куда вставлять
//                 var targetParent = sourceParentId.HasValue
//                     ? manager.GetTaskById(sourceParentId.Value)
//                     : null;
//
//                 // Создаём задачу из снимка
//                 var newTask = CreateTaskFromSnapshot(snapshot, manager, targetParent);
//                 createdTasks.Add(newTask);
//
//                 // Позиционируем после указанной задачи
//                 if (insertAfterTask != null)
//                 {
//                     PositionTaskAfter(newTask, insertAfterTask, manager);
//                     // Следующие задачи вставляем после только что созданной
//                     insertAfterTask = newTask;
//                 }
//             }
//         });
//
//         return createdTasks;
//     }
//
//     /// <summary>
//     /// Вставляет одну задачу из буфера (упрощённая версия).
//     /// </summary>
//     public Task? PasteSingle(TaskClipboard clipboard, ProjectManager manager, Task? insertAfterTask = null)
//     {
//         var tasks = Paste(clipboard, manager, insertAfterTask);
//         return tasks.FirstOrDefault();
//     }
//
//     #endregion
//
//     #region Private Methods - Snapshot Creation
//
//     /// <summary>
//     /// Создаёт снимок задачи с рекурсивным копированием детей.
//     /// </summary>
//     private TaskSnapshot CreateSnapshot(Task task, ProjectManager manager)
//     {
//         var snapshot = new TaskSnapshot
//         {
//             Name = task.Name + CopySuffix,
//             Start = task.Start,
//             End = task.End,
//             Duration = task.Duration,
//             Complete = task.Complete,
//             Deadline = task.Deadline,
//             Note = task.Note,
//             IsCollapsed = task.IsCollapsed,
//             IsSplit = manager.IsSplit(task)
//         };
//
//         // Копируем дочерние задачи для групп
//         if (manager.IsGroup(task))
//         {
//             foreach (var member in manager.DirectMembersOf(task))
//             {
//                 var childSnapshot = CreateSnapshot(member, manager);
//                 snapshot.Children.Add(childSnapshot);
//             }
//         }
//
//         // Копируем split-части
//         if (manager.IsSplit(task))
//         {
//             foreach (var part in manager.PartsOf(task))
//             {
//                 var partSnapshot = new TaskSnapshot
//                 {
//                     Name = part.Name + CopySuffix,
//                     Start = part.Start,
//                     End = part.End,
//                     Duration = part.Duration,
//                     Complete = part.Complete
//                 };
//                 snapshot.SplitParts.Add(partSnapshot);
//             }
//         }
//
//         return snapshot;
//     }
//
//     #endregion
//
//     #region Private Methods - Task Creation
//
//     /// <summary>
//     /// Создаёт задачу из снимка и добавляет в ProjectManager.
//     /// Рекурсивно создаёт дочерние задачи.
//     /// </summary>
//     private Task CreateTaskFromSnapshot(TaskSnapshot snapshot, ProjectManager manager, Task? parentGroup)
//     {
//         // Создаём основную задачу
//         var newTask = new Task
//         {
//             Id = Guid.NewGuid(),
//             Name = snapshot.Name,
//             Start = snapshot.Start,
//             End = snapshot.End,
//             Duration = snapshot.Duration,
//             Complete = snapshot.Complete,
//             Deadline = snapshot.Deadline,
//             Note = snapshot.Note,
//             IsCollapsed = snapshot.IsCollapsed
//         };
//
//         // Добавляем в ProjectManager
//         manager.Add(newTask);
//
//         // Добавляем в группу-родитель если есть
//         if (parentGroup != null)
//         {
//             manager.Group(parentGroup, newTask);
//         }
//
//         // Устанавливаем время (после добавления в manager)
//         manager.SetStart(newTask, snapshot.Start);
//         manager.SetDuration(newTask, snapshot.Duration);
//         manager.SetComplete(newTask, snapshot.Complete);
//
//         // Рекурсивно создаём дочерние задачи
//         if (snapshot.Children.Count > 0)
//         {
//             foreach (var childSnapshot in snapshot.Children)
//             {
//                 CreateTaskFromSnapshot(childSnapshot, manager, newTask);
//             }
//         }
//
//         // Воссоздаём split-структуру
//         if (snapshot.IsSplit && snapshot.SplitParts.Count >= 2)
//         {
//             RecreateSplitStructure(newTask, snapshot.SplitParts, manager);
//         }
//
//         return newTask;
//     }
//
//     /// <summary>
//     /// Воссоздаёт split-структуру задачи.
//     /// </summary>
//     private void RecreateSplitStructure(Task splitTask, List<TaskSnapshot> parts, ProjectManager manager)
//     {
//         if (parts.Count < 2)
//             return;
//
//         // Создаём первые две части
//         var part1 = new Task
//         {
//             Id = Guid.NewGuid(),
//             Name = parts[0].Name
//         };
//
//         var part2 = new Task
//         {
//             Id = Guid.NewGuid(),
//             Name = parts[1].Name
//         };
//
//         // Split создаёт структуру и устанавливает времена
//         manager.Split(splitTask, part1, part2, parts[0].Duration);
//
//         // Устанавливаем complete для частей
//         manager.SetComplete(part1, parts[0].Complete);
//         manager.SetComplete(part2, parts[1].Complete);
//
//         // Добавляем остальные части
//         var lastPart = part2;
//         for (var i = 2; i < parts.Count; i++)
//         {
//             var nextPartSnapshot = parts[i];
//             var nextPart = new Task
//             {
//                 Id = Guid.NewGuid(),
//                 Name = nextPartSnapshot.Name
//             };
//
//             manager.Split(lastPart, nextPart, lastPart.Duration / 2);
//             
//             // Корректируем времена
//             manager.SetStart(nextPart, nextPartSnapshot.Start);
//             manager.SetDuration(nextPart, nextPartSnapshot.Duration);
//             manager.SetComplete(nextPart, nextPartSnapshot.Complete);
//
//             lastPart = nextPart;
//         }
//     }
//
//     /// <summary>
//     /// Позиционирует задачу после указанной.
//     /// </summary>
//     private void PositionTaskAfter(Task taskToMove, Task targetTask, ProjectManager manager)
//     {
//         var targetIndex = manager.IndexOf(targetTask);
//         var currentIndex = manager.IndexOf(taskToMove);
//
//         if (targetIndex >= 0 && currentIndex >= 0 && currentIndex != targetIndex + 1)
//         {
//             // Если задача ниже целевой, нужно учесть сдвиг
//             var offset = targetIndex + 1 - currentIndex;
//             
//             // Учитываем количество детей целевой задачи (если это группа)
//             if (manager.IsGroup(targetTask))
//             {
//                 offset += manager.MembersOf(targetTask).Count();
//             }
//
//             manager.Move(taskToMove, offset);
//         }
//     }
//
//     #endregion
// }