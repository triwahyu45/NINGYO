using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using VRoidCore.Common.SpecificTypes;
using VRoidCore.Draw.TiledBitmap;
using VRoidCore.Editing;
using VRoidCore.Editing.History.Command;
using VRoidCore.Editing.Query;
using VroidStudioMcp;

namespace NingyoExtensions
{
    internal static class TextureHandlers
    {
        public static void Register()
        {
            McpRegistry.RegisterTool(
                name: "texture_list",
                description:
                    "List all active editable textures and raster layers across Face/Hair/Body/Outfit/Accessories without opening " +
                    "TextureEditor UI. Optional 'query' filters by type/resource_name/transferable_id. Read-only.",
                order: new[] { "query" },
                required: Array.Empty<string>(),
                props: new Dictionary<string, object>
                {
                    ["query"] = McpRegistry.Prop("string", "Optional case-insensitive filter (e.g. Tops, FaceSkin, Iris, Bottoms).")
                },
                handler: TextureListCoroutine,
                requireEdit: true
            );

            McpRegistry.RegisterTool(
                name: "texture_export_layer",
                description:
                    "Export a live VRoid raster texture layer directly to a PNG file on disk (headless, zero OS dialogs).",
                order: new[] { "transferable_id", "image_id", "layer_index", "path" },
                required: new[] { "transferable_id", "image_id", "path" },
                props: new Dictionary<string, object>
                {
                    ["transferable_id"] = McpRegistry.Prop("string", "transferable_id from texture_list()."),
                    ["image_id"] = McpRegistry.Prop("string", "image_id from texture_list()."),
                    ["layer_index"] = McpRegistry.Prop("integer", "Raster layer index (default 0)."),
                    ["path"] = McpRegistry.Prop("string", "Destination .png file path.")
                },
                handler: TextureExportLayerCoroutine,
                requireEdit: true
            );

            McpRegistry.RegisterTool(
                name: "texture_import_layer",
                description:
                    "Import a PNG file directly into a live VRoid raster texture layer and rebuild the character immediately " +
                    "(headless, zero OS dialogs, undoable). Blocking.",
                order: new[] { "transferable_id", "image_id", "layer_index", "path" },
                required: new[] { "transferable_id", "image_id", "path" },
                props: new Dictionary<string, object>
                {
                    ["transferable_id"] = McpRegistry.Prop("string", "transferable_id from texture_list()."),
                    ["image_id"] = McpRegistry.Prop("string", "image_id from texture_list()."),
                    ["layer_index"] = McpRegistry.Prop("integer", "Raster layer index (default 0)."),
                    ["path"] = McpRegistry.Prop("string", "Source .png file path.")
                },
                handler: TextureImportLayerCoroutine,
                requireEdit: true
            );
        }

        private static IEnumerator TextureListCoroutine(CommandDispatcher.Context ctx)
        {
            var editingCtx = Il2CppHelpers.GetEditingContext();
            var activeModel = editingCtx?.ActiveModel;
            if (editingCtx == null || activeModel == null)
            {
                ctx.Error = "texture_list: Editing Context or ActiveModel not available";
                yield break;
            }

            var filter = (ctx.Args != null && ctx.Args.Length > 0 && !string.IsNullOrWhiteSpace(ctx.Args[0]))
                ? ctx.Args[0].Trim()
                : null;

            var leafDict = editingCtx.GetActiveLeafTransferables();
            var entries = new List<object>();

            foreach (var kv in leafDict)
            {
                var tid = kv.Key;
                var info = kv.Value;
                var tType = info?.Type ?? "";
                var gType = info?.GroupType ?? "";
                var resName = info?.ResourceName ?? "";

                if (filter != null &&
                    !tid.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !tType.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !gType.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !resName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                Il2CppReferenceArray<EditableImagePath> imgPaths = null;
                try
                {
                    imgPaths = new GetEditableImagePathsForTransferableQuery(tid).Execute(activeModel);
                }
                catch { continue; }

                if (imgPaths == null || imgPaths.Length == 0) continue;

                for (int ii = 0; ii < imgPaths.Length; ii++)
                {
                    var imgPath = imgPaths[ii];
                    if (imgPath == null) continue;

                    var layersList = new List<object>();
                    try
                    {
                        var rLayers = new GetEditableImageRasterLayersQuery(imgPath).Execute(activeModel);
                        if (rLayers != null)
                        {
                            for (int li = 0; li < rLayers.Length; li++)
                            {
                                var lp = rLayers[li];
                                if (lp == null) continue;
                                var treePath = lp.TryCast<EditableImageLayerTreePath>();
                                var nodeId = treePath?.NodeId;
                                string lname = $"Layer {li}";
                                bool lvis = true;
                                float lop = 1f;
                                try
                                {
                                    if (!string.IsNullOrEmpty(nodeId))
                                    {
                                        var lps = new GetEditableImageLayerTreeParameterSetQuery(new EditableImageLayerTreePath(imgPath, nodeId)).Execute(activeModel);
                                        if (lps != null)
                                        {
                                            lname = lps.Name ?? lname;
                                            lvis = lps.Visibility;
                                            lop = lps.Opacity;
                                        }
                                    }
                                }
                                catch { }

                                layersList.Add(new Dictionary<string, object>
                                {
                                    ["index"] = li,
                                    ["node_id"] = nodeId,
                                    ["name"] = lname,
                                    ["visible"] = lvis,
                                    ["opacity"] = Il2CppHelpers.Round3(lop)
                                });
                            }
                        }
                    }
                    catch { }

                    entries.Add(new Dictionary<string, object>
                    {
                        ["transferable_id"] = tid,
                        ["type"] = tType,
                        ["group_type"] = gType,
                        ["resource_name"] = resName,
                        ["image_id"] = imgPath.ImageId,
                        ["layers"] = layersList
                    });
                }
            }

            ctx.Result = new Dictionary<string, object>
            {
                ["count"] = entries.Count,
                ["textures"] = entries
            };
        }

        private static bool ResolveLayerPath(
            VRoidCore.Common.SpecificTypes.Model activeModel,
            string transferableId,
            string imageId,
            int layerIndex,
            out EditableImageRasterLayerPath layerPath,
            out string error)
        {
            layerPath = null;
            error = null;

            var imgPath = new EditableImagePath(transferableId, imageId);
            var layers = new GetEditableImageRasterLayersQuery(imgPath).Execute(activeModel);
            if (layers == null || layers.Length == 0)
            {
                error = $"No raster layers found for transferable_id='{transferableId}', image_id='{imageId}'";
                return false;
            }
            if (layerIndex < 0 || layerIndex >= layers.Length)
            {
                error = $"layer_index {layerIndex} out of range [0..{layers.Length - 1}]";
                return false;
            }

            layerPath = layers[layerIndex];
            return layerPath != null;
        }

        private static IEnumerator TextureExportLayerCoroutine(CommandDispatcher.Context ctx)
        {
            var args = ctx.Args ?? Array.Empty<string>();
            var tid = args.Length > 0 ? args[0]?.Trim() : null;
            var imgId = args.Length > 1 ? args[1]?.Trim() : null;
            int layerIdx = (args.Length > 2 && Il2CppHelpers.TryParseInt(args[2], out var parsedIdx)) ? parsedIdx : 0;
            var rawPath = args.Length > 3 ? args[3]?.Trim() : null;

            if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(imgId) || string.IsNullOrEmpty(rawPath))
            {
                ctx.Error = "texture_export_layer: 'transferable_id', 'image_id', and 'path' are required";
                yield break;
            }

            var activeModel = Il2CppHelpers.GetEditingContext()?.ActiveModel;
            if (activeModel == null) { ctx.Error = "texture_export_layer: ActiveModel not available"; yield break; }

            if (!ResolveLayerPath(activeModel, tid, imgId, layerIdx, out var layerPath, out var err))
            {
                ctx.Error = "texture_export_layer: " + err;
                yield break;
            }

            var res = new GetRasterLayerContentQuery(layerPath).Execute(activeModel);
            if (res == null || res.rgbaBytes == null)
            {
                ctx.Error = "texture_export_layer: GetRasterLayerContentQuery returned null";
                yield break;
            }

            int w = res.size.x;
            int h = res.size.y;
            var rgba = res.rgbaBytes;
            if (w <= 0 || h <= 0 || rgba.Length < w * h * 4)
            {
                ctx.Error = $"texture_export_layer: invalid bitmap {w}x{h} (bytes={rgba.Length})";
                yield break;
            }

            var winPath = WinePaths.ToDos(rawPath);
            var parent = Path.GetDirectoryName(winPath);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                tex.LoadRawTextureData(rgba);
                tex.Apply(false, false);
                var png = ImageConversion.EncodeToPNG(tex);
                if (png == null || png.Length == 0)
                {
                    ctx.Error = "texture_export_layer: EncodeToPNG returned empty";
                    yield break;
                }
                File.WriteAllBytes(winPath, png);
                ctx.Result = new Dictionary<string, object>
                {
                    ["path"] = WinePaths.ToHost(winPath),
                    ["width"] = w,
                    ["height"] = h,
                    ["bytes"] = png.Length,
                    ["transferable_id"] = tid,
                    ["image_id"] = imgId,
                    ["layer_index"] = layerIdx
                };
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }

        private static IEnumerator TextureImportLayerCoroutine(CommandDispatcher.Context ctx)
        {
            var args = ctx.Args ?? Array.Empty<string>();
            var tid = args.Length > 0 ? args[0]?.Trim() : null;
            var imgId = args.Length > 1 ? args[1]?.Trim() : null;
            int layerIdx = (args.Length > 2 && Il2CppHelpers.TryParseInt(args[2], out var parsedIdx)) ? parsedIdx : 0;
            var rawPath = args.Length > 3 ? args[3]?.Trim() : null;

            if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(imgId) || string.IsNullOrEmpty(rawPath))
            {
                ctx.Error = "texture_import_layer: 'transferable_id', 'image_id', and 'path' are required";
                yield break;
            }

            var winPath = WinePaths.ToDos(rawPath);
            if (!File.Exists(winPath))
            {
                ctx.Error = $"texture_import_layer: file not found '{winPath}'";
                yield break;
            }

            var editingCtx = Il2CppHelpers.GetEditingContext();
            var activeModel = editingCtx?.ActiveModel;
            if (editingCtx == null || activeModel == null)
            {
                ctx.Error = "texture_import_layer: Editing Context not available";
                yield break;
            }

            if (!ResolveLayerPath(activeModel, tid, imgId, layerIdx, out var layerPath, out var err))
            {
                ctx.Error = "texture_import_layer: " + err;
                yield break;
            }

            var fileBytes = File.ReadAllBytes(winPath);
            var ilBytes = new Il2CppStructArray<byte>(fileBytes.Length);
            for (int i = 0; i < fileBytes.Length; i++) ilBytes[i] = fileBytes[i];

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(tex, ilBytes, false))
                {
                    ctx.Error = $"texture_import_layer: failed to decode image '{winPath}'";
                    yield break;
                }

                int w = tex.width;
                int h = tex.height;
                var pixels = tex.GetPixels();
                var cmd = new LoadImageToEditableImageRasterLayerCommand(layerPath, new BitmapSize(w, h), pixels);
                var syncCmd = cmd.Cast<ISyncCommand<VRoidCore.Editing.Context.HistoryManagerContext, VRoidCore.Common.SpecificTypes.Model>>();
                editingCtx.ExecuteSyncCommand(syncCmd);
                yield return Il2CppHelpers.WaitUntilSettled(25f, 3);

                ctx.Result = new Dictionary<string, object>
                {
                    ["imported"] = true,
                    ["path"] = WinePaths.ToHost(winPath),
                    ["width"] = w,
                    ["height"] = h,
                    ["transferable_id"] = tid,
                    ["image_id"] = imgId,
                    ["layer_index"] = layerIdx
                };
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }
    }
}
