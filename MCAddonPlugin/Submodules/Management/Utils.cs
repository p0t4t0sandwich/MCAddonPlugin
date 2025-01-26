using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using ModuleShared;
using Newtonsoft.Json;

namespace MCAddonPlugin.Submodules.Management;

public class Utils {
    private static readonly HttpClient cl = new();
    
    /// <summary>
    /// Query the XUID of a Bedrock player from Geysers API (which is essentially a wrapper around Xbox Live API)
    /// </summary>
    /// <param name="_log">The logger to use</param>
    /// <param name="gamertag">The gamertag/player name to look up</param>
    /// <param name="geyserPrefix">The prefix for Geyser players</param>
    /// <returns>The player's parsed XUID</returns>
    public static async Task<Guid> QueryGeyserXUID(ILogger _log, string gamertag, string geyserPrefix = ".") {
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
        } catch (Exception e) {
            _log.Error("Failed to parse XUID for " + gamertag);
            _log.Error("XUID: " + xuid);
            _log.Error(e.Message);
            return Guid.Empty;
        }
    }
    
    /// <summary>
    /// Query the UUID of a Java player from Mojang's API
    /// </summary>
    /// <param name="_log">The logger to use</param>
    /// <param name="username">The player's name</param>
    /// <returns>The player's UUID</returns>
    public static async Task<Guid> QueryJavaUUID(ILogger _log, string username) {
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
    /// <param name="_log">The logger to use</param>
    /// <param name="username">The player's name</param>
    /// <param name="geyserPrefix">The prefix for Geyser players</param>
    /// <returns>The player's uuid</returns>
    public static async Task<Guid> GetUserID(ILogger _log, string username, string geyserPrefix = ".") {
        // TODO: Local Cache utilizing the usercache.json file and a plugin-owned cache for misses (with a shorter TTL)
        _log.Debug("Looking up ID for " + username);
        return username.StartsWith(geyserPrefix)
            ? await QueryGeyserXUID(_log, username, geyserPrefix) 
            : await QueryJavaUUID(_log, username);
    }
    
    /// <summary>
    /// Look up a list of users and return their UUIDs
    /// </summary>
    /// <param name="_log">The logger to use</param>
    /// <param name="users">List of player names</param>
    /// <param name="geyserPrefix">The prefix for Geyser players</param>
    /// <returns>List of WhitelistEntry objects</returns>
    public static async Task<List<SimpleUser>> LookupUsers(ILogger _log, List<string> users, string geyserPrefix = ".") {
        List<SimpleUser> simpleUsers = [];
        List<Task> results = [];
        results.AddRange(from user in users
            select GetUserID(_log, user, geyserPrefix)
                .ContinueWith(task => {
                    if (task.Result != Guid.Empty) {
                        _log.Debug("Found ID for " + user + ": " + task.Result);
                        simpleUsers.Add(new SimpleUser(user, task.Result.ToString()));
                    }
                }));
        await Task.WhenAll(results);
        return simpleUsers;
    }
}
