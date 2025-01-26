using System;
using ModuleShared;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileManagerPlugin;
using GSMyAdmin.WebServer;
using MCAddonPlugin.Submodules.Management;
using MCAddonPlugin.Submodules.ServerTypeUtils;
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
    /// TODO: Bug Mike and/or James to make this a feature
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
    
    /// <summary>
    /// TODO: Bug Mike and/or James to make this a feature
    /// Get a setting via the Core module
    /// </summary>
    /// <param name="setting"></param>
    /// <returns></returns>
    public  T GetSetting<T>(string setting) => (T) Core.GetConfig(setting).CurrentValue;

    /// <summary>
    /// TODO: Bug Mike and/or James to make this a feature
    /// Register message handlers from the plugin to the actual application
    /// </summary>
    /// <param name="app"></param>
    /// <param name="registrant"></param>
    public void RegisterMessageHandlers(IApplicationWrapper app, object registrant) {
        if (app is not AppServerBase) {
            _log.Debug("Failed to register message handlers: AppServerBase not found");
            return;
        }
        // Reflection time
        var messageHandlersField = app.GetType().GetField("MessageHandlers", BindingFlags.NonPublic | BindingFlags.Instance);
        if (messageHandlersField == null) {
            _log.Debug("Failed to get MessageHandlers field from AppServerBase");
            return;
        }
        var messageHandlers = (Dictionary<Regex, AppServerBase.MessageHandlingDelegate>) messageHandlersField.GetValue(app);
        if (messageHandlers == null) {
            _log.Debug("Failed to get MessageHandlers from AppServerBase");
            return;
        }
        var asyncMessageHandlersField = app.GetType().GetField("AsyncMessageHandlers", BindingFlags.NonPublic | BindingFlags.Instance);
        if (asyncMessageHandlersField == null) {
            _log.Debug("Failed to get AsyncMessageHandlers field from AppServerBase");
            return;
        }
        var asyncMessageHandlers = (Dictionary<Regex, AppServerBase.MessageHandlingDelegateAsync>) asyncMessageHandlersField.GetValue(app);
        if (asyncMessageHandlers == null) {
            _log.Debug("Failed to get AsyncMessageHandlers from AppServerBase");
            return;
        }
        
        var methodData = registrant.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttributes<MessageHandlerAttribute>().Any())
            .Select(m => new { 
                method = m, 
                attribs = m.GetCustomAttributes<MessageHandlerAttribute>()
            })
            .OrderBy(p => { 
                MessageHandlerAttribute handlerAttribute = p.attribs.FirstOrDefault(); 
                return handlerAttribute?.Priority ?? 0;
            });
        
        foreach (var data in methodData) { 
            ParameterInfo[] parameters = data.method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof (Match)) {
                if (data.method.ReturnType == typeof (Task<bool>)) {
                    _log.Debug("Registering async message handler: " + data.method.Name);
                    AppServerBase.MessageHandlingDelegateAsync handlingDelegateAsync = data.method.CreateDelegate<AppServerBase.MessageHandlingDelegateAsync>(registrant);
                    foreach (MessageHandlerAttribute attrib in data.attribs)
                        asyncMessageHandlers.Add(attrib.Expression, handlingDelegateAsync);
                }
                else if (data.method.ReturnType == typeof (bool)) {
                    _log.Debug("Registering sync message handler: " + data.method.Name);
                    AppServerBase.MessageHandlingDelegate handlingDelegate = data.method.CreateDelegate<AppServerBase.MessageHandlingDelegate>(registrant);
                    foreach (MessageHandlerAttribute attrib in data.attribs)
                        messageHandlers.Add(attrib.Expression, handlingDelegate);
                }
            }
        }
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

    public override bool HasFrontendContent => true;
    
    public override void Init(out WebMethodsBase APIMethods) {
        APIMethods = new WebMethods(this, _settings, _log);
    }
    
    public override void PostInit() {
        _fileManager = (IVirtualFileService)_features.RequestFeature<IWSTransferHandler>();
        
        // Load MinecraftModule specific features
        if (_app.GetType().FullName == "MinecraftModule.MinecraftApp") {
            _log.Debug("MinecraftModule is loaded");
            Whitelist = new Whitelist(this, _app, _settings, _log, _tasks, _fileManager);
            RegisterMessageHandlers(_app, Whitelist);
            ServerTypeUtils = new ServerTypeUtils(this, _app, _settings, _log, _fileManager);
        }
    }

    public override IEnumerable<SettingStore> SettingStores => Utilities.EnumerableFrom(_settings);

    /// <summary>
    /// Listen to setting changes and fire off internal events
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="e">SettingModifiedEvent arguments and info</param>
    private void Settings_SettingModified(object sender, SettingModifiedEventArgs e) { }

    /// <summary>
    /// Simple T/F enum that defaults to false
    /// </summary>
    public enum NoYes {
        No,
        Yes
    }

    // ----------------------------- Management - Whitelist -----------------------------
    
    internal void FireUserNotWhitelisted(string name, string ip) {
        UserNotWhitelisted?.Invoke(this, new UserNotWhitelistedEventArgs {
            Name = name,
            IP = ip
        });
    }
    
    [ScheduleableEvent("A user tries to join the server and is not whitelisted")]
    public event EventHandler<UserNotWhitelistedEventArgs> UserNotWhitelisted;
    
    public class UserNotWhitelistedEventArgs : EventArgs {
        public string Name { get; init; }
        public string IP { get; init; }
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
}
