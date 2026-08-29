using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AILURONE.Opening;

[InitializeOnLoad]
public static class SyncCutsceneTimings
{
    static SyncCutsceneTimings()
    {
        EditorApplication.delayCall += Fix;
    }

    private static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool("SyncCutsceneTimings_RunOnce", false)) return;

        var scenePath = "Assets/Scenes/IntroCutscene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var controller = Object.FindAnyObjectByType<OpeningCinematicController>();
        var presentation = Object.FindAnyObjectByType<OpeningNarrativeSubtitlePresentation>();

        if (controller != null && presentation != null)
        {
            var controllerSo = new SerializedObject(controller);
            var segmentsProp = controllerSo.FindProperty("segments");
            
            var presentationSo = new SerializedObject(presentation);
            var shotsProp = presentationSo.FindProperty("shots");
            var shotEndingFadeOutProp = presentationSo.FindProperty("shotEndingFadeOutDuration");
            float fadeOutDur = shotEndingFadeOutProp != null ? shotEndingFadeOutProp.floatValue : 0.18f;

            float dialogueOnScreenTime = 5.0f;
            float firstDialogueStartTime = 1.0f;

            for (int i = 0; i < shotsProp.arraySize; i++)
            {
                if (i >= segmentsProp.arraySize) break;

                var segmentProp = segmentsProp.GetArrayElementAtIndex(i);
                var durationProp = segmentProp.FindPropertyRelative("duration");

                var shotProp = shotsProp.GetArrayElementAtIndex(i);
                var endingFadeTimeProp = shotProp.FindPropertyRelative("endingFadeTime");
                var beatsProp = shotProp.FindPropertyRelative("beats");

                if (beatsProp != null && beatsProp.arraySize > 0)
                {
                    // Force Beat 1 to start at 1.0s
                    var beat1Prop = beatsProp.GetArrayElementAtIndex(0);
                    var revealTime1Prop = beat1Prop.FindPropertyRelative("automaticRevealTime");
                    if (revealTime1Prop != null) revealTime1Prop.floatValue = firstDialogueStartTime;

                    float lastBeatTime = firstDialogueStartTime;

                    // Space subsequent beats by exactly 5.0 seconds
                    for (int j = 1; j < beatsProp.arraySize; j++)
                    {
                        var beatProp = beatsProp.GetArrayElementAtIndex(j);
                        var revealTimeProp = beatProp.FindPropertyRelative("automaticRevealTime");
                        if (revealTimeProp != null)
                        {
                            revealTimeProp.floatValue = lastBeatTime + dialogueOnScreenTime;
                            lastBeatTime = revealTimeProp.floatValue;
                        }
                    }

                    // The slide's text fades out 5.0 seconds after the final beat begins
                    if (endingFadeTimeProp != null)
                    {
                        endingFadeTimeProp.floatValue = lastBeatTime + dialogueOnScreenTime;
                    }

                    // The slide's duration ends right as the text finishes fading out
                    if (durationProp != null)
                    {
                        durationProp.floatValue = lastBeatTime + dialogueOnScreenTime + fadeOutDur;
                    }
                }
            }

            presentationSo.ApplyModifiedProperties();
            controllerSo.ApplyModifiedProperties();

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(presentation);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorPrefs.SetBool("SyncCutsceneTimings_RunOnce", true);
            Debug.Log("Successfully synchronized cutscene pacing: 5 seconds per dialogue minimum.");
        }
    }
}
