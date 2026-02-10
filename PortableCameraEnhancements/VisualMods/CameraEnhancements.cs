using System;
using ABI_RC.Systems.Camera;
using UnityEngine;

namespace Sketch.PortableCameraEnhancements.VisualMods
{
    public class CameraEnhancements
    {
        public static CameraEnhancements Instance;

        private PortableCameraSetting setting_MinDistance;
        private PortableCameraSetting setting_MaxDistance;

        public void Setup(PortableCamera __instance)
        {
            Instance = this;

            __instance.@interface.AddAndGetHeader(null, nameof(CameraEnhancements), "Player Tracking Settings");

            // UseNewLookTarget
            PortableCameraSetting setting_UseNewLookTarget = __instance.@interface.AddAndGetSetting(PortableCameraSettingType.Bool);
            setting_UseNewLookTarget.BoolChanged = new Action<bool>(value => UpdateCameraSettingBool("UseNewLookTarget", value));
            setting_UseNewLookTarget.SettingIdentifier = "UseNewLookTarget";
            setting_UseNewLookTarget.DisplayName = "Use New Look Target";
            setting_UseNewLookTarget.OriginType = nameof(CameraEnhancements);
            setting_UseNewLookTarget.DefaultValue = true;
            setting_UseNewLookTarget.Load();

            // UseBlending
            PortableCameraSetting setting_UseBlending = __instance.@interface.AddAndGetSetting(PortableCameraSettingType.Bool);
            setting_UseBlending.BoolChanged = new Action<bool>(value => UpdateCameraSettingBool("UseBlending", value));
            setting_UseBlending.SettingIdentifier = "UseBlending";
            setting_UseBlending.DisplayName = "Blend Between Targets";
            setting_UseBlending.OriginType = nameof(CameraEnhancements);
            setting_UseBlending.DefaultValue = true;
            setting_UseBlending.Load();

            // MinDistance
            setting_MinDistance = __instance.@interface.AddAndGetSetting(PortableCameraSettingType.Float);
            setting_MinDistance.FloatChanged = new Action<float>(value => UpdateCameraSettingFloat("MinDistance", value));
            setting_MinDistance.SettingIdentifier = "MinDistance";
            setting_MinDistance.DisplayName = "Min Distance";
            setting_MinDistance.isExpertSetting = true;
            setting_MinDistance.OriginType = nameof(CameraEnhancements);
            setting_MinDistance.DefaultValue = 1f;
            setting_MinDistance.MinValue = 0.01f;
            setting_MinDistance.MaxValue = 2.99f;
            setting_MinDistance.Load();

            // MaxDistance
            setting_MaxDistance = __instance.@interface.AddAndGetSetting(PortableCameraSettingType.Float);
            setting_MaxDistance.FloatChanged = new Action<float>(value => UpdateCameraSettingFloat("MaxDistance", value));
            setting_MaxDistance.SettingIdentifier = "MaxDistance";
            setting_MaxDistance.DisplayName = "Max Distance";
            setting_MaxDistance.isExpertSetting = true;
            setting_MaxDistance.OriginType = nameof(CameraEnhancements);
            setting_MaxDistance.DefaultValue = 3.01f;
            setting_MaxDistance.MinValue = 3f;
            setting_MaxDistance.MaxValue = 10f;
            setting_MaxDistance.Load();

            OnUpdateOptionsDisplay();
        }


        public void OnUpdateOptionsDisplay(bool expertMode = true)
        {
            if (!expertMode)
                return;

            setting_MinDistance.settingsObject.SetActive(true);
            setting_MaxDistance.settingsObject.SetActive(true);
        }

        private void UpdateCameraSettingBool(string setting, bool value)
        {
            if (PortableCamera.Instance != null)
            {
                switch (setting)
                {
                    case "UseNewLookTarget":
                        PortableCameraEnhancements.UseNewLookTarget = value;
                        break;
                    case "UseBlending":
                        PortableCameraEnhancements.AutoBlendTargets = value;
                        break;
                }
            }
        }

        private void UpdateCameraSettingFloat(string setting, float value)
        {
            if (PortableCamera.Instance != null)
            {
                switch (setting)
                {
                    case "MinDistance":
                        PortableCameraEnhancements.BlendMinDistance = value;
                        break;
                    case "MaxDistance":
                        PortableCameraEnhancements.BlendMaxDistance = value;
                        break;
                }
            }
        }
    }
}
