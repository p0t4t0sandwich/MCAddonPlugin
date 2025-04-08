using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using MCAddonPlugin.Submodules.ServerTypeUtils;
using ModuleShared;
using MinecraftModule;

namespace MCAddonPlugin;

[DisplayName("MCAddon")]
internal class WebMethods : WebMethodsBase {
    private readonly PluginMain _plugin;
    private readonly Settings _settings;
    private readonly ILogger _log;

    public WebMethods(PluginMain plugin, Settings settings, ILogger log) {
        _plugin = plugin;
        _settings = settings;
        _log = log;
    }

    public enum MCAddonPluginPermissions {
        SetServerInfo,
        ManageServerInfoQueue,
        ManageWhitelist
    }
    
    // ----------------------------- ServerTypeUtils ----------------------------- 

    [JSONMethod(
        "Switch the server to a different modloader or different version of Minecraft.",
        "An ActionResult indicating the success or failure of the operation."),
     RequiresPermissions(MCAddonPluginPermissions.SetServerInfo)]
    public ActionResult SetServerInfo(string serverType = "", string minecraftVersion = "", bool deleteWorld = false) {
        // Parse the platform and use the ServerType enum
        MCConfig.ServerType parsedType;
        if (string.IsNullOrEmpty(serverType)) {
            parsedType = _settings.ServerTypeUtils.ServerType;
        } else {
            Enum.TryParse(serverType, true, out parsedType);
        }

        // Parse the Minecraft version and use the MinecraftVersion enum
        MinecraftVersion parsedVersion;
        if (string.IsNullOrEmpty(minecraftVersion)) {
            parsedVersion = _settings.ServerTypeUtils.MinecraftVersion;
        } else {
            var tryParse = Enum.TryParse("V" + minecraftVersion.Replace(".", "_"), out parsedVersion);
            if (!tryParse) {
                return ActionResult.FailureReason("Invalid Minecraft version");
            }
        }

        return _plugin.ServerTypeUtils.SetServerInfo(parsedType, parsedVersion, deleteWorld);
    }

    [JSONMethod(
        "Add server info to the queue.",
        "An ActionResult indicating the success or failure of the operation."),
     RequiresPermissions(MCAddonPluginPermissions.ManageServerInfoQueue)]
    public ActionResult AddServerInfoToQueue(string serverType = "", string minecraftVersion = "", bool deleteWorld = false) {
        // Parse the platform and use the ServerType enum
        Enum.TryParse(serverType, true, out MCConfig.ServerType parsedType);
            
        // Parse the Minecraft version and use the MinecraftVersion enum
        var tryParse = Enum.TryParse("V" + minecraftVersion.Replace(".", "_"), out MinecraftVersion parsedVersion);
        if (!tryParse) {
            return ActionResult.FailureReason("Invalid Minecraft version");
        }

        _plugin.ServerTypeUtils.AddServerInfoToQueue(parsedType, parsedVersion, deleteWorld);
        return ActionResult.Success;
    }

    [JSONMethod(
        "Process the server info queue.",
        "An ActionResult indicating the success or failure of the operation."),
     RequiresPermissions(MCAddonPluginPermissions.ManageServerInfoQueue)]
    public ActionResult ProcessServerInfoQueue() {
        return _plugin.ServerTypeUtils.ProcessServerInfoQueue();
    }
    
    // ----------------------------- Whitelist -----------------------------
    [JSONMethod(
        "Refresh the whitelist.",
        "Nothing, but the task will be displayed in the task list."),
     RequiresPermissions(MCAddonPluginPermissions.ManageWhitelist)]
    public void RefreshWhitelist() {
        _ = _plugin.Whitelist.RefreshWhitelist();
    }
    
    [JSONMethod(
        "Add a list of users to the whitelist.",
        "Nothing, but the task will be displayed in the task list."),
     RequiresPermissions(MCAddonPluginPermissions.ManageWhitelist)]
    public void AddUsersToWhitelistByName([ParameterDescription("List of usernames")] List<string> users) {
        _ = _plugin.Whitelist.AddUsersToWhitelistByName(users);
    }
    
    [JSONMethod(
        "Remove a list of users from the whitelist.",
        "Nothing, but the task will be displayed in the task list."),
     RequiresPermissions(MCAddonPluginPermissions.ManageWhitelist)]
    public void RemoveUsersFromWhitelistByName([ParameterDescription("List of usernames")] List<string> users) {
        _plugin.Whitelist.RemoveUsersFromWhitelistByName(users);
    }
    
    [JSONMethod(
        "Set the whitelist to a list of users.",
        "Nothing, but the task will be displayed in the task list."),
     RequiresPermissions(MCAddonPluginPermissions.ManageWhitelist)]
    public void SetWhitelist(
        [ParameterDescription("List of usernames, if null it falls back to the setting value")]
        List<string> users = null) {
        _ = _plugin.Whitelist.SetWhitelist(users);
    }
    
    // ----------------------------- UserCache -----------------------------
}
