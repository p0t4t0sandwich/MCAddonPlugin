using System.Collections.Generic;
using System.ComponentModel;
using MCAddonPlugin.Submodules.ServerTypeUtils;
using MinecraftModule;
using ModuleShared;

namespace MCAddonPlugin;

[Description("MCAddon")]
public class Settings : SettingStore {
    public ServerTypeUtilsSettings ServerTypeUtils = new();
    
    [Description("MCAddon")]
    public class ServerTypeUtilsSettings : SettingSectionStore {
        [WebSetting("Server Type", "The server type or modloader to use", false)]
        [InlineAction("MCAddonPlugin", "SetServerInfo", "Setup Server")]
        public MCConfig.ServerType ServerType = MCConfig.ServerType.Forge;

        [WebSetting("Minecraft Version", "The version of Minecraft to use", false)]
        public MinecraftVersion MinecraftVersion = MinecraftVersion.V1_21_4;

        [WebSetting("Delete World Folder", "Delete the world folder when setting up the server", false)]
        public bool DelWorldFolder = false;
    }
    
    [Description("MCAddon")]
    public class WhitelistSettings : SettingSectionStore {
        [WebSetting("Whitelist Enabled", "Enable the whitelist", false)]
        public bool Enabled = false;
        
        [WebSetting("Whitelist", "The list of players allowed on the server", false)]
        [InlineAction("MCAddonPlugin", "UpdateWhitelist", "Update")]
        [InlineAction("MCAddonPlugin", "RefreshWhitelist", "Refresh")]
        public List<string> Players = [];
    }
}
