using System;
using System.Collections;
using System.Collections.Generic;
using VroidStudioMcp;

namespace NingyoExtensions
{
    internal static class LookHandlers
    {
        public static void Register()
        {
            McpRegistry.RegisterTool(
                name: "look_get",
                description:
                    "Read all Look tab (GeneralEditor) parameters: outline widths, rim light strength/compression, " +
                    "shading shift/toony/normal smoothing, spring bone amplitude, and eye excursion limits. Read-only.",
                order: Array.Empty<string>(),
                required: Array.Empty<string>(),
                props: new Dictionary<string, object>(),
                handler: LookGetCoroutine,
                requireEdit: true
            );

            McpRegistry.RegisterTool(
                name: "look_set",
                description:
                    "Set a Look tab (GeneralEditor) parameter for outline, rim_light, shading, spring_bone, or eye_excursion. Blocking.",
                order: new[] { "category", "part", "param", "value" },
                required: new[] { "category", "param", "value" },
                props: new Dictionary<string, object>
                {
                    ["category"] = McpRegistry.Prop("string", "Look category.", "outline", "rim_light", "shading", "spring_bone", "eye_excursion"),
                    ["part"] = McpRegistry.Prop("string", "Target part (hair, face, body, accessory, clothing). Not needed for eye_excursion."),
                    ["param"] = McpRegistry.Prop("string", "Parameter name (e.g. outline_width, strength, compression, shading_shift, shading_toony, normal_smoothing, amplitude, horizontal_inner, horizontal_outer, vertical_upper, vertical_lower)."),
                    ["value"] = McpRegistry.Prop("number", "Target numeric value.")
                },
                handler: LookSetCoroutine,
                requireEdit: true
            );
        }

        private static IEnumerator LookGetCoroutine(CommandDispatcher.Context ctx)
        {
            var ge = Il2CppHelpers.GetEditRootViewModel()?._GeneralEditor_k__BackingField;
            if (ge == null) { ctx.Error = "look_get: GeneralEditor not available"; yield break; }

            var result = new Dictionary<string, object>();

            var ol = ge.OutlineParameterEditor;
            if (ol != null)
            {
                result["outline"] = new Dictionary<string, object>
                {
                    ["hair"] = new Dictionary<string, object> { ["outline_width"] = Il2CppHelpers.Round3(ol.HairParameterSet?.OutlineWidth ?? 0f) },
                    ["face"] = new Dictionary<string, object> { ["outline_width"] = Il2CppHelpers.Round3(ol.FaceParameterSet?.OutlineWidth ?? 0f) },
                    ["body"] = new Dictionary<string, object> { ["outline_width"] = Il2CppHelpers.Round3(ol.BodyParameterSet?.OutlineWidth ?? 0f) },
                    ["accessory"] = new Dictionary<string, object> { ["outline_width"] = Il2CppHelpers.Round3(ol.AccessoryParameterSet?.OutlineWidth ?? 0f) },
                };
            }

            var rl = ge.RimLightEditor;
            if (rl != null)
            {
                result["rim_light"] = new Dictionary<string, object>
                {
                    ["hair"] = new Dictionary<string, object>
                    {
                        ["strength"] = Il2CppHelpers.Round3(rl.HairParameterSet?.Strength ?? 0f),
                        ["compression"] = Il2CppHelpers.Round3(rl.HairParameterSet?.Compression ?? 0f)
                    },
                    ["face"] = new Dictionary<string, object>
                    {
                        ["strength"] = Il2CppHelpers.Round3(rl.FaceParameterSet?.Strength ?? 0f),
                        ["compression"] = Il2CppHelpers.Round3(rl.FaceParameterSet?.Compression ?? 0f)
                    },
                    ["body"] = new Dictionary<string, object>
                    {
                        ["strength"] = Il2CppHelpers.Round3(rl.BodyParameterSet?.Strength ?? 0f),
                        ["compression"] = Il2CppHelpers.Round3(rl.BodyParameterSet?.Compression ?? 0f)
                    },
                };
            }

            var sh = ge.ShadingParameterEditor;
            if (sh != null)
            {
                result["shading"] = new Dictionary<string, object>
                {
                    ["hair"] = new Dictionary<string, object>
                    {
                        ["shading_shift"] = Il2CppHelpers.Round3(sh.HairParameterSet?.ShadingShift ?? 0f),
                        ["shading_toony"] = Il2CppHelpers.Round3(sh.HairParameterSet?.ShadingToony ?? 0f)
                    },
                    ["face"] = new Dictionary<string, object>
                    {
                        ["shading_shift"] = Il2CppHelpers.Round3(sh.FaceParameterSet?.ShadingShift ?? 0f),
                        ["shading_toony"] = Il2CppHelpers.Round3(sh.FaceParameterSet?.ShadingToony ?? 0f),
                        ["normal_smoothing"] = Il2CppHelpers.Round3(sh.FaceParameterSet?.NormalSmoothing ?? 0f)
                    },
                    ["body"] = new Dictionary<string, object>
                    {
                        ["shading_shift"] = Il2CppHelpers.Round3(sh.BodyParameterSet?.ShadingShift ?? 0f),
                        ["shading_toony"] = Il2CppHelpers.Round3(sh.BodyParameterSet?.ShadingToony ?? 0f)
                    },
                };
            }

            var sb = ge.SpringBonePropertyEditor;
            if (sb != null)
            {
                result["spring_bone"] = new Dictionary<string, object>
                {
                    ["clothing"] = new Dictionary<string, object> { ["amplitude"] = Il2CppHelpers.Round3(sb.ClothingParameterSet?.Amplitude ?? 0f) },
                    ["accessory"] = new Dictionary<string, object> { ["amplitude"] = Il2CppHelpers.Round3(sb.AccessoryParameterSet?.Amplitude ?? 0f) },
                    ["body"] = new Dictionary<string, object> { ["amplitude"] = Il2CppHelpers.Round3(sb.BodyParameterSet?.Amplitude ?? 0f) },
                };
            }

            var ee = ge.EyeExcursionEditor;
            if (ee != null)
            {
                result["eye_excursion"] = new Dictionary<string, object>
                {
                    ["horizontal_inner"] = Il2CppHelpers.Round3(ee.HorizontalInner),
                    ["horizontal_outer"] = Il2CppHelpers.Round3(ee.HorizontalOuter),
                    ["vertical_upper"] = Il2CppHelpers.Round3(ee.VerticalUpper),
                    ["vertical_lower"] = Il2CppHelpers.Round3(ee.VerticalLower),
                };
            }

            ctx.Result = result;
        }

        private static IEnumerator LookSetCoroutine(CommandDispatcher.Context ctx)
        {
            var args = ctx.Args ?? Array.Empty<string>();
            var cat = (args.Length > 0 ? args[0] : "")?.Trim().ToLowerInvariant();
            var part = (args.Length > 1 ? args[1] : "")?.Trim().ToLowerInvariant();
            var param = (args.Length > 2 ? args[2] : "")?.Trim().ToLowerInvariant();
            var valStr = args.Length > 3 ? args[3] : null;

            if (string.IsNullOrEmpty(cat) || string.IsNullOrEmpty(param) || !Il2CppHelpers.TryParseFloat(valStr, out var val))
            {
                ctx.Error = "look_set: 'category', 'param', and numeric 'value' are required";
                yield break;
            }

            var ge = Il2CppHelpers.GetEditRootViewModel()?._GeneralEditor_k__BackingField;
            if (ge == null) { ctx.Error = "look_set: GeneralEditor not available"; yield break; }

            switch (cat)
            {
                case "outline":
                {
                    var ol = ge.OutlineParameterEditor;
                    var ps = part switch
                    {
                        "hair" => ol?.HairParameterSet,
                        "face" => ol?.FaceParameterSet,
                        "body" => ol?.BodyParameterSet,
                        "accessory" => ol?.AccessoryParameterSet,
                        _ => null
                    };
                    if (ps == null) { ctx.Error = $"look_set(outline): unknown part '{part}' (hair|face|body|accessory)"; yield break; }
                    if (param is "outline_width" or "width" or "outlinewidth") ps.OutlineWidth = val;
                    else { ctx.Error = $"look_set(outline): unknown param '{param}' (use outline_width)"; yield break; }
                    break;
                }
                case "rim_light":
                {
                    var rl = ge.RimLightEditor;
                    var ps = part switch
                    {
                        "hair" => rl?.HairParameterSet,
                        "face" => rl?.FaceParameterSet,
                        "body" => rl?.BodyParameterSet,
                        _ => null
                    };
                    if (ps == null) { ctx.Error = $"look_set(rim_light): unknown part '{part}' (hair|face|body)"; yield break; }
                    if (param is "strength") ps.Strength = val;
                    else if (param is "compression") ps.Compression = val;
                    else { ctx.Error = $"look_set(rim_light): unknown param '{param}' (strength|compression)"; yield break; }
                    break;
                }
                case "shading":
                {
                    var sh = ge.ShadingParameterEditor;
                    if (part == "face")
                    {
                        var fps = sh?.FaceParameterSet;
                        if (fps == null) { ctx.Error = "look_set(shading): FaceParameterSet null"; yield break; }
                        if (param is "shading_shift" or "shift") fps.ShadingShift = val;
                        else if (param is "shading_toony" or "toony") fps.ShadingToony = val;
                        else if (param is "normal_smoothing" or "smoothing") fps.NormalSmoothing = val;
                        else { ctx.Error = $"look_set(shading/face): unknown param '{param}'"; yield break; }
                    }
                    else
                    {
                        if (part == "hair" && sh?.HairParameterSet != null)
                        {
                            if (param is "shading_shift" or "shift") sh.HairParameterSet.ShadingShift = val;
                            else if (param is "shading_toony" or "toony") sh.HairParameterSet.ShadingToony = val;
                            else { ctx.Error = $"look_set(shading): unknown param '{param}'"; yield break; }
                        }
                        else if (part == "body" && sh?.BodyParameterSet != null)
                        {
                            if (param is "shading_shift" or "shift") sh.BodyParameterSet.ShadingShift = val;
                            else if (param is "shading_toony" or "toony") sh.BodyParameterSet.ShadingToony = val;
                            else { ctx.Error = $"look_set(shading): unknown param '{param}'"; yield break; }
                        }
                        else
                        {
                            ctx.Error = $"look_set(shading): unknown part '{part}' (hair|face|body)";
                            yield break;
                        }
                    }
                    break;
                }
                case "spring_bone":
                {
                    var sb = ge.SpringBonePropertyEditor;
                    var ps = part switch
                    {
                        "clothing" or "outfit" => sb?.ClothingParameterSet,
                        "accessory" => sb?.AccessoryParameterSet,
                        "body" => sb?.BodyParameterSet,
                        _ => null
                    };
                    if (ps == null) { ctx.Error = $"look_set(spring_bone): unknown part '{part}' (clothing|accessory|body)"; yield break; }
                    if (param is "amplitude") ps.Amplitude = val;
                    else { ctx.Error = $"look_set(spring_bone): unknown param '{param}' (use amplitude)"; yield break; }
                    break;
                }
                case "eye_excursion":
                {
                    var ee = ge.EyeExcursionEditor;
                    if (ee == null) { ctx.Error = "look_set: EyeExcursionEditor null"; yield break; }
                    if (param is "horizontal_inner") ee.HorizontalInner = val;
                    else if (param is "horizontal_outer") ee.HorizontalOuter = val;
                    else if (param is "vertical_upper") ee.VerticalUpper = val;
                    else if (param is "vertical_lower") ee.VerticalLower = val;
                    else { ctx.Error = $"look_set(eye_excursion): unknown param '{param}'"; yield break; }
                    break;
                }
                default:
                    ctx.Error = $"look_set: unknown category '{cat}'";
                    yield break;
            }

            yield return Il2CppHelpers.WaitUntilSettled(15f, 3);
            ctx.Result = new Dictionary<string, object>
            {
                ["category"] = cat,
                ["part"] = part,
                ["param"] = param,
                ["value"] = Il2CppHelpers.Round3(val)
            };
        }
    }
}
