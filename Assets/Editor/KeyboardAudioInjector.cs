using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AILURONE.Opening;

[InitializeOnLoad]
public static class KeyboardAudioInjector
{
    static KeyboardAudioInjector()
    {
        EditorApplication.delayCall += Inject;
    }

    private static void Inject()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool("KeyboardAudio_RunOnce", false)) return;

        var scenePath = "Assets/Scenes/IntroCutscene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var presentation = Object.FindAnyObjectByType<OpeningNarrativeSubtitlePresentation>();
        if (presentation != null)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/keyboard soft.wav");

            if (clip != null)
            {
                var so = new SerializedObject(presentation);
                var clipProp = so.FindProperty("continuousTypingClip");
                clipProp.objectReferenceValue = clip;

                var cpsProp = so.FindProperty("charactersPerSecond");
                if (cpsProp != null) cpsProp.floatValue = 20f;

                var audioSourceProp = so.FindProperty("typewriterAudioSource");
                AudioSource source = presentation.GetComponent<AudioSource>();
                if (source == null)
                {
                    source = presentation.gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                }
                audioSourceProp.objectReferenceValue = source;

                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(presentation);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                EditorPrefs.SetBool("KeyboardAudio_RunOnce", true);
                Debug.Log("Successfully injected Keyboard Audio into IntroCutscene.");
            }
            else
            {
                Debug.LogError("KeyboardAudioInjector: Missing keyboard soft.wav.");
            }
        }
    }
}
