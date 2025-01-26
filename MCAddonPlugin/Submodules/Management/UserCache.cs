using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FileManagerPlugin;
using MinecraftModule;
using ModuleShared;
using Newtonsoft.Json;

namespace MCAddonPlugin.Submodules.Management;

public class UserCache {
    private readonly ILogger _log;
    private readonly IVirtualFileService _fileManager;
    private const string _cacheFile = "usercache.json";
    private readonly HttpClient cl = new();
    private List<UserCacheEntry> _cache;
    private readonly Dictionary<string, DateTime> _lookupMisses = new();

    public UserCache(IApplicationWrapper app, ILogger log, IVirtualFileService fileManager) {
        _log = log;
        _fileManager = fileManager;

        _cache = ReadUserCacheJSON().Result;
        
        (app as MinecraftApp)!.UserJoins += UserCache_OnUserJoins;
    }

    /// <summary>
    /// A user cache object with a name, UUID, and expiration date.
    /// </summary>
    public class UserCacheEntry(string name, string uuid, string expiresOn) {
        public readonly string name = name;
        public readonly string uuid = uuid;
        public string expiresOn = expiresOn;

        public UserCacheEntry(string name, string uuid) :
            this(name, uuid,
                DateTime.UtcNow.AddMonths(1).ToString("yyyy-MM-dd HH:mm:ss zzz")) {}
    }
    
    /// <summary>
    /// Update the cache for a given user.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void UserCache_OnUserJoins(object sender, UserEventArgs e) {
        if (e.User == null) return;
        var user = e.User;
        var entry = _cache.FirstOrDefault(x => x.name == user.Name && x.uuid == user.UID);
        if (entry != null) {
            entry.expiresOn = DateTime.UtcNow.AddMonths(1).ToString("yyyy-MM-dd HH:mm:ss zzz");
        }
        else {
            _cache.Add(new UserCacheEntry(user.Name, user.UID));
        }
    }
    
    /// <summary>
    /// Load the user cache from the JSON file.
    /// </summary>
    /// <returns>A list of UserCacheEntry objects</returns>
    public async Task<List<UserCacheEntry>> ReadUserCacheJSON() {
        var file = _fileManager.GetFile(_cacheFile);
        if (file == null) {
            _log.Warning("User cache file not found.");
            return [];
        }
        
        var reader = file.OpenText();
        var json = await reader.ReadToEndAsync();
        reader.Close();
        
        _log.Debug("Usercache: " + json);
        List<UserCacheEntry> cache;
        try {
            cache = JsonConvert.DeserializeObject<List<UserCacheEntry>>(json);
            cache.RemoveAll(x => DateTime.Parse(x.expiresOn) < DateTime.UtcNow);
        } catch (Exception e) {
            _log.Error("Failed to parse user cache JSON: " + e.Message);
            return [];
        }
        
        return cache;
    }
    
    /// <summary>
    /// Refresh the user cache from the JSON file.
    /// </summary>
    public async Task RefreshUserCache() {
        _cache = await ReadUserCacheJSON();
    }
    
    /// <summary>
    /// Write the user cache to the JSON file.
    /// </summary>
    public async Task WriteUserCacheJSON() {
        var file = _fileManager.GetFile(_cacheFile);
        if (file == null) {
            _log.Warning("User cache file not found.");
            return;
        }
        
        var writer = file.CreateText();
        var json = JsonConvert.SerializeObject(_cache);
        await writer.WriteAsync(json);
        writer.Close();
        
        _log.Debug("Wrote usercache: " + json);
    }
    
    /// <summary>
    /// Query the XUID of a Bedrock player from Geysers API (which is essentially a wrapper around Xbox Live API)
    /// </summary>
    /// <param name="gamertag">The gamertag/player name to look up</param>
    /// <param name="geyserPrefix">The prefix for Geyser players</param>
    /// <returns>The player's parsed XUID</returns>
    public async Task<Guid> QueryGeyserXUID(string gamertag, string geyserPrefix = ".") {
        // Remove the prefix
        gamertag = gamertag[geyserPrefix.Length..];
        
        var url = $"https://api.geysermc.org/v2/xbox/xuid/{gamertag}";
        HttpResponseMessage httpResponseMessage = await cl.SendAsync(new HttpRequestMessage(HttpMethod.Get, url) {
            Headers = {
                UserAgent = {
                    new ProductInfoHeaderValue("NeuralNexus-AMP-MCAddonPlugin", "1.0.0")
                }
            }
        });
        
        if (httpResponseMessage.StatusCode != HttpStatusCode.OK) {
            _log.Info("Failed to get XUID for " + gamertag);
            _log.Debug("Response: " + await httpResponseMessage.Content.ReadAsStringAsync());
            return Guid.Empty;
        }
        
        var response = await httpResponseMessage.Content.ReadAsStringAsync();
        var responseJson = JsonConvert.DeserializeObject<Dictionary<string, long>>(response);
        
        // Convert to Hex, left pad, then format as UUID (00000000-0000-0000-xxxx-xxxxxxxxxxxx)
        var xuid = responseJson["xuid"].ToString("X").PadLeft(32, '0');
        
        try {
            return Guid.ParseExact(xuid, "N");
        } catch (FormatException e) {
            _log.Error("Failed to parse XUID for " + gamertag);
            _log.Error("XUID: " + xuid);
            _log.Error(e.Message);
            return Guid.Empty;
        }
    }
    
    /// <summary>
    /// Query the UUID of a Java player from Mojang's API
    /// </summary>
    /// <param name="username">The player's name</param>
    /// <returns>The player's UUID</returns>
    public async Task<Guid> QueryJavaUUID(string username) {
        var url = $"https://api.mojang.com/users/profiles/minecraft/{username}";
        
        HttpResponseMessage httpResponseMessage = await cl.SendAsync(new HttpRequestMessage(HttpMethod.Get, url) {
            Headers = {
                UserAgent = {
                    new ProductInfoHeaderValue("NeuralNexus-AMP-MCAddonPlugin", "1.0.0")
                }
            }
        });
        
        if (httpResponseMessage.StatusCode != HttpStatusCode.OK) {
            _log.Info("Failed to get UUID for " + username);
            _log.Debug("Response: " + await httpResponseMessage.Content.ReadAsStringAsync());
            return Guid.Empty;
        }
        
        var response = await httpResponseMessage.Content.ReadAsStringAsync();
        var responseJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
        
        // Format as UUID (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
        return Guid.ParseExact(responseJson["id"], "N");
    }
    
    /// <summary>
    /// Get a player's UUID from their name
    /// </summary>
    /// <param name="username">The player's name</param>
    /// <param name="geyserPrefix">The prefix for Geyser players</param>
    /// <returns>The player's uuid</returns>
    public async Task<Guid> GetUserID(string username, string geyserPrefix = ".") {
        // Check if the user was missed in the last 5 minutes
        if (_lookupMisses.ContainsKey(username) && _lookupMisses[username] > DateTime.UtcNow.AddMinutes(-5)) {
            _log.Debug("Lookup miss for " + username);
            return Guid.Empty;
        }
        
        // Check the cache
        if (_cache.Any(x => x.name == username)) {
            _log.Debug("Cache hit for " + username);
            return Guid.ParseExact(_cache.First(x => x.name == username).uuid, "D");
        }
        
        // Look up the user
        _log.Debug("Looking up ID for " + username);
        Guid result = username.StartsWith(geyserPrefix)
            ? await QueryGeyserXUID(username, geyserPrefix) 
            : await QueryJavaUUID(username);
        if (result != Guid.Empty) {
            _cache.Add(new UserCacheEntry(username, result.ToString()));
        } else {
            _log.Debug("Adding " + username + " to lookup misses");
            _lookupMisses.Add(username, DateTime.UtcNow);
        }
        return result;
    }
    
    /// <summary>
    /// Look up a list of users and return their UUIDs
    /// </summary>
    /// <param name="users">List of player names</param>
    /// <param name="geyserPrefix">The prefix for Geyser players</param>
    /// <returns>List of WhitelistEntry objects</returns>
    public async Task<List<SimpleUser>> LookupUsers(List<string> users, string geyserPrefix = ".") {
        List<SimpleUser> simpleUsers = [];
        List<Task> results = [];
        results.AddRange(from user in users
            select GetUserID(user, geyserPrefix)
                .ContinueWith(task => {
                    if (task.Result != Guid.Empty) {
                        _log.Debug("Found ID for " + user + ": " + task.Result);
                        simpleUsers.Add(new SimpleUser(user, task.Result.ToString()));
                    }
                }));
        await Task.WhenAll(results);
        await WriteUserCacheJSON();
        return simpleUsers;
    }
}
