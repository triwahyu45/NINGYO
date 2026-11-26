using System;
using System.Collections;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using VRoidStudio.GUI.AvatarEditor.PhotoBooth;
using VroidStudioMcp;

namespace NingyoExtensions
{
    internal static class PhotoBoothHandlers
    {
        public static void Register()
        {
            McpRegistry.RegisterTool(
                name: "photobooth",
                description:
                    "Control PhotoBooth mode inside VRoid Studio: enter/exit PhotoBooth, inspect or switch pose_index, " +
                    "auto_blink, look_at_camera, bg_color (#RRGGBB), bg_alpha (0..1), reset_pose, or reset_lighting. Blocking.",
                order: new[] { "action", "pose_index", "auto_blink", "look_at_camera", "bg_color", "bg_alpha", "reset_pose", "reset_lighting" },
                required: new[] { "action" },
                props: new Dictionary<string, object>
                {
                    ["action"] = McpRegistry.Prop("string", "PhotoBooth action.", "enter", "exit", "state", "set"),
                    ["pose_index"] = McpRegistry.Prop("integer", "Pose mode index from poses[] list."),
                    ["auto_blink"] = McpRegistry.Prop("boolean", "Enable or disable auto-blink."),
                    ["look_at_camera"] = McpRegistry.Prop("boolean", "Enable or disable eye gaze tracking camera."),
                    ["bg_color"] = McpRegistry.Prop("string", "Background color as '#RRGGBB'."),
                    ["bg_alpha"] = McpRegistry.Prop("number", "Background opacity (0..1)."),
                    ["reset_pose"] = McpRegistry.Prop("boolean", "Reset current pose to default."),
                    ["reset_lighting"] = McpRegistry.Prop("boolean", "Reset PhotoBooth lighting to default.")
                },
                handler: PhotoBoothCoroutine,
                requireEdit: true
            );
        }

        private static IEnumerator PhotoBoothCoroutine(CommandDispatcher.Context ctx)
        {
            var args = ctx.Args ?? Array.Empty<string>();
            var action = (args.Length > 0 ? args[0] : "")?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
            {
                ctx.Error = "photobooth: 'action' is required (enter|exit|state|set)";
                yield break;
            }

            string Arg(int i) => (args.Length > i && !string.IsNullOrWhiteSpace(args[i])) ? args[i].Trim() : null;

            var editVm = Il2CppHelpers.GetEditRootViewModel();
            var pbVm = Il2CppHelpers.GetPhotoBoothViewModel();
            if (pbVm == null) { ctx.Error = "photobooth: PhotoBoothViewModel not available"; yield break; }

            if (action == "enter")
            {
                if (editVm != null)
                {
                    try { editVm.EnterToPhotoBooth(); }
                    catch { pbVm.SelectPhotoBoothMode(); }
                }
                else
                {
                    pbVm.SelectPhotoBoothMode();
                }

                for (int i = 0; i < 15; i++) yield return null;
                CameraRebindPatch.EnsureActiveCameraPosition();
                ctx.Result = BuildState(pbVm, true);
                yield break;
            }

            if (action == "exit")
            {
                Il2CppHelpers.CleanupPhotoBoothAndExport(pbVm, isExport: false);

                for (int i = 0; i < 10; i++) yield return null;
                CameraRebindPatch.EnsureActiveCameraPosition();
                ctx.Result = new Dictionary<string, object> { ["in_photobooth"] = false };
                yield break;
            }

            if (action == "set")
            {
                if (Il2CppHelpers.TryParseInt(Arg(1), out var poseIdx))
                {
                    if (PhotoBoothViewModel.PoseModeList != null && poseIdx >= 0 && poseIdx < PhotoBoothViewModel.PoseModeList.Count)
                    {
                        pbVm.PoseModeIndex = poseIdx;
                        pbVm.OnPoseModeIndexChanged((PoseMode)poseIdx);
                    }
                    else
                    {
                        ctx.Error = $"photobooth(set): pose_index {poseIdx} out of range";
                        yield break;
                    }
                }

                if (Il2CppHelpers.TryParseBool(Arg(2), out var blink))
                    pbVm.isAutoBlink = blink;

                if (Il2CppHelpers.TryParseBool(Arg(3), out var look))
                    pbVm.isLookAtCameraOn = look;

                if (Il2CppHelpers.TryParseColor(Arg(4), out var col))
                {
                    if (pbVm.avatarCamera != null)
                        pbVm.avatarCamera.backgroundColor = col;
                    pbVm.UpdateBackGroundColor();
                }

                if (Il2CppHelpers.TryParseFloat(Arg(5), out var alpha))
                    pbVm.colorAlpha = Mathf.Clamp01(alpha);

                if (Il2CppHelpers.TryParseBool(Arg(6), out var rstPose) && rstPose)
                    pbVm.ResetPose();

                if (Il2CppHelpers.TryParseBool(Arg(7), out var rstLight) && rstLight)
                    pbVm.ResetLighting();

                for (int i = 0; i < 8; i++) yield return null;
            }
            else if (action != "state")
            {
                ctx.Error = $"photobooth: unknown action '{action}' (expected enter|exit|state|set)";
                yield break;
            }

            var activeType = Il2CppHelpers.GetGlobalBus()?.ScreenNavigation?.ActiveScreen?.GetIl2CppType()?.FullName ?? "";
            ctx.Result = BuildState(pbVm, activeType.Contains("PhotoBooth"));
        }

        private static Dictionary<string, object> BuildState(PhotoBoothViewModel pbVm, bool inPhotoBooth)
        {
            var poses = new List<object>();
            if (PhotoBoothViewModel.PoseModeList != null)
            {
                for (int i = 0; i < PhotoBoothViewModel.PoseModeList.Count; i++)
                {
                    poses.Add(new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["name"] = PhotoBoothViewModel.PoseModeList[i]?.TranslatedDisplayName ?? $"Pose {i}"
                    });
                }
            }

            return new Dictionary<string, object>
            {
                ["in_photobooth"] = inPhotoBooth,
                ["pose_index"] = pbVm.poseModeIndex,
                ["poses"] = poses,
                ["auto_blink"] = pbVm.isAutoBlink,
                ["look_at_camera"] = pbVm.isLookAtCameraOn,
                ["bg_alpha"] = Il2CppHelpers.Round3(pbVm.colorAlpha)
            };
        }
    }
}
