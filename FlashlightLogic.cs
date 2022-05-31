using Scripts.Managers;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scripts.Interactables
{
    public class FlashlightLogic : ItemLogic, IItemLogic
    {
        #region Properties
        [SerializeField] private Light _lightSource;
        [SerializeField] private LensFlareComponentSRP _lensFlare;

        private IEnumerator _flickeringCoroutine;
        #endregion

        protected override void Awake()
        {
            base.Awake();

            _flickeringCoroutine = Flickering();
            SetLightState(isEnabled);
        }

        /// <summary>
        /// Execute interaction performed logic. Called when correct InteractionMethod was used in InteractionPerformed().
        /// </summary>
        protected override void PerformInteraction()
        {
            base.PerformInteraction();
            SetLightState(isEnabled);
        }

        /// <summary>
        /// Enable or disable the Light Source & SRP Lens Flare.
        /// </summary>
        private void SetLightState(bool enabled)
        {
            _lightSource.enabled = enabled;
            _lensFlare.enabled = enabled;
        }

        /// <summary>
        /// Flickers the flashlight's Light Source.
        /// </summary>
        private IEnumerator Flickering()
        {
            while (true)
            {
                SetLightState(true);

                yield return new WaitForSeconds(Random.value);

                SetLightState(false);

                yield return new WaitForSeconds(Random.value / 4);
            }
        }

        /// <summary>
        /// Make the Light Source flicker.
        /// </summary>
        private void StartFlickering() => StartCoroutine(_flickeringCoroutine);

        /// <summary>
        /// Stop the Light Source from flickering.
        /// </summary>
        private void StopFlickering()
        {
            StopCoroutine(_flickeringCoroutine);

            SetLightState(isEnabled);
        }

        private void OnEnable()
        {
            GameManager.OnHorrorEventStarted += StartFlickering;
            GameManager.OnHorrorEventEnded += StopFlickering;
        }

        private void OnDisable()
        {
            GameManager.OnHorrorEventStarted -= StartFlickering;
            GameManager.OnHorrorEventEnded -= StopFlickering;
        }
    }
}