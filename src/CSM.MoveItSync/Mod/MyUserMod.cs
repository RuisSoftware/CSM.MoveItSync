using ICities;
using CitiesHarmony.API;
using CSM.MoveItSync.Patching;
using CSM.MoveItSync.Services;

namespace CSM.MoveItSync.Mod
{
    public class MyUserMod : IUserMod
    {
        public string Name => ModMetadata.ModName + " " + ModMetadata.Version;
        public string Description => ModMetadata.Description;

        public void OnEnabled()
        {
            // Version check for CSM
            try
            {
                CompatibilityService.CheckCsmVersion();
            }
            catch
            {
                // Ignore errors
            }
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                ActionQueuePatch.UnpatchAll();
            }
        }
    }
}
