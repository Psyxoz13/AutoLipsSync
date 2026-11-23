using System;
using UnityEditor;
using UnityEngine;

public class PhonemeTimelineGeneratorWindow : EditorWindow
{
    private const string PREF_KEY = "PhonemeTimelineGeneratorWindow.SettingsV1";

    private enum AnalysisAlgorithm { FFTOnly = 0, LPC = 1, Hybrid = 2, MFCC = 3 }

    [SerializeField] private AudioClip _clip;
    [SerializeField] private float _frameDuration = 0.03f;
    [SerializeField] private float _overlap = 0.5f;
    [SerializeField] private float _silenceThreshold = 0.01f;
    [SerializeField] private float _minFormantFreq = 250f;
    [SerializeField] private float _maxFormantFreq = 3000f;
    [SerializeField] private float _energyThreshold = 0.0008f;
    [SerializeField] private float _vowelDistanceThreshold = 300000f;
    [SerializeField] private AnalysisAlgorithm _algorithm = AnalysisAlgorithm.FFTOnly;

    [MenuItem("Window/Phoneme Timeline Generator")]
    public static void ShowWindow() => GetWindow<PhonemeTimelineGeneratorWindow>("Phoneme Timeline Generator");

    private void OnEnable() => LoadPrefs();
    private void OnDisable() => SavePrefs();

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Audio Settings", EditorStyles.boldLabel);

        _clip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", _clip, typeof(AudioClip), false);
        _frameDuration = EditorGUILayout.FloatField(new GUIContent("Frame Duration (s)", "0.02..0.09"), _frameDuration);
        _overlap = EditorGUILayout.Slider(new GUIContent("Overlap", "0..0.9"), _overlap, 0f, 0.9f);
        _silenceThreshold = EditorGUILayout.FloatField(new GUIContent("Silence Threshold", "RMS below which is considered silence"), _silenceThreshold);
        _minFormantFreq = EditorGUILayout.FloatField(new GUIContent("Min Formant Freq (Hz)"), _minFormantFreq);
        _maxFormantFreq = EditorGUILayout.FloatField(new GUIContent("Max Formant Freq (Hz)"), _maxFormantFreq);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Consonant Detection", EditorStyles.boldLabel);

        _energyThreshold = EditorGUILayout.FloatField("Energy Threshold", _energyThreshold);
        _vowelDistanceThreshold = EditorGUILayout.FloatField("Vowel Distance Threshold", _vowelDistanceThreshold);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Analysis Algorithm", EditorStyles.boldLabel);
        _algorithm = (AnalysisAlgorithm)EditorGUILayout.EnumPopup("Algorithm", _algorithm);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Timeline"))
        {
            GenerateAndSave();
            SavePrefs();
        }
    }

    private void GenerateAndSave()
    {
        if (_clip == null || _clip.samples == 0)
        {
            Debug.LogWarning("No audio clip set or clip empty.");
            return;
        }

        var settings = new PhonemeTimelineGeneratorBase.GeneratorSettings
        {
            FrameDuration = _frameDuration,
            Overlap = _overlap,
            SilenceThreshold = _silenceThreshold,
            MinFormantFreq = _minFormantFreq,
            MaxFormantFreq = _maxFormantFreq,
            EnergyThreshold = _energyThreshold,
            VowelDistanceThreshold = _vowelDistanceThreshold
        };

        PhonemeTimelineGeneratorBase generator = _algorithm switch
        {
            AnalysisAlgorithm.FFTOnly => new FFTGenerator(settings),
            AnalysisAlgorithm.LPC => new LPCGenerator(settings),
            AnalysisAlgorithm.Hybrid => new HybridGenerator(settings),
            AnalysisAlgorithm.MFCC => new MFCCGenerator(settings),
            _ => new FFTGenerator(settings)
        };

        var timeline = generator.GenerateTimeline(_clip);

        if (timeline == null || timeline.Length == 0)
        {
            Debug.LogWarning("Timeline is empty, nothing to save.");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Save LipsSync Timeline Asset",
            _clip != null ? _clip.name + "_Timeline" : "NewLipsSyncTimeline",
            "asset",
            "Choose where to save the timeline asset"
        );

        if (string.IsNullOrEmpty(path))
            return;

        LipsSyncTimelineAsset asset = ScriptableObject.CreateInstance<LipsSyncTimelineAsset>();
        asset.TimelineEntries = timeline;

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Timeline asset created at {path}");
    }

    private void SavePrefs()
    {
        var settings = new SettingsData
        {
            ClipGuid = GetClipGuid(_clip),
            FrameDuration = _frameDuration,
            Overlap = _overlap,
            SilenceThreshold = _silenceThreshold,
            MinFormantFreq = _minFormantFreq,
            MaxFormantFreq = _maxFormantFreq,
            EnergyThreshold = _energyThreshold,
            VowelDistanceThreshold = _vowelDistanceThreshold,
            Algorithm = (int)_algorithm
        };

        string json = JsonUtility.ToJson(settings);
        EditorPrefs.SetString(PREF_KEY, json);
    }

    private void LoadPrefs()
    {
        if (!EditorPrefs.HasKey(PREF_KEY))
            return;

        string json = EditorPrefs.GetString(PREF_KEY, string.Empty);
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            var settings = JsonUtility.FromJson<SettingsData>(json);
            if (settings != null)
            {
                _frameDuration = settings.FrameDuration != 0f ? settings.FrameDuration : _frameDuration;
                _overlap = settings.Overlap;
                _silenceThreshold = settings.SilenceThreshold != 0f ? settings.SilenceThreshold : _silenceThreshold;
                _minFormantFreq = settings.MinFormantFreq != 0f ? settings.MinFormantFreq : _minFormantFreq;
                _maxFormantFreq = settings.MaxFormantFreq != 0f ? settings.MaxFormantFreq : _maxFormantFreq;
                _energyThreshold = settings.EnergyThreshold != 0f ? settings.EnergyThreshold : _energyThreshold;
                _vowelDistanceThreshold = settings.VowelDistanceThreshold != 0f ? settings.VowelDistanceThreshold : _vowelDistanceThreshold;
                _algorithm = (AnalysisAlgorithm)settings.Algorithm;

                if (!string.IsNullOrEmpty(settings.ClipGuid))
                {
                    string path = AssetDatabase.GUIDToAssetPath(settings.ClipGuid);
                    if (!string.IsNullOrEmpty(path))
                        _clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }
        }
        catch { }
    }

    private static string GetClipGuid(AudioClip clip)
    {
        if (clip == null)
            return string.Empty;
        string path = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(path))
            return string.Empty;
        return AssetDatabase.AssetPathToGUID(path);
    }

    [Serializable]
    private class SettingsData
    {
        public string ClipGuid;
        public float FrameDuration;
        public float Overlap;
        public float SilenceThreshold;
        public float MinFormantFreq;
        public float MaxFormantFreq;
        public float EnergyThreshold;
        public float VowelDistanceThreshold;
        public int Algorithm;
    }
}
