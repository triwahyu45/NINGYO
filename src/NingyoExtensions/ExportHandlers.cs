using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using VRoidStudio.Exporter;
using VRoidStudio.GUI.AvatarEditor.PhotoBooth;
using VRoidStudio.PhotoBooth;
using VroidStudioMcp;

namespace NingyoExtensions
{
    internal static class ExportHandlers
    {
        public static void Register()
        {
            McpRegistry.RegisterTool(
                name: "export_vrm",
                description:
                    "Export the currently open model directly to a .vrm file (VRM 1.0 or VRM 0.0) without OS dialogs, " +
                    "with optional optimization controls (hair_combine, mesh_erasure, material_reduction, atlas_resolution, " +
                    "texture_compression, disable_spring_bones) and clean return to Edit Mode. Blocking.",
                order: new[]
                {
                    "path", "version", "title", "author", "model_version",
                    "hair_combine", "mesh_erasure", "material_reduction",
                    "atlas_resolution", "texture_compression", "disable_spring_bones"
                },
                required: new[] { "path" },
                props: new Dictionary<string, object>
                {
                    ["path"] = McpRegistry.Prop("string", "Destination .vrm file path."),
                    ["version"] = McpRegistry.Prop("string", "VRM format version: '1.0' (default) or '0.0'.", "1.0", "0.0"),
                    ["title"] = McpRegistry.Prop("string", "Optional avatar title metadata (defaults to 'Avatar')."),
                    ["author"] = McpRegistry.Prop("string", "Optional avatar author metadata (defaults to 'VRoid Studio')."),
                    ["model_version"] = McpRegistry.Prop("string", "Optional avatar version metadata string (defaults to '1.0')."),
                    ["hair_combine"] = McpRegistry.Prop("boolean", "Combine hair meshes during export."),
                    ["mesh_erasure"] = McpRegistry.Prop("boolean", "Erase hidden body meshes under clothing."),
                    ["material_reduction"] = McpRegistry.Prop("string", "Material/atlas reduction preset.", "none", "low", "medium", "high"),
                    ["atlas_resolution"] = McpRegistry.Prop("string", "Texture atlas resolution when reducing materials.", "4k", "2k"),
                    ["texture_compression"] = McpRegistry.Prop("string", "Texture compression mode.", "original", "shrinked"),
                    ["disable_spring_bones"] = McpRegistry.Prop("boolean", "Disable SpringBones on export (default false).")
                },
                handler: ExportVrmCoroutine,
                requireEdit: true
            );
        }

        private static IEnumerator ExportVrmCoroutine(CommandDispatcher.Context ctx)
        {
            var args = ctx.Args ?? Array.Empty<string>();
            var rawPath = args.Length > 0 ? args[0] : null;
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                ctx.Error = "export_vrm: 'path' is required";
                yield break;
            }

            string Arg(int i) => (args.Length > i && !string.IsNullOrWhiteSpace(args[i])) ? args[i].Trim() : null;

            var versionArg = (Arg(1) ?? "1.0").ToLowerInvariant();
            var isVrm0 = versionArg is "0.0" or "0" or "vrm0";
            var title = Arg(2) ?? "Avatar";
            var author = Arg(3) ?? "VRoid Studio";
            var modelVersion = Arg(4) ?? "1.0";

            var winPath = WinePaths.ToDos(rawPath.Trim());
            var parentDir = Path.GetDirectoryName(winPath);
            if (!string.IsNullOrEmpty(parentDir))
                Directory.CreateDirectory(parentDir);

            if (File.Exists(winPath))
            {
                try { File.Delete(winPath); }
                catch (Exception ex)
                {
                    ctx.Error = $"export_vrm: could not overwrite '{winPath}': {ex.Message}";
                    yield break;
                }
            }

            var deadlineReady = Time.realtimeSinceStartup + 30f;
            while (!Il2CppHelpers.IsModelReady() && Time.realtimeSinceStartup < deadlineReady)
                yield return null;

            if (!Il2CppHelpers.IsModelReady())
            {
                ctx.Error = "export_vrm: model did not become ready within 30s";
                yield break;
            }

            var editVm = Il2CppHelpers.GetEditRootViewModel();
            var pbVm = Il2CppHelpers.GetPhotoBoothViewModel();
            if (pbVm == null)
            {
                ctx.Error = "export_vrm: PhotoBoothViewModel not found";
                yield break;
            }

            NingyoPlugin.Logger?.LogInfo($"export_vrm v2 starting: path='{winPath}', vrm0={isVrm0}, title='{title}'");

            bool enteredExport = false;
            if (editVm != null)
            {
                try
                {
                    editVm.EnterToVRMExport();
                    enteredExport = true;
                }
                catch (Exception ex)
                {
                    NingyoPlugin.Logger?.LogWarning($"EnterToVRMExport fallback: {ex.Message}");
                }
            }
            if (!enteredExport)
            {
                try
                {
                    pbVm.SelectExportMode();
                    enteredExport = true;
                }
                catch (Exception ex)
                {
                    ctx.Error = $"export_vrm: failed to enter export mode: {ex.Message}";
                    yield break;
                }
            }

            var exportWait = Time.realtimeSinceStartup + 15f;
            while ((pbVm.ExportSettings == null || pbVm.V1PhotoBooth == null) && Time.realtimeSinceStartup < exportWait)
                yield return null;

            for (int i = 0; i < 15; i++)
                yield return null;

            try { pbVm.EnsureReference(); }
            catch (Exception ex) { NingyoPlugin.Logger?.LogWarning($"EnsureReference warning: {ex.Message}"); }

            for (int i = 0; i < 5; i++)
                yield return null;

            var settings = pbVm.ExportSettings;
            if (settings != null)
            {
                if (Il2CppHelpers.TryParseBool(Arg(5), out var hairCombine))
                    settings.IsEnableHairCombination = hairCombine;

                if (settings.MeshReduction != null)
                {
                    if (Il2CppHelpers.TryParseBool(Arg(6), out var meshErasure))
                        settings.MeshReduction.MeshErasure = meshErasure;
                    if (Il2CppHelpers.TryParseBool(Arg(10), out var disableSpring))
                        settings.MeshReduction.DisableSpringBones = disableSpring;
                }

                var matRed = Arg(7)?.ToLowerInvariant();
                if (matRed != null)
                {
                    MaterialReductionMode mode = matRed switch
                    {
                        "none" => MaterialReductionMode.None,
                        "low" => MaterialReductionMode.Low,
                        "medium" => MaterialReductionMode.Medium,
                        "high" => MaterialReductionMode.High,
                        _ => settings.CurrentMaterialReductionMode
                    };
                    try { settings.SetMaterialReductionMode(mode); } catch { }
                }

                var atlas = Arg(8)?.ToLowerInvariant();
                if (atlas != null)
                {
                    if (atlas is "4k" or "4096") settings.AtlasResolutionIndex = 0;
                    else if (atlas is "2k" or "2048") settings.AtlasResolutionIndex = 1;
                }

                var texComp = Arg(9)?.ToLowerInvariant();
                if (texComp != null)
                {
                    if (texComp == "original") settings.TextureCompressionModeIndex = 0;
                    else if (texComp == "shrinked") settings.TextureCompressionModeIndex = 1;
                }
            }

            Il2CppSystem.Threading.Tasks.TaskCompletionSource<bool> tcs = null;
            string startError = null;
            try
            {
                tcs = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<bool>();
                if (isVrm0)
                {
                    var meta0 = VRoidStudioExportUtil.BuildDefaultVRM0MetaData()
                        ?? throw new InvalidOperationException("BuildDefaultVRM0MetaData returned null");
                    meta0.title = title;
                    meta0.author = author;
                    meta0.version = modelVersion;
                    meta0.reference = "NINGYO";
                    pbVm.OnVRMSettingChanged(meta0, winPath, tcs);
                }
                else
                {
                    var meta1 = VRoidStudioExportUtil.BuildDefaultVRM10MetaData()
                        ?? throw new InvalidOperationException("BuildDefaultVRM10MetaData returned null");
                    meta1.Name = title;
                    meta1.Version = modelVersion;
                    meta1.CopyrightInformation = author;
                    if (meta1.Authors != null)
                    {
                        meta1.Authors.Clear();
                        meta1.Authors.Add(author);
                    }
                    pbVm.OnVRM1SettingChanged(meta1, winPath, tcs);
                }
            }
            catch (Exception ex)
            {
                startError = ex.ToString();
                NingyoPlugin.Logger?.LogError($"OnVRMSettingChanged threw: {startError}");
            }

            if (startError != null)
            {
                ExitExportScreenCleanly(pbVm);
                ctx.Error = $"export_vrm: export trigger failed: {startError}";
                yield break;
            }

            var exportDeadline = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < exportDeadline)
            {
                bool taskDone = false;
                try { taskDone = tcs?.Task?.IsCompleted ?? false; } catch { }
                if (taskDone) break;
                yield return null;
            }

            var settleEnd = Time.realtimeSinceStartup + 0.4f;
            while (Time.realtimeSinceStartup < settleEnd)
                yield return null;

            ExitExportScreenCleanly(pbVm);

            for (int i = 0; i < 10; i++)
                yield return null;

            var readyAfter = Time.realtimeSinceStartup + 12f;
            while (Il2CppHelpers.IsModelUpdating() && Time.realtimeSinceStartup < readyAfter)
                yield return null;

            CameraRebindPatch.EnsureActiveCameraPosition();

            if (!File.Exists(winPath))
            {
                ctx.Error = $"export_vrm: file not produced at '{winPath}'";
                yield break;
            }

            var fi = new FileInfo(winPath);
            if (fi.Length == 0)
            {
                ctx.Error = $"export_vrm: produced 0-byte file at '{winPath}'";
                yield break;
            }

            NingyoPlugin.Logger?.LogInfo($"export_vrm v2 succeeded: '{winPath}' ({fi.Length} bytes)");
            ctx.Result = new Dictionary<string, object>
            {
                ["path"] = WinePaths.ToHost(winPath),
                ["bytes"] = fi.Length,
                ["version"] = isVrm0 ? "0.0" : "1.0",
                ["title"] = title,
                ["author"] = author,
                ["model_version"] = modelVersion
            };
        }

        private static void ExitExportScreenCleanly(PhotoBoothViewModel pbVm)
        {
            Il2CppHelpers.CleanupPhotoBoothAndExport(pbVm, isExport: true);
        }
    }
}
