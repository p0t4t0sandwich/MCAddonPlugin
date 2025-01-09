using System.Diagnostics.CodeAnalysis;
using ModuleShared;

namespace MCAddonPlugin.Submodules.ServerTypeUtils;

/// <summary>
/// Minecraft version enum used to parse and compare different MC runtimes
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum MinecraftVersion {
    [EnumDisplayName("1.14")]
    V1_14 = 1140,
    [EnumDisplayName("1.14.1")]
    V1_14_1 = 1141,
    [EnumDisplayName("1.14.2")]
    V1_14_2 = 1142,
    [EnumDisplayName("1.14.3")]
    V1_14_3 = 1143,
    [EnumDisplayName("1.14.4")]
    V1_14_4 = 1144,
    [EnumDisplayName("1.15")]
    V1_15 = 1150,
    [EnumDisplayName("1.15.1")]
    V1_15_1 = 1151,
    [EnumDisplayName("1.15.2")]
    V1_15_2 = 1152,
    [EnumDisplayName("1.16")]
    V1_16 = 1160,
    [EnumDisplayName("1.16.1")]
    V1_16_1 = 1161,
    [EnumDisplayName("1.16.2")]
    V1_16_2 = 1162,
    [EnumDisplayName("1.16.3")]
    V1_16_3 = 1163,
    [EnumDisplayName("1.16.4")]
    V1_16_4 = 1164,
    [EnumDisplayName("1.16.5")]
    V1_16_5 = 1165,
    [EnumDisplayName("1.17")]
    V1_17 = 1170,
    [EnumDisplayName("1.17.1")]
    V1_17_1 = 1171,
    [EnumDisplayName("1.18")]
    V1_18 = 1180,
    [EnumDisplayName("1.18.1")]
    V1_18_1 = 1181,
    [EnumDisplayName("1.18.2")]
    V1_18_2 = 1182,
    [EnumDisplayName("1.19")]
    V1_19 = 1190,
    [EnumDisplayName("1.19.1")]
    V1_19_1 = 1191,
    [EnumDisplayName("1.19.2")]
    V1_19_2 = 1192,
    [EnumDisplayName("1.19.3")]
    V1_19_3 = 1193,
    [EnumDisplayName("1.19.4")]
    V1_19_4 = 1194,
    [EnumDisplayName("1.20")]
    V1_20 = 1200,
    [EnumDisplayName("1.20.1")]
    V1_20_1 = 1201,
    [EnumDisplayName("1.20.2")]
    V1_20_2 = 1202,
    [EnumDisplayName("1.20.3")]
    V1_20_3 = 1203,
    [EnumDisplayName("1.20.4")]
    V1_20_4 = 1204,
    [EnumDisplayName("1.20.5")]
    V1_20_5 = 1205,
    [EnumDisplayName("1.20.6")]
    V1_20_6 = 1206,
    [EnumDisplayName("1.21")]
    V1_21 = 1210,
    [EnumDisplayName("1.21.1")]
    V1_21_1 = 1211,
    [EnumDisplayName("1.21.2")]
    V1_21_2 = 1212,
    [EnumDisplayName("1.21.3")]
    V1_21_3 = 1213,
    [EnumDisplayName("1.21.4")]
    V1_21_4 = 1214
}
