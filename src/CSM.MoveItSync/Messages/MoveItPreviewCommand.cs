using ProtoBuf;
using UnityEngine;
using System.Collections.Generic;

using CSM.API.Commands;

namespace CSM.MoveItSync.Messages
{
    [ProtoContract]
    public class MoveItPreviewCommand : CommandBase
    {
        [ProtoMember(1)]
        public long DragID { get; set; }

        [ProtoMember(2)]
        public Vector3 MoveDelta { get; set; }

        [ProtoMember(3)]
        public float AngleDelta { get; set; }

        [ProtoMember(4)]
        public Vector3 Center { get; set; }

        [ProtoMember(5)]
        public List<long> InstanceIDs { get; set; }
        [ProtoMember(6)]
        public bool AutoCurve { get; set; }

        [ProtoMember(7)]
        public ushort CurveStartNode { get; set; }

        [ProtoMember(8)]
        public ushort CurveEndNode { get; set; }

        [ProtoMember(9)]
        public Vector3 CurveStartDir { get; set; }

        [ProtoMember(10)]
        public Vector3 CurveEndDir { get; set; }
    }
}
