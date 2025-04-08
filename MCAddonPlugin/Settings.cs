using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using MCAddonPlugin.Submodules.ServerTypeUtils;
using MinecraftModule;
using ModuleShared;

namespace MCAddonPlugin;

[Description("MCAddon")]
public class Settings : SettingStore {
    public ServerTypeUtilsSettings ServerTypeUtils = new();
    public WhitelistSettings Whitelist = new();
    
    [Description("MCAddon"), SettingsGroupName("Server Type Utils:dns"), Serializable]
    public class ServerTypeUtilsSettings : SettingSectionStore {
        [WebSetting("Server Type", "The server type or modloader to use", false)]
        [InlineAction("MCAddonPlugin", "SetServerInfo", "Setup Server")]
        public MCConfig.ServerType ServerType = MCConfig.ServerType.Forge;

        [WebSetting("Minecraft Version", "The version of Minecraft to use", false)]
        public MinecraftVersion MinecraftVersion = MinecraftVersion.UNKNOWN;

        [WebSetting("Delete World Folder", "Delete the world folder when setting up the server", false)]
        public bool DelWorldFolder = false;
    }
    
    [Description("MCAddon"), SettingsGroupName("Whitelist:joystick"), Serializable]
    public class WhitelistSettings : SettingSectionStore {
        [WebSetting("Whitelist Enabled", "Enable the whitelist", false)]
        public bool Enabled = false;
        
        [WebSetting("Whitelist", "The list of players allowed on the server", false)]
        [InlineAction("MCAddonPlugin", "SetWhitelist", "Update")]
        [InlineAction("MCAddonPlugin", "RefreshWhitelist", "Refresh")]
        public List<string> Users = [];
        
        [WebSetting("Geyser Prefix", "The prefix for Geyser players", false)]
        public string GeyserPrefix = ".";
    }
}
