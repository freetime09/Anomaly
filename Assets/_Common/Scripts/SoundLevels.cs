using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 소리 파일마다 크기를 맞추는 음량표 (Resources/SoundLevels 에셋 하나).
///  - 씬이 열리면 씬 안 스크립트의 AudioClip 칸과 AudioSource 소리를, 표의 보정값만큼 키우거나 줄인 복사본으로 바꿔 끼운다.
///    → 각 스크립트의 볼륨 칸은 그대로 두고, 파일끼리의 크기 차이만 없앤다. 원본 파일은 건드리지 않는다.
///  - 에셋 오른쪽 위 ⋮ 메뉴 "측정해서 다시 채우기" 로 파일 크기를 재고 보정값을 자동으로 채운다.
///  - 하나만 더 키우거나 줄이고 싶으면 그 줄의 '추가 조절' 을 바꾸면 된다 (다시 채워도 유지됨).
/// </summary>
[CreateAssetMenu(fileName = "SoundLevels", menuName = "An§maly/음량표")]
public class SoundLevels : ScriptableObject
{
    public enum Kind
    {
        [InspectorName("효과음")] Sfx,
        [InspectorName("배경음")] Bgm,
        [InspectorName("계속 나는 소리")] Soft,
    }

    [Serializable]
    public class Entry
    {
        public AudioClip clip;
        public Kind kind;
        [Tooltip("측정된 크기 (dB). 자동으로 채워짐")]
        public float measuredDb;
        [Tooltip("자동 보정 (dB). 측정해서 다시 채우기로 바뀜")]
        public float autoDb;
        [Tooltip("추가 조절 (dB). +면 더 크게, -면 더 작게. 다시 채워도 유지됨")]
        public float extraDb;

        public float GainDb => autoDb + extraDb;
    }

    [Header("맞출 크기 (dB) — 종류별")]
    public float sfxTarget = -18f;
    public float bgmTarget = -24f;
    [Tooltip("발소리·그리는 소리·말소리·반복음처럼 계속 나는 소리")]
    public float softTarget = -26f;
    [Tooltip("키울 때 이 크기(dB)를 넘는 봉우리는 생기지 않게 보정을 줄인다 (찢어지는 소리 방지)")]
    public float peakCeiling = -1f;

    public List<Entry> entries = new List<Entry>();

    public float TargetFor(Kind k) => k == Kind.Bgm ? bgmTarget : k == Kind.Soft ? softTarget : sfxTarget;

    // ───────── 실행 중 ─────────

    static SoundLevels table;
    static readonly Dictionary<AudioClip, AudioClip> cache = new Dictionary<AudioClip, AudioClip>();
    static readonly Dictionary<Type, FieldInfo[]> clipFields = new Dictionary<Type, FieldInfo[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        cache.Clear();
        table = Resources.Load<SoundLevels>("SoundLevels");
        if (table == null) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < SceneManager.sceneCount; i++) // 처음 씬은 이미 열린 뒤라 여기서 바로
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded) OnSceneLoaded(s, LoadSceneMode.Additive);
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects()) Apply(root);
    }

    /// <summary>오브젝트(와 자식)의 소리를 음량표대로 바꿔 끼운다. 씬이 열릴 때 자동으로 불림 — 나중에 만드는 오브젝트는 직접 불러도 됨.</summary>
    public static void Apply(GameObject root)
    {
        if (table == null) return;
        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            foreach (var f in ClipFields(mb.GetType()))
            {
                object v = f.GetValue(mb);
                if (v is AudioClip c) f.SetValue(mb, Get(c));
                else if (v is AudioClip[] arr) for (int i = 0; i < arr.Length; i++) arr[i] = Get(arr[i]);
                else if (v is List<AudioClip> list) for (int i = 0; i < list.Count; i++) list[i] = Get(list[i]);
            }
        }
        foreach (var src in root.GetComponentsInChildren<AudioSource>(true))
        {
            var c = Get(src.clip);
            if (c == src.clip) continue;
            bool playing = src.isPlaying;
            float t = src.time;
            src.clip = c; // playOnAwake 로 이미 나오던 배경음은 같은 자리부터 이어서
            if (playing) { src.time = t; src.Play(); }
        }
    }

    /// <summary>음량표대로 크기를 맞춘 소리. 표에 없으면 그대로.</summary>
    public static AudioClip Get(AudioClip clip)
    {
        if (clip == null || table == null) return clip;
        if (cache.TryGetValue(clip, out var done)) return done;
        AudioClip result = clip;
        var e = table.entries.Find(x => x.clip == clip);
        if (e != null && Mathf.Abs(e.GainDb) >= 0.1f) result = Scaled(clip, Mathf.Pow(10f, e.GainDb / 20f));
        cache[clip] = result;
        cache[result] = result;
        return result;
    }

    static AudioClip Scaled(AudioClip src, float gain)
    {
        if (src.loadState != AudioDataLoadState.Loaded) src.LoadAudioData();
        var data = new float[src.samples * src.channels];
        if (src.loadState != AudioDataLoadState.Loaded || !src.GetData(data, 0)) return src; // 못 읽으면 원본 그대로
        for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);
        var c = AudioClip.Create(src.name, src.samples, src.channels, src.frequency, false);
        c.SetData(data, 0);
        return c;
    }

    static FieldInfo[] ClipFields(Type t)
    {
        if (clipFields.TryGetValue(t, out var fs)) return fs;
        var list = new List<FieldInfo>();
        for (var k = t; k != null && k != typeof(MonoBehaviour); k = k.BaseType)
            foreach (var f in k.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (f.FieldType == typeof(AudioClip) || f.FieldType == typeof(AudioClip[]) || f.FieldType == typeof(List<AudioClip>))
                    list.Add(f);
        return clipFields[t] = list.ToArray();
    }

    // ───────── 에디터: 측정 ─────────
#if UNITY_EDITOR
    static readonly string[] Folders = { "Assets/Sound Last", "Assets/_Common/Sounds" };
    static readonly string[] BgmNames = { "2C1", "2C2", "3C", "4C BOSS", "BackGround" };
    static readonly string[] SoftNames = { "Run", "Draw", "Sliding", "Flying", "branch return", "TextSound", "Laser Spin", "vaccine", "Rain Attack" };

    [ContextMenu("측정해서 다시 채우기")]
    void Refill()
    {
        UnityEditor.Undo.RecordObject(this, "음량표 측정");
        foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", Folders))
        {
            var c = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
            if (c != null && entries.Find(x => x.clip == c) == null)
                entries.Add(new Entry { clip = c, kind = Array.IndexOf(BgmNames, c.name) >= 0 ? Kind.Bgm : Array.IndexOf(SoftNames, c.name) >= 0 ? Kind.Soft : Kind.Sfx });
        }
        entries.RemoveAll(x => x.clip == null);
        foreach (var e in entries)
        {
            if (!Measure(e.clip, out float loud, out float peak)) { Debug.LogWarning("[음량표] 못 읽음: " + e.clip.name, e.clip); continue; }
            e.measuredDb = Round(loud);
            float want = TargetFor(e.kind) - loud;
            e.autoDb = Round(Mathf.Min(want, peakCeiling - peak - e.extraDb));
        }
        entries.Sort((a, b) => a.kind != b.kind ? a.kind.CompareTo(b.kind) : string.Compare(a.clip.name, b.clip.name, StringComparison.Ordinal));
        UnityEditor.EditorUtility.SetDirty(this);
    }

    static float Round(float v) => Mathf.Round(v * 10f) / 10f;

    /// <summary>50ms 구간마다 크기를 재서, 조용한 구간(-50dB 아래, 그리고 평균보다 10dB 아래)을 빼고 평균낸 크기와 봉우리.</summary>
    static bool Measure(AudioClip c, out float loud, out float peak)
    {
        loud = peak = -100f;
        if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
        var data = new float[c.samples * c.channels];
        if (!c.GetData(data, 0)) return false;
        int win = Mathf.Max(1, c.frequency / 20) * c.channels;
        var powers = new List<double>();
        float pk = 0f;
        for (int s = 0; s < data.Length; s += win)
        {
            double sum = 0; int n = Mathf.Min(win, data.Length - s);
            for (int i = 0; i < n; i++) { float x = data[s + i]; sum += x * x; float a = Math.Abs(x); if (a > pk) pk = a; }
            powers.Add(sum / n);
        }
        double Gated(double gate)
        {
            double sum = 0; int n = 0;
            foreach (var p in powers) if (p > gate) { sum += p; n++; }
            return n > 0 ? sum / n : 0;
        }
        double m = Gated(Math.Pow(10, -50 / 10.0));
        if (m <= 0) return false;
        double m2 = Gated(m * 0.1);
        loud = (float)(10 * Math.Log10(m2 > 0 ? m2 : m));
        peak = pk > 0 ? 20f * Mathf.Log10(pk) : -100f;
        return true;
    }
#endif
}
