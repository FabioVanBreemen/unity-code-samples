using System.Collections;
using UnityEngine;
using Cinemachine;
using Scripts.Managers;
using Scripts.Utility;
using System;
using UnityEngine.Animations.Rigging;
using Scripts.Interactables;
using UnityEngine.InputSystem;

namespace Scripts.Player
{
    public class PlayerLogic : MonoBehaviour
    {
        #region Action Events
        public static event Action<int> OnPlayerHealthChanged;
        public static event Action<string> OnHoverInteractableStart;
        public static event Action OnHoverInteractableEnd;
        public static event Action<int> OnHotbarScroll;
        public static event Action<ItemScriptableObject, int> OnPickupItem;
        public static event Action<int> OnRemoveItem;
        public static event Action<KeyScriptableObject> OnPickupKey;
        public static event Action<NoteScriptableObject> OnPickupNote;
        public static event Action<CollectibleScriptableObject> OnPickupCollectible;
        #endregion

        #region Scriptable Object
        [Header("Scriptable Object")]
        public PlayerScriptableObject playerSO;
        private int _playerHealth;
        private float _interactRange;
        private LayerMask _interactableLayerMask;
        #endregion

        #region Properties
        [Header("Info")]
        public int footstepCountLeft;
        public int footstepCountRight;
        public int PlayerHealth { get => _playerHealth; set => SetPlayerHealth(value); }

        [Header("Checks")]
        private bool _allowFootstepLeft = true;
        private bool _allowFootstepRight = true;
        private bool _allowHotbarSwapping = true;
        private bool _allowEquiping = true;
        private bool _isInteractableInFocus = false;

        [Header("Components")]
        [SerializeField] private AnimationEventController _animationController;
        [NonSerialized] public CinemachinePOV povCam;
        private PlayerController _playerController;
        private IItemLogic _currentInHandItemInterface;

        [Header("Sound Colliders")]
        [SerializeField] private GameObject _playerCollider;

        [Header("GameObjects")]
        public GameObject currentInHandItem;
        [SerializeField] private GameObject _playerHand;
        [SerializeField] private Rig _playerHandAimRig;
        private GameObject _cameraObject;

        [Header("Audio")]
        public AudioSource shortClipSource;
        [SerializeField] private AudioSource _footstepLeftSource;
        [SerializeField] private AudioSource _footstepRightSource;

        [Header("Other")]
        private IEnumerator _healthRegenCoroutine;
        private int _currentHotbarIndex = 0;
        #endregion

        /// <summary>
        /// Assign Scriptable Object properties to this class's properties.
        /// </summary>
        private void AssignScriptableObjectProperties()
        {
            _playerHealth = playerSO.playerLogic.maxHealth;
            _interactRange = playerSO.playerLogic.interactRange;
            _interactableLayerMask = playerSO.playerLogic.interactableLayerMask;
        }

        private void Awake()
        {
            AssignScriptableObjectProperties();

            GameManager.Instance.Player = gameObject;
            GameManager.Instance.PlayerLogicComponent = this;

            _playerController = GetComponent<PlayerController>();

            _cameraObject = _playerController.virtualCamera.gameObject;
            povCam = _playerController.virtualCamera.GetCinemachineComponent<CinemachinePOV>();

            InputManager.Instance.SetCameraSpeed(InputManager.Instance.inputSO.cameraSpeed);

            _healthRegenCoroutine = RegeneratePlayerHealth();
            OnPlayerHealthChanged?.Invoke(_playerHealth);
        }

        private void FixedUpdate() => CheckIfPlayerLookingAtInteractable();

        #region Inputs Performed
        private void Performed_Look(InputAction.CallbackContext context) => CheckIfPlayerLookingAtInteractable();

        private void Performed_ClickInteraction(InputAction.CallbackContext context)
        {
            if (!InteractionRaycastCheck(out RaycastHit hit)) return;
                
            GetInteractableInterface(hit.transform.gameObject).InteractionPerformed(InteractionMethod.Click);
            InputManager.Instance.PlayHaptics_TapRegular();
        }

        private void Performed_AlternateClickInteraction(InputAction.CallbackContext context)
        {
            if (currentInHandItem == null) return;

            GetInteractableInterface(currentInHandItem).InteractionPerformed(InteractionMethod.ClickAlternate);
            InputManager.Instance.PlayHaptics_TapRegular();
        }

        private void Performed_Equip(InputAction.CallbackContext context)
        {
            if (!InteractionRaycastCheck(out RaycastHit hit)) return;
            if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Interactable Item")) return;

            IItemLogic itemLogicInterface = hit.collider.transform.parent.gameObject.GetComponentInParent<IItemLogic>();
            if (!itemLogicInterface.IsEquipable || !_allowEquiping) return;

            EquipItem(hit.transform.gameObject);
            InputManager.Instance.PlayHaptics_TapLowFreq();
        }

        private void Performed_Drop(InputAction.CallbackContext context)
        {
            if (currentInHandItem == null) return;

            DropItem(currentInHandItem);
            InputManager.Instance.PlayHaptics_TapLowFreq();
        }

        private void Performed_Throw(InputAction.CallbackContext context)
        {
            if (currentInHandItem == null) return;
            if (!_currentInHandItemInterface.IsThrowable) return;
            
            ThrowItem(currentInHandItem);
            InputManager.Instance.PlayHaptics_TapLowFreq();
        }

        private void Performed_Scroll(InputAction.CallbackContext context)
        {
            float scrollWheelValue = context.ReadValue<float>();
            if (scrollWheelValue == 0 || !_allowHotbarSwapping) return;
            
            ScrollThroughHotbar(scrollWheelValue);
            InputManager.Instance.PlayHaptics_LongTapLowFreq();
        }

        private void Performed_HotbarSelect(InputAction.CallbackContext context)
        {
            float newHotbarIndex = context.ReadValue<float>();
            if (!_allowHotbarSwapping) return;

            SetHotbarSelection(newHotbarIndex);
        }
        #endregion

        #region Interaction Checks
        /// <summary>
        /// General Raycast check.
        /// </summary>
        /// <returns>
        /// Did the Raycast hit?
        /// </returns>
        private bool InteractionRaycastCheck(out RaycastHit rayCastHit)
        {
            if (Physics.Raycast(_cameraObject.transform.position, _cameraObject.transform.forward, out RaycastHit hit, _interactRange, _interactableLayerMask) && 
                (hit.collider.gameObject.layer == LayerMask.NameToLayer("Interactable Item") || hit.collider.gameObject.layer == LayerMask.NameToLayer("Interactable Static")))
            {
                rayCastHit = hit;
                return true;
            }

            rayCastHit = hit;
            return false;
        }

        /// <summary>
        /// Checks if the player is looking at an interactable object. If true, invokes the OnHoverInteractableStart event.
        /// </summary>
        private void CheckIfPlayerLookingAtInteractable()
        {
            if (InteractionRaycastCheck(out RaycastHit hit))
            {
                if (_isInteractableInFocus) return;

                OnHoverInteractableStart?.Invoke(hit.collider.gameObject.tag);
                _isInteractableInFocus = true;
                return;
            }

            if (!_isInteractableInFocus) return;

            OnHoverInteractableEnd?.Invoke();
            _isInteractableInFocus = false;
        }

        /// <summary>
        /// Returns given GameObject's IInteractable interface.
        /// </summary>
        private IInteractable GetInteractableInterface(GameObject gameObject)
        {
            IInteractable interactableInterface = gameObject.GetComponentInParent<IInteractable>();
            if (interactableInterface == null) throw new NullReferenceException("Interactable interface not found!");

            return interactableInterface;
        }
        #endregion

        #region Item Handling
        #region Current Item
        /// <summary>
        /// Finds and sets the current tool if there is one and sets the _currentInHandItemInterface parameter.
        /// </summary>
        private void SetCurrentItemProperties()
        {
            if (GetHotbarIndexTransform(_currentHotbarIndex).childCount != 0)
            {
                currentInHandItem = GetHotbarIndexTransform(_currentHotbarIndex).transform.GetChild(0).gameObject;
                _currentInHandItemInterface = currentInHandItem.GetComponent<IItemLogic>();
                _currentInHandItemInterface.SetItemEquipped(true);
                return;
            }

            currentInHandItem = null;
            _currentInHandItemInterface = null;
        }

        /// <summary>
        /// Finds the Transform object within the hand GameObject by given number value.
        /// </summary>
        /// <returns>Hand slot object Transform.</returns>
        private Transform GetHotbarIndexTransform(int index) { return _playerHand.transform.Find(index.ToString()); }
        #endregion

        #region Equip Item
        /// <summary>
        /// Equip item to current empty hand slot or to the next available slot. Ignores if hands are full.
        /// </summary>
        private void EquipItem(GameObject item)
        {
            if (GetHotbarIndexTransform(_currentHotbarIndex).childCount == 0)
            {
                EquipToHotbarIndex(_currentHotbarIndex, item);
                StartCoroutine(SmoothlyChangeRigWeight(1));
                return;
            }

            for (int i = _currentHotbarIndex; i <= _playerHand.transform.childCount; i++)
            {
                if (i == _playerHand.transform.childCount) i = 0;

                if (GetHotbarIndexTransform(i).childCount == 0)
                {
                    EquipToHotbarIndex(i, item);
                    return;
                }

                if (i == (_currentHotbarIndex - 1) || (_currentHotbarIndex == 0 && i == 3)) return;
            }
        }

        /// <summary>
        /// Extension of EquipItem. This part equips the item to the actual hand by setting the parent and transform.
        /// </summary>
        private void EquipToHotbarIndex(int hotbarIndex, GameObject item)
        {
            OnPickupItem?.Invoke(item.GetComponent<IItemLogic>().ItemSO, hotbarIndex);

            AudioManager.Instance.PlaySound(shortClipSource, AudioManager.Instance.audioSO.player.itemPickup);

            item.transform.SetParent(GetHotbarIndexTransform(hotbarIndex));
            item.transform.SetPositionAndRotation(item.transform.parent.transform.position, item.transform.parent.transform.rotation);

            SetItemTransformAndRigidbody(item, false, Vector3.zero, Quaternion.Euler(Vector3.zero));
            SetCurrentItemProperties();
        }
        #endregion

        /// <summary>
        /// Drops the currently active item.
        /// </summary>
        private void DropItem(GameObject item)
        {
            AudioManager.Instance.PlaySound(shortClipSource, AudioManager.Instance.audioSO.player.itemDrop);

            DetachItemFromPlayer(false, item);
            SetItemTransformAndRigidbody(item, true, _playerHand.transform.position, _playerHand.transform.rotation, 200);
        }

        /// <summary>
        /// Throws the currently active item.
        /// </summary>
        private void ThrowItem(GameObject item)
        {
            AudioManager.Instance.PlaySound(shortClipSource, AudioManager.Instance.audioSO.player.itemThrow);

            DetachItemFromPlayer(false, item);
            SetItemTransformAndRigidbody(item, true, _playerHand.transform.position, _playerHand.transform.rotation, 500);
        }

        /// <summary>
        /// Removes (all) items from the player's hotbar. Used for throwing and dropping.
        /// </summary>
        private void DetachItemFromPlayer(bool removeAll, GameObject item = null)
        {
            _allowEquiping = false;

            RemoveOneOrAll(removeAll, item);

            StartCoroutine(SmoothlyChangeRigWeight(0));
            _allowEquiping = true;
        }

        /// <summary>
        /// Removes one or all items from the hotbar.
        /// </summary>
        private void RemoveOneOrAll(bool removeAll, GameObject item)
        {
            if (removeAll)
            {
                for (int i = 0; i < _playerHand.transform.childCount; i++)
                {
                    if (GetHotbarIndexTransform(i).childCount == 0) return;

                    GameObject localItem = _playerHand.transform.GetChild(i).GetChild(0).gameObject;
                    SetItemTransformAndRigidbody(localItem, true, _playerHand.transform.position, _playerHand.transform.rotation);
                    _playerHand.transform.GetChild(i).SetParent(transform.parent);
                    OnRemoveItem?.Invoke(i);
                }
                return;
            }

            _currentInHandItemInterface.SetItemEquipped(false);
            GetHotbarIndexTransform(_currentHotbarIndex).GetChild(0).SetParent(transform.parent);
            SetItemTransformAndRigidbody(item, true, _playerHand.transform.position, _playerHand.transform.rotation);
            SetCurrentItemProperties();
            OnRemoveItem?.Invoke(_currentHotbarIndex);
        }

        /// <summary>
        /// Sets the properties of an item. Choose to set transform and/or apply forces.
        /// </summary>
        private void SetItemTransformAndRigidbody(GameObject item, bool setTransform, Vector3 newPos, Quaternion newRot, int force = 0)
        {
            Rigidbody itemRbody = item.GetComponentInChildren<Rigidbody>();
            itemRbody.isKinematic = true;

            if (setTransform)
                item.transform.SetPositionAndRotation(newPos, newRot);

            if (force == 0) return;

            itemRbody.isKinematic = false;
            itemRbody.AddForce(item.transform.forward * force);
        }

        #region Item Scrolling
        /// <summary>
        /// Scroll through all hotbar slot items and show the correct one.
        /// </summary>
        private void ScrollThroughHotbar(float scrollValue)
        {
            int previousSelectionIndex = _currentHotbarIndex;
            SetSelectionIndex(scrollValue);
            StartCoroutine(HotbarItemSwap(previousSelectionIndex));
            OnHotbarScroll?.Invoke(_currentHotbarIndex);
        }

        /// <summary>
        /// Set hotbar selection to pressed key's number.
        /// </summary>
        private void SetHotbarSelection(float newHotbarIndex)
        {
            int previousSelectionIndex = _currentHotbarIndex;
            if (_currentHotbarIndex == newHotbarIndex - 1) return;

            _currentHotbarIndex = (int)newHotbarIndex - 1;
            StartCoroutine(HotbarItemSwap(previousSelectionIndex));
            OnHotbarScroll?.Invoke(_currentHotbarIndex);
        }

        /// <summary>
        /// Increase or decrease the _currentHotbarIndex according to the scroll value.
        /// </summary>
        private void SetSelectionIndex(float scrollValue)
        {
            if (scrollValue < 0)
            {
                _currentHotbarIndex++;

                if (_currentHotbarIndex <= (_playerHand.transform.childCount - 1)) return;

                _currentHotbarIndex = 0;
                return;
            }

            _currentHotbarIndex--;

            if (_currentHotbarIndex >= 0) return;

            _currentHotbarIndex = _playerHand.transform.childCount - 1;
        }

        /// <summary>
        /// Change tools with motion and a delay. Plays sounds for empty / not empty hands.
        /// </summary>
        private IEnumerator HotbarItemSwap(int prevSelection)
        {
            _allowHotbarSwapping = false;

            PlayHotbarSwapSound();
            SetCurrentItemProperties();

            yield return SmoothlyChangeRigWeight(0, true);

            _playerHand.transform.GetChild(_currentHotbarIndex).gameObject.SetActive(true);
            _playerHand.transform.GetChild(prevSelection).gameObject.SetActive(false);

            if (GetHotbarIndexTransform(_currentHotbarIndex).childCount != 0)
                yield return SmoothlyChangeRigWeight(1);
            
            if (GetHotbarIndexTransform(_currentHotbarIndex).childCount == 0)
                yield return new WaitForSeconds(0.05f);

            _allowHotbarSwapping = true;
        }

        /// <summary>
        /// Plays regular or empty hand sound.
        /// </summary>
        private void PlayHotbarSwapSound()
        {
            if (_playerHand.transform.GetChild(_currentHotbarIndex).transform.childCount != 0)
            {
                AudioManager.Instance.PlaySound(shortClipSource, AudioManager.Instance.audioSO.player.scrollInventory);
                return;
            }
            
            AudioManager.Instance.PlaySound(shortClipSource, AudioManager.Instance.audioSO.player.scrollInventoryEmpty);
        }

        /// <summary>
        /// Moves the player hand up(1) / down(0).
        /// </summary>
        private IEnumerator SmoothlyChangeRigWeight(float newValue, bool disallowHotbarSwap = false)
        {
            _allowHotbarSwapping = false;
            float steps = 10;
            int newChange = 1;

            if (newValue == 0) newChange = -1;

            while (_playerHandAimRig.weight != newValue)
            {
                _playerHandAimRig.weight += (newChange / steps);
                yield return new WaitForSeconds(0.01f);
            }

            if (disallowHotbarSwap) yield break;
            _allowHotbarSwapping = true;
        }
        #endregion
        #endregion

        #region Player Movement
        /// <summary>
        /// Player ran out of stamina.
        /// </summary>
        private void PlayerFatigued() => AudioManager.Instance.PlaySound(shortClipSource, AudioManager.Instance.audioSO.player.staminaDepleted);

        /// <summary>
        /// PlayerController's move state changed. Set sound collider sizes.
        /// </summary>
        private void PlayerMoveStateChanged(PlayerMoveState state)
        {
            switch (state)
            {
                case PlayerMoveState.Idle:
                    SoundTriggerManager.Instance.SetSoundColliderSize(_playerCollider, SoundTriggerManager.Instance.soundTriggerSO.player.idle, false);
                    break;
                case PlayerMoveState.Walking:
                    SoundTriggerManager.Instance.SetSoundColliderSize(_playerCollider, SoundTriggerManager.Instance.soundTriggerSO.player.walking, false);
                    break;
                case PlayerMoveState.Crouching:
                    SoundTriggerManager.Instance.SetSoundColliderSize(_playerCollider, SoundTriggerManager.Instance.soundTriggerSO.player.crouching, false);
                    break;
                case PlayerMoveState.CrouchingIdle:
                    SoundTriggerManager.Instance.SetSoundColliderSize(_playerCollider, SoundTriggerManager.Instance.soundTriggerSO.player.crouchingIdle, false);
                    break;
                case PlayerMoveState.Running:
                    SoundTriggerManager.Instance.SetSoundColliderSize(_playerCollider, SoundTriggerManager.Instance.soundTriggerSO.player.running, false);
                    break;
                case PlayerMoveState.Jumping:
                    SoundTriggerManager.Instance.SetSoundColliderSize(_playerCollider, SoundTriggerManager.Instance.soundTriggerSO.player.jumping, false);
                    break;
                case PlayerMoveState.Falling:
                    SoundTriggerManager.Instance.SetSoundColliderSize(_playerCollider, SoundTriggerManager.Instance.soundTriggerSO.player.falling, false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        /// <summary>
        /// Character foot touched the ground. Add +1 to stats and play a sound.
        /// </summary>
        private void PlayerFootStepped(FootPosition foot, int instanceId)
        {
            if (instanceId != _animationController.GetInstanceID()) return;
            if (!_playerController.isPlayerGrounded) return;

            if (foot == FootPosition.Left && _allowFootstepLeft)
            {
                _allowFootstepLeft = false;
                AudioManager.Instance.PlayFootstepSound(_footstepLeftSource);
                footstepCountLeft++;
            }
            
            if (foot == FootPosition.Right && _allowFootstepRight)
            {
                _allowFootstepRight = false;
                AudioManager.Instance.PlayFootstepSound(_footstepRightSource);
                footstepCountRight++;
            }
        }

        /// <summary>
        /// Character foot was lifted up from the ground. Allow footstep to be played again once it hits the ground.
        /// </summary>
        private void PlayerFootLifted(FootPosition foot, int instanceId)
        {
            if (instanceId != _animationController.GetInstanceID()) return;
            if (foot == FootPosition.Left)
            {
                _allowFootstepLeft = true;
                return;
            }
            
            _allowFootstepRight = true;
        }
        #endregion

        #region Health & Inventory
        /// <summary>
        /// Player health changed. Invoke OnPlayerHealthChanged event and start regeneration coroutine.
        /// </summary>
        private void SetPlayerHealth(int newHealth)
        {
            _playerHealth = Math.Clamp(newHealth, 0, 100);
            OnPlayerHealthChanged?.Invoke(_playerHealth);

            StopCoroutine(_healthRegenCoroutine);

            if (_playerHealth == 0) return;

            StartCoroutine(_healthRegenCoroutine);
        }

        /// <summary>
        /// Slowly regain health.
        /// </summary>
        private IEnumerator RegeneratePlayerHealth()
        {
            yield return new WaitForSeconds(10);

            while (_playerHealth < 100)
            {
                _playerHealth += 1;
                OnPlayerHealthChanged?.Invoke(_playerHealth);
                yield return new WaitForSeconds(0.5f);
            }

            _playerHealth = 100;
        }

        /// <summary>
        /// Invoke OnPickupKey event.
        /// </summary>
        public void AddKeyToInventory(KeyScriptableObject keySO) => OnPickupKey?.Invoke(keySO);

        /// <summary>
        /// Invoke OnPickupNote event.
        /// </summary>
        public void AddNoteToInventory(NoteScriptableObject noteSO) => OnPickupNote?.Invoke(noteSO);

        /// <summary>
        /// Invoke OnPickupCollectible event.
        /// </summary>
        public void AddCollectibleToInventory(CollectibleScriptableObject collectibleSO) => OnPickupCollectible?.Invoke(collectibleSO);
        #endregion

        private void OnEnable()
        {
            #region Controls
            InputManager.Instance.Controls.Player.Look.started += Performed_Look;
            InputManager.Instance.Controls.Player.Click_Interaction.started += Performed_ClickInteraction;
            InputManager.Instance.Controls.Player.Alt_Interaction.started += Performed_AlternateClickInteraction;
            InputManager.Instance.Controls.Player.Equip.started += Performed_Equip;
            InputManager.Instance.Controls.Player.Drop.started += Performed_Drop;
            InputManager.Instance.Controls.Player.Throw.started += Performed_Throw;
            InputManager.Instance.Controls.Player.Scroll.started += Performed_Scroll;
            InputManager.Instance.Controls.Player.Hotbar_Select.started += Performed_HotbarSelect;
            #endregion

            PlayerController.OnPlayerFatigued += PlayerFatigued;
            PlayerController.OnPlayerMoveStateChanged += PlayerMoveStateChanged;

            _animationController.OnFootStepped += PlayerFootStepped;
            _animationController.OnFootLifted += PlayerFootLifted;
        }

        private void OnDisable()
        {
            #region Controls
            InputManager.Instance.Controls.Player.Look.started -= Performed_Look;
            InputManager.Instance.Controls.Player.Click_Interaction.started -= Performed_ClickInteraction;
            InputManager.Instance.Controls.Player.Alt_Interaction.started -= Performed_AlternateClickInteraction;
            InputManager.Instance.Controls.Player.Equip.started -= Performed_Equip;
            InputManager.Instance.Controls.Player.Drop.started -= Performed_Drop;
            InputManager.Instance.Controls.Player.Throw.started -= Performed_Throw;
            InputManager.Instance.Controls.Player.Scroll.started -= Performed_Scroll;
            InputManager.Instance.Controls.Player.Hotbar_Select.started -= Performed_HotbarSelect;
            #endregion

            PlayerController.OnPlayerFatigued -= PlayerFatigued;
            PlayerController.OnPlayerMoveStateChanged -= PlayerMoveStateChanged;

            _animationController.OnFootStepped -= PlayerFootStepped;
            _animationController.OnFootLifted -= PlayerFootLifted;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(new Vector3(transform.position.x, transform.position.y + 1.7f, transform.position.z), _interactRange);
        }
    }
}