using CSM.API.Commands;
using CSM.MoveItSync.Messages;
using CSM.MoveItSync.Services;
using ColossalFramework;
using MoveIt;
using System.Collections.Generic;
using UnityEngine;
using System;
using CSM.BaseGame.Injections.Tools;
using Log = CSM.MoveItSync.Services.Log;

namespace CSM.MoveItSync.Handlers
{
    public class MoveItPreviewHandler : CommandHandler<MoveItPreviewCommand>
    {
        private static PreviewSession _currentSession;

        protected override void Handle(MoveItPreviewCommand command)
        {
            // Sync cursor position
            if (SimulationManager.instance != null && SimulationManager.instance.m_ThreadingWrapper != null)
            {
                SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(() =>
                {
                    PlayerCursorManager cursorView =
                        Singleton<ToolSimulatorCursorManager>.instance.GetCursorView(command.SenderId);
                    if (cursorView)
                    {
                        cursorView.SetLabelContent(command.PlayerName, command.CursorWorldPosition);
                    }
                });
            }

            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try {
                    using (CsmBridge.StartIgnore())
                    {
                        if (_currentSession == null || _currentSession.DragID != command.DragID)
                        {
                            _currentSession = new PreviewSession(command.DragID, command.InstanceIDs);
                            Log.Info($"Starting new Live Preview session: {command.DragID}");
                        }

                        _currentSession.Apply(command.MoveDelta, command.AngleDelta, command.Center, command.InstanceIDs, command.AutoCurve, command.CurveStartNode, command.CurveEndNode, command.CurveStartDir, command.CurveEndDir, command.IsBending);
                    }
                } catch (Exception ex) {
                    Log.Error($"CRITICAL ERROR in MoveItPreviewHandler.Handle: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }

        public static void ClearSession()
        {
            _currentSession = null;
        }

        private class PreviewSession
        {
            public long DragID;
            private List<InstanceState> _initialStates = new List<InstanceState>();
            private List<Instance> _instances = new List<Instance>();
            private HashSet<long> _capturedIds = new HashSet<long>();

            public PreviewSession(long dragId, List<long> ids)
            {
                DragID = dragId;
                AddInstances(ids);
            }

            private bool IsValidId(InstanceID id)
            {
                switch (id.Type)
                {
                    case InstanceType.NetNode: return id.NetNode > 0 && id.NetNode < 32768;
                    case InstanceType.NetSegment: return id.NetSegment > 0 && id.NetSegment < 32768;
                    case InstanceType.Building: return id.Building > 0 && id.Building < 49152;
                    case InstanceType.Prop: return id.Prop > 0;
                    case InstanceType.Tree: return id.Tree > 0 && id.Tree < 262144;
                    default: return false;
                }
            }

            private void AddInstances(List<long> ids)
            {
                if (ids == null) return;
                foreach (var rawId in ids)
                {
                    if (_capturedIds.Contains(rawId)) continue;

                    InstanceID id = default;
                    id.RawData = (uint)rawId;
                    
                    if (!IsValidId(id)) continue;

                    try {
                        Instance instance = (Instance)id;
                        if (instance != null && instance.isValid)
                        {
                            var state = instance.SaveToState(false);
                            if (state != null)
                            {
                                _instances.Add(instance);
                                _initialStates.Add(state);
                                _capturedIds.Add(rawId);
                            }
                        }
                    } catch (Exception ex) {
                        Log.Warn($"PreviewSession: Failed to capture instance {rawId}: {ex.Message}");
                    }
                }
            }

            public void Apply(Vector3 moveDelta, float angleDelta, Vector3 center, List<long> currentIds, bool autoCurve, ushort curveStart, ushort curveEnd, Vector3 curveStartDir, Vector3 curveEndDir, bool isBending)
            {
                if (NetManager.instance == null || BuildingManager.instance == null || PropManager.instance == null || TreeManager.instance == null)
                {
                    Log.Warn("PreviewSession.Apply: One or more managers are null, skipping update.");
                    return;
                }

                if (currentIds == null) return;
                
                // Add any new objects that might have been added to the drag (like new nodes from bending)
                AddInstances(currentIds);

                try {
                    Matrix4x4 matrix = Matrix4x4.TRS(center + moveDelta, 
                        Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);

                    NetSegment curveGuide = default;
                    bool curveGuideValid = false;
                    if (autoCurve)
                    {
                        if (curveStart > 0 && curveStart < 32768 && curveEnd > 0 && curveEnd < 32768)
                        {
                            curveGuide.m_startNode = curveStart;
                            curveGuide.m_endNode = curveEnd;
                            curveGuide.m_startDirection = curveStartDir;
                            curveGuide.m_endDirection = curveEndDir;
                            curveGuideValid = true;
                        }
                    }

                    for (int i = 0; i < _instances.Count; i++)
                    {
                        var instance = _instances[i];
                        var state = _initialStates[i];

                        if (instance == null || state == null) continue;

                        try {
                            if (instance.isValid)
                            {
                                instance.Transform(state, ref matrix, moveDelta.y, angleDelta, center, false);
                                
                                // Force visual refresh
                                if (instance is MoveableNode n) {
                                    NetManager.instance.UpdateNodeRenderer(n.id.NetNode, true);
                                } else if (instance is MoveableSegment s) {
                                    NetManager.instance.UpdateSegmentRenderer(s.id.NetSegment, true);
                                    if (isBending)
                                    {
                                        NetSegment seg = NetManager.instance.m_segments.m_buffer[s.id.NetSegment];
                                        if (seg.m_startNode != 0) NetManager.instance.UpdateNodeRenderer(seg.m_startNode, true);
                                        if (seg.m_endNode != 0) NetManager.instance.UpdateNodeRenderer(seg.m_endNode, true);
                                    }
                                } else if (instance is MoveableBuilding b) {
                                    BuildingManager.instance.UpdateBuildingRenderer(b.id.Building, true);
                                } else if (instance is MoveableProp p) {
                                    PropManager.instance.UpdatePropRenderer(p.id.Prop, true);
                                } else if (instance is MoveableTree t) {
                                    TreeManager.instance.UpdateTreeRenderer(t.id.Tree, true);
                                }
                            }
                        } catch (Exception) {
                            // Silent during preview to avoid disk I/O lag
                        }
                    }

                    // Explicitly apply AutoCurve to the guide nodes if they aren't in the selection
                    if (autoCurve && curveGuideValid)
                    {
                        ushort[] guideNodes = { curveStart, curveEnd };
                        foreach (ushort nodeId in guideNodes)
                        {
                            if (nodeId > 0 && nodeId < 32768)
                            {
                                try {
                                    MoveableNode node = new MoveableNode(new InstanceID { NetNode = nodeId });
                                    if (node.isValid)
                                    {
                                        node.AutoCurve(curveGuide);
                                        if (nodeId < 32768)
                                        {
                                            NetManager.instance.UpdateNodeRenderer(nodeId, true);
                                            
                                            // Refresh all segments connected to this node to show the curve
                                            for (int j = 0; j < 8; j++)
                                            {
                                                ushort segmentId = NetManager.instance.m_nodes.m_buffer[nodeId].GetSegment(j);
                                                if (segmentId > 0 && segmentId < 32768)
                                                {
                                                    NetManager.instance.UpdateSegmentRenderer(segmentId, true);
                                                }
                                            }
                                        }
                                    }
                                } catch (Exception) {
                                    // Silent during preview
                                }
                            }
                        }
                    }
                } catch (Exception ex) {
                    Log.Error($"CRITICAL CRASH in PreviewSession.Apply: {ex}\nStack: {ex.StackTrace}");
                }
            }
        }
    }
}
