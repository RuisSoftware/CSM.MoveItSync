using ProtoBuf;
using UnityEngine;
using System.Collections.Generic;

using CSM.API.Commands;
using CSM.BaseGame.Injections.Tools;

namespace CSM.MoveItSync.Messages
{
    [ProtoContract]
    public class MoveItPreviewCommand : ToolCommandBase
    {
        [ProtoMember(3)]
        public long DragID { get; set; }

        [ProtoMember(4)]
        public Vector3 MoveDelta { get; set; }

        [ProtoMember(5)]
        public float AngleDelta { get; set; }

        [ProtoMember(6)]
        public Vector3 Center { get; set; }

        [ProtoMember(7)]
        public List<long> InstanceIDs { get; set; }

        [ProtoMember(8)]
        public bool AutoCurve { get; set; }

        [ProtoMember(9)]
        public ushort CurveStartNode { get; set; }

        [ProtoMember(10)]
        public ushort CurveEndNode { get; set; }

        [ProtoMember(11)]
        public Vector3 CurveStartDir { get; set; }

        [ProtoMember(12)]
        public Vector3 CurveEndDir { get; set; }

        [ProtoMember(13)]
        public bool IsBending { get; set; }
    }
}
