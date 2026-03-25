using CSM.API.Commands;
using MoveIt;
using CSM.MoveItSync.Messages;
using CSM.MoveItSync.Services;
using System.Collections.Generic;
using UnityEngine;
using Log = CSM.MoveItSync.Services.Log;

namespace CSM.MoveItSync.Handlers
{
    public class MoveItActionHandler : CommandHandler<MoveItActionCommand>
    {
        protected override void Handle(MoveItActionCommand command)
        {
            Log.Info($"Received {command.ActionType} for {command.InstanceIDs?.Length ?? 0} instances.");

            if (command.ActionType == MoveItActionType.Transform)
            {
                HandleTransform(command);
            }
            else if (command.ActionType == MoveItActionType.Bulldoze)
            {
                HandleBulldoze(command);
            }
            else if (command.ActionType == MoveItActionType.GenericStateUpdate)
            {
                HandleGenericStateUpdate(command);
            }
        }

        private void HandleTransform(MoveItActionCommand command)
        {
            var action = new TransformAction
            {
                moveDelta = command.MoveDelta,
                angleDelta = command.AngleDelta,
                center = command.Center,
                followTerrain = command.FollowTerrain
            };

            action.m_states.Clear();
            if (command.InstanceIDs != null)
            {
                foreach (var rawId in command.InstanceIDs)
                {
                    InstanceID id = default;
                    id.RawData = rawId;
                    Instance instance = (Instance)id;
                    if (instance != null && instance.isValid)
                    {
                        action.m_states.Add(instance.SaveToState(false));
                    }
                }
            }

            if (action.m_states.Count > 0)
            {
                using (CsmBridge.StartIgnore())
                {
                    action.Do();
                }
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

            using (CsmBridge.StartIgnore())
            {
                foreach (var stateData in command.States)
                {
                    InstanceID id = default;
                    id.RawData = stateData.InstanceID;
                    Instance instance = (Instance)id;

                    if (instance != null && instance.isValid)
                    {
                        if (instance is MoveableBuilding building)
                        {
                            building.Move(stateData.Position, stateData.Angle);
                        }
                        else if (instance is MoveableNode node)
                        {
                            node.Move(stateData.Position, stateData.Angle);
                        }
                        else if (instance is MoveableProp prop)
                        {
                            prop.Move(stateData.Position, stateData.Angle);
                        }
                        else if (instance is MoveableTree tree)
                        {
                            tree.Move(stateData.Position, stateData.Angle);
                        }
                        else
                        {
                            instance.position = stateData.Position;
                            instance.angle = stateData.Angle;
                        }
                    }
                }
            }
        }
    }
}
