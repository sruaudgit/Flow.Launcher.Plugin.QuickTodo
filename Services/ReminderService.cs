using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Toolkit.Uwp.Notifications;
using Flow.Launcher.Plugin.QuickTodo.Models;
using Windows.UI.Notifications;
using System.DirectoryServices.ActiveDirectory;
using Windows.Security.ExchangeActiveSyncProvisioning;

namespace Flow.Launcher.Plugin.QuickTodo.Services;

public class ReminderService : IDisposable
{
    private readonly TodoStore _store;
    private readonly Func<int> _getIntervalMinutes;
    private readonly Func<int> _getSnoozeMinutes;
    private readonly Func<bool> _getSoundEnabled;
    private Timer? _timer;
    private readonly HashSet<Guid> _notifiedTaskIds = new();

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
        interval = TimeSpan.FromSeconds(5);
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
            //File.AppendAllText(@"C:\temp\quicktodo-debug.log", "CheckReminders");
            var tasks = _store.GetDueReminders();

            if (tasks.Count == 0) return;

            var tasksToNotify = new List<TodoItem>();
            var totalDueCount = tasks.Count;


            // Remove task IDs that are no longer in due reminders (completed or snoozed elsewhere)
            var dueTaskIds = tasks.Select(t => t.Id).ToHashSet();
            _notifiedTaskIds.RemoveWhere(id => !dueTaskIds.Contains(id));

            // Get tasks we haven't notified about yet
            tasksToNotify = tasks.Where(t => !_notifiedTaskIds.Contains(t.Id)).ToList();

            /*             
                         //File.AppendAllText(@"C:\temp\quicktodo-debug.log", 
                             $"_notifiedTaskIds: {string.Join(", ", _notifiedTaskIds.Select(id => id.ToString()))}\n" +
                             $"tasksToNotify: {string.Join(", ", tasksToNotify.Select(t => t.Id.ToString()))}\n" +
                             $"dueTaskIds: {string.Join(", ", dueTaskIds.Select(id => id.ToString()))}\n");
             */

            if (tasksToNotify.Count == 0) return;

            foreach (var task in tasksToNotify)
            {
                ShowTaskToast(task);
                _notifiedTaskIds.Add(task.Id);
            }

        }
        catch (Exception)
        {
            // Don't let timer exceptions crash the plugin
            //File.AppendAllText(@"C:\temp\quicktodo-debug.log", "crash "+e.Message);
        }
        finally
        {
            RescheduleTimer();
        }
    }

    private void ShowTaskToast(TodoItem task)
    {
        //File.AppendAllText(@"C:\temp\quicktodo-debug.log", "ShowTaskToast {task.Id}");
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
            .AddButton($"Snooze ({_getSnoozeMinutes()})", ToastActivationType.Background, $"quicktodo:snooze:{task.Id}")
            .AddButton("Complete", ToastActivationType.Background, $"quicktodo:completed:{task.Id}")
            .SetToastScenario(ToastScenario.Reminder);

        if (!_getSoundEnabled())
            builder.AddAudio(null, null, true);

        builder.Show(tn =>
        {
            tn.Tag = task.Id.ToString();
            tn.Group = "QuickTodo";
            tn.Dismissed += (s, e) =>
            {
                if (e.Reason == ToastDismissalReason.TimedOut || e.Reason == ToastDismissalReason.UserCanceled)
                    ShowTaskToast(task);
            };
            tn.Activated += (s, e) =>
            {
                //File.AppendAllText(@"C:\temp\quicktodo-debug.log", "Activated \n" );

                if (!HandleToastAction((ToastActivatedEventArgs)e))
                    ShowTaskToast(task);
            };
        });
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

    public bool HandleToastAction(ToastActivatedEventArgs argument)
    {
        //File.AppendAllText(@"C:\temp\quicktodo-debug.log", "HandleToastAction\n");

        string msg = argument.Arguments;

        //File.AppendAllText(@"C:\temp\quicktodo-debug.log", "msg " + msg);

        if (string.IsNullOrEmpty(msg)) return false;

        if (!msg.StartsWith("quicktodo:")) return false;

        var parts = msg.Split(':', 3);
        if (parts.Length != 3 || !Guid.TryParse(parts[2], out var taskId)) return false;

        if (parts[1] == "snooze")
        {
            var snoozeUntil = DateTime.Now.AddMinutes(_getSnoozeMinutes());
            _store.SetSnoozedUntil(taskId, snoozeUntil);
            _notifiedTaskIds.Remove(taskId);
            return true;
        }
        if (parts[1] == "completed")
        {
            //File.AppendAllText(@"C:\temp\quicktodo-debug.log", "completed "+taskId);
            _store.ToggleComplete(taskId);
            _notifiedTaskIds.Remove(taskId);
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
