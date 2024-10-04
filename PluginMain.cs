using System;
using ModuleShared;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using FileManagerPlugin;
using MinecraftModule;
using Newtonsoft.Json;

//Your namespace must match the assembly name and the filename. Do not change one without changing the other two.
namespace MCAddonPlugin {
    //The first class must be called PluginName
    public class PluginMain : AMPPlugin {
        private readonly Settings _settings;
        private readonly ILogger _log;
        private readonly IConfigSerializer _config;
        private readonly IPlatformInfo platform;
        private readonly IRunningTasksManager _tasks;
        private readonly IPluginMessagePusher message;
        private readonly IFeatureManager _features;
        private readonly MinecraftApp _app;
        private readonly WebMethods webMethods;
        private readonly List<ServerInfo> _serverInfoQueue = new List<ServerInfo>();

        //All constructor arguments after currentPlatform are optional, and you may ommit them if you don't
        //need that particular feature. The features you request don't have to be in any particular order.
        //Warning: Do not add new features to the feature manager here, only do that in Init();
        public PluginMain(ILogger log, IConfigSerializer config, IPlatformInfo platform,
            IRunningTasksManager taskManager, IApplicationWrapper Application,
            IPluginMessagePusher Message, IFeatureManager Features) {
            //These are the defaults, but other mechanisms are available.
            config.SaveMethod = PluginSaveMethod.KVP;
            config.KVPSeparator = "=";
            _log = log;
            _config = config;
            this.platform = platform;
            _settings = config.Load<Settings>(AutoSave: true); //Automatically saves settings when they're changed.
            _tasks = taskManager;
            message = Message;
            _features = Features;
            _settings.SettingModified += Settings_SettingModified;
            
            _app = (MinecraftApp) Application;
            
            webMethods = new WebMethods(this, _settings, log, Features, _app);
        }

        /*
            Rundown of the different interfaces you can ask for in your constructor:
            IRunningTasksManager - Used to put tasks in the left hand side of AMP to update the user on progress.
            IApplicationWrapper - A reference to the running application from the running module.
            IPluginMessagePusher - For 'push' type notifications that your front-end code can react to via PushedMessage in Plugin.js
            IFeatureManager - To expose/consume features to/from other plugins.
        */

        //Your init function should not invoke any code that depends on other plugins.
        //You may expose functionality via IFeatureManager.RegisterFeature, but you cannot yet use RequestFeature.
        public override void Init(out WebMethodsBase APIMethods) {
            APIMethods = webMethods;
        }

        void Settings_SettingModified(object sender, SettingModifiedEventArgs e) {
            //If you need to export settings to a different application, this is where you'd do it.
        }

        public override bool HasFrontendContent => true;

        //This gets called after every plugin is loaded. From here on it's safe
        //to use code that depends on other plugins and use IFeatureManager.RequestFeature
        public override void PostInit() {
            try {
                var fileManager = (IVirtualFileService) _features.RequestFeature<IWSTransferHandler>();
                var queueFile = fileManager.GetFile("serverInfoQueue.json");
                if (!queueFile.Exists) {
                    return;
                }
                var fileStream = queueFile.OpenRead();
                var streamReader = new StreamReader(fileStream);
                var queueJson = streamReader.ReadToEnd();
                _serverInfoQueue.AddRange(JsonConvert.DeserializeObject<List<ServerInfo>>(queueJson));
            }
            catch (Exception e) {
                _log.Error("Error reading server info queue: " + e.Message);
            }
        }

        public override IEnumerable<SettingStore> SettingStores => Utilities.EnumerableFrom(_settings);
        
        public ActionResult SetServerInfo(MCConfig.ServerType serverType, MinecraftVersion minecraftVersion, bool deleteWorld = false) {
            // BEFORE
            _log.Debug("PreServerType: " + _app.Module.settings.Minecraft.ServerType);
            _log.Debug("PreLevelName: " + _app.Module.settings.Minecraft.LevelName);
            _log.Debug("PreJava: " + _app.Module.settings.Java.JavaVersion);
            
            var mcVersion = minecraftVersion.ToString().Substring(1).Replace("_", ".");
            
            object versionInfo = null;
            switch (serverType) {
                case MCConfig.ServerType.Forge:
                    foreach (var version in _app.Module.updates.ForgeVersionInfo.number) {
                        if (!mcVersion.Equals(version.Key.Split("-")[0])) continue;
                        _log.Debug("PreVersion: " + _app.Module.settings.Minecraft.SpecificForgeVersion);
                        versionInfo = version.Value.ToString();
                        _app.Module.settings.Minecraft.ServerType = serverType;
                        _app.Module.settings.Minecraft.SpecificForgeVersion = version.Value.ToString();
                        break;
                    }
                    break;
                case MCConfig.ServerType.NeoForge:
                    foreach (var version in _app.Module.updates.NeoForgeVersionInfo.number) {
                        if (!version.Key.StartsWith(mcVersion.Substring(2, mcVersion.Length - 2))) continue;
                        _log.Debug("PreVersion: " + _app.Module.settings.Minecraft.SpecificNeoForgeVersion);
                        versionInfo = version.Value.ToString();
                        _app.Module.settings.Minecraft.ServerType = serverType;
                        _app.Module.settings.Minecraft.SpecificNeoForgeVersion = version.Value.ToString();
                        break;
                    }
                    break;
                default:
                    return ActionResult.FailureReason("Could not parse the server type, or the server type is not supported.");
            }
            if (versionInfo == null) {
                return ActionResult.FailureReason("Could not find the version info for the specified server type and Minecraft version.");
            }
            
            // Set the correct Java runtime version
            UpdateJavaVersion(minecraftVersion);
            
            // Change the world name to the MC version
            if (!_app.Module.settings.Minecraft.LevelName.EndsWith(mcVersion)) {
                _app.Module.settings.Minecraft.LevelName = $"world_{mcVersion}";
            }
            
            // Delete the world folder if requested
            if (deleteWorld || _settings.MainSettings.DelWorldFolder) {
                var fileManager = (IVirtualFileService) _features.RequestFeature<IWSTransferHandler>();
                var worldFolder = fileManager.GetDirectory($"world_{mcVersion}");
                if (worldFolder.Exists) {
                    _log.Debug("Deleting world folder: " + worldFolder.FullName);
                    worldFolder.Delete(true);
                } else {
                    _log.Debug("World folder does not exist: " + worldFolder.FullName);
                }
            }
            
            _app.Module.config.Save(_app.Module.settings);
            
            // AFTER
            _log.Debug("PostServerType: " + _app.Module.settings.Minecraft.ServerType);
            _log.Debug("PostLevelName: " + _app.Module.settings.Minecraft.LevelName);
            _log.Debug("PostJava: " + _app.Module.settings.Java.JavaVersion);
            _log.Debug("PostVersion: " + versionInfo);
            
            // Update the modloader if necessary
            if (ShouldUpdate(serverType, minecraftVersion, versionInfo.ToString())) {
                _app.Update();
            }
            
            return ActionResult.Success;
        }

        [SuppressMessage("ReSharper", "SwitchStatementMissingSomeEnumCasesNoDefault")]
        private bool ShouldUpdate(MCConfig.ServerType serverType, MinecraftVersion minecraftVersion, string versionInfo) {
            var fileManager = (IVirtualFileService) _features.RequestFeature<IWSTransferHandler>();
            switch (serverType) {
                case MCConfig.ServerType.Forge:
                    switch (minecraftVersion) {
                        // TODO: Remove these once it's fixed internally
                        case MinecraftVersion.V1_17:
                        case MinecraftVersion.V1_17_1:
                        case MinecraftVersion.V1_18:
                        case MinecraftVersion.V1_18_1:
                        case MinecraftVersion.V1_18_2:
                        case MinecraftVersion.V1_19:
                        case MinecraftVersion.V1_19_1:
                        case MinecraftVersion.V1_19_2:
                        case MinecraftVersion.V1_19_3:
                        case MinecraftVersion.V1_19_4:
                        case MinecraftVersion.V1_20:
                        case MinecraftVersion.V1_20_1:
                        case MinecraftVersion.V1_20_2:
                            return !fileManager.GetFile($"libraries/net/minecraftforge/forge/{versionInfo}/unix_args.txt").Exists;
                    }
                    break;
                case MCConfig.ServerType.NeoForge:
                    return !fileManager.GetFile($"libraries/net/neoforged/neoforge/{versionInfo}/unix_args.txt").Exists;
            }
            return false;
        }

        private void UpdateJavaVersion(MinecraftVersion minecraftVersion) {
            // private Dictionary<string, string> _app.GetJavaVersions();
            Dictionary<string, string> javaVersions;
            try {
                MethodInfo method = _app.GetType().GetMethod("GetJavaVersions", BindingFlags.NonPublic | BindingFlags.Instance);
                // ReSharper disable once PossibleNullReferenceException
                javaVersions = (Dictionary<string, string>) method.Invoke(_app, null);
            } catch (Exception e) {
                _log.Error("Error getting Java versions: " + e.Message);
                return;
            }
            
            switch (minecraftVersion) {
                case MinecraftVersion.V1_14:
                case MinecraftVersion.V1_14_1:
                case MinecraftVersion.V1_14_2:
                case MinecraftVersion.V1_14_3:
                case MinecraftVersion.V1_14_4:
                    // Java 8
                    foreach (var keyValuePair in javaVersions.Where(vkp => vkp.Key.Contains("-8-")))
                    { _app.Module.settings.Java.JavaVersion = keyValuePair.Key; }
                    break;
                case MinecraftVersion.V1_15:
                case MinecraftVersion.V1_15_1:
                case MinecraftVersion.V1_15_2:
                case MinecraftVersion.V1_16:
                case MinecraftVersion.V1_16_1:
                case MinecraftVersion.V1_16_2:
                case MinecraftVersion.V1_16_3:
                case MinecraftVersion.V1_16_4:
                case MinecraftVersion.V1_16_5:
                    // Java 11
                    foreach (var keyValuePair in javaVersions.Where(vkp => vkp.Key.Contains("-11-")))
                    { _app.Module.settings.Java.JavaVersion = keyValuePair.Key; }
                    break;
                case MinecraftVersion.V1_17:
                case MinecraftVersion.V1_17_1:
                case MinecraftVersion.V1_18:
                case MinecraftVersion.V1_18_1:
                case MinecraftVersion.V1_18_2:
                case MinecraftVersion.V1_19:
                case MinecraftVersion.V1_19_1:
                case MinecraftVersion.V1_19_2:
                case MinecraftVersion.V1_19_3:
                case MinecraftVersion.V1_19_4:
                case MinecraftVersion.V1_20:
                case MinecraftVersion.V1_20_1:
                case MinecraftVersion.V1_20_2:
                case MinecraftVersion.V1_20_3:
                case MinecraftVersion.V1_20_4:
                    // Java 17
                    foreach (var keyValuePair in javaVersions.Where(vkp => vkp.Key.Contains("-17-")))
                    { _app.Module.settings.Java.JavaVersion = keyValuePair.Key; }
                    break;
                case MinecraftVersion.V1_20_5:
                case MinecraftVersion.V1_20_6:
                case MinecraftVersion.V1_21:
                case MinecraftVersion.V1_21_1:
                case MinecraftVersion.V1_21_2:
                default:
                    // Java 21
                    foreach (var keyValuePair in javaVersions.Where(vkp => vkp.Key.Contains("-21-")))
                    { _app.Module.settings.Java.JavaVersion = keyValuePair.Key; }
                    break;
            }
        }

        class ServerInfo {
            public MCConfig.ServerType ServerType { get; set; }
            public MinecraftVersion MinecraftVersion { get; set; }
            public bool DeleteWorld { get; set; }
        }
        
        public ActionResult AddServerInfoToQueue(MCConfig.ServerType serverType, MinecraftVersion minecraftVersion, bool deleteWorld = false) {
            _serverInfoQueue.Add(new ServerInfo {
                ServerType = serverType,
                MinecraftVersion = minecraftVersion,
                DeleteWorld = deleteWorld
            });
            
            var fileManager = (IVirtualFileService) _features.RequestFeature<IWSTransferHandler>();
            var queueFile = fileManager.GetFile("serverInfoQueue.json");
            try {
                var fileStream = queueFile.OpenWrite();
                var streamWriter = new StreamWriter(fileStream);
                streamWriter.Write(JsonConvert.SerializeObject(_serverInfoQueue));
            } catch (Exception e) {
                return ActionResult.FailureReason("Error processing server info queue: " + e.Message);
            }
            
            return ActionResult.Success;
        }
        
        public ActionResult ProcessServerInfoQueue() {
            ServerInfo serverInfo = _serverInfoQueue.FirstOrDefault();
            if (serverInfo == null) {
                return ActionResult.FailureReason("No server info in the queue.");
            }
            _serverInfoQueue.RemoveAt(0);
            
            var fileManager = (IVirtualFileService) _features.RequestFeature<IWSTransferHandler>();
            var queueFile = fileManager.GetFile("serverInfoQueue.json");
            try {
                var fileStream = queueFile.OpenWrite();
                var streamWriter = new StreamWriter(fileStream);
                streamWriter.Write(JsonConvert.SerializeObject(_serverInfoQueue));
            } catch (Exception e) {
                return ActionResult.FailureReason("Error processing server info queue: " + e.Message);
            }
            
            return SetServerInfo(serverInfo.ServerType, serverInfo.MinecraftVersion, serverInfo.DeleteWorld);
        }
        
        public enum NoYes {
            No,
            Yes
        }
        
        [ScheduleableTask("Switch the server to a different modloader and/or version.")]
        public ActionResult ScheduleSetServerInfo(
            [ParameterDescription("The server type or modloader to use")] MCConfig.ServerType serverType,
            [ParameterDescription("The version of Minecraft to use")] MinecraftVersion minecraftVersion,
            [ParameterDescription("Delete the world folder when setting up the server")] NoYes deleteWorld = NoYes.No)
            => SetServerInfo(serverType, minecraftVersion, deleteWorld == NoYes.Yes);
        
        [ScheduleableTask("Set the server's modloader and version based on the server info queue.")]
        public ActionResult ScheduleProcessServerInfoQueue() => ProcessServerInfoQueue();
    }
}
