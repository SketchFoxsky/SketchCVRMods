using MelonLoader;
using UnityEngine;
using ABI_RC;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using ABI_RC.API;
using ABI_RC.Core.Player;
using HarmonyLib;

namespace Sketch.PortableCameraEnhancements
{
    public class PortableCameraEnhancements : MelonMod
    {
        #region MelonLoader Preferences
        private const string SettingsCategory = nameof(PortableCameraEnhancements);

        private static readonly MelonPreferences_Category Category = MelonPreferences.CreateCategory(SettingsCategory);

        internal static MelonPreferences_Entry<bool> LogAviChanges =
            Category.CreateEntry("Log_Avatar_Changes", true, "Log_Avatar_Changes", description: "Print Avatar changes to the Log");


        #endregion

        public static Vector3 ModdedTarget;
        public static Vector3 BlendedTarget;

        private const float DefaultScale = 1.75f;
        public float AvatarHeight;

        private Animator AvatarAnimator;
        private bool UseDefaultTarget;

        public static Transform Head;
        public static Transform Hips;
        public static Transform Chest;
        public static Transform FootL;
        public static Transform FootR;


        public static bool UseNewLookTarget = true;
        public static bool AutoBlendTargets = true;
        public static float BlendMinDistance = 1f;
        public static float BlendMaxDistance = 3f;

        public override void OnInitializeMelon()
        {
            ApplyPatches(typeof(HarmonyPatches.PortableCameraPatches));
            ApplyPatches(typeof(HarmonyPatches.PlayerFaceTracking_Update_PrefixPatch));


            CVRGameEventSystem.Avatar.OnLocalAvatarClear.AddListener(_ =>
            {
                if (LogAviChanges.Value)
                {
                    LoggerInstance.Msg("Local Avatar has been cleared");
                }
                
                ClearLocalAvatar();
            });

            CVRGameEventSystem.Avatar.OnLocalAvatarLoad.AddListener(_ =>
            {
                if (LogAviChanges.Value)
                {
                    LoggerInstance.Msg("Local Avatar has been loaded");
                }
                
                LoadLocalAvatar();
            });
        }

        private void ApplyPatches(Type type)
        {
            try
            {
                HarmonyInstance.PatchAll(type);
                LoggerInstance.Msg("Patch Applied");
            }
            catch (Exception e)
            {
                LoggerInstance.Msg($"Failed while patching {type.Name}!");
                LoggerInstance.Error(e);
            }
        }

        private void ClearLocalAvatar()
        {
            UseDefaultTarget = true;
            Head = Hips = Chest = FootL = FootR = null;
        }

        private void LoadLocalAvatar()
        {
            AvatarAnimator = PlayerSetup.Instance.Animator;
            if (!AvatarAnimator.isHuman)
            {
                UseDefaultTarget = true;
                AvatarHeight = DefaultScale;
                return;
            }

            UseDefaultTarget = false;

            Head = AvatarAnimator.GetBoneTransform(HumanBodyBones.Head);
            Hips = AvatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Chest = AvatarAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? AvatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
            FootL = AvatarAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            FootR = AvatarAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            AvatarHeight = PlayerSetup.Instance.AvatarHeight;
        }

        public override void OnUpdate()
        {
            if (Head == null || Chest == null || Hips == null || FootL == null || FootR == null)
                return;
            Vector3 feet =(FootL.position + FootR.position) / 2f;
            Vector3 rawTarget = (Head.position + Chest.position + Hips.position + feet) / 4f;
            ModdedTarget = Vector3.Lerp(ModdedTarget, rawTarget, Time.deltaTime * 100f); // 100f is the Smooth to prevent micro stutters when moving in local space.

        }

        public static float GetAvatarHeight()
        {
            float I = PlayerSetup.Instance.AvatarHeight;
            return I;
        }
    }
}