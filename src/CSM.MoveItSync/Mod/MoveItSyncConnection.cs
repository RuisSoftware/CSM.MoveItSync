using System;
using CSM.API;
using MoveIt;
using CSM.MoveItSync.Patching;
using CSM.MoveItSync.Services;
using UnityEngine;
using Log = CSM.MoveItSync.Services.Log;

namespace CSM.MoveItSync.Mod
{
    public class MoveItSyncConnection : Connection
    {
        public MoveItSyncConnection()
        {
            Name = ModMetadata.ModName;
            Enabled = true;
            ModClass = typeof(ModInfo);
            CommandAssemblies.Add(typeof(MoveItSyncConnection).Assembly);
        }

        public override void RegisterHandlers()
        {
            try 
            {
                Log.Info("Registering Harmony patches...");
                ActionQueuePatch.PatchAll();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to register handlers: {ex}");
            }
        }

        public override void UnregisterHandlers()
        {
            try
            {
                Log.Info("Unregistering Harmony patches...");
                ActionQueuePatch.UnpatchAll();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to unregister handlers: {ex}");
            }
        }
    }
}
