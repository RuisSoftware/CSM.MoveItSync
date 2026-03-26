using HarmonyLib;
using MoveIt;
using CSM.MoveItSync.Messages;
using CSM.MoveItSync.Services;
using CSM.API.Helpers;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
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
        public static void Postfix(MoveIt.Action action)
        {
            if (action == null) return;
            if (IgnoreHelper.Instance.IsIgnored()) return;

            Log.Info($"Push intercepted (Postfix): {action.GetType().Name}");

            MoveItActionCommand command = null;

            if (action is BulldozeAction bulldozeAction)
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
                // For all other actions (Transform, Align, Line, etc.), send absolute final states
                IEnumerable<InstanceState> states = null;
                
                // Try to get states from common field names
                var fields = action.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var statesField = fields.FirstOrDefault(f => f.Name == "m_states" || f.Name == "m_States");
                
                if (statesField != null)
                {
                    states = statesField.GetValue(action) as IEnumerable<InstanceState>;
                }

                if (states != null)
                {
                    // Phase 1: Expand states to include the entire 'Geometric Unit' (Nodes + Segments)
                    var expandedStates = new HashSet<InstanceState>(states);
                    foreach (var state in states)
                    {
                        if (state.instance is MoveableNode node)
                        {
                            foreach (ushort segmentId in node.segmentList)
                            {
                                InstanceID segId = default;
                                segId.NetSegment = segmentId;
                                Instance segInstance = (Instance)segId;
                                if (segInstance != null && segInstance.isValid)
                                {
                                    expandedStates.Add(segInstance.SaveToState(false));
                                }
                            }
                        }
                        else if (state.instance is MoveableSegment segment)
                        {
                            NetSegment seg = NetManager.instance.m_segments.m_buffer[segment.id.NetSegment];
                            
                            // Add Start Node
                            if (seg.m_startNode != 0) {
                                InstanceID nodeId = default; nodeId.NetNode = seg.m_startNode;
                                Instance nodeInstance = (Instance)nodeId;
                                if (nodeInstance != null && nodeInstance.isValid) expandedStates.Add(nodeInstance.SaveToState(false));
                            }
                            
                            // Add End Node
                            if (seg.m_endNode != 0) {
                                InstanceID nodeId = default; nodeId.NetNode = seg.m_endNode;
                                Instance nodeInstance = (Instance)nodeId;
                                if (nodeInstance != null && nodeInstance.isValid) expandedStates.Add(nodeInstance.SaveToState(false));
                            }
                        }
                    }

                    command = new MoveItActionCommand
                    {
                        ActionType = MoveItActionType.GenericStateUpdate,
                        InstanceIDs = expandedStates.Select(s => s.instance.id.RawData).ToArray(),
                        States = expandedStates.Select(s => {
                            var instance = s.instance;
                            var data = new ObjectStateData
                            {
                                InstanceID = instance.id.RawData,
                                Position = instance.position,
                                Angle = instance.angle,
                                TerrainHeight = instance.isValid ? TerrainManager.instance.SampleRawHeightSmooth(instance.position) : 0f
                            };
                            
                            // Safety guard for game buffer access
                            if (instance is MoveableSegment seg && seg.id.NetSegment < 32768)
                            {
                                data.StartDirection = NetManager.instance.m_segments.m_buffer[seg.id.NetSegment].m_startDirection;
                                data.EndDirection = NetManager.instance.m_segments.m_buffer[seg.id.NetSegment].m_endDirection;
                            }
                            
                            Log.Info($"Capturing {instance.GetType().Name} {instance.id.RawData}: Pos={data.Position}, TanStart={data.StartDirection}");
                            return data;
                        }).ToList()
                    };

                    // Extra curvature data for TransformAction
                    if (action is BaseTransformAction transformAction) 
                    {
                        command.AutoCurve = transformAction.autoCurve;
                        if (transformAction.segmentCurve.m_startNode != 0 || transformAction.segmentCurve.m_endNode != 0) 
                        {
                            command.CurveStartNode = transformAction.segmentCurve.m_startNode;
                            command.CurveEndNode = transformAction.segmentCurve.m_endNode;
                            command.CurveStartDir = transformAction.segmentCurve.m_startDirection;
                            command.CurveEndDir = transformAction.segmentCurve.m_endDirection;
                        }
                    }
                }
            }

            if (command != null && command.InstanceIDs?.Length > 0)
            {
                var idCount = command.InstanceIDs?.Length ?? 0;
                var stateCount = command.States?.Count ?? 0;
                Log.Info($"Broadcasting {command.ActionType}: {idCount} IDs, {stateCount} states.");
                CsmBridge.SendToAll(command);
            }
        }
    }

    [HarmonyPatch(typeof(BaseTransformAction), "Do")]
    public class TransformActionDoPatch
    {
        private static long _lastPreviewTime = 0;
        private static long _currentDragID = 0;
        private static FieldInfo _draggingField = typeof(MoveItTool).GetField("dragging", BindingFlags.NonPublic | BindingFlags.Static);

        public static void Postfix(BaseTransformAction __instance)
        {
            if (CsmBridge.IsIgnoring()) return;

            try 
            {
                bool isDragging = (bool)_draggingField.GetValue(null);
                if (!isDragging) 
                {
                    _currentDragID = 0;
                    return;
                }

                // Throttling: only send every 40ms (~25 fps)
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                long frequency = System.Diagnostics.Stopwatch.Frequency;
                if ((now - _lastPreviewTime) * 1000 / frequency < 40) return;
                _lastPreviewTime = now;

                // Start a new drag ID if needed
                if (_currentDragID == 0) _currentDragID = now;

                var ids = new List<long>();
                foreach (var state in __instance.m_states)
                {
                    if (state.instance != null && state.instance.isValid)
                    {
                        ids.Add((long)state.instance.id.RawData);
                    }
                }

                if (ids.Count == 0) return;

                // Safely extract curvature data
                bool autoCurve = __instance.autoCurve;
                ushort curveStart = __instance.segmentCurve.m_startNode;
                ushort curveEnd = __instance.segmentCurve.m_endNode;
                Vector3 curveStartDir = __instance.segmentCurve.m_startDirection;
                Vector3 curveEndDir = __instance.segmentCurve.m_endDirection;

                CsmBridge.SendToAll(new MoveItPreviewCommand
                {
                    DragID = _currentDragID,
                    MoveDelta = __instance.moveDelta,
                    AngleDelta = __instance.angleDelta,
                    Center = __instance.center,
                    InstanceIDs = ids,
                    AutoCurve = autoCurve,
                    CurveStartNode = curveStart,
                    CurveEndNode = curveEnd,
                    CurveStartDir = curveStartDir,
                    CurveEndDir = curveEndDir
                });
            }
            catch (Exception ex) 
            {
                Log.Error($"SENDER CRASH in TransformAction.Do Postfix: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
