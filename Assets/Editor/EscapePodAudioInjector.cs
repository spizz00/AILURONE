using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class EscapePodAudioInjector
{
    static EscapePodAudioInjector()
    {
        EditorApplication.delayCall += Inject;
    }

    private static void Inject()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool("EscapePodAudio_RunOnce_v2", false)) return;

        string sourcePath = "Assets/Audio/SFX/eldadtsabary-up-and-down-lost-4-10258.mp3";
        string savePath = "Assets/Audio/SFX/eldadtsabary-up-and-down-lost-4-10258_cropped.wav";
        
        AudioClip sourceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(sourcePath);
        if (sourceClip != null)
        {
            float targetDuration = 15.083333f;
            int targetSamples = Mathf.Min(sourceClip.samples, Mathf.CeilToInt(targetDuration * sourceClip.frequency));
            
            float[] data = new float[targetSamples * sourceClip.channels];
            sourceClip.GetData(data, 0);

            AudioClip newClip = AudioClip.Create(sourceClip.name + "_cropped", targetSamples, sourceClip.channels, sourceClip.frequency, false);
            newClip.SetData(data, 0);

            SaveWav(savePath, newClip);
            AssetDatabase.ImportAsset(savePath);
        }

        var croppedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(savePath);
        if (croppedClip != null)
        {
            var scenePath = "Assets/Scenes/EscapePodAnimation.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject audioObj = GameObject.Find("EscapePod Audio");
            if (audioObj == null)
            {
                audioObj = new GameObject("EscapePod Audio");
            }

            var audioSource = audioObj.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = audioObj.AddComponent<AudioSource>();
            }

            audioSource.clip = croppedClip;
            audioSource.playOnAwake = true;
            audioSource.loop = false;
            
            EditorUtility.SetDirty(audioObj);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorPrefs.SetBool("EscapePodAudio_RunOnce_v2", true);
            Debug.Log("Successfully restored and cropped Escape Pod Audio.");
        }
    }

    public static bool SaveWav(string filename, AudioClip clip)
    {
        var filepath = Path.Combine(Application.dataPath.Replace("Assets",""), filename);
        Directory.CreateDirectory(Path.GetDirectoryName(filepath));

        using (var fileStream = new FileStream(filepath, FileMode.Create))
        {
            for (int i = 0; i < 44; i++) 
            {
                fileStream.WriteByte(0);
            }
            ConvertAndWrite(fileStream, clip);
            WriteHeader(fileStream, clip);
        }

        return true; 
    }

    static void ConvertAndWrite(FileStream fileStream, AudioClip clip)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];
        int rescaleFactor = 32767; 

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * rescaleFactor);
            byte[] byteArr = System.BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }
        fileStream.Write(bytesData, 0, bytesData.Length);
    }

    static void WriteHeader(FileStream fileStream, AudioClip clip)
    {
        var hz = clip.frequency;
        var channels = clip.channels;
        var samples = clip.samples;

        fileStream.Seek(0, SeekOrigin.Begin);

        byte[] riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
        fileStream.Write(riff, 0, 4);

        byte[] chunkSize = System.BitConverter.GetBytes(fileStream.Length - 8);
        fileStream.Write(chunkSize, 0, 4);

        byte[] wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
        fileStream.Write(wave, 0, 4);

        byte[] fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
        fileStream.Write(fmt, 0, 4);

        byte[] subChunk1 = System.BitConverter.GetBytes(16);
        fileStream.Write(subChunk1, 0, 4);

        ushort two = 2;
        ushort one = 1;

        byte[] audioFormat = System.BitConverter.GetBytes(one);
        fileStream.Write(audioFormat, 0, 2);

        byte[] numChannels = System.BitConverter.GetBytes(channels);
        fileStream.Write(numChannels, 0, 2);

        byte[] sampleRate = System.BitConverter.GetBytes(hz);
        fileStream.Write(sampleRate, 0, 4);

        byte[] byteRate = System.BitConverter.GetBytes(hz * channels * 2); 
        fileStream.Write(byteRate, 0, 4);

        ushort blockAlign = (ushort)(channels * 2);
        fileStream.Write(System.BitConverter.GetBytes(blockAlign), 0, 2);

        ushort bps = 16;
        byte[] bitsPerSample = System.BitConverter.GetBytes(bps);
        fileStream.Write(bitsPerSample, 0, 2);

        byte[] datastring = System.Text.Encoding.UTF8.GetBytes("data");
        fileStream.Write(datastring, 0, 4);

        byte[] subChunk2 = System.BitConverter.GetBytes(samples * channels * 2);
        fileStream.Write(subChunk2, 0, 4);
    }
}
