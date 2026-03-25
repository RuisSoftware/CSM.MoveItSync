using HarmonyLib;
using MoveIt;
using CSM.MoveItSync.Messages;
using CSM.MoveItSync.Services;
using CSM.API.Helpers;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Log = CSM.MoveItSync.Services.Log;

namespace CSM.MoveItSync.Patching
{
    public static class ActionQueuePatch
    {
        private const string HarmonyId = "CSM.MoveItSync";

        public static void PatchAll()
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(typeof(ActionQueuePatch).Assembly);
        }

        public static void UnpatchAll()
        {
            var harmony = new Harmony(HarmonyId);
            harmony.UnpatchAll(HarmonyId);
        }
    }

    [HarmonyPatch(typeof(ActionQueue), "Push")]
    public static class PushPatch
    {
        public static void Prefix(MoveIt.Action action)
        {
            if (action == null) return;
            if (IgnoreHelper.Instance.IsIgnored()) return;

            Log.Info($"Push intercepted: {action.GetType().Name}");

            MoveItActionCommand command = null;

            if (action is TransformAction transformAction)
            {
                command = new MoveItActionCommand
                {
                    ActionType = MoveItActionType.Transform,
                    InstanceIDs = transformAction.m_states.Select(s => s.instance.id.RawData).ToArray(),
                    MoveDelta = transformAction.moveDelta,
                    AngleDelta = transformAction.angleDelta,
                    Center = transformAction.center,
                    FollowTerrain = transformAction.followTerrain
                };
            }
            else if (action is BulldozeAction bulldozeAction)
            {
                var statesField = typeof(BulldozeAction).GetField("m_states", BindingFlags.NonPublic | BindingFlags.Instance);
                if (statesField != null)
                {
                    var states = statesField.GetValue(bulldozeAction) as IEnumerable<InstanceState>;
                    if (states != null)
                    {
                        command = new MoveItActionCommand
                        {
                            ActionType = MoveItActionType.Bulldoze,
                            InstanceIDs = states.Select(s => s.instance.id.RawData).ToArray()
                        };
                    }
                }
            }
            else
            {
                // Generic state-based catch-all (Align, Line, etc.)
                var statesField = action.GetType().GetField("m_states", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (statesField != null)
                {
                    var states = statesField.GetValue(action) as IEnumerable<InstanceState>;
                    if (states != null)
                    {
                        command = new MoveItActionCommand
                        {
                            ActionType = MoveItActionType.GenericStateUpdate,
                            InstanceIDs = states.Select(s => s.instance.id.RawData).ToArray(),
                            States = states.Select(s => new ObjectStateData
                            {
                                InstanceID = s.instance.id.RawData,
                                Position = s.position,
                                Angle = s.angle,
                                TerrainHeight = s.terrainHeight
                            }).ToList()
                        };
                    }
                }
            }

            if (command != null)
            {
                CsmBridge.SendToAll(command);
            }
        }
    }
}
