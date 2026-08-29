using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AILURONE.Audio;
using StarterAssets;

[InitializeOnLoad]
public static class RobustLevelBGMInjector
{
    static RobustLevelBGMInjector()
    {
        EditorApplication.update += TryInject;
    }

    static void TryInject()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool("RobustLevelBGMInjector_RunOnce", false))
        {
            EditorApplication.update -= TryInject;
            return;
        }

        EditorApplication.update -= TryInject;

        // Open Level scene, inject, and return to previous scene if possible
        var currentScene = EditorSceneManager.GetActiveScene().path;
        bool changedScene = false;
        
        if (currentScene != "Assets/Scenes/Level.unity")
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Level.unity", OpenSceneMode.Single);
            changedScene = true;
        }

        var scene = EditorSceneManager.GetActiveScene();

        var bgmObj = GameObject.Find("BGM Manager");
        if (bgmObj == null)
        {
            bgmObj = new GameObject("BGM Manager");
        }

        var manager = bgmObj.GetComponent<LevelBGMManager>();
        if (manager == null)
        {
            manager = bgmObj.AddComponent<LevelBGMManager>();
        }

        AudioClip clip1 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/Sewerslvt - Mr. Kill Myself.flac");
        AudioClip clip2 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/ANTI BPM - CRYSTAL WAVES.flac");
        AudioClip clip3 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/ANTI BPM - the ends.flac");

        manager.bgmClips = new AudioClip[] { clip1, clip2, clip3 };
        manager.playerController = Object.FindAnyObjectByType<FirstPersonController>();

        EditorUtility.SetDirty(bgmObj);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorPrefs.SetBool("RobustLevelBGMInjector_RunOnce", true);
        Debug.Log("Successfully permanently injected BGM Manager into Level scene.");

        if (changedScene && !string.IsNullOrEmpty(currentScene))
        {
            EditorSceneManager.OpenScene(currentScene, OpenSceneMode.Single);
        }
    }
}
