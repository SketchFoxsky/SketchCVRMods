using ABI_RC.Systems.Camera;
using HarmonyLib;
using MelonLoader;
using Sketch.PortableCameraEnhancements.VisualMods;

namespace Sketch.PortableCameraEnhancements.HarmonyPatches
{
    internal class PortableCameraPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PortableCamera), nameof(PortableCamera.Start))]
        private static void Postfix_PortableCamera_Start(ref PortableCamera __instance)
        {
            CameraEnhancements mainMod = new();
            mainMod.Setup(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PortableCamera), nameof(PortableCamera.UpdateOptionsDisplay))]
        private static void Postfix_PortableCamera_UpdateOptionsDisplay(ref bool ____showExpertSettings)
        {
            CameraEnhancements.Instance?.OnUpdateOptionsDisplay(____showExpertSettings);
        }
    }
}
