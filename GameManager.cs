using UnityEngine;
using System;
using Scripts.Player;
using Scripts.Enemy;
using UnityEngine.InputSystem;
using Scripts.Scriptables;
using System.Collections.Generic;

namespace Scripts.Managers
{
    public class GameManager : SingletonBuilder<GameManager>
    {
        public GameDifficulty Difficulty;
        public GameState State;

        #region Action Events
        public static event Action<GameDifficulty> OnDifficultyChanged;
        public static event Action<GameState> OnGameStateChanged;
        public static event Action<bool> OnPowerStateChanged;
        public static event Action OnHorrorEventStarted;
        public static event Action OnHorrorEventEnded;
        public static event Action<KeyScriptableObject> OnNewKeyPickedUp;
        public static event Action<NoteScriptableObject> OnNewNotePickedUp;
        public static event Action<CollectibleScriptableObject> OnNewCollectiblePickedUp;
        public static event Action OnAllCollectiblesFound;
        public static event Action<Dictionary<string, float>> OnGameStatisticsChanged;
        #endregion

        #region Properties
        private GameObject _player;
        public GameObject Player { get => _player; set => _player = value; }

        public PlayerLogic _playerLogicComponent;
        public PlayerLogic PlayerLogicComponent { get => _playerLogicComponent; set => _playerLogicComponent = value; }

        public PlayerController _playerControllerComponent;
        public PlayerController PlayerControllerComponent { get => _playerControllerComponent; set => _playerControllerComponent = value; }

        public Dictionary<string, KeyScriptableObject> collectedKeys = new();
        public Dictionary<string, NoteScriptableObject> collectedNotes = new();
        public Dictionary<string, float> gameStatistics = new();

        public bool isPowerOn = false;
        private float lastTimeScale = 1f;
        public int collectiblesCollected;
        public int totalCollectibles;
        public double startTime;
        private bool _isHorrorEventActive = false;
        #endregion

        #region Scriptable Object
        public DifficultyScriptableObject ActiveDifficultySO { get; private set; }
        private DifficultyScriptableObject normalDifficultySO;
        private DifficultyScriptableObject hardDifficultySO;
        private DifficultyScriptableObject nightmareDifficultySO;

        public float soundTriggerSizeMultiplier;
        #endregion

        private void AssignScriptableObjectProperties(DifficultyScriptableObject difficultySO)
        {
            soundTriggerSizeMultiplier = difficultySO.gameSettings.soundTriggerSizeMultiplier;
            totalCollectibles = difficultySO.gameSettings.totalCollectibles;
            ActiveDifficultySO = difficultySO;
        }

        private void Awake()
        {
            GameObject container = Resources.Load("Prefabs/ScriptableAssetsContainer") as GameObject;
            normalDifficultySO = container.GetComponent<ScriptableAssetsContainer>().normalDifficultySO;
            hardDifficultySO = container.GetComponent<ScriptableAssetsContainer>().hardDifficultySO;
            nightmareDifficultySO = container.GetComponent<ScriptableAssetsContainer>().nightmareDifficultySO;

            UpdateGameState(GameState.MainMenu);
        }

        #region Starting and stopping a game
        /// <summary>
        /// Starts a new game with given difficulty level.
        /// </summary>
        public void StartNewGame(GameDifficulty difficulty)
        {
            SceneLoadManager.Instance.LoadLevel("Mansion_Outside");
            InitializeGame(difficulty);
        }

        /// <summary>
        /// Starts the game from a save file with given difficulty level.
        /// </summary>
        public void StartSavedGame(GameDifficulty difficulty)
        {
            InitializeGame(difficulty);
        }

        /// <summary>
        /// Basic functionalities that run regardless of starting a new game or continuing one.
        /// </summary>
        private void InitializeGame(GameDifficulty difficulty)
        {
            UpdateGameDifficulty(difficulty);
            startTime = Time.fixedUnscaledTimeAsDouble;
        }

        /// <summary>
        /// Cleans up the game and returns the player to the menu
        /// </summary>
        public void ReturnToMainMenu()
        {
            SceneLoadManager.Instance.LoadLevel("Main_Menu");

            collectedKeys.Clear();
            collectedNotes.Clear();
            gameStatistics.Clear();
            collectiblesCollected = 0;
        }
        #endregion

        #region Game statistics
        /// <summary>
        /// Adds statistics to the gameStatistics Dictionary and invokes OnGameStatisticsChanged.
        /// </summary>
        public void ChangeGameStats(Dictionary<string, float> statistics)
        {
            float playTime = (float)(Time.fixedUnscaledTimeAsDouble - startTime);
            gameStatistics.TryAdd("Play Time", Mathf.Round(playTime * 100f) / 100f);

            if (gameStatistics.ContainsKey("Play Time"))
                gameStatistics["Play Time"] += Mathf.Round(playTime * 100f) / 100f;

            Dictionary<string, float> statisticChanges = new();

            foreach (var statistic in statistics)
            {
                if (gameStatistics.ContainsKey(statistic.Key))
                {
                    statisticChanges.Add(statistic.Key, gameStatistics[statistic.Key] + statistic.Value);
                    continue;
                }
                
                gameStatistics.TryAdd(statistic.Key, Mathf.Round(statistic.Value * 100f) / 100f);
            }

            foreach(var statChange in statisticChanges)
                gameStatistics[statChange.Key] = Mathf.Round(statChange.Value * 100f) / 100f;

            OnGameStatisticsChanged?.Invoke(gameStatistics);
        }
        #endregion

        #region Scenes changed
        private void NewSceneLoaded(string sceneName)
        {
            if (sceneName == "Main_Menu")
                UpdateGameState(GameState.MainMenu);

            if (sceneName == "Mansion_Outside")            
                UpdateGameState(GameState.Started);
        }
        #endregion

        #region Player health & damage
        /// <summary>
        /// Deal damage to player. Set amount of damage to be done.
        /// </summary>
        public void DealDamageToPlayer(int damage)
        {
            if (Difficulty != GameDifficulty.Normal)
            {
                _playerLogicComponent.PlayerHealth -= 999;
                return;
            }
            
            _playerLogicComponent.PlayerHealth -= damage;
        }

        /// <summary>
        /// Change to death scene if player has died.
        /// </summary>
        private void HasPlayerDied(GameObject attacker)
        {
            if (_playerLogicComponent.PlayerHealth != 0) return;

            UpdateGameState(GameState.PlayerDied);
        }
        #endregion

        #region Inventory
        public void AddNewKeyToInventory(KeyScriptableObject keySO)
        {
            if (collectedKeys.ContainsKey(keySO.id))
            {
                Debug.LogWarning("Duplicate item ID!");
                return;
            }

            collectedKeys.Add(keySO.id, keySO);
            OnNewKeyPickedUp?.Invoke(keySO);
        }

        public void AddNewNoteToInventory(NoteScriptableObject noteSO)
        {
            if (collectedNotes.ContainsKey(noteSO.id))
            {
                Debug.LogWarning("Duplicate item ID!");
                return;
            }

            collectedNotes.Add(noteSO.id, noteSO);
            OnNewNotePickedUp?.Invoke(noteSO);
        }

        public void AddCollectibleToInventory(CollectibleScriptableObject collectibleSO)
        {
            collectiblesCollected++;
            OnNewCollectiblePickedUp?.Invoke(collectibleSO);

            if (collectiblesCollected != totalCollectibles) return;

            OnAllCollectiblesFound?.Invoke();
        }
        #endregion

        #region Pause / Resume game
        /// <summary>
        /// Freeze the game completely.
        /// </summary>
        public void FreezeGameTime()
        {
            lastTimeScale = Time.timeScale;
            Time.timeScale = 0;
            AudioListener.pause = true;
            InputSystem.PauseHaptics();
        }

        /// <summary>
        /// Unfreeze the game completely.
        /// </summary>
        public void UnfreezeGameTime()
        {
            Time.timeScale = lastTimeScale;
            AudioListener.pause = false;
            InputSystem.ResumeHaptics();
        }
        #endregion

        #region Power management
        /// <summary>
        /// Set isPowerOn true and invoke OnPowerStateChanged event.
        /// </summary>
        public void SetPowerOn()
        {
            isPowerOn = true;
            OnPowerStateChanged?.Invoke(isPowerOn);
        }

        /// <summary>
        /// Set isPowerOn false and invoke OnPowerStateChanged event.
        /// </summary>
        public void SetPowerOff()
        {
            isPowerOn = false;
            OnPowerStateChanged?.Invoke(isPowerOn);
        }
        #endregion

        #region Horror event
        /// <summary>
        /// Invoke the OnHorrorEventStarted event. And play chase music.
        /// </summary>
        public void StartHorrorEvent()
        {
            if (_isHorrorEventActive) return;
            _isHorrorEventActive = true;
            OnHorrorEventStarted?.Invoke();
            AudioManager.Instance.StartChaseMusic();
        }

        /// <summary>
        /// Invoke the OnHorrorEventEnded event. And stop the chase music.
        /// </summary>
        public void StopHorrorEvent()
        {
            _isHorrorEventActive = false;
            OnHorrorEventEnded?.Invoke();
            AudioManager.Instance.StopChaseMusic(4);
        }
        #endregion

        #region Update difficulty / game state
        /// <summary>
        /// Other scripts subscribed to the event will perform their own actions.
        /// </summary>
        public void UpdateGameDifficulty(GameDifficulty setDifficulty)
        {
            Difficulty = setDifficulty;

            switch (setDifficulty)
            {
                case GameDifficulty.Normal:
                    AssignScriptableObjectProperties(normalDifficultySO);
                    break;
                case GameDifficulty.Hard:
                    AssignScriptableObjectProperties(hardDifficultySO);
                    break;
                case GameDifficulty.Nightmare:
                    AssignScriptableObjectProperties(nightmareDifficultySO);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(setDifficulty), setDifficulty, null);
            }

            OnDifficultyChanged?.Invoke(setDifficulty);
        }

        /// <summary>
        /// Other scripts subscribed to the event will perform their own actions.
        /// </summary>
        public void UpdateGameState(GameState newState)
        {
            State = newState;

            switch (newState)
            {
                case GameState.MainMenu:
                    UnfreezeGameTime();
                    break;
                case GameState.Started:
                    //InitializeGameOnStart();
                    break;
                case GameState.PlayerDied:
                    SceneLoadManager.Instance.LoadLevel("Death_Scene");
                    break;
                case GameState.Ending:
                    SceneLoadManager.Instance.LoadLevel("Ending_Scene");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
            }

            OnGameStateChanged?.Invoke(newState);
        }
        #endregion

        private void OnEnable()
        {
            SceneLoadManager.OnNewSceneLoaded += NewSceneLoaded;
            EnemyLogic.OnEnemyAttackEnded += HasPlayerDied;
            PlayerLogic.OnPickupKey += AddNewKeyToInventory;
            PlayerLogic.OnPickupNote += AddNewNoteToInventory;
            PlayerLogic.OnPickupCollectible += AddCollectibleToInventory;
        }

        private void OnDisable()
        {
            SceneLoadManager.OnNewSceneLoaded -= NewSceneLoaded;
            EnemyLogic.OnEnemyAttackEnded -= HasPlayerDied;
            PlayerLogic.OnPickupKey -= AddNewKeyToInventory;
            PlayerLogic.OnPickupNote -= AddNewNoteToInventory;
            PlayerLogic.OnPickupCollectible -= AddCollectibleToInventory;
        }

        private void OnDestroy() => Time.timeScale = 1;
    }

    public enum GameDifficulty
    {
        NOT_SET,
        Normal,
        Hard,
        Nightmare
    }

    public enum GameState
    {
        MainMenu,
        Started,
        PlayerDied,
        Ending
    }
}