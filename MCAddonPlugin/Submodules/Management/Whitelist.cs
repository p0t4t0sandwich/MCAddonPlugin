using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileManagerPlugin;
using ModuleShared;
using Newtonsoft.Json;

namespace MCAddonPlugin.Submodules.Management;

public class Whitelist {
    private readonly PluginMain _plugin;
    private readonly IApplicationWrapper _app;
    private readonly Settings _settings;
    private readonly ILogger _log;
    private readonly IVirtualFileService _fileManager;
    private List<SimpleUser> _whitelist;

    public Whitelist(PluginMain plugin, IApplicationWrapper app, Settings settings, ILogger log, IVirtualFileService fileManager) {
        _plugin = plugin;
        _app = app;
        _settings = settings;
        _log = log;
        _fileManager = fileManager;
        
        _whitelist = ReadWhitelistJSON().Result;
    }
    
    internal void Settings_SettingModified(SettingModifiedEventArgs e) {
        var settings = new Dictionary<string, object>();
        
        switch (e.NodeName) {
            // Update the MinecraftModule's setting when our setting is changed
            case "MCAddonPlugin.Whitelist.Enabled": {
                if (e.NewValue is bool enabled) {
                    settings["MinecraftModule.Game.Whitelist"] = enabled;
                }
                break;
            }
            // Vice versa
            case "MinecraftModule.Game.Whitelist": {
                if (e.NewValue is bool enabled) {
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
        
        var stream = whitelistFile.OpenRead();
        var reader = new StreamReader(stream);
        var whitelistJson = await reader.ReadToEndAsync();
        reader.Close();
        stream.Close();
        
        var whitelist = new List<WhitelistEntry>();
        try {
            whitelist = JsonConvert.DeserializeObject<List<WhitelistEntry>>(whitelistJson);
        } catch (Exception e) {
            _log.Debug("Failed to parse whitelist.json" + e);
        }
        _log.Info("Whitelist loaded");
        _log.Debug("Whitelist: " + whitelist);

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
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(JsonConvert.SerializeObject(converted));
        writer.Close();
        stream.Close();
        
        _log.Info("Whitelist saved");
        _log.Debug("Whitelist: " + whitelist);
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
            { "MCAddonPlugin.Whitelist.Players", GetWhitelistNames() }
        });
    }

    /// <summary>
    /// Read the whitelist.json and refresh things across the board
    /// </summary>
    public async Task RefreshWhitelist() {
        _whitelist = await ReadWhitelistJSON();
        ReloadSettings();
    }
    
    /// <summary>
    /// Add a player (or list of players) to the whitelist
    /// </summary>
    /// <param name="users">List of player names</param>
    public async Task AddUsersToWhitelist(List<string> users) {
        _whitelist.AddRange(await Utils.LookupPlayers(_log, users, _settings.Whitelist.GeyserPrefix));
        await WriteWhitelistJSON(_whitelist);
        ReloadSettings();
    }
    
    /// <summary>
    /// Remove a player from the whitelist
    /// </summary>
    /// <param name="users">List of player names</param>
    public void RemoveUsersFromWhitelist(List<string> users) {
        // Remove the player from the whitelist
        _whitelist.RemoveAll(entry => users.Contains(entry.Name));
        WriteWhitelistJSON(_whitelist).Wait(); // In theory this should be quick
        ReloadSettings();
    }
    
    /// <summary>
    /// Set the whitelist to a list of players
    /// </summary>
    /// <param name="users">List of player names</param>
    public async Task SetWhitelist(List<string> users = null) {
        users ??= _settings.Whitelist.Players;
        if (_app.State != ApplicationState.Ready) {
            _log.Debug("Server is not fully started, skipping whitelist update");
            return;
        }
        _whitelist = await Utils.LookupPlayers(_log, users, _settings.Whitelist.GeyserPrefix);
        await WriteWhitelistJSON(_whitelist);
        ReloadSettings();
    }
    
    /// <summary>
    /// A class representing a whitelist entry
    /// </summary>
    /// <param name="name">Player name</param>
    /// <param name="uuid">Player UUID</param>
    public class WhitelistEntry(string name, string uuid) {
        public readonly string name = name;
        public readonly string uuid = uuid;
    }
    
    /// <summary>
    /// Get the names of players on the whitelist
    /// </summary>
    /// <returns>A list of player names</returns>
    public List<string> GetWhitelistNames() {
        return _whitelist.Select(whitelistEntry => whitelistEntry.Name).ToList();
    }
}
