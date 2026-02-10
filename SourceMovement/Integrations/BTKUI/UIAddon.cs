using BTKUILib;
using BTKUILib.UIObjects;
using System.Reflection;
using MelonLoader;

namespace SourceMovement.Integrations
{
    public static partial class BTKUIAddon
    {
        private static Page _rootPage;
        private static string _rootPageElementID;
        private static bool _isSMTabOpened;

        public static void Initialize()
        {
            try
            {
                SetupIcons();
                SetupSM_ModTab();
                MelonLogger.Msg("[BTKUIAddon] Source Movement tab created successfully.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BTKUIAddon] Failed to create Source Movement tab: {ex}");
            }
        }


        private static void SetupIcons()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string assemblyName = assembly.GetName().Name;

            QuickMenuAPI.PrepareIcon("Source Movement", "Crowbar", GetIconStream("Crowbar.png"));
            MelonLogger.Msg("BTKUI Tab should be made");
            Stream GetIconStream(string iconName) => assembly.GetManifestResourceStream($"{assemblyName}.Resources.{iconName}");
            
            
        }

        private static void SetupSM_ModTab()
        {
            _rootPage = new Page("Source Movement", "Source Movement Settings", true, "Crowbar", null)
            {
                MenuTitle = "Source Movement",
                MenuSubtitle = "Move around like Gordon Freeman, or dont; up to you."
            };

            _rootPageElementID = _rootPage.ElementID;

            var category = _rootPage.AddCategory("Source Movement Mod");
            var USMtoggle = category.AddToggle("Use Source Movement", "Click to toggle source movement", (Sketch.SourceMovement.SourceMovement.EntryUseSourceMovement.Value));
            USMtoggle.OnValueUpdated += USM =>
            {
                if (USM == true)
                {
                    Sketch.SourceMovement.SourceMovement.EntryUseSourceMovement.Value = true;
                }
                else
                {
                    Sketch.SourceMovement.SourceMovement.EntryUseSourceMovement.Value = false;
                }
            };
            
            var UDTtoggle = category.AddToggle("Use Double Tap", "Double tap the crowbar to toggle Source Movement", (Sketch.SourceMovement.SourceMovement.UseDoubleClickTab.Value));
            UDTtoggle.OnValueUpdated += UDT =>
            {
                if (UDT == true)
                {
                    Sketch.SourceMovement.SourceMovement.UseDoubleClickTab.Value = true;
                }
                else
                {
                    Sketch.SourceMovement.SourceMovement.UseDoubleClickTab.Value = false;
                }
            };

            QuickMenuAPI.OnTabChange += OnTabChange;
            MelonLogger.Msg("BTKUI Tab should be made");
        }

        private static DateTime lastTime = DateTime.Now;

        private static void OnTabChange(string newTab, string previousTab)
        {
            _isSMTabOpened = newTab == _rootPageElementID;
            if (!_isSMTabOpened) return;

            TimeSpan timeDifference = DateTime.Now - lastTime;
            if (timeDifference.TotalSeconds <= 0.5)
            {
                if (Sketch.SourceMovement.SourceMovement.UseDoubleClickTab.Value)
                {
                    ToggleSourceMovement();
                }
                return;
            }
            lastTime = DateTime.Now;
        }

        private static void ToggleSourceMovement()
        {
            if (Sketch.SourceMovement.SourceMovement.EntryUseSourceMovement.Value)
            {
                Sketch.SourceMovement.SourceMovement.EntryUseSourceMovement.Value = false;
            }
            else
            {
                Sketch.SourceMovement.SourceMovement.EntryUseSourceMovement.Value = true;
            }
        }
    }
}
