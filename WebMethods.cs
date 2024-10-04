using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using FileManagerPlugin;
using ModuleShared;
using MinecraftModule;

namespace MCAddonPlugin {
    [DisplayName("MCAddon")]
    internal class WebMethods : WebMethodsBase {
        private readonly PluginMain _plugin;
        private readonly Settings _settings;
        private readonly IFeatureManager _features;
        private readonly ILogger _log;
        private readonly MinecraftApp _app;

        public WebMethods(PluginMain plugin, Settings settings, ILogger log, IFeatureManager features, MinecraftApp app) {
            _plugin = plugin;
            _settings = settings;
            _features = features;
            _log = log;
            _app = app;
        }

        public enum MCAddonPluginPermissions {
            SetServerInfo
        }

        [JSONMethod(
            "Switch the server to a different modloader or different version of Minecraft.",
            "An ActionResult indicating the success or failure of the operation.")]
        [RequiresPermissions(MCAddonPluginPermissions.SetServerInfo)]
        public ActionResult SetServerInfo(string serverType = "", string minecraftVersion = "", bool deleteWorld = false) {
            // Parse the platform and use the ServerType enum
            MCConfig.ServerType parsedType;
            if (string.IsNullOrEmpty(serverType)) {
                parsedType = _settings.MainSettings.ServerType;
            } else {
                Enum.TryParse(serverType, true, out parsedType);
            }
            
            // Parse the Minecraft version and use the MinecraftVersion enum
            MinecraftVersion parsedVersion;
            if (string.IsNullOrEmpty(minecraftVersion)) {
                parsedVersion = _settings.MainSettings.MinecraftVersion;
            } else {
                Enum.TryParse("V" + minecraftVersion.Replace(".", "_"), out parsedVersion);
            }
            
            return _plugin.SetServerInfo(parsedType, parsedVersion, deleteWorld);
        }
        
        [JSONMethod(
            "Add server info to the queue.",
            "An ActionResult indicating the success or failure of the operation.")]
        public ActionResult AddServerInfoToQueue(string serverType = "", string minecraftVersion = "", bool deleteWorld = false) {
            // Parse the platform and use the ServerType enum
            Enum.TryParse(serverType, true, out MCConfig.ServerType parsedType);
            
            // Parse the Minecraft version and use the MinecraftVersion enum
            Enum.TryParse("V" + minecraftVersion.Replace(".", "_"), out MinecraftVersion parsedVersion);
            
            _plugin.AddServerInfoToQueue(parsedType, parsedVersion, deleteWorld);
            return ActionResult.Success;
        }
        
        [JSONMethod(
            "Process the server info queue.",
            "An ActionResult indicating the success or failure of the operation.")]
        public ActionResult ProcessServerInfoQueue() {
            return _plugin.ProcessServerInfoQueue();
        }
    }
}
