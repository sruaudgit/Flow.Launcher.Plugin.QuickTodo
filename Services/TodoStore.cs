using System.IO;
using System.Text.Json;
using Flow.Launcher.Plugin.QuickTodo.Models;

namespace Flow.Launcher.Plugin.QuickTodo.Services;

public class TodoStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly string _backupPath;
    private readonly object _lock = new();
    private TodoData _data = new();
    private Action<string, string>? _logWarn;

    public TodoStore(string? dataDir = null, Action<string, string>? logWarn = null)
    {
        var dir = dataDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "QuickTodo");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "quicktodo.json");
        _backupPath = Path.Combine(dir, "quicktodo.backup.json");
        _logWarn = logWarn;
    }

    public void Load()
    {
        lock (_lock)
        {
            _data = TryLoadFile(_filePath)
                    ?? TryLoadFile(_backupPath)
                    ?? new TodoData();
        }
    }

    private TodoData? TryLoadFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TodoData>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logWarn?.Invoke(nameof(TodoStore), $"Failed to load {path}: {ex.Message}");
            return null;
        }
    }

    public string FilePath => _filePath;

    private void Save()
    {
        // Caller must hold _lock
        try
        {
            if (File.Exists(_filePath))
                File.Copy(_filePath, _backupPath, overwrite: true);
        }
        catch { /* best-effort backup */ }

        try
        {
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logWarn?.Invoke(nameof(TodoStore), $"Failed to save {_filePath}: {ex.Message}");
        }
    }

    // --- CRUD ---

    public List<TodoItem> GetAll()
    {
        lock (_lock) return _data.Tasks.ToList();
    }

    public TodoItem? GetById(Guid id)
    {
        lock (_lock) return _data.Tasks.FirstOrDefault(t => t.Id == id);
    }

    public TodoItem Add(string title, Priority priority, string category, DateTime? dueDate,
        Recurrence recurrence = Recurrence.None, bool hasDueTime = false)
    {
        lock (_lock)
        {
            var item = new TodoItem
            {
                Title = title,
                Priority = priority,
                Category = category,
                DueDate = dueDate,
                Recurrence = recurrence,
                HasDueTime = hasDueTime
            };
            _data.Tasks.Add(item);
            Save();
            return item;
        }
    }

    public void Update(TodoItem item)
    {
        lock (_lock)
        {
            var idx = _data.Tasks.FindIndex(t => t.Id == item.Id);
            if (idx >= 0)
            {
                _data.Tasks[idx] = item;
                Save();
            }
        }
    }

    public void Delete(Guid id)
    {
        lock (_lock)
        {
            _data.Tasks.RemoveAll(t => t.Id == id);
            Save();
        }
    }

    public void ToggleComplete(Guid id)
    {
        lock (_lock)
        {
            var item = _data.Tasks.FirstOrDefault(t => t.Id == id);
            if (item == null) return;

            // Recurring tasks roll forward to the next occurrence instead of completing.
            if (!item.IsCompleted && item.Recurrence != Recurrence.None && item.DueDate.HasValue)
            {
                item.DueDate = NextOccurrence(item.DueDate.Value, item.Recurrence);
                item.SnoozedUntil = null;
                Save();
                return;
            }

            item.IsCompleted = !item.IsCompleted;
            item.CompletedAt = item.IsCompleted ? DateTime.Now : null;
            Save();
        }
    }

    /// <summary>
    /// Advances <paramref name="from"/> by one recurrence step, skipping past any
    /// occurrences that are still in the past so the next due moment is in the future.
    /// Preserves the time-of-day component.
    /// </summary>
    private static DateTime NextOccurrence(DateTime from, Recurrence recurrence)
    {
        var next = from;
        var guard = 0;
        do
        {
            next = recurrence switch
            {
                Recurrence.Daily => next.AddDays(1),
                Recurrence.Weekly => next.AddDays(7),
                Recurrence.Monthly => next.AddMonths(1),
                Recurrence.Yearly => next.AddYears(1),
                _ => next.AddDays(1)
            };
        }
        while (next < DateTime.Now && ++guard < 1000);
        return next;
    }

    public void SetTitle(Guid id, string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        lock (_lock)
        {
            var item = _data.Tasks.FirstOrDefault(t => t.Id == id);
            if (item == null) return;
            item.Title = title.Trim();
            Save();
        }
    }

    public void SetPriority(Guid id, Priority priority)
    {
        lock (_lock)
        {
            var item = _data.Tasks.FirstOrDefault(t => t.Id == id);
            if (item == null) return;
            item.Priority = priority;
            Save();
        }
    }

    public void SetCategory(Guid id, string category)
    {
        lock (_lock)
        {
            var item = _data.Tasks.FirstOrDefault(t => t.Id == id);
            if (item == null) return;
            item.Category = category;
            Save();
        }
    }

    public void SetDueDate(Guid id, DateTime? dueDate, bool hasDueTime = false)
    {
        lock (_lock)
        {
            var item = _data.Tasks.FirstOrDefault(t => t.Id == id);
            if (item == null) return;
            item.DueDate = dueDate;
            item.HasDueTime = dueDate.HasValue && hasDueTime;
            Save();
        }
    }

    public void SetRecurrence(Guid id, Recurrence recurrence)
    {
        lock (_lock)
        {
            var item = _data.Tasks.FirstOrDefault(t => t.Id == id);
            if (item == null) return;
            item.Recurrence = recurrence;
            Save();
        }
    }

    public void SetSnoozedUntil(Guid id, DateTime? until)
    {
        lock (_lock)
        {
            var item = _data.Tasks.FirstOrDefault(t => t.Id == id);
            if (item == null) return;
            item.SnoozedUntil = until;
            Save();
        }
    }

    // --- Categories ---

    public List<string> GetCategories()
    {
        lock (_lock) return _data.Categories.ToList();
    }

    public bool AddCategory(string name)
    {
        lock (_lock)
        {
            if (_data.Categories.Any(c => c.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return false;
            _data.Categories.Add(name);
            Save();
            return true;
        }
    }

    public bool RemoveCategory(string name)
    {
        lock (_lock)
        {
            if (_data.Tasks.Any(t => t.Category.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return false; // category in use
            var removed = _data.Categories.RemoveAll(
                c => c.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) Save();
            return removed > 0;
        }
    }

    // --- Queries ---

    public List<TodoItem> GetOverdue()
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            return _data.Tasks
                .Where(t => !t.IsCompleted
                    && t.DueDate.HasValue
                    && DueMomentPast(t, now)
                    && NotSnoozed(t, now))
                .ToList();
        }
    }

    public List<TodoItem> GetDueToday()
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            return _data.Tasks
                .Where(t => !t.IsCompleted
                    && t.DueDate.HasValue
                    && t.DueDate.Value.Date == now.Date
                    && !DueMomentPast(t, now))
                .ToList();
        }
    }

    /// <summary>
    /// Tasks whose reminder should fire now: anything past its due moment, plus
    /// date-only tasks due today (which fire throughout the day). Timed tasks due
    /// later today are excluded until their time arrives.
    /// </summary>
    public List<TodoItem> GetDueReminders()
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            return _data.Tasks
                .Where(t => !t.IsCompleted
                    && t.DueDate.HasValue
                    && NotSnoozed(t, now)
                    && (DueMomentPast(t, now)
                        || (!t.HasDueTime && t.DueDate!.Value.Date == now.Date)))
                .ToList();
        }
    }

    // A timed task's due moment is its exact DateTime; a date-only task is "due"
    // for its whole day and only becomes past once the day has rolled over.
    private static bool DueMomentPast(TodoItem t, DateTime now)
        => t.HasDueTime ? t.DueDate!.Value < now : t.DueDate!.Value.Date < now.Date;

    private static bool NotSnoozed(TodoItem t, DateTime now)
        => !t.SnoozedUntil.HasValue || t.SnoozedUntil.Value < now;
}
