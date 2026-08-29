using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AILURONE.Opening;

[InitializeOnLoad]
public static class SlowDownCutscene
{
    static SlowDownCutscene()
    {
        EditorApplication.delayCall += Fix;
    }

    private static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool("SlowDownCutscene_RunOnce", false)) return;

        var scenePath = "Assets/Scenes/IntroCutscene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var controller = Object.FindAnyObjectByType<OpeningCinematicController>();
        var presentation = Object.FindAnyObjectByType<OpeningNarrativeSubtitlePresentation>();

        if (controller != null && presentation != null)
        {
            float addedTime = 2.0f; // Add 2 seconds to each slide

            // Update Cinematic Controller Durations
            var controllerSo = new SerializedObject(controller);
            var segmentsProp = controllerSo.FindProperty("segments");
            
            for (int i = 0; i < segmentsProp.arraySize; i++)
            {
                var segmentProp = segmentsProp.GetArrayElementAtIndex(i);
                var durationProp = segmentProp.FindPropertyRelative("duration");
                if (durationProp != null && durationProp.floatValue > 0)
                {
                    durationProp.floatValue += addedTime;
                }
            }
            controllerSo.ApplyModifiedProperties();

            // Update Subtitle Ending Fade Times
            var presentationSo = new SerializedObject(presentation);
            var shotsProp = presentationSo.FindProperty("shots");

            for (int i = 0; i < shotsProp.arraySize; i++)
            {
                var shotProp = shotsProp.GetArrayElementAtIndex(i);
                var endingFadeTimeProp = shotProp.FindPropertyRelative("endingFadeTime");
                if (endingFadeTimeProp != null && endingFadeTimeProp.floatValue > 0)
                {
                    endingFadeTimeProp.floatValue += addedTime;
                }
            }
            presentationSo.ApplyModifiedProperties();

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(presentation);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorPrefs.SetBool("SlowDownCutscene_RunOnce", true);
            Debug.Log($"Successfully added {addedTime} seconds to each IntroCutscene slide and text fade time.");
        }
    }
}
