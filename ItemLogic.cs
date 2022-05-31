using Scripts.Managers;
using Scripts.Utility;
using System;
using System.Collections;
using UnityEngine;

public interface IItemLogic
{
    public bool IsEquipped { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsEquipable { get; set; }
    public bool IsThrowable { get; set; }
    public void SetItemEquipped(bool equipped);
    public ItemScriptableObject ItemSO { get; }
}

namespace Scripts.Interactables
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class ItemLogic : InteractableLogic, IItemLogic
    {
        #region Scriptable Object
        [Header("Scriptable Object")]
        public ItemScriptableObject itemSO;
        private bool _isEquipable;
        private bool _isThrowable;
        private AudioClip _interactStartClip;
        private AudioClip _interactEndClip;
        private AudioClip _dropClip;
        private AudioClip _whileEnabledClip;
        #endregion

        #region Properties
        bool IItemLogic.IsEquipped { get => isEquipped; set { isEquipped = value; rigidbodyComponent.isKinematic = value; } }
        bool IItemLogic.IsEnabled { get => isEnabled; set => isEnabled = value; }
        bool IItemLogic.IsEquipable { get => _isEquipable; set => _isEquipable = value; }
        bool IItemLogic.IsThrowable { get => _isThrowable; set => _isThrowable = value; }
        ItemScriptableObject IItemLogic.ItemSO { get => itemSO; }

        [Header("Info - Do not edit!")]
        public bool isEquipped = false;
        public bool isEnabled = false;
        [SerializeField] private float _rbodyMagnitude = 0f;

        [Header("Settings")]
        private float _collisionMultiplier = 0f;

        [Header("GameObjects & Components")]
        public GameObject graphics;
        public GameObject soundCollider;
        [Tooltip("Sound source for main sounds like drop & toggle.")]
        public AudioSource mainAudioSource;
        [Tooltip("Sound to play as long as the item is enabled.")]
        public AudioSource whileEnabledAudioSource;
        [NonSerialized] public Rigidbody rigidbodyComponent;

        private IEnumerator _checkVelocity;
        #endregion

        /// <summary>
        /// Assign Scriptable Object properties to this class's properties.
        /// </summary>
        private void AssignScriptableObjectProperties()
        {
            _isEquipable = itemSO.isEquipable;
            _isThrowable = itemSO.isThrowable;
            _interactStartClip = itemSO.interactStartClip;
            _interactEndClip = itemSO.interactEndClip;
            _dropClip = itemSO.dropClip;
            _whileEnabledClip = itemSO.whileEnabledClip;
        }

        protected virtual void Awake() 
        {
            AssignScriptableObjectProperties();
            rigidbodyComponent = GetComponent<Rigidbody>();
            _checkVelocity = CheckVelocity();

            if (whileEnabledAudioSource == null) return;
            whileEnabledAudioSource.clip = _whileEnabledClip;
        }

        /// <summary>
        /// Player interaction override from InteractableLogic. Passes the InteractionMethod that was used.
        /// </summary>
        public override void InteractionPerformed(InteractionMethod interaction)
        {
            base.InteractionPerformed(interaction);
            if (!isInteractable) return;

            if (interaction != InteractionMethod.Click && (interaction != InteractionMethod.ClickAlternate || !isEquipped)) return;

            PerformInteraction();
        }

        /// <summary>
        /// Execute interaction performed logic. Called when correct InteractionMethod was used in InteractionPerformed().
        /// </summary>
        protected virtual void PerformInteraction()
        {
            if (_interactStartClip != null)
                AudioManager.Instance.PlaySound(mainAudioSource, GetInteractionClip(), 0.9f, 1.1f);
            
            PlayOrStopContinuousSound();

            ActivateSoundColliderTimed(soundCollider, SoundTriggerManager.Instance.soundTriggerSO.items.toggle);
                        
            isEnabled = !isEnabled;
        }

        /// <summary>
        /// Return the correct interaction clip that needs to be played.
        /// </summary>
        private AudioClip GetInteractionClip()
        {
            if (_interactEndClip == null) return _interactStartClip;

            if (isEnabled) return _interactEndClip;

            return _interactStartClip;
        }

        /// <summary>
        /// Play or stop the continous sound Audio Source.
        /// </summary>
        private void PlayOrStopContinuousSound()
        {
            if (whileEnabledAudioSource == null || whileEnabledAudioSource.clip == null) return;

            if (isEnabled)
            {
                AudioManager.Instance.StopSound(whileEnabledAudioSource);
                return;
            }

            AudioManager.Instance.PlaySound(whileEnabledAudioSource);
        }

        /// <summary>
        /// Set item status to equipped and disable the highlight if active.
        /// </summary>
        public void SetItemEquipped(bool equipped)
        {
            isEquipped = equipped;

            if (!isEquipped || graphics.layer != LayerMask.NameToLayer("Highlighted")) return;

            UtilityFunctions.SetGameObjectAndChildLayers(graphics, LayerMask.NameToLayer("Item Graphics"));
        }

        #region Item Collisions
        /// <summary>
        /// An object has entered the trigger. Check Rigidbody magnitude to find velocity of impact.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (isEquipped) return;

            StartCoroutine(_checkVelocity);
        }

        /// <summary>
        /// The graphics of the item has now collided with another collider.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            StopCoroutine(_checkVelocity);
            ItemCollided();
        }

        /// <summary>
        /// Calculate _collisionMultiplier by using Rigidbody magnitude and use for Audio Source volume and noise collider size.
        /// </summary>
        private void ItemCollided()
        {
            _collisionMultiplier = Mathf.Min(Mathf.Pow(_rbodyMagnitude / 10, 1.5f), 1);
            mainAudioSource.volume = _collisionMultiplier;

            AudioManager.Instance.PlaySound(mainAudioSource, _dropClip, 0.9f, 1.1f);

            ActivateItemDroppedCollider();
        }

        /// <summary>
        /// Quickly check the current velocity of the object to find the Rigidbody magnitude at the point of colliding.
        /// </summary>
        private IEnumerator CheckVelocity()
        {
            while (true)
            {
                _rbodyMagnitude = rigidbodyComponent.velocity.magnitude;

                if (_rbodyMagnitude <= 0) break;

                yield return new WaitForSeconds(0.05f);
            }
        }
        #endregion

        /// <summary>
        /// Activate the sound collider.
        /// </summary>
        private void ActivateItemDroppedCollider() => SoundTriggerManager.Instance.SetSoundColliderSize(soundCollider, SoundTriggerManager.Instance.soundTriggerSO.items.drop * _collisionMultiplier);
    }
}