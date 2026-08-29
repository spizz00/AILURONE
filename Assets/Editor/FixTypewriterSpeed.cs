using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AILURONE.Opening;

[InitializeOnLoad]
public static class FixTypewriterSpeed
{
    static FixTypewriterSpeed()
    {
        EditorApplication.delayCall += Fix;
    }

    private static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool("FixTypewriterSpeed_RunOnce", false)) return;

        var scenePath = "Assets/Scenes/IntroCutscene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var presentation = Object.FindAnyObjectByType<OpeningNarrativeSubtitlePresentation>();
        if (presentation != null)
        {
            var so = new SerializedObject(presentation);
            var cpsProp = so.FindProperty("charactersPerSecond");
            if (cpsProp != null)
            {
                cpsProp.floatValue = 55f; // Must be at least 50 to prevent cut-offs based on slide timings
                so.ApplyModifiedProperties();
                
                EditorUtility.SetDirty(presentation);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                EditorPrefs.SetBool("FixTypewriterSpeed_RunOnce", true);
                Debug.Log("Successfully increased typewriter speed to 55 CPS to prevent dialogue cut-offs.");
            }
        }
    }
}
