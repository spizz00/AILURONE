using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AILURONE.Opening;
using System.Linq;

[InitializeOnLoad]
public static class FixCutsceneAudioInjector
{
    static FixCutsceneAudioInjector()
    {
        EditorApplication.delayCall += Fix;
    }

    private static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool("FixCutsceneAudio_RunOnce", false)) return;

        var scenePath = "Assets/Scenes/IntroCutscene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var controller = Object.FindAnyObjectByType<OpeningCinematicController>();
        var presentation = Object.FindAnyObjectByType<OpeningNarrativeSubtitlePresentation>();
        
        if (controller != null && presentation != null)
        {
            var go = controller.gameObject;
            var audioSources = go.GetComponents<AudioSource>();

            // We need 2 AudioSources: one for media, one for typewriter.
            while (audioSources.Length < 2)
            {
                go.AddComponent<AudioSource>();
                audioSources = go.GetComponents<AudioSource>();
            }

            // Assign first one to mediaAudioSource
            var controllerSo = new SerializedObject(controller);
            var mediaAudioSourceProp = controllerSo.FindProperty("mediaAudioSource");
            mediaAudioSourceProp.objectReferenceValue = audioSources[0];
            
            // Set properties for media audio source (usually default is fine, maybe playOnAwake false)
            audioSources[0].playOnAwake = false;
            controllerSo.ApplyModifiedProperties();

            // Assign second one to typewriterAudioSource
            var presentationSo = new SerializedObject(presentation);
            var typewriterAudioSourceProp = presentationSo.FindProperty("typewriterAudioSource");
            typewriterAudioSourceProp.objectReferenceValue = audioSources[1];
            
            audioSources[1].playOnAwake = false;
            presentationSo.ApplyModifiedProperties();

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(presentation);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorPrefs.SetBool("FixCutsceneAudio_RunOnce", true);
            Debug.Log("Successfully fixed missing mediaAudioSource and isolated typewriterAudioSource.");
        }
    }
}
