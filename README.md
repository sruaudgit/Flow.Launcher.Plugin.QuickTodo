# QuickTodo for Flow Launcher

QuickTodo is a Flow Launcher plugin for creating and reviewing lightweight tasks with priorities, categories, due dates, reminders, and optional Outlook Tasks support.

## Commands

- `td <task>` adds a local QuickTodo task.
- `td <task> !high @Work #tomorrow` adds a local task with priority, category, and due date.
- `td list` lists local tasks.
- `td outlook <task> !low @Work #tomorrow` creates a real Outlook task through desktop Outlook's COM object model.
- `td outlook list` lists incomplete Outlook tasks. Press Enter on a result to mark it complete.
- `tdo <task> !low @Work #tomorrow` is a shortcut for `td outlook <task>` — the `tdo` keyword goes straight to Outlook mode.
- `tdo list` lists incomplete Outlook tasks (same as `td outlook list`).
- `td cat add <name>` adds a local category.
- `td cat remove <name>` removes an unused local category.

## Date and priority modifiers

- Priorities: `!low`, `!medium`, `!high` or `!l`, `!m`, `!h`.
- Dates: `#today`, `#tomorrow`, `#monday`, `#yyyy-MM-dd`, or `#MM-dd`.
- Times: append `@<time>` to a date, e.g. `#tomorrow@1430`, `#friday@9am`, `#today@17:00`. Accepts `HHmm`, `H:mm`, and `h[:mm]am/pm`. A timed task only counts as overdue once its time passes, and its reminder fires at that time.
- Recurrence (local tasks only): `#daily`, `#weekly`, `#monthly`, `#yearly`, or `#every-monday` … `#every-sunday`. Completing a recurring task rolls its due date forward to the next occurrence instead of marking it done. Combine with a time, e.g. `#daily@9am`.
- Categories: `@Work`, `@Personal`, `@Errands`, or custom local categories.

## Outlook support

The Outlook path uses desktop Outlook automation only:

- `Outlook.Application`
- `GetNamespace("MAPI")`
- `CreateItem(3)` for tasks
- `GetDefaultFolder(13)` for the default Tasks folder
- `Save()`, `Delete()`, and the task object model properties for mutations

Outlook sync is handled by Outlook itself. If your default Tasks folder is backed by Exchange or Microsoft 365, saved tasks should sync to the mailbox and Microsoft To Do.

## Manual install

Download the release zip, then in Flow Launcher run:

```text
pm install <path-or-url-to-zip>
```

After install or update, restart Flow Launcher if the plugin was already loaded.
