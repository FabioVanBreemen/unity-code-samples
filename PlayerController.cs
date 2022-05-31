using UnityEngine;
using Cinemachine;
using UnityEngine.InputSystem;
using System;
using System.Collections;
using Scripts.Utility;
using Scripts.Managers;
using Scripts.Enemy;
using System.Collections.Generic;

namespace Scripts.Player
{
    public class PlayerController : MonoBehaviour
    {
        public PlayerMoveState State;

        public static event Action OnPlayerFatigued;
        public static event Action<float> OnPlayerStaminaChanged;
        public static event Action<PlayerMoveState> OnPlayerMoveStateChanged;

        #region Scriptable Object
        [Header("Scriptable Object")]
        public PlayerScriptableObject playerSO;
        private float _playerWalkSpeed;
        private float _playerCrouchSpeed;
        private float _playerRunSpeed;
        private float _maxStamina;
        private float _runStaminaCost;
        private float _jumpStaminaCost;
        private float _fatigueTime;
        private float _jumpHeight;
        #endregion

        #region Properties
        [Header("Info")]
        public float totalWalkTime;
        public float totalRunTime;
        public float totalCrouchTime;
        public float crouchCount;
        public float jumpCount;
        public float currentStamina;
        public float playerSpeed;
        public bool isPerformingCrouch = false;
        public bool isRunning = false;
        public bool isPerformingRun = false;
        public bool isPlayerGrounded = true;
        private bool _isFatigued = false;
        private bool _hasJumpStarted = false;
        private bool _allowNoStaminaEvent = true;

        [Header("Components")]
        public CinemachineVirtualCamera virtualCamera;
        public Animator animator;
        private CharacterController _charController;

        [Header("Other")]
        private Transform _cameraTransform;
        private float _fatigueTimer = 0f;
        private Vector3 _move;
        private Vector3 _playerVelocity;
        private Vector3 _gravityValue;
        private Vector2 _movementInput;
        #endregion

        /// <summary>
        /// Assign Scriptable Object properties to this class's properties.
        /// </summary>
        private void AssignScriptableObjectProperties()
        {
            _playerWalkSpeed = playerSO.playerController.playerWalkSpeed;
            _playerCrouchSpeed = playerSO.playerController.playerCrouchSpeed;
            _playerRunSpeed = playerSO.playerController.playerRunSpeed;
            _maxStamina = playerSO.playerController.maxStamina;
            _runStaminaCost = playerSO.playerController.runStaminaCost;
            _jumpStaminaCost = playerSO.playerController.jumpStaminaCost;
            _fatigueTime = playerSO.playerController.fatigueTime;
            _jumpHeight = playerSO.playerController.jumpHeight;
        }

        private void Awake()
        {
            AssignScriptableObjectProperties();

            GameManager.Instance.PlayerControllerComponent = this;

            _charController = GetComponent<CharacterController>();
            _cameraTransform = virtualCamera.transform;
            _gravityValue = Physics.gravity;
            currentStamina = _maxStamina;
            playerSpeed = _playerWalkSpeed;

            OnPlayerStaminaChanged?.Invoke(currentStamina);
        }

        private void FixedUpdate()
        {
            /// Check isPlayerGrounded in 2 ways; Raycast and CharacterController.isGrounded
            if (Physics.Raycast(new Vector3(transform.position.x, transform.position.y + 0.1f, transform.position.z), -transform.up, 0.5f) || _charController.isGrounded)
            {
                isPlayerGrounded = true;
                return;
            }
            
            isPlayerGrounded = false;
        }

        private void Update()
        {
            ApplyGravity();
            CharacterAnimations();
            CharacterMovement();
            RefreshPlayerStamina();
            CheckIfStateChanged();
            UpdateMovementStats();
        }

        #region Inputs Performed
        private void Performed_Walk(InputAction.CallbackContext context)
        {
            _movementInput = context.action.ReadValue<Vector2>();

            UpdatePlayerMoveStateIfPossible(PlayerMoveState.Crouching);
        }

        private void Performed_Crouch(InputAction.CallbackContext context)
        {
            isPerformingCrouch = !isPerformingCrouch;

            if (!isPerformingCrouch) return;

            UpdatePlayerMoveStateIfPossible(PlayerMoveState.Crouching);
            crouchCount++;
        }

        private void Performed_Run(InputAction.CallbackContext context)
        {
            isPerformingRun = !isPerformingRun;

            if (!isPerformingRun) return;

            UpdatePlayerMoveStateIfPossible(PlayerMoveState.Running);
        }

        private void Performed_Jump(InputAction.CallbackContext context)
        {
            UpdatePlayerMoveStateIfPossible(PlayerMoveState.Jumping);
        }
        #endregion

        #region Move State
        private void CheckIfStateChanged()
        {
            UpdatePlayerMoveStateIfPossible(PlayerMoveState.Idle);
            UpdatePlayerMoveStateIfPossible(PlayerMoveState.Walking);
            UpdatePlayerMoveStateIfPossible(PlayerMoveState.Running);
            UpdatePlayerMoveStateIfPossible(PlayerMoveState.CrouchingIdle);
            UpdatePlayerMoveStateIfPossible(PlayerMoveState.Falling);
        }

        private void MoveState_Idle()
        {
            playerSpeed = _playerWalkSpeed;

            animator.SetBool("Crouched", false);
            animator.SetBool("Running", false);
        }

        private void MoveState_Walking()
        {
            playerSpeed = _playerWalkSpeed;

            animator.SetBool("Crouched", false);
            animator.SetBool("Running", false);
        }

        private void MoveState_Crouching()
        {
            playerSpeed = _playerCrouchSpeed;

            animator.SetBool("Crouched", true);
            animator.SetBool("Running", false);
        }

        private void MoveState_CrouchingIdle()
        {
            playerSpeed = _playerCrouchSpeed;

            animator.SetBool("Crouched", true);
            animator.SetBool("Running", false);
        }

        private void MoveState_Running()
        {
            isRunning = true;
            playerSpeed = _playerRunSpeed;

            animator.SetBool("Crouched", false);
            animator.SetBool("Running", true);
        }

        private void MoveState_Jumping()
        {
            _hasJumpStarted = true;
            _playerVelocity.y += Mathf.Sqrt(_jumpHeight * -3.0f * _gravityValue.y);
            StartCoroutine(JumpToFall());
            jumpCount++;
            currentStamina -= _jumpStaminaCost;
            OnPlayerStaminaChanged?.Invoke(currentStamina);

            animator.SetBool("Jumping", true);
        }

        private void MoveState_Falling() => animator.SetBool("Grounded", false);

        /// <summary>
        /// Sets the player move state to a new state if that state is reachable.<br></br>
        /// Example: only if a player is grounded, they can perform a jump.
        /// </summary>
        private void UpdatePlayerMoveStateIfPossible(PlayerMoveState newState)
        {
            if (newState == State) return;

            switch (newState)
            {
                case PlayerMoveState.Idle:

                    if (!isPlayerGrounded) return;
                    if (isPerformingCrouch && State == PlayerMoveState.CrouchingIdle) return;
                    if (_movementInput != new Vector2(0, 0)) return;

                    MoveState_Idle();
                    break;
                case PlayerMoveState.Walking:

                    if (!isPlayerGrounded) return;
                    if (isPerformingCrouch) return;
                    if (isRunning) return;
                    if (_movementInput == new Vector2(0, 0)) return;

                    MoveState_Walking();
                    break;
                case PlayerMoveState.Crouching:

                    if (!isPlayerGrounded) return;
                    if (!isPerformingCrouch) return;
                    if (_movementInput == new Vector2(0, 0)) return;

                    MoveState_Crouching();
                    break;
                case PlayerMoveState.CrouchingIdle:

                    if (!isPlayerGrounded) return;
                    if (!isPerformingCrouch) return;
                    if (_movementInput != new Vector2(0, 0)) return;

                    MoveState_CrouchingIdle();
                    break;
                case PlayerMoveState.Running:

                    if (!isPlayerGrounded) return;
                    if (!isPerformingRun) return;
                    if (_movementInput == new Vector2(0, 0)) return;
                    if (_movementInput.y <= 0) return;
                    if (_isFatigued) return;

                    MoveState_Running();
                    break;
                case PlayerMoveState.Jumping:

                    if (!isPlayerGrounded) return;
                    if (isPerformingCrouch) return;
                    if (_isFatigued) return;
                    if (currentStamina < _jumpStaminaCost) return;

                    MoveState_Jumping();
                    break;
                case PlayerMoveState.Falling:

                    if (isPlayerGrounded) return;

                    MoveState_Falling();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
            }

            State = newState;

            OnPlayerMoveStateChanged?.Invoke(newState);
        }
        #endregion

        #region Character Controlling
        /// <summary>
        /// Apply constant gravity to the player.
        /// </summary>
        private void ApplyGravity()
        {
            if (isPlayerGrounded && !_hasJumpStarted)
                _playerVelocity = new Vector3(0, -1, 0);

            if (isPlayerGrounded) return;
                
            _playerVelocity.y += _gravityValue.y * Time.deltaTime;
        }

        /// <summary>
        /// Update the character animations to match the current state.
        /// </summary>
        private void CharacterAnimations()
        {
            animator.SetFloat("Forward", _movementInput.y, 1f, Time.deltaTime * 10f);
            animator.SetFloat("Sideways", _movementInput.x, 1f, Time.deltaTime * 10f);
            
            if (!isPlayerGrounded) return;
            
            animator.SetBool("Grounded", true);
        }

        /// <summary>
        /// Move the player character around in the world.
        /// </summary>
        private void CharacterMovement()
        {
            // TODO add some sort of acceleration value to the charcontroller to reduce air control
            //_playerVelocity += new Vector3(_charController.velocity.x, _gravityValue.y, _charController.velocity.z) * Time.deltaTime;
            _move = new Vector3(_movementInput.x, 0, _movementInput.y);
            _move = new Vector3(-_cameraTransform.right.z, 0, _cameraTransform.right.x) * _move.z + _cameraTransform.right * _move.x;
            _move += _playerVelocity;
            _charController.Move(_move * Time.deltaTime * playerSpeed);
        }

        /// <summary>
        /// Refresh the stamina of the player. This includes regenerating if not running and depleting if running.
        /// </summary>
        private void RefreshPlayerStamina()
        {
            if (currentStamina <= 0f)
                _isFatigued = true;
            /// Invoke OnPlayerFatigued event
            if (_allowNoStaminaEvent && _isFatigued)
            {
                _allowNoStaminaEvent = false;

                OnPlayerFatigued?.Invoke();

                if (Gamepad.current != null) isPerformingRun = false;

                if (!isRunning) return;
                UpdatePlayerMoveStateIfPossible(PlayerMoveState.Walking);
                isRunning = false;
            }
            /// Regen stamina
            if (currentStamina < _maxStamina && !isRunning)
            {
                currentStamina += Time.deltaTime;
                OnPlayerStaminaChanged?.Invoke(currentStamina);
            }
            /// Update _fatigueTimer which delays run ability
            if (_isFatigued && _fatigueTimer <= _fatigueTime)
            {
                _fatigueTimer += Time.deltaTime;
            }
            
            if (_fatigueTimer >= _fatigueTime && !isRunning)
            {
                _fatigueTimer = 0f;
                _isFatigued = false;
                _allowNoStaminaEvent = true;
            }

            if (!isRunning) return;
            
            if (_movementInput.y <= 0 || !isPerformingRun)
            {
                isRunning = false;
                UpdatePlayerMoveStateIfPossible(PlayerMoveState.Walking);
                return;
            }

            currentStamina -= (Time.deltaTime * _runStaminaCost);
            OnPlayerStaminaChanged?.Invoke(currentStamina);
        }

        /// <summary>
        /// Update the statistics of the player such as total run & crouch times.
        /// </summary>
        private void UpdateMovementStats()
        {
            if (isRunning)
            {
                totalRunTime += Time.deltaTime;
                return;
            }

            if (isPerformingCrouch)
            {
                totalCrouchTime += Time.deltaTime;
                return;
            }

            if (_movementInput == new Vector2(0, 0) || !isPlayerGrounded) return;
            totalWalkTime += Time.deltaTime;
        }

        /// <summary>
        /// Move the player character up in the air.
        /// </summary>
        private void PlayerJump() => _playerVelocity.y += Mathf.Sqrt(_jumpHeight * -3.0f * _gravityValue.y);

        /// <summary>
        /// Add a delay to wait for isPlayerGrounded to turn false right after jumping.
        /// </summary>
        private IEnumerator JumpToFall()
        {
            yield return new WaitForSeconds(0.5f);
            
            _hasJumpStarted = false;
        }
        #endregion

        private void OnEnable()
        {
            #region Controls
            InputManager.Instance.Controls.Player.Move.performed += Performed_Walk;
            InputManager.Instance.Controls.Player.Move.canceled += Performed_Walk;
            InputManager.Instance.Controls.Player.Crouch.performed += Performed_Crouch;
            InputManager.Instance.Controls.Player.Crouch.canceled += Performed_Crouch;
            InputManager.Instance.Controls.Player.Run.performed += Performed_Run;
            InputManager.Instance.Controls.Player.Run.canceled += Performed_Run;
            InputManager.Instance.Controls.Player.Jump.started += Performed_Jump;
            #endregion

            AnimationEventController.OnJump += PlayerJump;
            EnemyLogic.OnEnemyAttackStarted += ctx => InputManager.Instance.DisableAllControls(true);
            EnemyLogic.OnEnemyAttackEnded += ctx => InputManager.Instance.DisableAllControls(false);
        }

        private void OnDisable()
        {
            #region Controls
            InputManager.Instance.Controls.Player.Move.performed -= Performed_Walk;
            InputManager.Instance.Controls.Player.Move.canceled -= Performed_Walk;
            InputManager.Instance.Controls.Player.Crouch.performed -= Performed_Crouch;
            InputManager.Instance.Controls.Player.Crouch.canceled -= Performed_Crouch;
            InputManager.Instance.Controls.Player.Run.performed -= Performed_Run;
            InputManager.Instance.Controls.Player.Run.canceled -= Performed_Run;
            InputManager.Instance.Controls.Player.Jump.started -= Performed_Jump;
            #endregion

            GameManager.Instance.ChangeGameStats(GetGameStatsAsDictionary());

            AnimationEventController.OnJump -= PlayerJump;
            EnemyLogic.OnEnemyAttackStarted -= ctx => InputManager.Instance.DisableAllControls(true);
            EnemyLogic.OnEnemyAttackEnded -= ctx => InputManager.Instance.DisableAllControls(false);
        }

        /// <summary>
        /// Put all PlayerController statistics in a Dictionary.
        /// </summary>
        private Dictionary<string, float> GetGameStatsAsDictionary()
        {
            Dictionary<string, float> gameStats = new();
            gameStats.Add("Walk Time", totalWalkTime);
            gameStats.Add("Run Time", totalRunTime);
            gameStats.Add("Crouch Time", totalCrouchTime);
            gameStats.Add("Crouch Count", crouchCount);
            gameStats.Add("Jump Count", jumpCount);

            return gameStats;
        }
    }

    public enum PlayerMoveState
    {
        Idle,
        Walking,
        Crouching,
        CrouchingIdle,
        Running,
        Jumping,
        Falling
    }
}