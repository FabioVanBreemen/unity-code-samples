using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Audio Config", menuName = "ScriptableObject/Audio Config")]
public class AudioScriptableObject : ScriptableObject
{
    public PlayerAudio player = new();
    public EnvironmentAudio environment = new();
    public EnemyAudio enemy = new();
}

[Serializable]
public class PlayerAudio
{
    public AudioClip staminaDepleted;
    public AudioClip itemPickup;
    public AudioClip itemDrop;
    public AudioClip itemThrow;
    public AudioClip scrollInventory;
    public AudioClip scrollInventoryEmpty;
    public AudioClip footstepSingle_05;
}

[Serializable]
public class EnvironmentAudio
{
    public AudioClip lightSwitchOn;
    public AudioClip lightSwitchOff;
}

[Serializable]
public class EnemyAudio
{
    public AudioClip enemyScream;
}