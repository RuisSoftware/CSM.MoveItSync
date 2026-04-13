using HarmonyLib;
using MoveIt;
using CSM.MoveItSync.Services;
using System;
using System.Collections.Generic;
using ColossalFramework;
using UnityEngine;

namespace CSM.MoveItSync.Patching
{
    using Log = CSM.MoveItSync.Services.Log;

    [HarmonyPatch]
    public static class MoveablePatches
    {
        // Reflection caches
        private static readonly System.Reflection.FieldInfo NSField = typeof(MoveItTool).GetField("NS", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        private static readonly System.Reflection.FieldInfo IsSubInstanceField = typeof(MoveableBuilding).GetField("isSubInstance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // --- 1. MoveableSegment.SaveToState ---
        
        [HarmonyPatch(typeof(MoveableSegment), "SaveToState")]
        [HarmonyPrefix]
        public static bool SaveToStatePrefix(MoveableSegment __instance, ref bool integrate)
        {
            try
            {
                if (integrate)
                {
                    var ns = NSField?.GetValue(null);
                    if (ns == null)
                    {
                        integrate = false;
                    }
                }
            }
            catch
            {
                integrate = false;
            }
            return true;
        }

        // --- 2. MoveableNode.GetSubInstances ---

        [HarmonyPatch(typeof(MoveableNode), "GetSubInstances")]
        [HarmonyPrefix]
        public static bool GetSubInstancesPrefix(MoveableNode __instance, ref List<Instance> __result)
        {
            __result = GetSubInstancesSafe(__instance);
            return false;
        }

        private static List<Instance> GetSubInstancesSafe(MoveableNode nodeInstance)
        {
            List<Instance> instances = new List<Instance>();
            
            var nodeBuffer = NetManager.instance.m_nodes.m_buffer;
            var buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;
            
            ushort building = nodeBuffer[nodeInstance.id.NetNode].m_building;
            int count = 0;
            while (building != 0)
            {
                InstanceID buildingID = default;
                buildingID.Building = building;

                instances.Add(new MoveableBuilding(buildingID));
                building = buildingBuffer[building].m_subBuilding;

                if (++count > 49152)
                {
                    Log.Info("Buildings: Invalid list detected (Potential infinite loop) in GetSubInstances!");
                    break;
                }
            }

            if (nodeInstance.id.Building == 0) return instances;

            ushort node = buildingBuffer[nodeInstance.id.Building].m_netNode;
            count = 0;
            while (node != 0)
            {
                ItemClass.Layer layer = nodeBuffer[node].Info.m_class.m_layer;
                if (layer != ItemClass.Layer.PublicTransport)
                {
                    InstanceID nodeID = default;
                    nodeID.NetNode = node;
                    instances.Add(new MoveableNode(nodeID));
                }

                node = nodeBuffer[node].m_nextBuildingNode;
                if ((nodeBuffer[node].m_flags & NetNode.Flags.Created) != NetNode.Flags.Created)
                {
                    node = 0;
                }

                if (++count > 32768)
                {
                    Log.Info("Nodes: Invalid list detected (Potential infinite loop) in GetSubInstances!");
                    break;
                }
            }

            return instances;
        }

        // --- 3. MoveableBuilding.ResetSubInstances ---

        [HarmonyPatch(typeof(MoveableBuilding), "ResetSubInstances")]
        [HarmonyPrefix]
        public static bool ResetSubInstancesPrefix(MoveableBuilding __instance)
        {
            ResetSubInstancesSafe(__instance);
            return false;
        }

        private static void ResetSubInstancesSafe(MoveableBuilding buildingInstance)
        {
            List<Instance> instances = new List<Instance>();
            var nodeBuffer = NetManager.instance.m_nodes.m_buffer;
            var buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;
            
            bool isSub = false;
            if (IsSubInstanceField != null)
            {
                isSub = (bool)IsSubInstanceField.GetValue(buildingInstance);
            }
            
            if (!isSub)
            {
                ushort building = buildingBuffer[buildingInstance.id.Building].m_subBuilding;
                int count = 0;
                while (building != 0)
                {
                    InstanceID buildingID = default;
                    buildingID.Building = building;

                    instances.Add(new MoveableBuilding(buildingID, true));
                    building = buildingBuffer[building].m_subBuilding;

                    if (++count > 49152)
                    {
                        Log.Info("Buildings: Invalid list detected (Potential infinite loop) in ResetSubInstances!");
                        break;
                    }
                }
            }

            ushort node = buildingBuffer[buildingInstance.id.Building].m_netNode;
            int nodeCount = 0;
            while (node != 0)
            {
                ItemClass.Layer layer = nodeBuffer[node].Info.m_class.m_layer;
                if (layer != ItemClass.Layer.PublicTransport)
                {
                    InstanceID nodeID = default;
                    nodeID.NetNode = node;
                    instances.Add(new MoveableNode(nodeID));
                }

                node = nodeBuffer[node].m_nextBuildingNode;
                if ((nodeBuffer[node].m_flags & NetNode.Flags.Created) != NetNode.Flags.Created)
                {
                    node = 0;
                }

                if (++nodeCount > 32768)
                {
                    Log.Info("Nodes: Invalid list detected (Potential infinite loop) in ResetSubInstances!");
                    break;
                }
            }

            buildingInstance.subInstances = instances;
        }
    }

    [HarmonyPatch(typeof(NetNode), "CalculateNode")]
    public static class CalculateNodePatch
    {
        public static bool Prefix(ushort nodeID)
        {
            if (nodeID == 0 || nodeID >= 32768)
            {
                return false; // Skip invalid node IDs
            }

            // Check if node is essentially 'junk' (no flags) which can happen during desynced states
            if (NetManager.instance.m_nodes.m_buffer[nodeID].m_flags == NetNode.Flags.None)
            {
                return false;
            }

            return true;
        }
    }
}
