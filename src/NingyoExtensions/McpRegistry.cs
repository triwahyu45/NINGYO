using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VroidStudioMcp;

namespace NingyoExtensions
{
    internal static class McpRegistry
    {
        private static readonly Dictionary<string, Func<CommandDispatcher.Context, IEnumerator>> Handlers;
        private static readonly HashSet<string> EditRequired;
        private static readonly IDictionary Tools;
        private static readonly Type ToolType;

        static McpRegistry()
        {
            RuntimeHelpers.RunClassConstructor(typeof(CommandDispatcher).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(McpHost).TypeHandle);

            var handlersField = typeof(CommandDispatcher).GetField("Handlers", BindingFlags.Static | BindingFlags.NonPublic);
            var editRequiredField = typeof(CommandDispatcher).GetField("EditRequired", BindingFlags.Static | BindingFlags.NonPublic);
            Handlers = handlersField?.GetValue(null) as Dictionary<string, Func<CommandDispatcher.Context, IEnumerator>>
                ?? throw new InvalidOperationException("CommandDispatcher.Handlers not found");
            EditRequired = editRequiredField?.GetValue(null) as HashSet<string>;

            var toolsField = typeof(McpHost).GetField("Tools", BindingFlags.Static | BindingFlags.NonPublic);
            ToolType = typeof(McpHost).GetNestedType("Tool", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("McpHost.Tool type not found");
            Tools = toolsField?.GetValue(null) as IDictionary
                ?? throw new InvalidOperationException("McpHost.Tools dictionary not found");
        }

        public static void RegisterTool(
            string name,
            string description,
            string[] order,
            string[] required,
            Dictionary<string, object> props,
            Func<CommandDispatcher.Context, IEnumerator> handler,
            bool requireEdit = true)
        {
            Handlers[name] = handler;
            if (EditRequired != null)
            {
                if (requireEdit) EditRequired.Add(name);
                else EditRequired.Remove(name);
            }

            var toolInstance = Activator.CreateInstance(ToolType)
                ?? throw new InvalidOperationException("Could not instantiate McpHost.Tool");

            SetField(toolInstance, "Name", name);
            SetField(toolInstance, "Description", description);
            SetField(toolInstance, "Order", order ?? Array.Empty<string>());
            SetField(toolInstance, "Required", required ?? Array.Empty<string>());
            SetField(toolInstance, "Props", props ?? new Dictionary<string, object>());

            Tools[name] = toolInstance;
        }

        public static Dictionary<string, object> Prop(string type, string desc, params string[] enumVals)
        {
            var d = new Dictionary<string, object>
            {
                ["type"] = type,
                ["description"] = desc
            };
            if (enumVals != null && enumVals.Length > 0)
            {
                var list = new List<object>(enumVals.Length);
                foreach (var v in enumVals) list.Add(v);
                d["enum"] = list;
            }
            return d;
        }

        private static void SetField(object target, string name, object value)
        {
            var f = ToolType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(ToolType.FullName, name);
            f.SetValue(target, value);
        }
    }
}
