using System;
using System.Linq;
using System.Reflection;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using UnityEngine;

namespace CSM.MoveItSync.Services
{
    internal static class CompatibilityService
    {
        private const string TargetCsmVersion = "2603.307";

        internal static void CheckCsmVersion()
        {
            try
            {
                var csmPlugin = FindCsmPlugin();
                if (csmPlugin == null)
                {
                    ShowWarning("CSM (Multiplayer) mod not found or disabled. Move It synchronization will not work.");
                    return;
                }

                string installedVersion = GetPluginVersion(csmPlugin);
                if (string.IsNullOrEmpty(installedVersion) || (installedVersion != "0.0.0.0" && !installedVersion.Contains(TargetCsmVersion)))
                {
                    ShowWarning($"Move It - CSM Sync was built for CSM version {TargetCsmVersion}.\n\nDetected version: {installedVersion ?? "unknown"}.\n\nSynchronization might be unstable or fail to work correctly.");
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[MoveIt-CSM--Sync] Compatibility check failed: {ex}");
            }
        }

        private static PluginManager.PluginInfo FindCsmPlugin()
        {
            return PluginManager.instance.GetPluginsInfo().FirstOrDefault(p =>
                p.isEnabled && (p.name.Contains("CSM") || (p.userModInstance != null && p.userModInstance.GetType().Namespace?.Contains("CSM") == true)));
        }

        private static string GetPluginVersion(PluginManager.PluginInfo plugin)
        {
            if (plugin.userModInstance == null) return null;

            // Try to find a 'version' property or field (case-insensitive check for common names)
            var type = plugin.userModInstance.GetType();
            var versionMember = type.GetProperty("version", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? type.GetProperty("Version", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? (MemberInfo)type.GetField("version", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? (MemberInfo)type.GetField("Version", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (versionMember != null)
            {
                object val = (versionMember is PropertyInfo pi) ? pi.GetValue(plugin.userModInstance, null) : ((FieldInfo)versionMember).GetValue(plugin.userModInstance);
                return val?.ToString();
            }

            // Fallback to assembly version
            return type.Assembly.GetName().Version.ToString();
        }

        private static void ShowWarning(string message)
        {
            UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(
                "Move It - CSM Sync Warning",
                message,
                false);
        }
    }
}
