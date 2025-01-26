using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileManagerPlugin;
using MinecraftModule;
using ModuleShared;
using Newtonsoft.Json;

namespace MCAddonPlugin.Submodules.Management;

public partial class Whitelist {
    private readonly PluginMain _plugin;
    private readonly IApplicationWrapper _app;
    private readonly Settings _settings;
    private readonly ILogger _log;
    private IRunningTasksManager _tasks;
    private readonly IVirtualFileService _fileManager;
    private List<SimpleUser> _whitelist;

    public Whitelist(PluginMain plugin, IApplicationWrapper app, Settings settings, ILogger log, IRunningTasksManager tasks, IVirtualFileService fileManager) {
        _plugin = plugin;
        _app = app;
        _settings = settings;
        _settings.SettingModified += Settings_SettingModified;
        ((MinecraftApp) _app).Module.Settings.SettingModified += Settings_SettingModified;
        _log = log;
        _tasks = tasks;
        _fileManager = fileManager;
        
        _whitelist = ReadWhitelistJSON().Result;
        _settings.Whitelist.Users = GetWhitelistNames();
        
        mca_whitelist = _settings.Whitelist.Enabled;
        mcm_whitelist = mca_whitelist;
        
        _log.MessageLogged += Whitelist_MessageLogged;
    }
    
    // This bastard implementation to get around regex priority
    [GeneratedRegex(@"^\[\d\d:\d\d:\d\d\] \[(.+?)?INFO\]: (\w+) \(\/\b((?:\d{1,3}\.){3}\d{1,3})\b:\d{5}\) lost connection: You are not whitelisted on this server!$")]
    private static partial Regex WhitelistRegex();
    private readonly Regex _whitelistRegex = WhitelistRegex();
    private void Whitelist_MessageLogged(object sender, LogEventArgs e) {
        var match = _whitelistRegex.Match(e.Message);
        if (match.Success) {
            WhiteListKick(match);
        }
    }
    
    private bool mca_whitelist;
    private bool mcm_whitelist;
    internal void Settings_SettingModified(object sender, SettingModifiedEventArgs e) {
        var settings = new Dictionary<string, object>();
        
        switch (e.NodeName) {
            // Update the MinecraftModule's setting when our setting is changed
            case "MCAddonPlugin.Whitelist.Enabled": {
                if (e.NewValue is bool enabled && mcm_whitelist != enabled) {
                    mcm_whitelist = enabled;
                    settings["MinecraftModule.Game.Whitelist"] = enabled;
                }
                break;
            }
            // Vice versa
            case "MinecraftModule.Game.Whitelist": {
                if (e.NewValue is bool enabled && mca_whitelist != enabled) {
                    mca_whitelist = enabled;
                    settings["MCAddonPlugin.Whitelist.Enabled"] = enabled;
                }
                break;
            }
        }
        
        // Send setting updates to the UI
        _plugin.SetSettings(settings);
    }
    
    /// <summary>
    /// Read the whitelist from whitelist.json
    /// </summary>
    /// <returns>A list of whitelist entries</returns>
    public async Task<List<SimpleUser>> ReadWhitelistJSON() {
        var whitelistFile = _fileManager.GetFile("whitelist.json");
        if (whitelistFile == null) {
            _log.Debug("Failed to get whitelist.json");
            return [];
        }
        
        var reader = whitelistFile.OpenText();
        var whitelistJson = await reader.ReadToEndAsync();
        reader.Close();
        
        _log.Debug("Whitelist: " + whitelistJson);
        List<WhitelistEntry> whitelist;
        try {
            whitelist = JsonConvert.DeserializeObject<List<WhitelistEntry>>(whitelistJson);
        } catch (Exception e) {
            _log.Info("Failed to parse whitelist.json" + e);
            return [];
        }
        _log.Info("Whitelist loaded");

        // Convert WhitelistEntry objects to SimpleUser objects
        var converted = whitelist.Select(entry => new SimpleUser(entry.name, entry.uuid));
        return converted.ToList();
    }
    
    /// <summary>
    /// Write the whitelist to whitelist.json
    /// </summary>
    /// <param name="whitelist">The list of whitelist entries</param>
    public async Task WriteWhitelistJSON(List<SimpleUser> whitelist) {
        var whitelistFile = _fileManager.GetFile("whitelist.json");
        if (whitelistFile == null) {
            _log.Debug("Failed to get whitelist.json");
            return;
        }
        
        // Convert SimpleUser objects to WhitelistEntry objects
        var converted = whitelist.Select(user => new WhitelistEntry(user.Name, user.Id));
        
        var stream = whitelistFile.OpenWrite();
        stream.SetLength(0);
        var writer = new StreamWriter(stream);
        var json = JsonConvert.SerializeObject(converted);
        await writer.WriteAsync(json);
        writer.Close();
        stream.Close();
        
        _log.Info("Whitelist saved");
        _log.Debug("Whitelist: " + json);
    }

    /// <summary>
    /// Reload the whitelist and send the updated settings to the UI
    /// </summary>
    public void ReloadSettings() {
        // Send the `/whitelist reload` command
        if (_app.State == ApplicationState.Ready) {
            ((IHasWriteableConsole) _app).WriteLine("whitelist reload");
        }
        
        // Send setting updates to the UI
        _plugin.SetSettings(new Dictionary<string, object> {
            { "MCAddonPlugin.Whitelist.Users", GetWhitelistNames() }
        });
    }

    private RunningTask RefreshWhitelistTask;
    
    /// <summary>
    /// Read the whitelist.json and refresh things across the board
    /// </summary>
    public async Task RefreshWhitelist() {
        if (RefreshWhitelistTask != null) {
            return;
        }
        RefreshWhitelistTask = _tasks.CreateTask("RefreshWhitelist", "Refreshing whitelist...");
        
        _whitelist = await ReadWhitelistJSON();
        
        RefreshWhitelistTask.End();
        RefreshWhitelistTask = null;
    }
    
    private RunningTask AddUsersToWhitelistTask;
    
    /// <summary>
    /// Add a player (or list of players) to the whitelist
    /// </summary>
    /// <param name="users">List of player names</param>
    public async Task AddUsersToWhitelist(List<string> users) {
        if (AddUsersToWhitelistTask != null) {
            return;
        }
        AddUsersToWhitelistTask = _tasks.CreateTask("AddUsersToWhitelist", "Adding users to whitelist...");
        
        _whitelist.AddRange(await Utils.LookupUsers(_log, users, _settings.Whitelist.GeyserPrefix));
        await WriteWhitelistJSON(_whitelist);
        ReloadSettings();
        
        AddUsersToWhitelistTask.End();
        AddUsersToWhitelistTask = null;
    }
    
    /// <summary>
    /// Remove a user from the whitelist
    /// </summary>
    /// <param name="users">List of usernames</param>
    public void RemoveUsersFromWhitelist(List<string> users) {
        // Remove the user from the whitelist
        _whitelist.RemoveAll(entry => users.Contains(entry.Name));
        WriteWhitelistJSON(_whitelist).Wait(); // In theory this should be quick
        ReloadSettings();
    }
    
    private RunningTask SetWhitelistTask;
    
    /// <summary>
    /// Set the whitelist to a list of users
    /// </summary>
    /// <param name="users">List of usernames</param>
    public async Task SetWhitelist(List<string> users = null) {
        if (SetWhitelistTask != null) {
            return;
        }
        SetWhitelistTask = _tasks.CreateTask("SetWhitelist", "Updating the whitelist's users...");
        
        users ??= _settings.Whitelist.Users;
        if (_app.State is not (ApplicationState.Ready or ApplicationState.Stopped)) {
            _log.Debug("Server is not fully started or fully shut down, skipping whitelist update");

            await RefreshWhitelist();
            SetWhitelistTask.End(TaskState.Failed, "Server is not fully started or fully shut down");
            SetWhitelistTask = null;
            return;
        }
        _whitelist = await Utils.LookupUsers(_log, users, _settings.Whitelist.GeyserPrefix);
        await WriteWhitelistJSON(_whitelist);
        ReloadSettings();
        
        SetWhitelistTask.End();
        SetWhitelistTask = null;
    }
    
    /// <summary>
    /// A class representing a whitelist entry
    /// </summary>
    /// <param name="name">User name</param>
    /// <param name="uuid">User UUID</param>
    public class WhitelistEntry(string name, string uuid) {
        public readonly string name = name;
        public readonly string uuid = uuid;
    }
    
    /// <summary>
    /// Get the names of users on the whitelist
    /// </summary>
    /// <returns>A list of usernames</returns>
    public List<string> GetWhitelistNames() {
        return _whitelist.Select(whitelistEntry => whitelistEntry.Name).ToList();
    }
    
    // ----------------------------- Message Handlers ----------------------------- 
    
    [MessageHandler(@"^\[\d\d:\d\d:\d\d\] \[(.+?)?INFO\]: Whitelist is now turned on$")]
    internal bool WhiteListEnable(Match match) {
        _log.Debug("Whitelist enabled via console");
        mca_whitelist = true;
        _plugin.SetSettings(new Dictionary<string, object> {
            { "MCAddonPlugin.Whitelist.Enabled", true }
        });
        return false;
    }
    
    [MessageHandler(@"^\[\d\d:\d\d:\d\d\] \[(.+?)?INFO\]: Whitelist is now turned off$")]
    internal bool WhiteListDisable(Match match) {
        _log.Debug("Whitelist disabled via console");
        mca_whitelist = false;
        _plugin.SetSettings(new Dictionary<string, object> {
            { "MCAddonPlugin.Whitelist.Enabled", false }
        });
        return false;
    }
    
    // [MessageHandler(@"^\[\d\d:\d\d:\d\d\] \[(.+?)?INFO\]: Disconnecting (\w+) \(\/\b((?:\d{1,3}\.){3}\d{1,3})\b:\d{5}\): You are not whitelisted on this server!$", 1)]
    // [MessageHandler(@"^\[\d\d:\d\d:\d\d\] \[(.+?)?INFO\]: (\w+) \(\/\b((?:\d{1,3}\.){3}\d{1,3})\b:\d{5}\) lost connection: You are not whitelisted on this server!$", 1)]
    internal bool WhiteListKick(Match match) {
        _log.Debug("User not whitelisted: " + match.Groups[2].Value);
        _plugin.FireUserNotWhitelisted(match.Groups[2].Value, match.Groups[3].Value);
        return false;
    }
}
