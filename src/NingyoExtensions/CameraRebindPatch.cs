using System;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using VRoid.UI.Component;
using VroidStudioMcp;

namespace NingyoExtensions
{
    /// <summary>
    /// Fixes the post-main_menu / post-export_vrm camera freeze bug where VroidState._cameraPosition
    /// held a destroyed AvatarCameraPosition instance after BackToModelSelect().
    /// </summary>
    internal static class CameraRebindPatch
    {
        private static readonly FieldInfo CameraPosField =
            typeof(VroidState).GetField("_cameraPosition", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo HasCachedBoundsField =
            typeof(CameraApi).GetField("_hasCachedBounds", BindingFlags.Static | BindingFlags.NonPublic);

        public static void Apply(Harmony harmony)
        {
            var prefix = new HarmonyMethod(typeof(CameraRebindPatch).GetMethod(nameof(CameraApiPrefix), BindingFlags.Static | BindingFlags.NonPublic));
            foreach (var name in new[] { "ApplyCamera", "SetFocus", "GetState", "ProbeCamera", "ApplyCameraPreset", "ApplyCameraView" })
            {
                foreach (var m in typeof(CameraApi).GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name == name)
                        harmony.Patch(m, prefix: prefix);
                }
            }

            var backMethod = typeof(VRoid.Studio.EditRootScreen.ViewModel).GetMethod("BackToModelSelect", BindingFlags.Public | BindingFlags.Instance);
            if (backMethod != null)
            {
                var postfix = new HarmonyMethod(typeof(CameraRebindPatch).GetMethod(nameof(BackToModelSelectPostfix), BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(backMethod, postfix: postfix);
            }
        }

        private static void CameraApiPrefix() => EnsureActiveCameraPosition();

        private static void BackToModelSelectPostfix()
        {
            try
            {
                CameraPosField?.SetValue(null, null);
                HasCachedBoundsField?.SetValue(null, false);
            }
            catch { }
        }

        public static void EnsureActiveCameraPosition()
        {
            try
            {
                var bus = Il2CppHelpers.GetGlobalBus();
                if (bus == null) return;

                AvatarCameraPosition target = null;
                var activeScreen = bus.ScreenNavigation?.ActiveScreen;
                var screenType = activeScreen?.GetIl2CppType()?.FullName ?? "";
                if (screenType.Contains("PhotoBooth") || screenType.Contains("VRMExport"))
                    target = bus.PhotoBoothAvatarCameraPosition ?? bus.EditAvatarCameraPosition;
                else
                    target = bus.EditAvatarCameraPosition ?? bus.PhotoBoothAvatarCameraPosition;

                if (target != null && target)
                {
                    CameraPosField?.SetValue(null, target);
                }
            }
            catch { }
        }
    }
}
