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
        private readonly Settings _settings;
        private readonly IFeatureManager _features;
        private readonly ILogger _log;
        private readonly MinecraftApp _app;

        public WebMethods(ILogger log, IFeatureManager features, Settings settings, MinecraftApp app) {
            _settings = settings;
            _features = features;
            _log = log;
            _app = app;
        }

        public enum MCAddonPluginPermissions {
            SetVersion
        }

        [JSONMethod(
            "",
            "")]
        [RequiresPermissions(MCAddonPluginPermissions.SetVersion)]
        public ActionResult SetServerInfo(string serverType = "", string mcVersion = "", bool deleteWorld = false) {
            // BEFORE
            _log.Debug("PreServerType: " + _app.Module.settings.Minecraft.ServerType);
            _log.Debug("PreLevelName: " + _app.Module.settings.Minecraft.LevelName);
            _log.Debug("PreJava: " + _app.Module.settings.Java.JavaVersion);
            
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
                mcVersion = minecraftVersion.ToString().Substring(1).Replace("_", ".");
            } else {
                Enum.TryParse("V" + mcVersion.Replace(".", "_"), out minecraftVersion);
            }
            
            object versionInfo = null;
            switch (parsedType) {
                case MCConfig.ServerType.Forge:
                    foreach (var version in _app.Module.updates.ForgeVersionInfo.number) {
                        if (!mcVersion.Equals(version.Key.Split("-")[0])) continue;
                        _log.Debug("PreVersion: " + _app.Module.settings.Minecraft.SpecificForgeVersion);
                        versionInfo = version.Value.ToString();
                        _app.Module.settings.Minecraft.ServerType = parsedType;
                        _app.Module.settings.Minecraft.SpecificForgeVersion = version.Value.ToString();
                        break;
                    }
                    break;
                case MCConfig.ServerType.NeoForge:
                    foreach (var version in _app.Module.updates.NeoForgeVersionInfo.number) {
                        if (!version.Key.StartsWith(mcVersion.Substring(2, mcVersion.Length - 2))) continue;
                        _log.Debug("PreVersion: " + _app.Module.settings.Minecraft.SpecificNeoForgeVersion);
                        versionInfo = version.Value.ToString();
                        _app.Module.settings.Minecraft.ServerType = parsedType;
                        _app.Module.settings.Minecraft.SpecificNeoForgeVersion = version.Value.ToString();
                        break;
                    }
                    break;
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
            
            // Update the modloader to fix server starting
            // TODO: Remove when fixed internally
            if (ShouldUpdate(parsedType, minecraftVersion)) {
                _app.Update();
            }
            
            return ActionResult.Success;
        }

        [SuppressMessage("ReSharper", "SwitchStatementMissingSomeEnumCasesNoDefault")]
        private static bool ShouldUpdate(MCConfig.ServerType serverType, MinecraftVersion minecraftVersion) {
            if (serverType != MCConfig.ServerType.Forge) {
                return false;
            }
            
            switch (minecraftVersion) {
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
                    return false;
            }
            return true;
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
    }
}
