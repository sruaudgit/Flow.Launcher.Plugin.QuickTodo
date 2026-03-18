using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.QuickTodo;

public class Main : IAsyncPlugin, IContextMenu, ISettingProvider, IDisposable
{
    private PluginInitContext _context = null!;

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        return Task.FromResult(new List<Result>
        {
            new()
            {
                Title = "QuickTodo",
                SubTitle = "Plugin loaded successfully",
                IcoPath = "Images\\todo.png"
            }
        });
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        return new List<Result>();
    }

    public System.Windows.Controls.Control CreateSettingPanel()
    {
        // Placeholder — real settings panel added in a later task
        return new System.Windows.Controls.UserControl();
    }

    public void Dispose()
    {
    }
}
