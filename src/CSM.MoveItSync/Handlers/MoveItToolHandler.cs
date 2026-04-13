using CSM.API.Commands;
using CSM.MoveItSync.Messages;
using CSM.BaseGame.Injections.Tools;
using ColossalFramework;
using UnityEngine;

namespace CSM.MoveItSync.Handlers
{
    public class MoveItToolHandler : CommandHandler<MoveItToolCommand>
    {
        protected override void Handle(MoveItToolCommand command)
        {
            if (!Singleton<LoadingManager>.exists || !Singleton<LoadingManager>.instance.m_loadingComplete)
                return;

            if (SimulationManager.instance != null && SimulationManager.instance.m_ThreadingWrapper != null)
            {
                SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(() =>
                {
                    PlayerCursorManager cursorView =
                        Singleton<ToolSimulatorCursorManager>.instance.GetCursorView(command.SenderId);
                    if (cursorView)
                    {
                        cursorView.SetLabelContent(command);
                        // Optional: Set a specific cursor for Move It if desired
                    }
                });
            }
        }
    }
}
