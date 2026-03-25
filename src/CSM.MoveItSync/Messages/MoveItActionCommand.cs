using ProtoBuf;
using UnityEngine;
using System.Collections.Generic;
using CSM.API.Commands;

namespace CSM.MoveItSync.Messages
{
    [ProtoContract]
    public class MoveItActionCommand : CommandBase
    {
        [ProtoMember(2)]
        public MoveItActionType ActionType { get; set; }

        [ProtoMember(3)]
        public uint[] InstanceIDs { get; set; }

        // Data for Transform-style actions
        [ProtoMember(4)]
        public Vector3 MoveDelta { get; set; }

        [ProtoMember(5)]
        public float AngleDelta { get; set; }

        [ProtoMember(6)]
        public Vector3 Center { get; set; }

        [ProtoMember(7)]
        public bool FollowTerrain { get; set; }

        // Data for absolute state updates (Align, Line, etc.)
        [ProtoMember(8)]
        public List<ObjectStateData> States { get; set; }
    }

    public enum MoveItActionType
    {
        Transform,
        Bulldoze,
        GenericStateUpdate
    }

    [ProtoContract]
    public class ObjectStateData
    {
        [ProtoMember(1)]
        public uint InstanceID { get; set; }

        [ProtoMember(2)]
        public Vector3 Position { get; set; }

        [ProtoMember(3)]
        public float Angle { get; set; }

        [ProtoMember(4)]
        public float TerrainHeight { get; set; }
    }
}
