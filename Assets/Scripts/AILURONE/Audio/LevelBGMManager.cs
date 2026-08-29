using UnityEngine;
using StarterAssets;

namespace AILURONE.Audio
{
    public class LevelBGMManager : MonoBehaviour
    {
        public FirstPersonController playerController;
        public AudioClip[] bgmClips;

        [SerializeField, Range(0f, 1f)]
        private float bgmVolume = 0.3f;

        private AudioSource audioSource;
        private int currentClipIndex = 0;
        private bool hasTriggered = false;
        private bool isApplicationFocused = true;

        void Start()
        {
            audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.volume = bgmVolume;

            if (playerController == null)
            {
                playerController = Object.FindAnyObjectByType<FirstPersonController>();
            }
        }

        void Update()
        {
            if (!isApplicationFocused)
                return;

            if (!hasTriggered)
            {
                if (playerController != null && playerController.Grounded)
                {
                    hasTriggered = true;
                    PlayNextClip();
                }
            }
            else
            {
                if (!audioSource.isPlaying)
                {
                    PlayNextClip();
                }
            }
        }

        void PlayNextClip()
        {
            if (bgmClips == null || bgmClips.Length == 0)
                return;

            audioSource.clip = bgmClips[currentClipIndex];
            audioSource.volume = bgmVolume;
            audioSource.Play();

            currentClipIndex = (currentClipIndex + 1) % bgmClips.Length;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            isApplicationFocused = hasFocus;
        }
    }
}