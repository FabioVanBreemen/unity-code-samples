using Scripts.Scriptables;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Scripts.Managers
{
    public class AudioManager : SingletonBuilder<AudioManager>
    {
        public AudioScriptableObject audioSO;
        private AudioMixer _mainMixer;
        private AudioClip _chaseMusicClip;
        private AudioSource _chaseMusicSource;

        private void Awake()
        {
            GameObject container = Resources.Load("Prefabs/ScriptableAssetsContainer") as GameObject;
            audioSO = container.GetComponent<ScriptableAssetsContainer>().audioSO;
            _mainMixer = Resources.Load("Audio/Main") as AudioMixer;
            _chaseMusicClip = Resources.Load("Audio/Music/Chase_Music") as AudioClip;
        }

        #region Play, Pause, Stop sounds
        /// <summary>
        /// Plays the AudioSource.
        /// </summary>
        public void PlaySound(AudioSource audioSource) => audioSource.Play();

        /// <summary>
        /// Plays the AudioSource with set volume
        /// </summary>
        public void PlaySound(AudioSource audioSource, float volume)
        {
            audioSource.volume = volume;

            audioSource.Play();
        }

        /// <summary>
        /// Plays the AudioSource with random pitch.
        /// </summary>
        public void PlaySound(AudioSource audioSource, float minPitch, float maxPitch)
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);

            audioSource.Play();
        }

        /// <summary>
        /// Plays the AudioSource with clip as OneShot.
        /// </summary>
        public void PlaySound(AudioSource audioSource, AudioClip clip) => audioSource.PlayOneShot(clip);

        /// <summary>
        /// Plays the AudioSource with clip as OneShot with random pitch.<br></br><br></br>
        /// <b>WARNING</b>: if pitch is randomized on a OneShot clip, all currently playing sounds on that AudioSource will be affected.
        /// </summary>
        public void PlaySound(AudioSource audioSource, AudioClip clip, float minPitch, float maxPitch)
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);

            audioSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Plays specified AudioClip on a new GameObject at the player's position. Choose if Source should act spatialized.<br></br>
        /// Optional: volume
        /// </summary>
        public AudioSource PlaySound(AudioClip clip, bool spatialized = true, float volume = 1)
        {
            GameObject newAudioContainer = CreateNewAudioSourceGameObject(spatialized, GameManager.Instance.Player.transform.position + new Vector3(0, 1f, 0));
            AudioSource audioSource = newAudioContainer.GetComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.Play();

            StartCoroutine(DestroyNewAudioContainer(audioSource, newAudioContainer));
            return audioSource;
        }

        /// <summary>
        /// Plays specified AudioClip on a new GameObject at given Vector3 position. Choose if Source should act spatialized.<br></br>
        /// Optional: volume
        /// </summary>
        public AudioSource PlaySound(AudioClip clip, Vector3 position, bool spatialized = true, float volume = 1)
        {
            GameObject newAudioContainer = CreateNewAudioSourceGameObject(spatialized, position);
            AudioSource audioSource = newAudioContainer.GetComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.Play();

            StartCoroutine(DestroyNewAudioContainer(audioSource, newAudioContainer));
            return audioSource;
        }

        /// <summary>
        /// Stops the AudioSource.
        /// </summary>
        public void StopSound(AudioSource audioSource)
        {
            if (!audioSource.isPlaying) return;
            audioSource.Stop();
        }

        /// <summary>
        /// Stops the AudioSource with a volume fade.
        /// </summary>
        public void StopSound(AudioSource audioSource, float fadeTime)
        {
            if (!audioSource.isPlaying || audioSource.volume == 0) return;
            StartCoroutine(FadeOut(audioSource, fadeTime));
        }

        /// <summary>
        /// Pauses the AudioSource.
        /// </summary>
        public static void PauseSound(AudioSource audioSource)
        {
            if (!audioSource.isPlaying) return;
            audioSource.Pause();
        }
        #endregion

        /// <summary>
        /// Fades out the volume of given AudioSource.
        /// </summary>
        private IEnumerator FadeOut(AudioSource audioSource, float fadeTime)
        {
            float previousVolume = audioSource.volume;

            while (audioSource != null && audioSource.volume > 0)
            {
                audioSource.volume -= previousVolume / 20;
                yield return new WaitForSeconds(fadeTime / 20);
            }

            if (audioSource == null) yield break;
            audioSource.Stop();

            if (audioSource == null) yield break;
            audioSource.volume = previousVolume;
        }

        #region New Audio GameObject
        /// <summary>
        /// Creates and sets up a temporary GameObject with an AudioSource.
        /// </summary>
        /// <returns>New audio container GameObject</returns>
        private GameObject CreateNewAudioSourceGameObject(bool spatialized, Vector3 newPosition)
        {
            GameObject audioContainer = new();
            AudioSource audioSource = audioContainer.AddComponent<AudioSource>();

            audioSource.outputAudioMixerGroup = _mainMixer.FindMatchingGroups("Master")[0];

            if (spatialized)
            {
                audioSource.spatialize = true;
                audioSource.spatialBlend = 1f;
            }

            audioContainer.name = "TempAudioSource";
            audioContainer.transform.position = newPosition;

            return audioContainer;
        }

        /// <summary>
        /// Destroy the new audio container after the AudioSource stopped playing.
        /// </summary>
        private IEnumerator DestroyNewAudioContainer(AudioSource audioSource, GameObject audioContainer)
        {
            yield return new WaitUntil(() => audioSource == null || (!audioSource.isPlaying && Time.timeScale > 0));
            if (audioSource == null) yield break;
            Destroy(audioContainer);
        }
        #endregion

        /// <summary>
        /// Plays footstep sound on given Audio Source.
        /// </summary>
        public void PlayFootstepSound(AudioSource audioSource)
        {
            // TODO ground/floor material based footsteps
            PlaySound(audioSource, audioSO.player.footstepSingle_05, 0.9f, 1.1f);
        }

        #region Chase Music
        /// <summary>
        /// Starts the chase music and assigns _chaseMusicSource.
        /// </summary>
        public void StartChaseMusic()
        {
            if (_chaseMusicSource != null)
                StopSound(_chaseMusicSource);

            _chaseMusicSource = PlaySound(_chaseMusicClip, false, 0.3f);
            _chaseMusicSource.loop = true;
        }

        /// <summary>
        /// Stops the _chaseMusicSource AudioSource with a fadeout.
        /// </summary>
        public void StopChaseMusic(float fadeTime)
        {
            if (_chaseMusicSource == null) return;
            StopSound(_chaseMusicSource, fadeTime);
        }
        #endregion
    }
}