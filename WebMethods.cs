using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using ModuleShared;
using MinecraftModule;

namespace MCAddonPlugin
{
    internal class WebMethods : WebMethodsBase
    {
        private readonly IRunningTasksManager _tasks;
        private readonly MinecraftApp _app;
        // private readonly IFeatureManager _features;
        private readonly ILogger _log;

        public WebMethods(IRunningTasksManager tasks, MinecraftApp app, ILogger log)
        {
            _tasks = tasks;
            _app = app;
            _log = log;
        }

        public enum MCAddonPluginPermissions
        {
            SetVersion
        }

        [JSONMethod(
            "",
            "")]
        [RequiresPermissions(MCAddonPluginPermissions.SetVersion)]
        public ActionResult SetVersion(string serverType, string mcVersion)
        {
            // BEFORE
            _log.Debug("--------BEFORE");
            _log.Debug("ServerType: " + _app.Module.settings.Minecraft.ServerType);
            _log.Debug("LevelName: " + _app.Module.settings.Minecraft.LevelName);
            _log.Debug("Java: " + _app.Module.settings.Java.JavaVersion);
            
            // Parse the platform and use the ServerType enum
            Enum.TryParse(serverType, true, out MCConfig.ServerType parsedType);
            object versionInfo = null;
            switch (parsedType)
            {
                case MCConfig.ServerType.Forge:
                    foreach (var version in _app.Module.updates.ForgeVersionInfo.number)
                    {
                        if (mcVersion.Equals(version.Key.Split("-")[0]))
                        {
                            _log.Debug("Version: " + _app.Module.settings.Minecraft.SpecificForgeVersion);
                            versionInfo = version.Value.ToString();
                            _app.Module.settings.Minecraft.ServerType = parsedType;
                            _app.Module.settings.Minecraft.SpecificForgeVersion = version.Value.ToString();
                            break;
                        }
                    }
                    break;
                case MCConfig.ServerType.NeoForge:
                    foreach (var version in _app.Module.updates.NeoForgeVersionInfo.number)
                    {
                        if (version.Key.StartsWith(mcVersion.Substring(2, mcVersion.Length - 2)))
                        {
                            _log.Debug("Version: " + _app.Module.settings.Minecraft.SpecificNeoForgeVersion);
                            versionInfo = version.Value.ToString();
                            _app.Module.settings.Minecraft.ServerType = parsedType;
                            _app.Module.settings.Minecraft.SpecificNeoForgeVersion = version.Value.ToString();
                            break;
                        }
                    }
                    break;
            }
            
            // Set the correct Java runtime version
            UpdateJavaVersion(mcVersion);
            
            // Change the world name to the MC version
            if (!_app.Module.settings.Minecraft.LevelName.EndsWith(mcVersion))
            {
                _app.Module.settings.Minecraft.LevelName = "world_" + mcVersion;
            }
            
            _app.Module.config.Save(_app.Module.settings);
            
            // AFTER
            _log.Debug("--------AFTER");
            _log.Debug("ServerType: " + _app.Module.settings.Minecraft.ServerType);
            _log.Debug("LevelName: " + _app.Module.settings.Minecraft.LevelName);
            _log.Debug("Java: " + _app.Module.settings.Java.JavaVersion);
            _log.Debug("Version: " + versionInfo);
            
            // Update the modloader to fix server starting
            // TODO: Remove when fixed internally
            if (ShouldUpdate(parsedType, mcVersion))
            {
                _app.Update();
            }
            
            return ActionResult.Success;
        }

        private static bool ShouldUpdate(MCConfig.ServerType serverType, string mcVersion)
        {
            Enum.TryParse("V" + mcVersion.Replace(".", "_"), out MinecraftVersion minecraftVersion);
            switch (serverType)
            {
                case MCConfig.ServerType.Forge:
                    switch (minecraftVersion)
                    {
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
            return false;
        }

        private void UpdateJavaVersion(string mcVersion)
        {
            // private Dictionary<string, string> _app.GetJavaVersions();
            Dictionary<string, string> javaVersions;
            try
            {
                MethodInfo method = _app.GetType().GetMethod("GetJavaVersions", BindingFlags.NonPublic | BindingFlags.Instance);
                // ReSharper disable once PossibleNullReferenceException
                javaVersions = (Dictionary<string, string>) method.Invoke(_app, null);
            } catch (Exception e)
            {
                _log.Error("Error getting Java versions: " + e.Message);
                return;
            }
            
            Enum.TryParse("V" + mcVersion.Replace(".", "_"), out MinecraftVersion minecraftVersion);
            switch (minecraftVersion)
            {
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
                    // Java 21
                    foreach (var keyValuePair in javaVersions.Where(vkp => vkp.Key.Contains("-21-")))
                    { _app.Module.settings.Java.JavaVersion = keyValuePair.Key; }
                    break;
            }
        }
        
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum MinecraftVersion
        {
            V1_14 = 1140,
            V1_14_1 = 1141,
            V1_14_2 = 1142,
            V1_14_3 = 1143,
            V1_14_4 = 1144,
            V1_15 = 1150,
            V1_15_1 = 1151,
            V1_15_2 = 1152,
            V1_16 = 1160,
            V1_16_1 = 1161,
            V1_16_2 = 1162,
            V1_16_3 = 1163,
            V1_16_4 = 1164,
            V1_16_5 = 1165,
            V1_17 = 1170,
            V1_17_1 = 1171,
            V1_18 = 1180,
            V1_18_1 = 1181,
            V1_18_2 = 1182,
            V1_19 = 1190,
            V1_19_1 = 1191,
            V1_19_2 = 1192,
            V1_19_3 = 1193,
            V1_19_4 = 1194,
            V1_20 = 1200,
            V1_20_1 = 1201,
            V1_20_2 = 1202,
            V1_20_3 = 1203,
            V1_20_4 = 1204,
            V1_20_5 = 1205,
            V1_20_6 = 1206,
            V1_21 = 1210,
            V1_21_1 = 1211,
            V1_21_2 = 1212
        }
    }
}
