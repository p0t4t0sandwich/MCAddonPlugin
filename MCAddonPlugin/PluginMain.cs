using System;
using ModuleShared;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using FileManagerPlugin;
using GSMyAdmin.WebServer;
using MCAddonPlugin.Submodules.ServerTypeUtils;
using MCAddonPlugin.Submodules.Whitelist;
using MinecraftModule;
using Newtonsoft.Json;

namespace MCAddonPlugin;

/// <summary>
/// MCAddonPlugin expands on the default MinecraftModule with various bits and bobs
/// </summary>
[AMPDependency("FileManagerPlugin", "MinecraftModule")]
public class PluginMain : AMPPlugin {
    private readonly Settings _settings;
    private readonly ILogger _log;
    private readonly IConfigSerializer _config;
    private readonly IRunningTasksManager _tasks;
    private readonly IPluginMessagePusher _message;
    private readonly IFeatureManager _features;
    private IVirtualFileService _fileManager;
    private readonly IApplicationWrapper _app;
    
    public ServerTypeUtils ServerTypeUtils { get; private set; }
    public Whitelist Whitelist { get; private set; }
    
    // Reflection my beloved
    private GSMyAdmin.WebServer.WebMethods _core;
    private GSMyAdmin.WebServer.WebMethods Core {
        get {
            if (_core != null) {
                return _core;
            }
            var webServer = (LocalWebServer) _features.RequestFeature<ISessionInjector>();
            var field = webServer.GetType().GetField("APImodule", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) {
                _log.Debug("Failed to get APImodule field from LocalWebServer");
                return null;
            }
            var apiService = (ApiService) field.GetValue(webServer);
            if (apiService == null) {
                _log.Debug("Failed to get APImodule from LocalWebServer");
                return null;
            }
            var baseMethodsField = apiService.GetType().GetField("baseMethods", BindingFlags.NonPublic | BindingFlags.Instance);
            if (baseMethodsField == null) {
                _log.Debug("Failed to get baseMethods field from ApiService");
                return null;
            }
            Core = (GSMyAdmin.WebServer.WebMethods) baseMethodsField.GetValue(apiService);
            if (Core == null) {
                _log.Debug("Failed to get baseMethods from ApiService");
                return null;
            }

            return _core;
        }
        set => _core = value;
    }

    /// <summary>
    /// A utility method that sets settings via the Core module and updates them in the UI (assuming there's a connected UI)
    /// </summary>
    /// <param name="settings">A Dictionary of settings, in the format of settings["Some.Setting.Node"] = someObject</param>
    public void SetSettings(Dictionary<string, object> settings) {
        Dictionary<string, string> configs = new();
        foreach (var kvp in settings) {
            configs[kvp.Key] = kvp.Value switch {
                string str => str,
                _ => JsonConvert.SerializeObject(settings[kvp.Key])
            };
            Core.SetConfig(kvp.Key, configs[kvp.Key]);
        }
        _message.Push("setsettings", settings);
    }

    public PluginMain(ILogger log, IConfigSerializer config, IPlatformInfo platform,
        IRunningTasksManager taskManager, IApplicationWrapper Application,
        IPluginMessagePusher Message, IFeatureManager Features) {

        config.SaveMethod = PluginSaveMethod.KVP;
        config.KVPSeparator = "=";
        _log = log;
        _config = config;
        _settings = config.Load<Settings>(AutoSave: true);
        _tasks = taskManager;
        _message = Message;
        _features = Features;
        _settings.SettingModified += Settings_SettingModified;
            
        _app = Application;
    }

    public override void Init(out WebMethodsBase APIMethods) {
        APIMethods = new WebMethods(this, _settings, _log);
    }
    
    public override void PostInit() {
        _fileManager = (IVirtualFileService)_features.RequestFeature<IWSTransferHandler>();
        // Load MinecraftModule specific features
        if (_app.GetType().FullName == "MinecraftModule.MinecraftApp") {
            _log.Debug("MinecraftModule is loaded");
            ServerTypeUtils = new ServerTypeUtils(this, _app, _settings, _log, _fileManager);
            Whitelist = new Whitelist(this, _app, _settings, _log, _fileManager);
        }
    }

    public override IEnumerable<SettingStore> SettingStores => Utilities.EnumerableFrom(_settings);

    /// <summary>
    /// Listen to setting changes and fire off internal events
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="e">SettingModifiedEvent arguments and info</param>
    private void Settings_SettingModified(object sender, SettingModifiedEventArgs e) {
        Whitelist.Settings_SettingModified(e);
    }

    /// <summary>
    /// Simple T/F enum that defaults to false
    /// </summary>
    public enum NoYes {
        No,
        Yes
    }

    // ----------------------------- ServerTypeUtils ----------------------------- 
    
    /// <summary>
    /// Switch the server to a different modloader and/or version
    /// </summary>
    /// <param name="serverType">The server type or modloader to use</param>
    /// <param name="minecraftVersion">The version of Minecraft to use</param>
    /// <param name="deleteWorld">Delete the world folder when setting up the server</param>
    /// <returns>An ActionResult</returns>
    [ScheduleableTask("Switch the server to a different modloader and/or version.")]
    // TODO: Make Server Type conversion system to replace references to MCConfig.ServerType
    // Also would allow for custom server types (defined by meeee)
    public ActionResult ScheduleSetServerInfo(
        [ParameterDescription("The server type or modloader to use")] MCConfig.ServerType serverType,
        [ParameterDescription("The version of Minecraft to use")] MinecraftVersion minecraftVersion,
        [ParameterDescription("Delete the world folder when setting up the server")] NoYes deleteWorld = NoYes.No)
        => ServerTypeUtils.SetServerInfo(serverType, minecraftVersion, deleteWorld == NoYes.Yes);
        
    /// <summary>
    /// Set the server's modloader and version based on the server info queue
    /// </summary>
    /// <returns></returns>
    [ScheduleableTask("Set the server's modloader and version based on the server info queue.")]
    public ActionResult ScheduleProcessServerInfoQueue() => ServerTypeUtils.ProcessServerInfoQueue();
    
    // ----------------------------- Whitelist -----------------------------
    
    [MessageHandler(@"^\[\d\d:\d\d:\d\d\] \[(.+?)?INFO\]: Whitelist is now turned on$")]
    internal bool WhiteListEnable(Match match) {
        _settings.Whitelist.Enabled = true;
        return false;
    }
    
    [MessageHandler(@"^\[\d\d:\d\d:\d\d\] \[(.+?)?INFO\]: Whitelist is now turned off$")]
    internal bool WhiteListDisable(Match match) {
        _settings.Whitelist.Enabled = false;
        return false;
    }
    
    [MessageHandler(@"^\[\d\d:\d\d:\d\d\] \[(.+?)?INFO\]: (\w+) \(/\b((?:\d{1,3}\.){3}\d{1,3})\b:\d{5}\) lost connection: You are not white-listed on this server!$")]
    internal bool WhiteListKick(Match match) {
        PlayerNotWhitelisted?.Invoke(this, new PlayerNotWhitelistedEventArgs {
            Name = match.Groups[2].Value,
            IP = match.Groups[3].Value
        });
        return false;
    }
    
    [ScheduleableEvent("A player tries to join the server but is not whitelisted")]
    public event EventHandler<PlayerNotWhitelistedEventArgs> PlayerNotWhitelisted;
    
    public class PlayerNotWhitelistedEventArgs : EventArgs {
        public string Name { get; init; }
        public string IP { get; init; }
    }
}
