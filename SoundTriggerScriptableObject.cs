using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Sound Trigger Config", menuName = "ScriptableObject/Sound Trigger Config")]
public class SoundTriggerScriptableObject : ScriptableObject
{
    public PlayerSoundTrigger player = new();
    public ItemsSoundTrigger items = new();
    public EnvironmentSoundTrigger environment = new();
    public EnemySoundTrigger enemy = new();
}

[Serializable]
public class PlayerSoundTrigger
{
    public float idle = 1;
    public float walking = 4;
    public float crouching = 2.5f;
    public float crouchingIdle = 0.5f;
    public float running = 7;
    public float jumping = 9;
    public float falling = 6;
}

[Serializable]
public class ItemsSoundTrigger
{
    public float drop = 8;
    public float toggle = 5;
    public float noisemaker = 20;
}

[Serializable]
public class EnvironmentSoundTrigger
{
    public float door = 12;
    public float closet = 6;
    public float cabinet = 8;
    public float dresser = 5;
    public float lightSwitch = 5;
    public float generatorNoise = 50;
    public float loudNoiseObject = 30;
}

[Serializable]
public class EnemySoundTrigger
{
    public float trigger = 15;
    public float attack = 30;
    public float kill = 10;
}