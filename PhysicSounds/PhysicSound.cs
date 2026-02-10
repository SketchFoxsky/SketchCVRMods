using UnityEngine;

namespace Sketch.PhysicSounds
{
    [AddComponentMenu("SketchFoxsky/ModdedCCK/PhysicSounds")]

    public class PhysicSound : MonoBehaviour
    {
        public AudioSource AudioSourceReference;
        public bool UseRandomPitch;
        public AudioClip[] MinCollisionAudio;
        public AudioClip[] MiddleCollisionAudio;
        public AudioClip[] MaxCollisionAudio;
        private Vector2 Pitch = new Vector2(0.9f, 1.25f);

        private void OnCollisionEnter(Collision collision)
        {
            if (AudioSourceReference == null)
                return;

            var ColMag = collision.relativeVelocity.magnitude;

            if (UseRandomPitch)
                AudioSourceReference.pitch = (UnityEngine.Random.Range(Pitch.x, Pitch.y));

            if ((ColMag >= 0.1f) && (ColMag <= 4.9))
            {
                var clip = MinCollisionAudio[UnityEngine.Random.Range(0, MinCollisionAudio.Length)];
                //Debug.Log("Small");
                AudioSourceReference.PlayOneShot(clip);
            }
            if ((ColMag >= 5f) && (ColMag <= 10))
            {
                var clip = MiddleCollisionAudio[UnityEngine.Random.Range(0, MinCollisionAudio.Length)];
                //Debug.Log("Mid");
                AudioSourceReference.PlayOneShot(clip);
            }
            if (ColMag >= 10.01f)
            {
                var clip = MaxCollisionAudio[UnityEngine.Random.Range(0, MinCollisionAudio.Length)];
                //Debug.Log("Large");
                AudioSourceReference.PlayOneShot(clip);
            }
        }
    }
}

