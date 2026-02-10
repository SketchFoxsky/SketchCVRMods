using MelonLoader;
using UnityEngine;
using HarmonyLib;
using ABI_RC.Core.Util.AssetFiltering;
using ABI_RC.Core.Player;

namespace Sketch.ClimbingSystem
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            WorldFilter._Base.Add(typeof(Climbable));
            HarmonyInstance.PatchAll();
            MelonLogger.Msg("Mod initialized");
        }

        [HarmonyPatch]
        internal class HarmonyPatches
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.Start))]
            public static void After_PlayerSetup_Start()
            {
                try
                {
                    // Jank ass adding shit to the DDOL scene
                    var climbingmanagerGO = new GameObject("_ClimbManager");
                    UnityEngine.Object.DontDestroyOnLoad(climbingmanagerGO);
                    climbingmanagerGO.AddComponent<ClimbManager>();
                    MelonLogger.Msg("ClimbingManager Created");
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"I'm gonna fucking blow my god damn brains out why the FUCK is it not working! ANYWAYS I failed to patch PlayerSetup");
                    MelonLogger.Error (e);
                }
            }
        }
    }
}
