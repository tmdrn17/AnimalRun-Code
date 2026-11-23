using System.Collections;
using UnityEngine;

namespace ShaderGallery.Samples
{
    [RequireComponent(typeof(Renderer))]
    public class DissolveController : MonoBehaviour
    {
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");

        [SerializeField, Min(0.01f)] private float duration = 1.2f;
        [SerializeField] private bool destroyWhenDone = true;

        private Material materialInstance;
        private Coroutine running;

        private void Awake()
        {
            // Renderer.material creates a per-renderer material instance.
            // This prevents this object's dissolve animation from changing shared materials.
            materialInstance = GetComponent<Renderer>().material;

            // Always begin fully visible, regardless of the preview value saved in the material.
            materialInstance.SetFloat(ThresholdId, 0f);
        }

        /// <summary>Play the dissolve-out effect and optionally destroy this GameObject.</summary>
        public void DissolveOut()
        {
            Play(0f, 1f, destroyWhenDone);
        }

        /// <summary>Play the dissolve-in effect. This does not destroy the GameObject.</summary>
        public void DissolveIn()
        {
            Play(1f, 0f, false);
        }

        private void Play(float from, float to, bool destroyAtEnd)
        {
            if (!isActiveAndEnabled || materialInstance == null)
                return;

            if (running != null)
                StopCoroutine(running);

            running = StartCoroutine(Animate(from, to, destroyAtEnd));
        }

        private IEnumerator Animate(float from, float to, bool destroyAtEnd)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                float progress = elapsed / safeDuration;
                materialInstance.SetFloat(ThresholdId, Mathf.Lerp(from, to, progress));
                elapsed += Time.deltaTime;
                yield return null;
            }

            materialInstance.SetFloat(ThresholdId, to);
            running = null;

            if (destroyAtEnd)
                Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
        }

        private void OnDestroy()
        {
            if (materialInstance != null)
            {
                Destroy(materialInstance);
                materialInstance = null;
            }
        }
    }
}
