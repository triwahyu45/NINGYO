using System;
using System.Collections;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using VRoid.Studio.AccessoryEditor.InstanceItem.ViewModel;
using VRoid.Studio.AccessoryEditor.TemplateItem.ViewModel;
using VRoid.UI;
using VroidStudioMcp;

namespace NingyoExtensions
{
    internal static class AccessoryHandlers
    {
        public static void Register()
        {
            McpRegistry.RegisterTool(
                name: "accessory_list",
                description: "List available accessory templates and currently equipped accessory instances. Read-only.",
                order: Array.Empty<string>(),
                required: Array.Empty<string>(),
                props: new Dictionary<string, object>(),
                handler: AccessoryListCoroutine,
                requireEdit: true
            );

            McpRegistry.RegisterTool(
                name: "accessory_add",
                description: "Add an accessory instance from the template list by template_index. Blocking.",
                order: new[] { "template_index" },
                required: new[] { "template_index" },
                props: new Dictionary<string, object>
                {
                    ["template_index"] = McpRegistry.Prop("integer", "Template index from accessory_list().")
                },
                handler: AccessoryAddCoroutine,
                requireEdit: true
            );

            McpRegistry.RegisterTool(
                name: "accessory_remove",
                description: "Remove an equipped accessory instance by index. Blocking.",
                order: new[] { "index" },
                required: new[] { "index" },
                props: new Dictionary<string, object>
                {
                    ["index"] = McpRegistry.Prop("integer", "Instance index from accessory_list().")
                },
                handler: AccessoryRemoveCoroutine,
                requireEdit: true
            );

            McpRegistry.RegisterTool(
                name: "accessory_params",
                description:
                    "Inspect or modify an equipped accessory instance (visibility, attachment bone_index, or numeric parameters). " +
                    "Pass only 'index' to read its parameters and bones; pass 'visible', 'bone_index', or 'param'+'value' to edit.",
                order: new[] { "index", "visible", "bone_index", "param", "value" },
                required: new[] { "index" },
                props: new Dictionary<string, object>
                {
                    ["index"] = McpRegistry.Prop("integer", "Instance index from accessory_list()."),
                    ["visible"] = McpRegistry.Prop("boolean", "Show/hide accessory instance."),
                    ["bone_index"] = McpRegistry.Prop("integer", "Target parent bone index from bones[] list."),
                    ["param"] = McpRegistry.Prop("string", "Parameter label or index."),
                    ["value"] = McpRegistry.Prop("number", "New numeric value for param.")
                },
                handler: AccessoryParamsCoroutine,
                requireEdit: true
            );
        }

        private static IEnumerator AccessoryListCoroutine(CommandDispatcher.Context ctx)
        {
            var acc = Il2CppHelpers.GetEditRootViewModel()?._AccessoryEditor_k__BackingField;
            if (acc == null) { ctx.Error = "accessory_list: AccessoryEditor not available"; yield break; }

            var templates = new List<object>();
            int ti = 0;
            if (acc.Templates != null)
            {
                foreach (var o in Il2CppHelpers.Enumerate(acc.Templates))
                {
                    var tpl = (o as TransferableGroupTemplateItemViewModel) ?? (o as Il2CppObjectBase)?.TryCast<TransferableGroupTemplateItemViewModel>();
                    templates.Add(new Dictionary<string, object>
                    {
                        ["index"] = ti++,
                        ["name"] = tpl?.TranslatedDisplayName ?? $"Template {ti}"
                    });
                }
            }

            var instances = new List<object>();
            int ii = 0;
            if (acc.Instances != null)
            {
                foreach (var o in Il2CppHelpers.Enumerate(acc.Instances))
                {
                    var inst = (o as InstanceItemViewModel) ?? (o as Il2CppObjectBase)?.TryCast<InstanceItemViewModel>();
                    if (inst == null) { ii++; continue; }
                    var p = inst.PartOriginGlobalPosition;
                    var r = inst.PartOriginLocalRotation.eulerAngles;
                    var s = inst.PartOriginLocalScale;
                    instances.Add(new Dictionary<string, object>
                    {
                        ["index"] = ii++,
                        ["name"] = inst.TranslatedDisplayName ?? $"Instance {ii}",
                        ["visible"] = inst.IsVisible,
                        ["editable"] = inst.IsEditable,
                        ["bone_index"] = inst.SelectedBoneIndex,
                        ["position"] = new[] { Il2CppHelpers.Round3(p.x), Il2CppHelpers.Round3(p.y), Il2CppHelpers.Round3(p.z) },
                        ["rotation_euler"] = new[] { Il2CppHelpers.Round3(r.x), Il2CppHelpers.Round3(r.y), Il2CppHelpers.Round3(r.z) },
                        ["scale"] = new[] { Il2CppHelpers.Round3(s.x), Il2CppHelpers.Round3(s.y), Il2CppHelpers.Round3(s.z) },
                        ["params_count"] = inst.Parameters?.Length ?? 0
                    });
                }
            }

            ctx.Result = new Dictionary<string, object>
            {
                ["templates"] = templates,
                ["instances"] = instances
            };
        }

        private static IEnumerator AccessoryAddCoroutine(CommandDispatcher.Context ctx)
        {
            var args = ctx.Args ?? Array.Empty<string>();
            if (args.Length == 0 || !Il2CppHelpers.TryParseInt(args[0], out var tplIdx))
            {
                ctx.Error = "accessory_add: integer 'template_index' is required";
                yield break;
            }

            var acc = Il2CppHelpers.GetEditRootViewModel()?._AccessoryEditor_k__BackingField;
            if (acc == null || acc.Templates == null || tplIdx < 0 || tplIdx >= acc.Templates.Length)
            {
                ctx.Error = $"accessory_add: invalid template_index {tplIdx}";
                yield break;
            }

            var tpl = acc.Templates[tplIdx];
            acc.SelectedTemplate = tpl;
            acc.AddSelectedTemplate();
            yield return Il2CppHelpers.WaitUntilSettled(20f, 3);

            int count = acc.Instances?.Length ?? 0;
            ctx.Result = new Dictionary<string, object>
            {
                ["added"] = true,
                ["template_index"] = tplIdx,
                ["name"] = tpl?.TranslatedDisplayName,
                ["instances_count"] = count
            };
        }

        private static IEnumerator AccessoryRemoveCoroutine(CommandDispatcher.Context ctx)
        {
            var args = ctx.Args ?? Array.Empty<string>();
            if (args.Length == 0 || !Il2CppHelpers.TryParseInt(args[0], out var idx))
            {
                ctx.Error = "accessory_remove: integer 'index' is required";
                yield break;
            }

            var acc = Il2CppHelpers.GetEditRootViewModel()?._AccessoryEditor_k__BackingField;
            if (acc == null || acc.Instances == null || idx < 0 || idx >= acc.Instances.Length)
            {
                ctx.Error = $"accessory_remove: invalid instance index {idx}";
                yield break;
            }

            acc.SelectedIndex = idx;
            acc.RemoveSelectedInstance(idx);
            yield return Il2CppHelpers.WaitUntilSettled(20f, 3);

            ctx.Result = new Dictionary<string, object>
            {
                ["removed"] = true,
                ["index"] = idx,
                ["remaining_count"] = acc.Instances?.Length ?? 0
            };
        }

        private static IEnumerator AccessoryParamsCoroutine(CommandDispatcher.Context ctx)
        {
            var args = ctx.Args ?? Array.Empty<string>();
            if (args.Length == 0 || !Il2CppHelpers.TryParseInt(args[0], out var idx))
            {
                ctx.Error = "accessory_params: integer 'index' is required";
                yield break;
            }

            var acc = Il2CppHelpers.GetEditRootViewModel()?._AccessoryEditor_k__BackingField;
            if (acc == null || acc.Instances == null || idx < 0 || idx >= acc.Instances.Length)
            {
                ctx.Error = $"accessory_params: invalid instance index {idx}";
                yield break;
            }

            var inst = acc.Instances[idx];
            if (inst == null) { ctx.Error = "accessory_params: instance is null"; yield break; }

            bool mutated = false;
            var visArg = args.Length > 1 ? args[1] : null;
            if (Il2CppHelpers.TryParseBool(visArg, out var vis))
            {
                inst.SetVisibility(vis);
                mutated = true;
            }

            var boneArg = args.Length > 2 ? args[2] : null;
            if (Il2CppHelpers.TryParseInt(boneArg, out var boneIdx))
            {
                inst.SelectedBoneIndex = boneIdx;
                inst.UpdateParentBone();
                mutated = true;
            }

            var paramArg = args.Length > 3 ? args[3]?.Trim() : null;
            var valArg = args.Length > 4 ? args[4] : null;
            if (!string.IsNullOrEmpty(paramArg))
            {
                if (!Il2CppHelpers.TryParseFloat(valArg, out var fval))
                {
                    ctx.Error = "accessory_params: numeric 'value' is required when 'param' is provided";
                    yield break;
                }

                bool found = false;
                int pi = 0;
                int targetIdx = Il2CppHelpers.TryParseInt(paramArg, out var parsedIdx) ? parsedIdx : -1;
                if (inst.Parameters != null)
                {
                    foreach (var pObj in Il2CppHelpers.Enumerate(inst.Parameters))
                    {
                        var sp = (pObj as SingleParameterViewModel) ?? (pObj as Il2CppObjectBase)?.TryCast<SingleParameterViewModel>();
                        if (sp != null)
                        {
                            bool match = (targetIdx >= 0 && pi == targetIdx) ||
                                         string.Equals(sp.Label, paramArg, StringComparison.OrdinalIgnoreCase);
                            if (match)
                            {
                                sp.Value = fval;
                                found = true;
                                mutated = true;
                                break;
                            }
                        }
                        var cp = (pObj as CombinedParameterViewModel) ?? (pObj as Il2CppObjectBase)?.TryCast<CombinedParameterViewModel>();
                        if (cp != null)
                        {
                            bool match = (targetIdx >= 0 && pi == targetIdx) ||
                                         string.Equals(cp.Label, paramArg, StringComparison.OrdinalIgnoreCase);
                            if (match)
                            {
                                cp.CombinedValue = fval;
                                found = true;
                                mutated = true;
                                break;
                            }
                        }
                        pi++;
                    }
                }
                if (!found)
                {
                    ctx.Error = $"accessory_params: parameter '{paramArg}' not found on instance {idx}";
                    yield break;
                }
            }

            if (mutated)
                yield return Il2CppHelpers.WaitUntilSettled(15f, 3);

            var bones = new List<object>();
            if (inst.Bones != null)
            {
                int bi = 0;
                foreach (var bObj in Il2CppHelpers.Enumerate(inst.Bones))
                {
                    var b = (bObj as BoneViewModel) ?? (bObj as Il2CppObjectBase)?.TryCast<BoneViewModel>();
                    bones.Add(new Dictionary<string, object> { ["index"] = bi++, ["name"] = b?.TranslatedDisplayName ?? $"Bone {bi}" });
                }
            }

            var parameters = new List<object>();
            if (inst.Parameters != null)
            {
                int pi = 0;
                foreach (var pObj in Il2CppHelpers.Enumerate(inst.Parameters))
                {
                    var sp = (pObj as SingleParameterViewModel) ?? (pObj as Il2CppObjectBase)?.TryCast<SingleParameterViewModel>();
                    if (sp != null)
                    {
                        parameters.Add(new Dictionary<string, object>
                        {
                            ["index"] = pi++,
                            ["label"] = sp.Label,
                            ["kind"] = "float",
                            ["value"] = Il2CppHelpers.Round3(sp.Value),
                            ["min"] = Il2CppHelpers.Round3(sp.MinValue),
                            ["max"] = Il2CppHelpers.Round3(sp.MaxValue),
                        });
                        continue;
                    }
                    var cp = (pObj as CombinedParameterViewModel) ?? (pObj as Il2CppObjectBase)?.TryCast<CombinedParameterViewModel>();
                    if (cp != null)
                    {
                        parameters.Add(new Dictionary<string, object>
                        {
                            ["index"] = pi++,
                            ["label"] = cp.Label,
                            ["kind"] = "combined",
                            ["value"] = Il2CppHelpers.Round3(cp.CombinedValue),
                            ["min"] = Il2CppHelpers.Round3(cp.MinValue),
                            ["max"] = Il2CppHelpers.Round3(cp.MaxValue),
                        });
                        continue;
                    }
                    pi++;
                }
            }

            ctx.Result = new Dictionary<string, object>
            {
                ["index"] = idx,
                ["name"] = inst.TranslatedDisplayName,
                ["visible"] = inst.IsVisible,
                ["bone_index"] = inst.SelectedBoneIndex,
                ["bones"] = bones,
                ["parameters"] = parameters
            };
        }
    }
}
