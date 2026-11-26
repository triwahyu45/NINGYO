using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VroidStudioMcp;

namespace NingyoExtensions
{
    internal static class SessionExtHandlers
    {
        public static void Register()
        {
            McpRegistry.RegisterTool(
                name: "undo",
                description: "Undo the last editing operation on the open model. Blocking.",
                order: Array.Empty<string>(),
                required: Array.Empty<string>(),
                props: new Dictionary<string, object>(),
                handler: UndoCoroutine,
                requireEdit: true
            );

            McpRegistry.RegisterTool(
                name: "redo",
                description: "Redo a previously undone editing operation on the open model. Blocking.",
                order: Array.Empty<string>(),
                required: Array.Empty<string>(),
                props: new Dictionary<string, object>(),
                handler: RedoCoroutine,
                requireEdit: true
            );

            McpRegistry.RegisterTool(
                name: "body_metrics",
                description:
                    "Measure the open character's real 3D height in cm, bounding box, anatomical landmarks (head/neck/hips/feet), " +
                    "SkinnedMeshRenderer count, and active transferable count. Read-only.",
                order: Array.Empty<string>(),
                required: Array.Empty<string>(),
                props: new Dictionary<string, object>(),
                handler: BodyMetricsCoroutine,
                requireEdit: true
            );
        }

        private static IEnumerator UndoCoroutine(CommandDispatcher.Context ctx)
        {
            var editVm = Il2CppHelpers.GetEditRootViewModel();
            if (editVm == null) { ctx.Error = "undo: EditRootScreen.ViewModel not available"; yield break; }

            if (!editVm.GetCanUndo())
            {
                ctx.Result = new Dictionary<string, object>
                {
                    ["undone"] = false,
                    ["can_undo"] = false,
                    ["can_redo"] = editVm.GetCanRedo()
                };
                yield break;
            }

            editVm.Undo();
            yield return Il2CppHelpers.WaitUntilSettled(20f, 3);

            ctx.Result = new Dictionary<string, object>
            {
                ["undone"] = true,
                ["can_undo"] = editVm.GetCanUndo(),
                ["can_redo"] = editVm.GetCanRedo()
            };
        }

        private static IEnumerator RedoCoroutine(CommandDispatcher.Context ctx)
        {
            var editVm = Il2CppHelpers.GetEditRootViewModel();
            if (editVm == null) { ctx.Error = "redo: EditRootScreen.ViewModel not available"; yield break; }

            if (!editVm.GetCanRedo())
            {
                ctx.Result = new Dictionary<string, object>
                {
                    ["redone"] = false,
                    ["can_undo"] = editVm.GetCanUndo(),
                    ["can_redo"] = false
                };
                yield break;
            }

            editVm.Redo();
            yield return Il2CppHelpers.WaitUntilSettled(20f, 3);

            ctx.Result = new Dictionary<string, object>
            {
                ["redone"] = true,
                ["can_undo"] = editVm.GetCanUndo(),
                ["can_redo"] = editVm.GetCanRedo()
            };
        }

        private static IEnumerator BodyMetricsCoroutine(CommandDispatcher.Context ctx)
        {
            Bounds bounds = default;
            bool hasBounds = false;
            int meshCount = 0;
            var landmarks = new Dictionary<string, object>();

            try
            {
                var animArr = UnityEngine.Object.FindObjectsOfType<Animator>();
                foreach (var aObj in Il2CppHelpers.Enumerate(animArr))
                {
                    var anim = aObj as Animator;
                    if (anim == null || !anim.isHuman) continue;
                    void AddBoneY(string key, HumanBodyBones bone)
                    {
                        var bt = anim.GetBoneTransform(bone);
                        if (bt != null)
                            landmarks[key] = Il2CppHelpers.Round3(bt.position.y * 100.0);
                    }
                    AddBoneY("head_y_cm", HumanBodyBones.Head);
                    AddBoneY("neck_y_cm", HumanBodyBones.Neck);
                    AddBoneY("hips_y_cm", HumanBodyBones.Hips);
                    AddBoneY("left_foot_y_cm", HumanBodyBones.LeftFoot);

                    var rootSmrs = anim.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                    foreach (var sObj in Il2CppHelpers.Enumerate(rootSmrs))
                    {
                        if (sObj is SkinnedMeshRenderer r && r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.y > 0.001f)
                        {
                            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                            else bounds.Encapsulate(r.bounds);
                            meshCount++;
                        }
                    }
                    break;
                }
            }
            catch { }

            if (!hasBounds)
            {
                if (!CameraApi.TryGetAvatarBounds(out bounds))
                {
                    ctx.Error = "body_metrics: could not compute avatar bounds";
                    yield break;
                }
                try
                {
                    var arr = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>();
                    foreach (var o in Il2CppHelpers.Enumerate(arr))
                    {
                        if (o is SkinnedMeshRenderer r && r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.y > 0.001f)
                            meshCount++;
                    }
                }
                catch { }
            }

            int transferablesCount = 0;
            try
            {
                var tDict = Il2CppHelpers.GetEditingContext()?.GetActiveLeafTransferables();
                if (tDict != null) transferablesCount = tDict.Count;
            }
            catch { }

            ctx.Result = new Dictionary<string, object>
            {
                ["total_height_cm"] = Il2CppHelpers.Round3(bounds.size.y * 100.0),
                ["width_cm"] = Il2CppHelpers.Round3(bounds.size.x * 100.0),
                ["depth_cm"] = Il2CppHelpers.Round3(bounds.size.z * 100.0),
                ["mesh_count"] = meshCount,
                ["active_transferables_count"] = transferablesCount,
                ["landmarks_cm"] = landmarks,
                ["bounds"] = new Dictionary<string, object>
                {
                    ["min"] = new[] { Il2CppHelpers.Round3(bounds.min.x), Il2CppHelpers.Round3(bounds.min.y), Il2CppHelpers.Round3(bounds.min.z) },
                    ["max"] = new[] { Il2CppHelpers.Round3(bounds.max.x), Il2CppHelpers.Round3(bounds.max.y), Il2CppHelpers.Round3(bounds.max.z) },
                    ["size"] = new[] { Il2CppHelpers.Round3(bounds.size.x), Il2CppHelpers.Round3(bounds.size.y), Il2CppHelpers.Round3(bounds.size.z) },
                }
            };
        }
    }
}
