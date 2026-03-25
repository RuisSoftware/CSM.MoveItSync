using HarmonyLib;
using CSM.API.Helpers;
using System;
using CSM.MoveItSync.Services;

namespace CSM.MoveItSync.Patching
{
    [HarmonyPatch(typeof(SimulationManager), "AddAction")]
    [HarmonyPatch(new Type[] { typeof(Action) })]
    public class SimulationManagerAddActionPatch
    {
        public static void Prefix(ref Action action)
        {
            // If the current thread is in ignore mode (e.g., a CSM command handler),
            // we want the action on the simulation thread to also be ignored.
            if (IgnoreHelper.Instance.IsIgnored())
            {
                var originalAction = action;
                action = () =>
                {
                    using (CsmBridge.StartIgnore())
                    {
                        originalAction();
                    }
                };
            }
        }
    }
}
