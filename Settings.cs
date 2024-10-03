using ModuleShared;

namespace MCAddonPlugin
{
    public class Settings : SettingStore
    {
        public enum SomeEnum
        {
            Foo,
            Bar,
            Baz,
            [EnumDisplayName("Cheese Burger")]
            Cheese_Burger
        }


        public class TemplateSettings : SettingSectionStore
        {
            [WebSetting("Message", "Some message", false)]
            public string Setting1 = "Hello World!";

            [WebSetting("Number", "Some number", false)]
            public int Setting2 = 1234;

            [WebSetting("Boolean", "Some toggle", false)]
            public bool Setting3 = false;

            [WebSetting("Enum", "Some enum", false)]
            public SomeEnum Setting4 = SomeEnum.Bar;
        }

        public TemplateSettings MainSettings = new TemplateSettings();
    }
}
