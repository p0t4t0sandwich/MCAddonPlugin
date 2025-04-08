using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using FileManagerPlugin;
using MinecraftModule;
using ModuleShared;
using Newtonsoft.Json;

namespace MCAddonPlugin.Submodules.ServerTypeUtils;

public class ServerTypeUtils {
    private readonly PluginMain _plugin;
    private readonly MinecraftApp _app;
    private readonly Settings _settings;
    private readonly ILogger _log;
    private readonly IVirtualFileService _fileManager;
    private readonly List<ServerInfo> _serverInfoQueue = [];
    
    public ServerTypeUtils(PluginMain plugin, IApplicationWrapper app, Settings settings, ILogger log, IVirtualFileService fileManager) {
        _plugin = plugin;
        _app = (MinecraftApp) app;
        _settings = settings;
        _log = log;
        _fileManager = fileManager;
        
        try {
            var queueFile = _fileManager.GetFile("serverInfoQueue.json");
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
    
    /// <summary>
    /// Set the server type and Minecraft version for the server
    /// </summary>
    /// <param name="serverType">The Server Type</param>
    /// <param name="minecraftVersion">The Minecraft Version</param>
    /// <param name="deleteWorld">if the world should be deleted</param>
    /// <returns>An ActionResult</returns>
    public ActionResult SetServerInfo(MCConfig.ServerType serverType, MinecraftVersion minecraftVersion, bool deleteWorld = false) {
        var settings = new Dictionary<string, object>();
        
        // BEFORE
        _log.Debug("PreServerType: " + _app.Module.Settings.Minecraft.ServerType);
        _log.Debug("PreLevelName: " + _app.Module.Settings.Minecraft.LevelName);
        _log.Debug("PreJava: " + _app.Module.Settings.Java.JavaVersion);
            
        var mcVersion = minecraftVersion.ToString().Substring(1).Replace("_", ".");
        
        object versionInfo = null;
        switch (serverType) {
            case MCConfig.ServerType.Forge:
                foreach (var version in _app.Module.Updates.ForgeVersionInfo.number) {
                    if (!mcVersion.Equals(version.Key.Split("-")[0])) continue;
                    _log.Debug("PreVersion: " + _app.Module.Settings.Minecraft.SpecificForgeVersion);
                    versionInfo = version.Value.ToString();
                    settings["MinecraftModule.Minecraft.ServerType"] = serverType;
                    settings["MinecraftModule.Minecraft.SpecificForgeVersion"] = version.Value.ToString();
                    break;
                }
                break;
            case MCConfig.ServerType.NeoForge:
                foreach (var version in _app.Module.Updates.NeoForgeVersionInfo.number) {
                    if (!version.Key.StartsWith(mcVersion.Substring(2, mcVersion.Length - 2))) continue;
                    _log.Debug("PreVersion: " + _app.Module.Settings.Minecraft.SpecificNeoForgeVersion);
                    versionInfo = version.Value.ToString();
                    settings["MinecraftModule.Minecraft.ServerType"] = serverType;
                    settings["MinecraftModule.Minecraft.SpecificNeoForgeVersion"] = version.Value.ToString();
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
        var javaVersion = UpdateJavaVersion(minecraftVersion);
        if (javaVersion != null) {
            settings["MinecraftModule.Java.JavaVersion"] = javaVersion;
        }
            
        // Change the world name to the MC version
        if (!_app.Module.Settings.Minecraft.LevelName.EndsWith(mcVersion)) {
            settings["MinecraftModule.Minecraft.LevelName"] = $"world_{mcVersion}";
        }

        // Delete the world folder if requested
        if (deleteWorld || _settings.ServerTypeUtils.DelWorldFolder) {
            var worldFolder = _fileManager.GetDirectory($"world_{mcVersion}");
            if (worldFolder.Exists) {
                _log.Debug("Deleting world folder: " + worldFolder.FullName);
                worldFolder.Delete(true);
            } else {
                _log.Debug("World folder does not exist: " + worldFolder.FullName);
            }
        }
            
        // Update the modloader if necessary
        if (ShouldUpdate(serverType, minecraftVersion, versionInfo.ToString())) {
            _app.Update();
        }
        
        // Send setting updates to the UI
        _plugin.SetSettings(settings);
        
        // AFTER
        _log.Debug("PostServerType: " + _app.Module.Settings.Minecraft.ServerType);
        _log.Debug("PostLevelName: " + _app.Module.Settings.Minecraft.LevelName);
        _log.Debug("PostJava: " + _app.Module.Settings.Java.JavaVersion);
        _log.Debug("PostVersion: " + versionInfo);
            
        return ActionResult.Success;
    }

    /// <summary>
    /// Check if the server should be updated (ie if the modloader is missing)
    /// </summary>
    /// <param name="serverType">The Server Type</param>
    /// <param name="minecraftVersion">The Minecraft Version</param>
    /// <param name="versionInfo">Additional version info, usually the Modloader version</param>
    /// <returns>A boolean to indicate the result</returns>
    [SuppressMessage("ReSharper", "SwitchStatementMissingSomeEnumCasesNoDefault")]
    private bool ShouldUpdate(MCConfig.ServerType serverType, MinecraftVersion minecraftVersion, string versionInfo) {
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
                        return !_fileManager.GetFile($"libraries/net/minecraftforge/forge/{versionInfo}/unix_args.txt").Exists;
                }
                break;
            case MCConfig.ServerType.NeoForge:
                return !_fileManager.GetFile($"libraries/net/neoforged/neoforge/{versionInfo}/unix_args.txt").Exists;
        }
        return false;
    }

    /// <summary>
    /// Update the Java version based on the Minecraft version
    /// </summary>
    /// <param name="minecraftVersion">Find the right Java LTS from the Minecraft version</param>
    /// <returns>The Java runtime enum value</returns>
    private string UpdateJavaVersion(MinecraftVersion minecraftVersion) {
        // private Dictionary<string, string> _app.GetJavaVersions();
        Dictionary<string, string> javaVersions;
        try {
            MethodInfo method = _app.GetType().GetMethod("GetJavaVersions", BindingFlags.NonPublic | BindingFlags.Instance);
            javaVersions = (Dictionary<string, string>) method.Invoke(_app, null);
        } catch (Exception e) {
            _log.Error("Error getting Java versions: " + e.Message);
            return null;
        }
        
        switch (minecraftVersion) {
            case MinecraftVersion.V1_14:
            case MinecraftVersion.V1_14_1:
            case MinecraftVersion.V1_14_2:
            case MinecraftVersion.V1_14_3:
            case MinecraftVersion.V1_14_4:
                // Java 8
                foreach (var keyValuePair in javaVersions.Where(keyValuePair => keyValuePair.Key.Contains("-8-") || keyValuePair.Key.Contains("-1.8-")))
                { return keyValuePair.Key; }
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
                { return keyValuePair.Key; }
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
                { return keyValuePair.Key; }
                break;
            case MinecraftVersion.V1_20_5:
            case MinecraftVersion.V1_20_6:
            case MinecraftVersion.V1_21:
            case MinecraftVersion.V1_21_1:
            case MinecraftVersion.V1_21_2:
            case MinecraftVersion.V1_21_3:
            case MinecraftVersion.V1_21_4:
            case MinecraftVersion.V1_21_5:
            default:
                // Java 21
                foreach (var keyValuePair in javaVersions.Where(vkp => vkp.Key.Contains("-21-")))
                { return keyValuePair.Key; }
                break;
        }

        return null;
    }
        
    /// <summary>
    /// Adds server info to the processing queue
    /// </summary>
    /// <param name="serverType">The Server Type</param>
    /// <param name="minecraftVersion">The Minecraft version</param>
    /// <param name="deleteWorld">If the world should be deleted in the process</param>
    /// <returns>An ActionResult</returns>
    public ActionResult AddServerInfoToQueue(MCConfig.ServerType serverType, MinecraftVersion minecraftVersion, bool deleteWorld = false) {
        _serverInfoQueue.Add(new ServerInfo {
            ServerType = serverType,
            MinecraftVersion = minecraftVersion,
            DeleteWorld = deleteWorld
        });
            
        var queueFile = _fileManager.GetFile("serverInfoQueue.json");
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

        var queueFile = _fileManager.GetFile("serverInfoQueue.json");
        try {
            var fileStream = queueFile.OpenWrite();
            var streamWriter = new StreamWriter(fileStream);
            streamWriter.Write(JsonConvert.SerializeObject(_serverInfoQueue));
        } catch (Exception e) {
            return ActionResult.FailureReason("Error processing server info queue: " + e.Message);
        }
            
        return SetServerInfo(serverInfo.ServerType, serverInfo.MinecraftVersion, serverInfo.DeleteWorld);
    }
    
    /// <summary>
    /// A class to store server info for the queue
    /// </summary>
    private class ServerInfo {
        public MCConfig.ServerType ServerType { get; init; }
        public MinecraftVersion MinecraftVersion { get; init; }
        public bool DeleteWorld { get; init; }
    }
}
