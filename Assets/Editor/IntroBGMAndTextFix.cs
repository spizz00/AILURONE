using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AILURONE.Opening;

[InitializeOnLoad]
public static class IntroBGMAndTextFix
{
    static IntroBGMAndTextFix()
    {
        EditorApplication.delayCall += Fix;
    }

    private static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool("IntroBGMAndTextFix_RunOnce", false)) return;

        var scenePath = "Assets/Scenes/IntroCutscene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var controller = Object.FindAnyObjectByType<OpeningCinematicController>();
        var presentation = Object.FindAnyObjectByType<OpeningNarrativeSubtitlePresentation>();

        if (controller != null && presentation != null)
        {
            // 1. Sync Text EndingFadeTime precisely with Slide Duration
            var controllerSo = new SerializedObject(controller);
            var segmentsProp = controllerSo.FindProperty("segments");
            
            var presentationSo = new SerializedObject(presentation);
            var shotsProp = presentationSo.FindProperty("shots");
            var shotEndingFadeOutProp = presentationSo.FindProperty("shotEndingFadeOutDuration");
            float fadeOutDur = shotEndingFadeOutProp != null ? shotEndingFadeOutProp.floatValue : 0.18f;

            for (int i = 0; i < shotsProp.arraySize; i++)
            {
                if (i >= segmentsProp.arraySize) break;

                var segmentProp = segmentsProp.GetArrayElementAtIndex(i);
                var durationProp = segmentProp.FindPropertyRelative("duration");
                float segmentDuration = durationProp.floatValue;

                var shotProp = shotsProp.GetArrayElementAtIndex(i);
                var endingFadeTimeProp = shotProp.FindPropertyRelative("endingFadeTime");
                
                // Keep the text on until the very end (minus the crossfade so it's perfectly timed)
                if (endingFadeTimeProp != null && segmentDuration > 0)
                {
                    endingFadeTimeProp.floatValue = segmentDuration - fadeOutDur;
                }
            }
            presentationSo.ApplyModifiedProperties();

            // 2. Add Background Music
            string bgmPath = "Assets/Audio/BGM/freesound_community-apoambient-17878.mp3";
            AudioClip bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>(bgmPath);

            if (bgmClip != null)
            {
                GameObject bgmObj = GameObject.Find("Intro BGM");
                if (bgmObj == null)
                {
                    bgmObj = new GameObject("Intro BGM");
                }

                AudioSource audioSource = bgmObj.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = bgmObj.AddComponent<AudioSource>();
                }

                audioSource.clip = bgmClip;
                audioSource.playOnAwake = true;
                audioSource.loop = true;
                audioSource.volume = 0.5f;

                EditorUtility.SetDirty(bgmObj);
            }
            else
            {
                Debug.LogError("IntroBGMAndTextFix: Missing background music file.");
            }

            EditorUtility.SetDirty(presentation);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorPrefs.SetBool("IntroBGMAndTextFix_RunOnce", true);
            Debug.Log("Successfully synced text fade with slide transitions and added continuous BGM.");
        }
    }
}
