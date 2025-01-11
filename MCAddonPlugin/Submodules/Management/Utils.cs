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
            _log.Debug("Response: " + httpResponseMessage.Content.ReadAsStringAsync().Result);
            return Guid.Empty;
        }
        
        var response = httpResponseMessage.Content.ReadAsStringAsync().Result;
        var responseJson = JsonConvert.DeserializeObject<Dictionary<string, long>>(response);
        
        // Convert to Hex, left pad, then format as UUID (00000000-0000-0000-xxxx-xxxxxxxxxxxx)
        var xuid = responseJson["xuid"].ToString("X").PadLeft(16, '0');
        return Guid.ParseExact(xuid, "N");
    }
    
    /// <summary>
    /// Query the UUID of a Java player from Mojang's API
    /// </summary>
    /// <param name="_log">The logger to use</param>
    /// <param name="playerName">The player's name</param>
    /// <returns>The player's UUID</returns>
    public static async Task<Guid> QueryJavaUUID(ILogger _log, string playerName) {
        var url = $"https://api.mojang.com/users/profiles/minecraft/{playerName}";
        
        HttpResponseMessage httpResponseMessage = await cl.SendAsync(new HttpRequestMessage(HttpMethod.Get, url) {
            Headers = {
                UserAgent = {
                    new ProductInfoHeaderValue("NeuralNexus-AMP-MCAddonPlugin", "1.0.0")
                }
            }
        });
        
        if (httpResponseMessage.StatusCode != HttpStatusCode.OK) {
            _log.Info("Failed to get UUID for " + playerName);
            _log.Debug("Response: " + httpResponseMessage.Content.ReadAsStringAsync().Result);
            return Guid.Empty;
        }
        
        var response = httpResponseMessage.Content.ReadAsStringAsync().Result;
        var responseJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
        
        // Format as UUID (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
        return Guid.ParseExact(responseJson["id"], "N");
    }
    
    /// <summary>
    /// Get a player's UUID from their name
    /// </summary>
    /// <param name="_log">The logger to use</param>
    /// <param name="playerName">The player's name</param>
    /// <param name="geyserPrefix">The prefix for Geyser players</param>
    /// <returns>The player's uuid</returns>
    public static async Task<Guid> GetPlayerID(ILogger _log, string playerName, string geyserPrefix = ".") {
        // TODO: Local Cache utilizing the usercache.json file and a plugin-owned cache for misses (with a shorter TTL)
        return playerName.StartsWith(geyserPrefix)
            ? await QueryGeyserXUID(_log, playerName, geyserPrefix) 
            : await QueryJavaUUID(_log, playerName);
    }
    
    /// <summary>
    /// Look up a list of players and return their UUIDs
    /// </summary>
    /// <param name="_log">The logger to use</param>
    /// <param name="players">List of player names</param>
    /// <param name="geyserPrefix">The prefix for Geyser players</param>
    /// <returns>List of WhitelistEntry objects</returns>
    public static async Task<List<SimpleUser>> LookupPlayers(ILogger _log, List<string> players, string geyserPrefix = ".") {
        List<SimpleUser> users = [];
        List<Task> results = [];
        results.AddRange(from player in players
            select GetPlayerID(_log, player, geyserPrefix)
                .ContinueWith(task => {
                    if (task.Result != Guid.Empty) {
                        users.Add(new SimpleUser(player, task.Result.ToString()));
                    }
                }));
        await Task.WhenAll(results);
        return users;
    }
}
