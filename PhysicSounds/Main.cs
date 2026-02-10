using MelonLoader;
using ABI_RC.Core.Util.AssetFiltering;

namespace Sketch.PhysicSounds
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            SharedFilter.SpawnableWhitelist.Add(typeof(PhysicSound));
            WorldFilter._Base.Add(typeof(PhysicSound));
            LoggerInstance.Msg("Initialized.");
        }
    }
}