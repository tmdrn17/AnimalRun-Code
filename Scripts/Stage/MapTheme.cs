using System;
using UnityEngine;

/// <summary>
/// 한 맵 테마에서 쓰는 청크 묶음. ChunkManager가 레벨마다 테마를 골라 이 청크들로 맵을 만든다.
/// 청크 규칙(길이, 문, 코인, 결승선)은 기본 청크와 같고 바닥색과 길 옆 장식만 다르다.
/// 조명을 켜 두면 테마 분위기(밤, 안개 등)에 맞게 씬 조명도 바꾼다.
/// </summary>
[CreateAssetMenu(fileName = "Map Theme", menuName = "Animal Run/Map Theme")]
public class MapTheme : ScriptableObject
{
    [Serializable]
    public class ThemeLighting
    {
        [Tooltip("끄면 씬에 설정된 조명을 그대로 쓴다.")]
        public bool enabled;

        [Header(" Sun ")]
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;

        [Header(" Ambient ")]
        public Color ambientColor = new Color(0.5f, 0.5f, 0.5f);

        [Header(" Fog / Background ")]
        public bool useFog;
        public Color fogColor = Color.gray;
        public float fogStart = 30f;
        public float fogEnd = 90f;
        [Tooltip("켜면 하늘(스카이박스) 대신 이 색으로 배경을 칠한다. 안개색과 맞추면 지평선이 자연스럽게 묻힌다.")]
        public bool useSolidBackground;
        public Color backgroundColor = Color.black;
    }

    [SerializeField] private string displayName;
    [SerializeField] private Chunk firstChunk;
    [SerializeField] private Chunk[] randomChunks;
    [SerializeField] private Chunk finishChunk;
    [Tooltip("이 테마에 나오는 적. 비어 있으면 EnemyGroup 프리팹에 설정된 기본 적(농부)이 나온다.")]
    [SerializeField] private Enemy[] enemyPrefabs;
    [SerializeField] private ThemeLighting lighting = new ThemeLighting();

    public string DisplayName => displayName;
    public Chunk FirstChunk => firstChunk;
    public Chunk[] RandomChunks => randomChunks;
    public Chunk FinishChunk => finishChunk;
    public Enemy[] EnemyPrefabs => enemyPrefabs;
    public ThemeLighting Lighting => lighting;

    public bool IsValid()
    {
        if (firstChunk == null || finishChunk == null || randomChunks == null || randomChunks.Length == 0)
            return false;

        foreach (Chunk chunk in randomChunks)
        {
            if (chunk == null)
                return false;
        }

        return true;
    }

    /// <summary>테마 조명을 씬에 적용한다. 조명이 꺼져 있으면 아무것도 바꾸지 않는다.</summary>
    public void ApplyLighting(Light sun, Camera targetCamera)
    {
        if (lighting == null || lighting.enabled == false)
            return;

        if (sun != null)
        {
            sun.color = lighting.sunColor;
            sun.intensity = lighting.sunIntensity;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = lighting.ambientColor;

        RenderSettings.fog = lighting.useFog;
        if (lighting.useFog)
        {
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = lighting.fogColor;
            RenderSettings.fogStartDistance = lighting.fogStart;
            RenderSettings.fogEndDistance = lighting.fogEnd;
        }

        if (targetCamera != null && lighting.useSolidBackground)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = lighting.backgroundColor;
        }
    }

#if UNITY_EDITOR
    /// <summary>에디터 빌더(ChunkThemeBuilder)가 생성한 청크를 채워 넣을 때 쓴다.</summary>
    public void EditorSetup(string newDisplayName, Chunk newFirstChunk, Chunk[] newRandomChunks, Chunk newFinishChunk, ThemeLighting newLighting)
    {
        displayName = newDisplayName;
        firstChunk = newFirstChunk;
        randomChunks = newRandomChunks;
        finishChunk = newFinishChunk;
        lighting = newLighting ?? new ThemeLighting();
    }
#endif
}
