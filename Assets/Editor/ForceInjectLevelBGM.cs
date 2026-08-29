using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AILURONE.Audio;
using StarterAssets;

public static class ForceInjectLevelBGM
{
    [MenuItem("AILURONE/Inject BGM into Level Scene")]
    static void Inject()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) 
        {
            Debug.LogWarning("Cannot inject BGM while playing! Stop play mode first.");
            return;
        }

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity", OpenSceneMode.Single);

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

        EditorPrefs.SetBool("ForceInjectLevelBGM_RunOnce", true);
        Debug.Log("Successfully forcefully injected BGM Manager into Level scene.");
    }
}
// trigger
