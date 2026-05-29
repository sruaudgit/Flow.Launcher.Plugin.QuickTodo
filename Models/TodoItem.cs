using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.QuickTodo.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Priority
{
    Low,
    Medium,
    High
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Recurrence
{
    None,
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public Priority Priority { get; set; } = Priority.Medium;
    public string Category { get; set; } = "Personal";
    public DateTime? DueDate { get; set; }

    /// <summary>True when <see cref="DueDate"/> carries a meaningful time-of-day (not just a date).</summary>
    public bool HasDueTime { get; set; }

    /// <summary>How this task repeats. Completing a recurring task rolls <see cref="DueDate"/> forward.</summary>
    public Recurrence Recurrence { get; set; } = Recurrence.None;

    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public DateTime? SnoozedUntil { get; set; }
    public string? OutlookEntryId { get; set; }
    public string? OutlookStoreId { get; set; }
}
