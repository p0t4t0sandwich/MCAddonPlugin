using ModuleShared;
using System.Collections.Generic;
using MinecraftModule;

//Your namespace must match the assembly name and the filename. Do not change one without changing the other two.
namespace MCAddonPlugin {
    //The first class must be called PluginName
    public class PluginMain : AMPPlugin {
        private readonly Settings _settings;
        private readonly ILogger log;
        private readonly IConfigSerializer _config;
        private readonly IPlatformInfo platform;
        private readonly IRunningTasksManager _tasks;
        private readonly MinecraftApp app;
        private readonly IPluginMessagePusher message;
        private readonly IFeatureManager features;

        //All constructor arguments after currentPlatform are optional, and you may ommit them if you don't
        //need that particular feature. The features you request don't have to be in any particular order.
        //Warning: Do not add new features to the feature manager here, only do that in Init();
        public PluginMain(ILogger log, IConfigSerializer config, IPlatformInfo platform,
            IRunningTasksManager taskManager, IApplicationWrapper Application,
            IPluginMessagePusher Message, IFeatureManager Features) {
            //These are the defaults, but other mechanisms are available.
            config.SaveMethod = PluginSaveMethod.KVP;
            config.KVPSeparator = "=";
            this.log = log;
            _config = config;
            this.platform = platform;
            _settings = config.Load<Settings>(AutoSave: true); //Automatically saves settings when they're changed.
            _tasks = taskManager;
            app = (MinecraftApp) Application;
            message = Message;
            features = Features;
            _settings.SettingModified += Settings_SettingModified;
        }

        /*
            Rundown of the different interfaces you can ask for in your constructor:
            IRunningTasksManager - Used to put tasks in the left hand side of AMP to update the user on progress.
            IApplicationWrapper - A reference to the running application from the running module.
            IPluginMessagePusher - For 'push' type notifications that your front-end code can react to via PushedMessage in Plugin.js
            IFeatureManager - To expose/consume features to/from other plugins.
        */

        //Your init function should not invoke any code that depends on other plugins.
        //You may expose functionality via IFeatureManager.RegisterFeature, but you cannot yet use RequestFeature.
        public override void Init(out WebMethodsBase APIMethods) {
            APIMethods = new WebMethods(log, features, _settings, app);
        }

        void Settings_SettingModified(object sender, SettingModifiedEventArgs e) {
            //If you need to export settings to a different application, this is where you'd do it.
        }

        public override bool HasFrontendContent => true;

        //This gets called after every plugin is loaded. From here on it's safe
        //to use code that depends on other plugins and use IFeatureManager.RequestFeature
        public override void PostInit() { }

        public override IEnumerable<SettingStore> SettingStores => Utilities.EnumerableFrom(_settings);
    }
}
