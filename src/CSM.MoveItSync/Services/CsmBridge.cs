using System;
using CSM.API.Commands;
using CSM.API.Helpers;

namespace CSM.MoveItSync.Services
{
    internal static class CsmBridge
    {
        internal static void SendToAll(CommandBase command)
        {
            if (command == null) return;
            
            if (Command.SendToAll == null)
            {
                UnityEngine.Debug.Log($"[MoveIt-CSM--Sync] CsmBridge: Command.SendToAll is NULL! Mod not fully connected to CSM session?");
                return;
            }

            UnityEngine.Debug.Log($"[MoveIt-CSM--Sync] CsmBridge: Sending {command.GetType().Name} to all peers.");
            Command.SendToAll.Invoke(command);
        }

        internal static bool IsServerInstance() => Command.CurrentRole == MultiplayerRole.Server;

        internal static IDisposable StartIgnore()
        {
            try
            {
                var helper = IgnoreHelper.Instance;
                if (helper == null) return DummyScope.Instance;
                helper.StartIgnore();
                return new IgnoreScope(helper);
            }
            catch { return DummyScope.Instance; }
        }

        private sealed class IgnoreScope : IDisposable
        {
            private readonly IgnoreHelper _helper;
            internal IgnoreScope(IgnoreHelper helper) { _helper = helper; }
            public void Dispose() { _helper.EndIgnore(); }
        }

        private sealed class DummyScope : IDisposable
        {
            internal static readonly DummyScope Instance = new DummyScope();
            public void Dispose() { }
        }
    }
}
