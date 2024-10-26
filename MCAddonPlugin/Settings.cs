using MinecraftModule;
using ModuleShared;

namespace MCAddonPlugin;

public class Settings : SettingStore {
    public class MCAddonSettings : SettingSectionStore {
        [WebSetting("Server Type", "The server type or modloader to use", false)]
        [InlineAction("MCAddonPlugin", "SetServerInfo", "Setup Server")]
        public MCConfig.ServerType ServerType = MCConfig.ServerType.Forge;

        [WebSetting("Minecraft Version", "The version of Minecraft to use", false)]
        public MinecraftVersion MinecraftVersion = MinecraftVersion.V1_20_2;

        [WebSetting("Delete World Folder", "Delete the world folder when setting up the server", false)]
        public bool DelWorldFolder = false;
    }

    public MCAddonSettings MainSettings = new MCAddonSettings();
}