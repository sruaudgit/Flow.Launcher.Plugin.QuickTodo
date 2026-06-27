using Microsoft.Toolkit.Uwp.Notifications;
using Flow.Launcher.Plugin.QuickTodo.Models;

namespace Flow.Launcher.Plugin.QuickTodo.Services;

public class ReminderService : IDisposable
{
    private readonly TodoStore _store;
    private readonly Func<int> _getIntervalMinutes;
    private readonly Func<int> _getSnoozeMinutes;
    private readonly Func<bool> _getSoundEnabled;
    private Timer? _timer;

    public ReminderService(
        TodoStore store,
        Func<int> getIntervalMinutes,
        Func<int> getSnoozeMinutes,
        Func<bool> getSoundEnabled)
    {
        _store = store;
        _getIntervalMinutes = getIntervalMinutes;
        _getSnoozeMinutes = getSnoozeMinutes;
        _getSoundEnabled = getSoundEnabled;
    }

    public void Start()
    {
        var interval = TimeSpan.FromMinutes(_getIntervalMinutes());
        _timer = new Timer(_ => CheckReminders(), null, interval, Timeout.InfiniteTimeSpan);
    }

    private void RescheduleTimer()
    {
        var interval = TimeSpan.FromMinutes(_getIntervalMinutes());
        _timer?.Change(interval, Timeout.InfiniteTimeSpan);
    }

    private void CheckReminders()
    {
        try
        {
            var tasks = _store.GetDueReminders();

            if (tasks.Count == 0) return;

            if (tasks.Count > 3)
            {
                ShowSummaryToast(tasks.Count);
            }
            else
            {
                foreach (var task in tasks)
                {
                    ShowTaskToast(task);
                }
            }
        }
        catch
        {
            // Don't let timer exceptions crash the plugin
        }
        finally
        {
            RescheduleTimer();
        }
    }

    private void ShowTaskToast(TodoItem task)
    {
        var now = DateTime.Now;
        var due = task.DueDate!.Value;
        var isOverdue = task.HasDueTime ? due < now : due.Date < now.Date;
        var fmt = task.HasDueTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd";
        var subtitle = isOverdue
            ? $"Overdue since {due.ToString(fmt)}"
            : task.HasDueTime ? $"Due today at {due:HH:mm}" : "Due today";

        var builder = new ToastContentBuilder()
            .AddText($"QuickTodo: {task.Title}", hintMaxLines: 1)
            .AddText(subtitle)
            .AddButton("Snooze", ToastActivationType.Background, $"quicktodo:snooze:{task.Id}")
            .AddButton("Dismiss", ToastActivationType.Background, $"quicktodo:dismiss:{task.Id}")
            .SetToastScenario(ToastScenario.Reminder);

        if (!_getSoundEnabled())
            builder.AddAudio(null, null, true);

        builder.Show();
    }

    private void ShowSummaryToast(int count)
    {
        var builder = new ToastContentBuilder()
            .AddText("QuickTodo", hintMaxLines: 1)
            .AddText($"You have {count} overdue or due-today tasks");

        if (!_getSoundEnabled())
            builder.AddAudio(null, null, true);

        builder.Show();
    }

    public void HandleToastAction(string argument)
    {
        if (string.IsNullOrEmpty(argument)) return;

        if (!argument.StartsWith("quicktodo:")) return;

        var parts = argument.Split(':', 3);
        if (parts.Length != 3 || !Guid.TryParse(parts[2], out var taskId)) return;

        if (parts[1] == "snooze")
        {
            var snoozeUntil = DateTime.Now.AddMinutes(_getSnoozeMinutes());
            _store.SetSnoozedUntil(taskId, snoozeUntil);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
