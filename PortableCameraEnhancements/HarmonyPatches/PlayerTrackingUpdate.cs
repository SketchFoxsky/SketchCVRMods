using HarmonyLib;
using UnityEngine;
using ABI_RC.Systems.Camera.VisualMods;
using ABI_RC.Systems.Movement;
using System.Reflection;

namespace Sketch.PortableCameraEnhancements.HarmonyPatches
{
    [HarmonyPatch(typeof(PlayerFaceTracking), nameof(PlayerFaceTracking.Update))]
    public static class PlayerFaceTracking_Update_PrefixPatch
    {
        static FieldInfo cameraField = AccessTools.Field(typeof(PlayerFaceTracking), "_camera");
        static FieldInfo isTrackingField = AccessTools.Field(typeof(PlayerFaceTracking), "_isTracking");

        static bool Prefix(PlayerFaceTracking __instance)
        {
            bool isTracking = (bool)isTrackingField.GetValue(__instance);
            if (!isTracking)
                return false; // skip original if not tracking

            // Get the _camera Transform from the instance (private field)
            Transform cameraTransform = cameraField.GetValue(__instance) as Transform;
            if (cameraTransform == null)
                return false; // skip original to avoid errors

            // Original target position
            Vector3 defaultTarget = BetterBetterCharacterController.Instance.RotationPivot.position;

            // Use mod settings and bones
            if (!PortableCameraEnhancements.UseNewLookTarget || PortableCameraEnhancements.Head == null)
            {
                // Just do original behavior (look at default target)
                RotateCameraTowards(cameraTransform, defaultTarget);
                return false; // skip original
            }

            // Calculate blend factor based on distance from original target to camera
            float distance = Vector3.Distance(cameraTransform.position, defaultTarget);

            // Scale with player height cause duh 1m for tiny people is not the same for big people
            float avatarHeight = PortableCameraEnhancements.GetAvatarHeight();
            float scale = avatarHeight / 1.75f;

            float min = PortableCameraEnhancements.BlendMinDistance * scale;
            float max = PortableCameraEnhancements.BlendMaxDistance * scale;

            // Clamp and normalize between min and max
            float t = Mathf.InverseLerp(min, max, distance); // 0 = close, 1 = far
            float blendFactor = Mathf.Clamp01(t);
            blendFactor = blendFactor * blendFactor * (3f - 2f * blendFactor); // SMOOOOTH


            Vector3 blendedTarget;

            if (PortableCameraEnhancements.AutoBlendTargets)
            {
                blendedTarget = Vector3.Lerp(defaultTarget, PortableCameraEnhancements.ModdedTarget, blendFactor);
                PortableCameraEnhancements.BlendedTarget = blendedTarget;
            }
            else
            {
                blendedTarget = PortableCameraEnhancements.ModdedTarget;
            }

            RotateCameraTowards(cameraTransform, blendedTarget);

            return false; // skip original Update()
        }

        private static void RotateCameraTowards(Transform cameraTransform, Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - cameraTransform.position).normalized;
            Vector3 upVector = BetterBetterCharacterController.Instance.GetUpVector();

            Quaternion targetRotation = Quaternion.LookRotation(direction, upVector);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, 10f * Time.deltaTime);
        }
    }

}