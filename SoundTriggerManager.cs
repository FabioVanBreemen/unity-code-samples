using System.Collections;
using UnityEngine;

namespace Scripts.Managers
{
    public class SoundTriggerManager : SingletonBuilder<SoundTriggerManager>
    {
        public SoundTriggerScriptableObject soundTriggerSO;

        private void Awake() => soundTriggerSO = ScriptableObject.CreateInstance("SoundTriggerScriptableObject") as SoundTriggerScriptableObject;

        /// <summary>
        /// Set a sound trigger detection size. Provide a GameObject and collider radius.<br></br>
        /// Choose to reset automatically, which is <b>ENABLED by default</b>.
        /// </summary>
        public void SetSoundColliderSize(GameObject colliderObject, float size, bool resetAutomatically = true)
        {
            ResetSoundColliderSize(colliderObject);
            SphereCollider collider = colliderObject.GetComponent<SphereCollider>();
            StartCoroutine(SetColliderProperties(collider, colliderObject, size, resetAutomatically));
        }

        /// <summary>
        /// Disable sound trigger detection GameObject and reset its size. Provide a GameObject.
        /// </summary>
        public void ResetSoundColliderSize(GameObject colliderObject)
        {
            SphereCollider collider = colliderObject.GetComponent<SphereCollider>();
            colliderObject.SetActive(false);
            collider.radius = 1;
        }

        /// <summary>
        /// Set collider active and radius to specified size. Reset automatically after ~1 second if true.
        /// </summary>
        private IEnumerator SetColliderProperties(SphereCollider collider, GameObject colliderObject, float size, bool resetAutomatically)
        {
            colliderObject.SetActive(true);

            collider.radius = size * GameManager.Instance.soundTriggerSizeMultiplier;

            if (!resetAutomatically) yield break;

            yield return new WaitForSeconds(0.9f);

            if (colliderObject == null) yield break;

            ResetSoundColliderSize(colliderObject);
        }
    }
}