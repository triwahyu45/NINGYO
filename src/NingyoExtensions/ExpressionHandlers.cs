using System;
using System.Collections;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using VRoid.Studio.ExpressionParameter;
using VroidStudioMcp;

namespace NingyoExtensions
{
    internal static class ExpressionHandlers
    {
        public static void Register()
        {
            McpRegistry.RegisterTool(
                name: "expressions_list",
                description: "List Expression Editor blendshape categories, parts, and current parameter values. Read-only.",
                order: new[] { "category" },
                required: Array.Empty<string>(),
                props: new Dictionary<string, object>
                {
                    ["category"] = McpRegistry.Prop("string", "Optional expression category filter (e.g. Joy, Angry, Sorrow, Fun, Surprised, Neutral).")
                },
                handler: ExpressionsListCoroutine,
                requireEdit: true
            );

            McpRegistry.RegisterTool(
                name: "expression_set",
                description: "Set an Expression Editor blendshape parameter value or reset all expressions (reset=true). Blocking.",
                order: new[] { "category", "param", "value", "part", "reset" },
                required: Array.Empty<string>(),
                props: new Dictionary<string, object>
                {
                    ["category"] = McpRegistry.Prop("string", "Expression category name (e.g. Joy, Fun, Angry)."),
                    ["param"] = McpRegistry.Prop("string", "Expression parameter name (e.g. Joy, Mouth, Eye)."),
                    ["value"] = McpRegistry.Prop("number", "Target weight value (typically 0..100)."),
                    ["part"] = McpRegistry.Prop("string", "Optional part name filter (e.g. Eyes, Mouth, Eyebrows)."),
                    ["reset"] = McpRegistry.Prop("boolean", "If true, reset all expression parameters to default.")
                },
                handler: ExpressionSetCoroutine,
                requireEdit: true
            );
        }

        private static List<object> GetExpressionCategories(VRoid.Studio.FaceEditor.ViewModel face)
        {
            var cats = Il2CppHelpers.Enumerate(face.ExpressionParameterCategories);
            if (cats.Count > 0) return cats;

            try
            {
                var editVm = Il2CppHelpers.GetEditRootViewModel();
                var res = editVm?._mainActionHandler?._resources;
                var bus = Il2CppHelpers.GetGlobalBus();
                var eng = Il2CppHelpers.GetEngineModel();
                if (res != null && bus != null && eng != null)
                {
                    var inited = face.InitExpression(res, bus, eng);
                    if (inited != null)
                    {
                        try { face._ExpressionParameterCategories_k__BackingField = inited; } catch { }
                        cats = Il2CppHelpers.Enumerate(inited);
                    }
                }
            }
            catch (Exception ex)
            {
                NingyoPlugin.Logger?.LogWarning($"InitExpression fallback warning: {ex.Message}");
            }
            return cats;
        }

        private static IEnumerator ExpressionsListCoroutine(CommandDispatcher.Context ctx)
        {
            var face = Il2CppHelpers.GetEditRootViewModel()?._FaceEditor_k__BackingField;
            if (face == null) { ctx.Error = "expressions_list: FaceEditor not available"; yield break; }

            var filter = (ctx.Args != null && ctx.Args.Length > 0 && !string.IsNullOrWhiteSpace(ctx.Args[0]))
                ? ctx.Args[0].Trim()
                : null;

            var categories = new List<object>();
            foreach (var cObj in GetExpressionCategories(face))
            {
                var cat = (cObj as ExpressionParameterCategoryViewModel) ?? (cObj as Il2CppObjectBase)?.TryCast<ExpressionParameterCategoryViewModel>();
                if (cat == null) continue;
                var catName = cat.TranslatedDisplayName ?? cat.SynthesizedCompatKey ?? "Expression";
                if (filter != null &&
                    !catName.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !(cat.SynthesizedCompatKey ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = new List<object>();
                foreach (var pObj in Il2CppHelpers.Enumerate(cat.ParameterParts))
                {
                    var part = (pObj as ExpressionParameterPartViewModel) ?? (pObj as Il2CppObjectBase)?.TryCast<ExpressionParameterPartViewModel>();
                    if (part == null) continue;

                    var prms = new List<object>();
                    foreach (var vObj in Il2CppHelpers.Enumerate(part.Parameters))
                    {
                        var pv = (vObj as ExpressionParameterViewModel) ?? (vObj as Il2CppObjectBase)?.TryCast<ExpressionParameterViewModel>();
                        if (pv == null) continue;
                        prms.Add(new Dictionary<string, object>
                        {
                            ["name"] = pv.TranslatedDisplayName,
                            ["value"] = Il2CppHelpers.Round3(pv.Value),
                            ["min"] = Il2CppHelpers.Round3(pv.MinValue),
                            ["max"] = Il2CppHelpers.Round3(pv.MaxValue)
                        });
                    }
                    parts.Add(new Dictionary<string, object>
                    {
                        ["part"] = part.TranslatedDisplayName,
                        ["parameters"] = prms
                    });
                }

                categories.Add(new Dictionary<string, object>
                {
                    ["category"] = catName,
                    ["compat_key"] = cat.SynthesizedCompatKey,
                    ["parts"] = parts
                });
            }

            ctx.Result = new Dictionary<string, object> { ["categories"] = categories };
        }

        private static IEnumerator ExpressionSetCoroutine(CommandDispatcher.Context ctx)
        {
            var face = Il2CppHelpers.GetEditRootViewModel()?._FaceEditor_k__BackingField;
            if (face == null) { ctx.Error = "expression_set: FaceEditor not available"; yield break; }

            var args = ctx.Args ?? Array.Empty<string>();
            string Arg(int i) => (args.Length > i && !string.IsNullOrWhiteSpace(args[i])) ? args[i].Trim() : null;

            if (Il2CppHelpers.TryParseBool(Arg(4), out var doReset) && doReset)
            {
                face.ResetExpression();
                face.UpdateExpression();
                for (int i = 0; i < 4; i++) yield return null;
                ctx.Result = new Dictionary<string, object> { ["reset"] = true };
                yield break;
            }

            var catFilter = Arg(0);
            var paramFilter = Arg(1);
            var valStr = Arg(2);
            var partFilter = Arg(3);

            if (string.IsNullOrEmpty(paramFilter) || !Il2CppHelpers.TryParseFloat(valStr, out var val))
            {
                ctx.Error = "expression_set: 'param' and numeric 'value' are required (or set reset=true)";
                yield break;
            }

            ExpressionParameterCategoryViewModel matchedCatVm = null;
            ExpressionParameterViewModel target = null;
            string matchedCat = null, matchedPart = null;

            foreach (var cObj in GetExpressionCategories(face))
            {
                var cat = (cObj as ExpressionParameterCategoryViewModel) ?? (cObj as Il2CppObjectBase)?.TryCast<ExpressionParameterCategoryViewModel>();
                if (cat == null) continue;
                var cName = cat.TranslatedDisplayName ?? cat.SynthesizedCompatKey ?? "";
                if (catFilter != null &&
                    !cName.Contains(catFilter, StringComparison.OrdinalIgnoreCase) &&
                    !(cat.SynthesizedCompatKey ?? "").Contains(catFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var pObj in Il2CppHelpers.Enumerate(cat.ParameterParts))
                {
                    var part = (pObj as ExpressionParameterPartViewModel) ?? (pObj as Il2CppObjectBase)?.TryCast<ExpressionParameterPartViewModel>();
                    if (part == null) continue;
                    var pName = part.TranslatedDisplayName ?? "";
                    if (partFilter != null && !pName.Contains(partFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var vObj in Il2CppHelpers.Enumerate(part.Parameters))
                    {
                        var pv = (vObj as ExpressionParameterViewModel) ?? (vObj as Il2CppObjectBase)?.TryCast<ExpressionParameterViewModel>();
                        if (pv == null) continue;
                        if (string.Equals(pv.TranslatedDisplayName, paramFilter, StringComparison.OrdinalIgnoreCase) ||
                            (pv.TranslatedDisplayName ?? "").Contains(paramFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedCatVm = cat;
                            target = pv;
                            matchedCat = cName;
                            matchedPart = pName;
                            break;
                        }
                    }
                    if (target != null) break;
                }
                if (target != null) break;
            }

            if (target == null)
            {
                ctx.Error = $"expression_set: parameter '{paramFilter}' not found (category='{catFilter}', part='{partFilter}')";
                yield break;
            }

            if (matchedCatVm != null)
            {
                try { face.SelectedExpressionParameterCategory = matchedCatVm; } catch { }
            }
            var clamped = Math.Clamp(val, target.MinValue, target.MaxValue);
            target.Value = clamped;
            face.UpdateExpression();
            for (int i = 0; i < 4; i++) yield return null;

            ctx.Result = new Dictionary<string, object>
            {
                ["category"] = matchedCat,
                ["part"] = matchedPart,
                ["param"] = target.TranslatedDisplayName,
                ["value"] = Il2CppHelpers.Round3(target.Value)
            };
        }
    }
}
