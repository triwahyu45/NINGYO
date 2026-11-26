using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace NingyoExtensions
{
    [BepInPlugin("com.triwahyu45.ningyo.extensions", "NINGYO VRoid Studio MCP Extensions", "2.0.0")]
    [BepInDependency("com.kokokoshka.vroidstudiomcp", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class NingyoPlugin : BasePlugin
    {
        internal static ManualLogSource Logger;
        internal static Harmony HarmonyInstance;

        public override void Load()
        {
            Logger = Log;
            HarmonyInstance = new Harmony("com.triwahyu45.ningyo.extensions");

            CameraRebindPatch.Apply(HarmonyInstance);
            ExportHandlers.Register();
            SessionExtHandlers.Register();
            LookHandlers.Register();
            AccessoryHandlers.Register();
            ExpressionHandlers.Register();
            TextureHandlers.Register();
            PhotoBoothHandlers.Register();

            Logger.LogInfo("NingyoExtensions v2.0.0 loaded: camera rebind patch + 16 MCP tools registered.");
        }
    }
}
