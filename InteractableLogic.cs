using Scripts.Interactables;
using Scripts.Managers;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

public interface IInteractable
{
    public bool IsInteractable { get; set; }
    public void InteractionPerformed(InteractionMethod interaction);
}

namespace Scripts.Interactables
{
    public class InteractableLogic : MonoBehaviour, IInteractable
    {
        bool IInteractable.IsInteractable { get => isInteractable; set => isInteractable = value; }

        [Header("InteractableLogic Params")]
        public bool isInteractable = true;
        private bool _allowSoundCollider = true;
        private bool _directorFinished = true;

        /// <summary>
        /// Called as soon as the player interacts with the object. Passes the InteractionMethod that was used.
        /// </summary>
        public virtual void InteractionPerformed(InteractionMethod interaction) { }

        /// <summary>
        /// Set sound collider to specific size. Collider is automatically reset and disabled after timeEnabled ended.
        /// </summary>
        public void ActivateSoundColliderTimed(GameObject soundCollider, float size, float timeEnabled = 1)
        {
            if (!_allowSoundCollider) return;
            StartCoroutine(SetSoundColliderTimed(soundCollider, size, timeEnabled));
        }

        /// <summary>
        /// Set sound collider to specific size. Collider is automatically reset and disabled after the Audio Source has stopped playing.
        /// </summary>
        public void ActivateSoundColliderWhileAudioPlaying(AudioSource audioSource, GameObject soundCollider, float size)
        {
            if (!_allowSoundCollider) return;
            StartCoroutine(SetSoundColliderWhileAudioPlaying(audioSource, soundCollider, size));
        }

        /// <summary>
        /// Set sound collider to specific size. Collider is automatically reset and disabled after the Playable Director has stopped playing.
        /// </summary>
        public void ActivateSoundColliderWhileDirectorPlaying(PlayableDirector playableDirector, GameObject soundCollider, float size)
        {
            if (!_allowSoundCollider) return;
            StartCoroutine(SetSoundColliderWhileDirectorPlaying(playableDirector, soundCollider, size));
        }

        /// <summary>
        /// Set collider radius to given size and reset the collider after x seconds.
        /// </summary>
        private IEnumerator SetSoundColliderTimed(GameObject soundCollider, float size, float timeEnabled)
        {
            _allowSoundCollider = false;
            SoundTriggerManager.Instance.SetSoundColliderSize(soundCollider, size, false);

            yield return new WaitForSeconds(timeEnabled);

            SoundTriggerManager.Instance.ResetSoundColliderSize(soundCollider);
            _allowSoundCollider = true;
        }

        /// <summary>
        /// Set collider radius to given size and reset the collider after the Audio Source has stopped playing.
        /// </summary>
        private IEnumerator SetSoundColliderWhileAudioPlaying(AudioSource audioSource, GameObject soundCollider, float size)
        {
            _allowSoundCollider = false;
            SoundTriggerManager.Instance.SetSoundColliderSize(soundCollider, size, false);

            yield return new WaitUntil(() => !audioSource.isPlaying);

            SoundTriggerManager.Instance.ResetSoundColliderSize(soundCollider);
            _allowSoundCollider = true;
        }

        /// <summary>
        /// Set collider radius to given size and reset the collider after the Playable Director has stopped playing.
        /// </summary>
        private IEnumerator SetSoundColliderWhileDirectorPlaying(PlayableDirector playableDirector, GameObject soundCollider, float size)
        {
            _allowSoundCollider = false;
            SoundTriggerManager.Instance.SetSoundColliderSize(soundCollider, size, false);

            playableDirector.played += ctx => _directorFinished = false;
            playableDirector.stopped += ctx => _directorFinished = true;

            yield return new WaitUntil(() => _directorFinished);

            SoundTriggerManager.Instance.ResetSoundColliderSize(soundCollider);
            _allowSoundCollider = true;

            playableDirector.played -= ctx => _directorFinished = false;
            playableDirector.stopped -= ctx => _directorFinished = true;
        }
    }

    public enum InteractionMethod
    {
        Click,
        ClickAlternate,
    }
}