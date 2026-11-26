using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using VRoid.Studio;
using VRoid.Studio.EditRootScreen;
using VRoidStudio.GUI.AvatarEditor;
using VRoidStudio.GUI.AvatarEditor.PhotoBooth;
using VroidStudioMcp;

namespace NingyoExtensions
{
    internal static class Il2CppHelpers
    {
        private static readonly FieldInfo MainVmField =
            typeof(VroidState).GetField("_mainViewModel", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo EditRootVmField =
            typeof(VroidState).GetField("_editRootVm", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo ModelReadyField =
            typeof(VroidState).GetField("_modelReady", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo ModelUpdatingField =
            typeof(VroidState).GetField("_modelUpdating", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        public static MainViewModel GetMainViewModel()
        {
            var raw = MainVmField?.GetValue(null);
            if (raw is MainViewModel m) return m;
            if (raw is Il2CppObjectBase obj) return obj.TryCast<MainViewModel>();
            return null;
        }

        public static GlobalBus GetGlobalBus()
        {
            try { return GetMainViewModel()?.GlobalBus; }
            catch { return null; }
        }

        public static ViewModel GetEditRootViewModel()
        {
            var raw = EditRootVmField?.GetValue(null);
            if (raw is ViewModel vm) return vm;
            if (raw is Il2CppObjectBase obj) return obj.TryCast<ViewModel>();
            return null;
        }

        public static VRoid.Studio.Engine.Model GetEngineModel()
        {
            var edit = GetEditRootViewModel();
            if (edit != null && edit._engineModel != null) return edit._engineModel;
            return GetMainViewModel()?.ActionHandler?.Engine;
        }

        public static VRoidCore.Editing.Context GetEditingContext()
        {
            return GetEngineModel()?.Context;
        }

        public static PhotoBoothViewModel GetPhotoBoothViewModel()
        {
            try
            {
                var editor = UnityEngine.Object.FindObjectOfType<AvatarEditor>();
                if (editor != null && editor._instantiatedPhotoBoothViewModel != null)
                    return editor._instantiatedPhotoBoothViewModel;
            }
            catch { }

            var editVm = GetEditRootViewModel();
            if (editVm != null && editVm.obsoleteLegacyPhotoBoothViewModel != null)
            {
                var cast = editVm.obsoleteLegacyPhotoBoothViewModel.TryCast<PhotoBoothViewModel>();
                if (cast != null) return cast;
            }
            return null;
        }

        public static bool IsModelReady()
        {
            var updating = ModelUpdatingField?.GetValue(null) is bool u && u;
            var ready = ModelReadyField?.GetValue(null) is bool r && r;
            return !updating && ready;
        }

        public static bool IsModelUpdating()
        {
            return ModelUpdatingField?.GetValue(null) is bool u && u;
        }

        public static IEnumerator WaitUntilSettled(float timeoutSec = 25f, int extraFrames = 3)
        {
            for (int i = 0; i < 2; i++)
                yield return null;

            float deadline = Time.realtimeSinceStartup + timeoutSec;
            while (!IsModelReady() && Time.realtimeSinceStartup < deadline)
                yield return null;

            for (int i = 0; i < extraFrames; i++)
                yield return null;
        }

        public static bool TryParseBool(string s, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim().ToLowerInvariant();
            if (t is "true" or "1" or "yes" or "on") { value = true; return true; }
            if (t is "false" or "0" or "no" or "off") { value = false; return true; }
            return false;
        }

        public static bool TryParseFloat(string s, out float value)
        {
            return float.TryParse(s?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && float.IsFinite(value);
        }

        public static bool TryParseInt(string s, out int value)
        {
            return int.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseColor(string s, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim();
            if (t.StartsWith("#"))
            {
                var hex = t.Substring(1);
                if (hex.Length == 6 &&
                    byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                    byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                    byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                {
                    color = new Color(r / 255f, g / 255f, b / 255f, 1f);
                    return true;
                }
                if (hex.Length == 8 &&
                    byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r8) &&
                    byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g8) &&
                    byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b8) &&
                    byte.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a8))
                {
                    color = new Color(r8 / 255f, g8 / 255f, b8 / 255f, a8 / 255f);
                    return true;
                }
                return false;
            }
            var parts = t.Split(',');
            if (parts.Length >= 3 &&
                TryParseFloat(parts[0], out var rf) &&
                TryParseFloat(parts[1], out var gf) &&
                TryParseFloat(parts[2], out var bf))
            {
                float af = 1f;
                if (parts.Length >= 4 && TryParseFloat(parts[3], out var aParsed)) af = aParsed;
                color = new Color(Mathf.Clamp01(rf), Mathf.Clamp01(gf), Mathf.Clamp01(bf), Mathf.Clamp01(af));
                return true;
            }
            return false;
        }

        public static double Round3(double v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);

        public static void CleanupPhotoBoothAndExport(PhotoBoothViewModel pbVm, bool isExport)
        {
            if (isExport)
            {
                try { pbVm?.OnLeaveExportEditMode(); } catch { }
                try { pbVm?.DeselectExportMode(); } catch { }
            }
            else
            {
                try { pbVm?.DeselectPhotoBoothMode(); } catch { }
            }

            try
            {
                var nav = GetGlobalBus()?.ScreenNavigation;
                var active = nav?.ActiveScreen;
                var full = active?.GetIl2CppType()?.FullName ?? "";
                if (full.Contains("VRMExportScreen") || full.Contains("PhotoBoothScreen"))
                {
                    nav.Pop(active, false);
                }
            }
            catch (Exception ex)
            {
                NingyoPlugin.Logger?.LogWarning($"ScreenNavigation.Pop warning: {ex.Message}");
            }

            try { pbVm?.ExportSettings?.MeshReduction?.ClearReference(); } catch { }
            try { pbVm?.ClearReference(); } catch { }
            try { pbVm?.ClearPreviewAvatar(); } catch { }
            try { pbVm?.OnEditorWindowModeLeave(); } catch { }

            try
            {
                if (pbVm?._umaGameObject != null && pbVm._umaGameObject)
                    pbVm._umaGameObject.SetActive(false);
            }
            catch { }

            try
            {
                var rc = pbVm?.ExportSettings?.MeshReduction?._referenceCache;
                if (rc != null && rc)
                    rc.SetActive(false);
            }
            catch { }
        }

        public static System.Collections.Generic.List<object> Enumerate(object col)
        {
            var list = new System.Collections.Generic.List<object>();
            if (col == null) return list;
            var t = col.GetType();
            var countProp = t.GetProperty("Count") ?? t.GetProperty("Length");
            var itemProp = t.GetProperty("Item");
            var getItem = itemProp?.GetGetMethod() ?? t.GetMethod("get_Item", new[] { typeof(int) });
            if (countProp != null && getItem != null)
            {
                int n = Convert.ToInt32(countProp.GetValue(col));
                for (int i = 0; i < n; i++)
                    list.Add(getItem.Invoke(col, new object[] { i }));
                return list;
            }
            if (col is System.Collections.IEnumerable ie)
            {
                foreach (var x in ie) list.Add(x);
                return list;
            }
            if (col is Il2CppObjectBase ilObj)
            {
                try
                {
                    var ilEnum = ilObj.TryCast<Il2CppSystem.Collections.IEnumerable>();
                    if (ilEnum != null)
                    {
                        var e = ilEnum.GetEnumerator();
                        while (e != null && e.MoveNext())
                            list.Add(e.Current);
                        return list;
                    }
                }
                catch { }
            }
            try
            {
                var getEnum = t.GetMethod("GetEnumerator", Type.EmptyTypes);
                if (getEnum != null)
                {
                    var e = getEnum.Invoke(col, null);
                    if (e != null)
                    {
                        var et = e.GetType();
                        var moveNext = et.GetMethod("MoveNext", Type.EmptyTypes);
                        var currentProp = et.GetProperty("Current");
                        if (moveNext != null && currentProp != null)
                        {
                            while (moveNext.Invoke(e, null) is bool ok && ok)
                                list.Add(currentProp.GetValue(e));
                            return list;
                        }
                    }
                }
            }
            catch { }
            return list;
        }
    }
}
