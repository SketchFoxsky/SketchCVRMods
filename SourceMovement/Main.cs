using MelonLoader;
using UnityEngine;
using ABI_RC.Systems.Movement;
using ABI.CCK.Components;
using ABI_RC.Systems.GameEventSystem;
using SourceMovement.Integrations;


namespace Sketch.SourceMovement
{
    public class SourceMovement : MelonMod
    {
        #region Melon Loader Preferences

        private const string SettingsCategory = nameof(SourceMovement);

        private static readonly MelonPreferences_Category Category = MelonPreferences.CreateCategory(SettingsCategory);

        internal static MelonPreferences_Entry<bool> EntryUseSourceMovement =
            Category.CreateEntry("Use_source_movement", false, "Use Source Movement", description: "Toggle source movement.");
        internal static MelonPreferences_Entry<bool> UseDoubleClickTab =
            Category.CreateEntry("Use_DoubleClick", true, "Use Double Click Tab", description: "Allows double clicking Crowbar Icon to toggle the  Use Source Movement' setting");

        #endregion Melon Loader Preferences

        #region Default Settings
        // Original values reference the BBCC value on world load.
        // Falling Speed
        private static float _currentMaxFallSpeed = 40f; //this is curently default.
        float _OriginalMaxFallSpeed = 40f;
        private static float _currentLateralFallingFriction = 2f;
        float _OriginalLateralFallingFriction = 0f;

        //Acceleration and Walking
        private static float _currentMaxAcceleration = 15f;
        float _OriginalMaxAcceleration = 25f;
        private static float _currentWalkSpeed = 100f;
        float _OriginalWalkSpeed;

        //Friction
        //These original Values are not touched by game code and must be manually reapplied.
        private static float _currentGroundFriction = 0f;
        private static float _OriginalGroundFriction = 8f;
        private static float _currentbrakingDecelerationWalking = 0f;
        private static float _OriginalbrakingDecelerationWalking = 25f;
        private static float _OriginalSlopeLimit = 70f;

        bool _PassedWorldCheck = false;

        BetterBetterCharacterController BBCC;

        #endregion Default Settings


        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
            //Add listeners to call world load and unload so we can apply world movement settings.

            //this can be spammed by world author toggling the gamobject with CVRWorld, needs a fix.
            CVRGameEventSystem.World.OnUnload.AddListener(_ =>
            {
                LoggerInstance.Msg("World unload event fired!");
                OnWorldUnload();
            });

            CVRWorld.GameRulesUpdated += OnApplyMovementSettings;
            BTKUIAddon.Initialize();
        }

        // Get World Movement Settings, This will also work on runtime with animated CVR World Settings!
        private void OnApplyMovementSettings()
        {
            if (BetterBetterCharacterController.Instance != null)
            {
                BBCC = BetterBetterCharacterController.Instance;
                _OriginalMaxAcceleration = BBCC.maxAcceleration;
                _OriginalWalkSpeed = BBCC.maxWalkSpeed;
                LoggerInstance.Msg("Original World Movement Settings acquired");

                //Prevent OnUpdate() from overiding the original values the same frame they change.
                _PassedWorldCheck = true;
            }
        }

        private void OnWorldUnload()
        {
            // Prevent loading old friction data from another world.
            // Add null check for BBCC cause WorldUnload fires on startup for some reason!
            if (BBCC != null)
            {
                LoggerInstance.Msg("Removed Old World movement.");
                BBCC.groundFriction = _OriginalGroundFriction;
                BBCC.brakingDecelerationWalking = _OriginalbrakingDecelerationWalking;
            }
            _PassedWorldCheck = false;
        }

        public override void OnUpdate()
        {
            if (BBCC != null)
            {
                if (((!EntryUseSourceMovement.Value)
                   || BBCC.IsFlying()
                   || BBCC.FlightAllowedInWorld == false
                   || BBCC.fallingTime <= 0.01f)
                   && (_PassedWorldCheck == true))
                {
                    BBCC.groundFriction = _OriginalGroundFriction;
                    BBCC.brakingDecelerationWalking = _OriginalbrakingDecelerationWalking;
                    BBCC.fallingLateralFriction = _OriginalLateralFallingFriction;
                    BBCC.maxFallSpeed = _OriginalMaxFallSpeed;
                    BBCC.maxWalkSpeed = _OriginalWalkSpeed;
                    BBCC.maxAcceleration = _OriginalMaxAcceleration;
                    BBCC.CharacterMovement.slopeLimit = _OriginalSlopeLimit;
                }
                //Apply Source Movement settings when jumping/falling
                if ((EntryUseSourceMovement.Value) && BBCC.fallingTime > 0.25f
                    && BBCC.FlightAllowedInWorld == true)
                {
                    BBCC.CharacterMovement.slopeLimit = 60f;
                    BBCC.fallingLateralFriction = _currentLateralFallingFriction;
                    BBCC.maxFallSpeed = _currentMaxFallSpeed;
                    BBCC.maxWalkSpeed = _currentWalkSpeed;
                    BBCC.maxAcceleration = _currentMaxAcceleration;
                    BBCC.groundFriction = _currentGroundFriction;
                    BBCC.brakingDecelerationWalking = _currentbrakingDecelerationWalking;
                }
            }
        }
    }
}
