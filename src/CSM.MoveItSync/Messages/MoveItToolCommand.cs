using ProtoBuf;
using UnityEngine;
using CSM.API.Commands;
using CSM.BaseGame.Injections.Tools;

namespace CSM.MoveItSync.Messages
{
    [ProtoContract]
    public class MoveItToolCommand : ToolCommandBase
    {
        // Inherits PlayerName and CursorWorldPosition from ToolCommandBase
        
        [ProtoMember(3)]
        public bool IsActive { get; set; }

        [ProtoMember(4)]
        public ushort HoverID { get; set; }
    }
}
