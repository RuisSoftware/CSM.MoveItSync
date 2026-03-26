using CSM.API.Commands;
using ColossalFramework;
using ColossalFramework.Math;
using MoveIt;
using CSM.MoveItSync.Messages;
using CSM.MoveItSync.Services;
using System;
using System.Collections.Generic;
using UnityEngine;
using Log = CSM.MoveItSync.Services.Log;

namespace CSM.MoveItSync.Handlers
{
    public class MoveItActionHandler : CommandHandler<MoveItActionCommand>
    {
        protected override void Handle(MoveItActionCommand command)
        {
            var idCount = command.InstanceIDs?.Length ?? 0;
            var stateCount = command.States?.Count ?? 0;
            Log.Info($"Handling {command.ActionType}: {idCount} IDs, {stateCount} states.");

            // Temporarily clear selection to ensure Move It's internal logic (like UpdateSegments)
            // doesn't skip segments that are locally selected on the receiver.
            var oldSelection = new HashSet<Instance>(MoveIt.Action.selection);
            MoveIt.Action.selection.Clear();

            try
            {
                if (command.ActionType == MoveItActionType.Bulldoze)
                {
                    HandleBulldoze(command);
                }
                else
                {
                    HandleGenericStateUpdate(command);
                }
            }
            finally
            {
                // Restore selection
                foreach (var instance in oldSelection)
                {
                    MoveIt.Action.selection.Add(instance);
                }
            }
        }

        private static bool IsValidId(InstanceID id)
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

        private void HandleBulldoze(MoveItActionCommand command)
        {
            var action = new BulldozeAction();
            var statesField = typeof(BulldozeAction).GetField("m_states", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (statesField == null) return;

            var states = new HashSet<InstanceState>();
            if (command.InstanceIDs != null)
            {
                foreach (var rawId in command.InstanceIDs)
                {
                    InstanceID id = default;
                    id.RawData = rawId;
                    if (!IsValidId(id)) continue;

                    Instance instance = (Instance)id;
                    if (instance != null && instance.isValid)
                    {
                        states.Add(instance.SaveToState(false));
                    }
                }
            }

            if (states.Count > 0)
            {
                statesField.SetValue(action, states);
                using (CsmBridge.StartIgnore())
                {
                    action.Do();
                }
            }
        }

        private void HandleGenericStateUpdate(MoveItActionCommand command)
        {
            if (command.States == null) return;

            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                // Finalize any ongoing live preview
                MoveItPreviewHandler.ClearSession();

                using (CsmBridge.StartIgnore())
                {
                    // Phase 0: Force select all incoming objects to bypass Move It's automatic recalculations.
                    var oldSelection = new HashSet<Instance>(MoveIt.Action.selection);
                    MoveIt.Action.selection.Clear();
                    foreach (var state in command.States)
                    {
                        InstanceID id = default;
                        id.RawData = state.InstanceID;
                        if (!IsValidId(id)) continue;

                        Instance instance = (Instance)id;
                        if (instance != null && instance.isValid)
                        {
                            MoveIt.Action.selection.Add(instance);
                        }
                    }

                    try
                    {
                        // Phase 1: Synchronous Geometric Moves
                        foreach (var stateData in command.States)
                        {
                            InstanceID id = default;
                            id.RawData = stateData.InstanceID;
                            if (!IsValidId(id)) continue;

                            Instance instance = (Instance)id;

                            if (instance != null && instance.isValid)
                            {
                                try 
                                {
                                    if (instance is MoveableBuilding bInstance)
                                    {
                                        Log.Info($"Phase 1: Moving MoveableBuilding (ID: {bInstance.id.Building})");
                                        bInstance.Move(stateData.Position, stateData.Angle);
                                        BuildingManager.instance.UpdateBuilding(bInstance.id.Building);
                                        // Force immediate renderer update to refresh AoE (electricity, etc.)
                                        BuildingManager.instance.UpdateBuildingRenderer(bInstance.id.Building, true);
                                    }
                                    else if (instance is MoveableTree tInstance)
                                    {
                                        Log.Info($"Phase 1: Moving MoveableTree (ID: {tInstance.id.Tree})");
                                        tInstance.Move(stateData.Position, stateData.Angle);
                                    }
                                    else if (instance is MoveableProp pInstance)
                                    {
                                        Log.Info($"Phase 1: Moving MoveableProp (ID: {pInstance.id.Prop})");
                                        pInstance.Move(stateData.Position, stateData.Angle);
                                    }
                                    else if (instance is MoveableNode nInstance)
                                    {
                                        Log.Info($"Phase 1: Immediate MoveProcess for Node {nInstance.id.NetNode}");
                                        var method = typeof(MoveableNode).GetMethod("MoveProcess", 
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                        if (method != null) {
                                            method.Invoke(nInstance, new object[] { stateData.Position, stateData.Angle });
                                        }
                                        
                                        // Manually tell the NetManager to update the node and all connected segments
                                        NetManager.instance.UpdateNode(nInstance.id.NetNode);
                                        
                                        // Dirty all connected segments to force road mesh refresh
                                        for (int i = 0; i < 8; i++) {
                                            ushort segmentId = NetManager.instance.m_nodes.m_buffer[nInstance.id.NetNode].GetSegment(i);
                                            if (segmentId != 0 && segmentId < 32768) {
                                                NetManager.instance.UpdateSegment(segmentId);
                                                NetManager.instance.UpdateSegmentRenderer(segmentId, true);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex) {
                                    Log.Error($"Error in Phase 1 for ID {stateData.InstanceID}: {ex.Message}");
                                }
                            }
                        }

                        // Phase 2: Force explicit segment tangents & Curvature
                        foreach (var stateData in command.States)
                        {
                            InstanceID id = default;
                            id.RawData = stateData.InstanceID;
                            if (!IsValidId(id)) continue;

                            Instance instance = (Instance)id;

                            if (instance is MoveableSegment segInstance)
                            {
                                try {
                                    Log.Info($"Phase 2: Forcing Segment {segInstance.id.NetSegment} curvature.");
                                    HandleSegmentUpdate(segInstance, stateData);
                                }
                                catch (Exception ex) {
                                    Log.Error($"Error in Phase 2 for Segment {segInstance.id.NetSegment}: {ex.Message}");
                                }
                            }
                        }

                        // Phase 2b: Apply AutoCurve if requested
                        if (command.AutoCurve)
                        {
                            try {
                                NetSegment curveGuide = default;
                                curveGuide.m_startNode = command.CurveStartNode;
                                curveGuide.m_endNode = command.CurveEndNode;
                                curveGuide.m_startDirection = command.CurveStartDir;
                                curveGuide.m_endDirection = command.CurveEndDir;

                                ushort[] nodes = { command.CurveStartNode, command.CurveEndNode };
                                foreach (ushort nodeId in nodes)
                                {
                                    if (nodeId > 0 && nodeId < 32768)
                                    {
                                        MoveableNode node = new MoveableNode(new InstanceID { NetNode = nodeId });
                                        if (node.isValid)
                                        {
                                            node.AutoCurve(curveGuide);
                                            NetManager.instance.UpdateNode(nodeId);
                                            NetManager.instance.UpdateNodeRenderer(nodeId, true);
                                            for (int j = 0; j < 8; j++)
                                            {
                                                ushort sid = NetManager.instance.m_nodes.m_buffer[nodeId].GetSegment(j);
                                                if (sid > 0 && sid < 32768) {
                                                    NetManager.instance.UpdateSegment(sid);
                                                    NetManager.instance.UpdateSegmentRenderer(sid, true);
                                                }
                                            }
                                        }
                                    }
                                }
                            } catch (Exception ex) {
                                Log.Error($"Error in Phase 2b (AutoCurve): {ex.Message}");
                            }
                        }

                        // Phase 3: Force immediate visual refresh
                        if (command.States.Count > 0)
                        {
                            Bounds bounds = new Bounds(command.States[0].Position, Vector3.one * 10f);
                            foreach (var state in command.States)
                            {
                                bounds.Encapsulate(state.Position);
                                
                                var idObj = state.InstanceID;
                                // Cities: Skylines InstanceID type is at byte 3 (bits 24-31)
                                var type = (int)((idObj >> 24) & 0xFF);
                                var realId = (ushort)(idObj & 0xFFFF);

                                try {
                                    switch (type)
                                    {
                                        case 1: // Building
                                            if (realId < 49152) BuildingManager.instance.UpdateBuildingRenderer(realId, true);
                                            else Log.Warn($"Ignoring invalid Building ID for renderer update: {realId}");
                                            break;
                                        case 5: // NetNode (Move It's InstanceType.NetNode is 5)
                                            if (realId < 32768) NetManager.instance.UpdateNodeRenderer(realId, true);
                                            else Log.Warn($"Ignoring invalid Node ID for renderer update: {realId}");
                                            break;
                                        case 6: // NetSegment
                                            if (realId < 32768) NetManager.instance.UpdateSegmentRenderer(realId, true);
                                            else Log.Warn($"Ignoring invalid Segment ID for renderer update: {realId}");
                                            break;
                                        case 2: // Prop
                                            PropManager.instance.UpdatePropRenderer(realId, true);
                                            break;
                                        case 3: // Tree
                                            TreeManager.instance.UpdateTreeRenderer(realId, true);
                                            break;
                                    }
                                } catch (Exception ex) {
                                    Log.Error($"Error updating renderer for InstanceID {idObj} (Type: {type}, RealID: {realId}): {ex.Message}");
                                }
                            }
                            ForceRefresh(bounds);
                        }
                    }
                    finally
                    {
                        // Restore original selection
                        MoveIt.Action.selection.Clear();
                        foreach (var inst in oldSelection)
                        {
                            MoveIt.Action.selection.Add(inst);
                        }
                    }
                }
            });
        }

        private void ForceRefresh(Bounds bounds)
        {
            try
            {
                var tool = MoveItTool.instance;
                if (tool == null) return;

                // Expand bounds to ensure all render groups and segments are covered
                Bounds updateBounds = bounds;
                updateBounds.Expand(48f); // Expanded from 32f for better coverage

                Log.Info($"Phase 3: Forcing DIRECT visual refresh for bounds {updateBounds.center}");

                // 1. Process all queued segment AI updates immediately
                tool.UpdateSegments();
                tool.segmentUpdateCountdown = -1;

                // 2. Immediate terrain update
                TerrainModify.UpdateArea(updateBounds.min.x, updateBounds.min.z, updateBounds.max.x, updateBounds.max.z, true, true, false);

                // 3. Immediate render update (marks groups dirty)
                var updateRenderMethod = typeof(MoveItTool).GetMethod("UpdateRender", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (updateRenderMethod != null)
                {
                    updateRenderMethod.Invoke(null, new object[] { updateBounds });
                }

                // Clear Move It's internal counters
                tool.areasToUpdate.Clear();
                tool.areasToQuickUpdate.Clear();
                tool.areaUpdateCountdown = -1;
            }
            catch (Exception ex)
            {
                Log.Error($"Error in ForceRefresh: {ex.Message}");
            }
        }

        private void HandleSegmentUpdate(MoveableSegment segment, ObjectStateData stateData)
        {
            ushort segmentId = segment.id.NetSegment;
            var segBuffer = NetManager.instance.m_segments.m_buffer;
            
            if (segmentId < 32768 && (segBuffer[segmentId].m_flags & NetSegment.Flags.Created) != 0)
            {
                Log.Info($"Updating segment {segmentId} tangents: Start={stateData.StartDirection}");
                
                // 1. Force absolute tangents in a local copy
                NetSegment seg = segBuffer[segmentId];
                seg.m_startDirection = stateData.StartDirection;
                seg.m_endDirection = stateData.EndDirection;
                
                // 2. Write back to buffer
                segBuffer[segmentId] = seg;

                // 3. Update blocks (zoning/terrain) via reflection. 
                // MUST happen before UpdateNode for correct mesh blending in some cases.
                var method = typeof(Instance).GetMethod("UpdateSegmentBlocks", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (method != null) {
                    method.Invoke(null, new object[] { segmentId, seg });
                }

                // 4. Force DEEP refresh of the segment and its end nodes
                NetManager.instance.UpdateSegment(segmentId);
                NetManager.instance.UpdateSegmentRenderer(segmentId, true);

                if (seg.m_startNode != 0) {
                    NetManager.instance.UpdateNode(seg.m_startNode);
                    NetManager.instance.UpdateNodeRenderer(seg.m_startNode, true);
                }
                if (seg.m_endNode != 0) {
                    NetManager.instance.UpdateNode(seg.m_endNode);
                    NetManager.instance.UpdateNodeRenderer(seg.m_endNode, true);
                }
            }
        }
    }
}
