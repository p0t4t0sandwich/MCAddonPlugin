using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using FileManagerPlugin;
using ModuleShared;
using MinecraftModule;

namespace MCAddonPlugin {
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
            SetVersion
        }

        [JSONMethod(
            "Switch the server to a different modloader or different version of Minecraft.",
            "An ActionResult indicating the success or failure of the operation.")]
        [RequiresPermissions(MCAddonPluginPermissions.SetVersion)]
        public ActionResult SetServerInfo(string serverType = "", string mcVersion = "", bool deleteWorld = false) {
            // Parse the platform and use the ServerType enum
            MCConfig.ServerType parsedType;
            if (string.IsNullOrEmpty(serverType)) {
                parsedType = _settings.MainSettings.ServerType;
            } else {
                Enum.TryParse(serverType, true, out parsedType);
            }
            
            // Parse the Minecraft version and use the MinecraftVersion enum
            MinecraftVersion minecraftVersion;
            if (string.IsNullOrEmpty(mcVersion)) {
                minecraftVersion = _settings.MainSettings.MinecraftVersion;
            } else {
                Enum.TryParse("V" + mcVersion.Replace(".", "_"), out minecraftVersion);
            }
            
            return _plugin.SetServerInfo(parsedType, minecraftVersion, deleteWorld);
        }
    }
}
