using Sketch.PortableCameraEnhancements.Properties;
using MelonLoader;
using System.Reflection;

[assembly: MelonInfo(
    typeof(Sketch.PortableCameraEnhancements.PortableCameraEnhancements),
    nameof(Sketch.PortableCameraEnhancements),
    AssemblyInfoParams.Version,
    AssemblyInfoParams.Author,
    downloadLink: "https://github.com/SketchFoxsky/CVR-Mods/releases/download/Releases/PortableCameraEnhancements.dll"
)]

[assembly: MelonGame(null, "ChilloutVR")]
[assembly: MelonPlatform(MelonPlatformAttribute.CompatiblePlatforms.WINDOWS_X64)]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
[assembly: MelonColor(255, 255, 196, 0)]
[assembly: MelonAuthorColor(255, 40, 144, 209)] 
[assembly: HarmonyDontPatchAll]

namespace Sketch.PortableCameraEnhancements.Properties
{
    internal static class AssemblyInfoParams
    {
        public const string Version = "1.0.1";
        public const string Author = "SketchFoxsky";
    }
}