using System.Collections.Generic;
using FileManagerPlugin;
using MinecraftModule;
using ModuleShared;

namespace MCAddonPlugin.Submodules.Whitelist;

public class Whitelist {
    private readonly PluginMain _plugin;
    private readonly Settings _settings;
    private readonly ILogger _log;
    private readonly IVirtualFileService _fileManager;
    private readonly MinecraftApp _mcApp;

    public Whitelist(PluginMain plugin, Settings settings, ILogger log, IVirtualFileService fileManager,
        MinecraftApp mcApp) {
        _plugin = plugin;
        _settings = settings;
        _log = log;
        _fileManager = fileManager;
        _mcApp = mcApp;
    }
    
    internal void Settings_SettingModified(SettingModifiedEventArgs e) {
        var settings = new Dictionary<string, object>();
        
        switch (e.NodeName) {
            // Update the MinecraftModule's setting when our setting is changed
            case "MCAddonPlugin.WhitelistSettings.Enabled": {
                if (e.NewValue is bool enabled) {
                    settings["MinecraftModule.Game.Whitelist"] = enabled;
                }
                break;
            }
            // Vice versa
            case "MinecraftModule.Game.Whitelist": {
                if (e.NewValue is bool enabled) {
                    settings["MCAddonPlugin.WhitelistSettings.Enabled"] = enabled;
                }
                break;
            }
        }
        
        // Send setting updates to the UI
        _plugin.SetSettings(settings);
    }
}
