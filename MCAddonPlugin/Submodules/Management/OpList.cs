using FileManagerPlugin;
using ModuleShared;

namespace MCAddonPlugin.Submodules.Management;

public class OpList {
    private readonly PluginMain _plugin;
    private readonly IApplicationWrapper _app;
    private readonly IHasWriteableConsole _console;
    private readonly Settings _settings;
    private readonly UserCache _cache;
    private readonly ILogger _log;
    private IRunningTasksManager _tasks;
    private readonly IVirtualFileService _fileManager;

    public OpList(PluginMain plugin, IApplicationWrapper app, Settings settings, UserCache cache,
        ILogger log, IRunningTasksManager tasks, IVirtualFileService fileManager) {
        _plugin = plugin;
        _app = app;
        _console = app as IHasWriteableConsole;
        _settings = settings;
        _cache = cache;
        _log = log;
        _tasks = tasks;
        _fileManager = fileManager;
    }
}
