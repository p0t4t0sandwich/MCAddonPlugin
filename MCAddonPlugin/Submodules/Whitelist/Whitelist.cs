using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using FileManagerPlugin;
using MinecraftModule;
using ModuleShared;
using Newtonsoft.Json;

namespace MCAddonPlugin.Submodules.Whitelist;

public class Whitelist {
    private readonly PluginMain _plugin;
    private readonly MinecraftApp _app;
    private readonly Settings _settings;
    private readonly ILogger _log;
    private readonly IVirtualFileService _fileManager;
    private readonly List<WhitelistEntry> _whitelist = [];
    private readonly HttpClient cl = new();

    public Whitelist(PluginMain plugin, IApplicationWrapper app, Settings settings, ILogger log, IVirtualFileService fileManager) {
        _plugin = plugin;
        _app = (MinecraftApp) app;
        _settings = settings;
        _log = log;
        _fileManager = fileManager;
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
    
    /// <summary>
    /// Read the whitelist from whitelist.json
    /// </summary>
    /// <returns>A list of whitelist entries</returns>
    public List<WhitelistEntry> ReadWhitelistJSON() {
        var whitelist = new List<WhitelistEntry>();
        var whitelistFile = _fileManager.GetFile("whitelist.json");
        if (whitelistFile == null) {
            _log.Debug("Failed to get whitelist.json");
            return whitelist;
        }
        
        var stream = whitelistFile.OpenRead();
        var whitelistJson = new StreamReader(stream).ReadToEnd();
        stream.Close();
        
        try {
            whitelist = JsonConvert.DeserializeObject<List<WhitelistEntry>>(whitelistJson);
        } catch (Exception e) {
            _log.Debug("Failed to parse whitelist.json" + e);
        }
        _log.Info("Whitelist loaded");
        _log.Debug("Whitelist: " + whitelist);
        
        return whitelist;
    }
    
    /// <summary>
    /// Write the whitelist to whitelist.json
    /// </summary>
    /// <param name="whitelist">The list of whitelist entries</param>
    public void WriteWhitelistJSON(List<WhitelistEntry> whitelist) {
        var whitelistFile = _fileManager.GetFile("whitelist.json");
        if (whitelistFile == null) {
            _log.Debug("Failed to get whitelist.json");
            return;
        }
        
        var stream = whitelistFile.OpenWrite();
        var writer = new StreamWriter(stream);
        writer.Write(JsonConvert.SerializeObject(whitelist));
        writer.Close();
        
        _log.Info("Whitelist saved");
        _log.Debug("Whitelist: " + whitelist);
    }
    
    /// <summary>
    /// Query the XUID of a Bedrock player from Geysers API (which is essentially a wrapper around Xbox Live API)
    /// </summary>
    /// <param name="gamertag">The gamertag/player name to look up</param>
    /// <returns>The player's parsed XUID</returns>
    public string QueryGeyserXUID(string gamertag) {
        // Remove the prefix
        gamertag = gamertag.Substring(_settings.Whitelist.GeyserPrefix.Length);
        
        // Query XUID from Geyser API
        var url = $"https://api.geysermc.org/v2/xbox/xuid/{gamertag}";
        HttpResponseMessage httpResponseMessage = cl.Send(new HttpRequestMessage(HttpMethod.Get, url) {
            Headers = {
                UserAgent = {
                    new ProductInfoHeaderValue("NeuralNexus-AMP-MCAddonPlugin", "1.0.0")
                }
            }
        });
        
        if (httpResponseMessage.StatusCode != HttpStatusCode.OK) {
            _log.Debug("Failed to get XUID for " + gamertag);
            return null;
        }
        
        var response = httpResponseMessage.Content.ReadAsStringAsync().Result;
        var responseJson = JsonConvert.DeserializeObject<Dictionary<string, long>>(response);
        
        // Convert to Hex, then format as UUID (00000000-0000-0000-xxxx-xxxxxxxxxxxx)
        var xuid = responseJson["xuid"].ToString("X");
        return string.Concat("00000000-0000-0000-", xuid.AsSpan(0, 4), "-", xuid.AsSpan(4));
    }
    
    /// <summary>
    /// Query the UUID of a Java player from Mojang's API
    /// </summary>
    /// <param name="playerName">The player's name</param>
    /// <returns>The player's UUID</returns>
    public string QueryJavaUUID(string playerName) {
        var url = $"https://api.mojang.com/users/profiles/minecraft/{playerName}";
        
        HttpResponseMessage httpResponseMessage = cl.Send(new HttpRequestMessage(HttpMethod.Get, url) {
            Headers = {
                UserAgent = {
                    new ProductInfoHeaderValue("NeuralNexus-AMP-MCAddonPlugin", "1.0.0")
                }
            }
        });
        
        if (httpResponseMessage.StatusCode != HttpStatusCode.OK) {
            _log.Debug("Failed to get UUID for " + playerName);
            return null;
        }
        
        var response = httpResponseMessage.Content.ReadAsStringAsync().Result;
        var responseJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
        return responseJson["id"];
    }
    
    /// <summary>
    /// Get a player's UUID from their name
    /// </summary>
    /// <param name="playerName">The player's name</param>
    /// <returns>The player's uuid</returns>
    public string GetPlayerID(string playerName) {
        // TODO: Local Cache utilizing the usercache.json file
        return playerName.StartsWith(_settings.Whitelist.GeyserPrefix) 
            ? QueryGeyserXUID(playerName) 
            : QueryJavaUUID(playerName);
    }
    
    public void UpdateWhitelist(List<string> players = null) {
        var settings = new Dictionary<string, object>();
        players ??= _settings.Whitelist.Players;
        if (_app.State != ApplicationState.Ready) {
            _log.Debug("Server is not fully started, skipping whitelist update");
            return;
        }
        
        // Update the whitelist
        var whitelist = ReadWhitelistJSON();
        foreach (var name in players) {
            if (whitelist.Find(entry => entry.name == name) == null) {
                // Query the player's UUID
                var uuid = GetPlayerID(name);
                if (uuid == null) {
                    _log.Debug("Failed to get UUID for " + name);
                    continue;
                }
                _log.Info("Adding " + name + " to the whitelist");
                whitelist.Add(new WhitelistEntry {
                    name = name,
                    uuid = uuid
                });
            }
        }
        
        // Write the updated whitelist
        WriteWhitelistJSON(whitelist);
        
        // Send the `/whitelist reload` command
        if (_app.State == ApplicationState.Ready) {
            _app.SendConsole("whitelist reload");
        }
        
        // Send setting updates to the UI
        _plugin.SetSettings(settings);
    }
    
    public class WhitelistEntry {
        public string name { get; init; }
        public string uuid { get; set; }
    }
}
