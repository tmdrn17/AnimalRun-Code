using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public static ChunkManager instance;

    [Header(" Elements ")]
    [SerializeField] private EnemyGroup enemyGroupPrefab;
    
    [Header(" Themes ")]
    [SerializeField] private MapTheme[] themes;

    [Header(" Enemy Settings ")]
    [Tooltip("이 순서(0부터 시작)의 청크부터 적이 등장한다. 2 = 첫 번째, 두 번째 청크 제외")]
    [SerializeField] private int firstEnemyChunkIndex = 2;
    [SerializeField] private float enemySpawnHalfWidth = 3f;
    [SerializeField] private float enemyDistanceFromDoors = 3f;

    [Header(" Play Time Settings ")]
    [SerializeField] private float referenceMoveSpeed = 5f;
    [SerializeField] private float minPlayTime = 60f;
    [SerializeField] private float maxPlayTime = 90f;

    private Chunk firstChunk;
    private Chunk[] randomChunks;
    private Chunk finishChunk;
    
    private GameObject finishLine;
    private Enemy[] themeEnemyPrefabs;

    private void Awake()
    {
        if (instance != null)
            Destroy(gameObject);
        else
            instance = this;
    }

    private void Start()
    {
        ApplyTheme();
        GenerateLevel();

        finishLine = GameObject.FindWithTag("Finish");
    }

    // 테마가 있으면 이번 레벨의 테마 청크로 기본 청크를 바꿔 끼운다.
    private void ApplyTheme()
    {
        MapTheme theme = GetCurrentTheme();

        themeEnemyPrefabs = theme.EnemyPrefabs;
        firstChunk = theme.FirstChunk;
        randomChunks = theme.RandomChunks;
        finishChunk = theme.FinishChunk;

        theme.ApplyLighting(FindSun(), Camera.main);
    }

    private Light FindSun()
    {
        if (RenderSettings.sun != null)
            return RenderSettings.sun;

        foreach (Light sceneLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (sceneLight.type == LightType.Directional)
                return sceneLight;
        }

        return null;
    }

    public MapTheme GetCurrentTheme()
    {
        int index = Random.Range(0, themes.Length);
        MapTheme theme = themes[index];

        return theme;
    }

    private void GenerateLevel()
    {
        float startZ = PlayerController.instance.transform.position.z;
        float finishLineOffset = GetFinishLineOffset();

        // 플레이어 시작 지점 ~ 결승선 사이 거리 범위 (moveSpeed 5 기준 1분 ~ 1분 30초)
        float minDistance = minPlayTime * referenceMoveSpeed;
        float maxDistance = maxPlayTime * referenceMoveSpeed;
        float targetDistance = Random.Range(minDistance, maxDistance);

        List<Chunk> levelChunks = new List<Chunk> { firstChunk };

        // 첫 청크는 원점에 중심을 두므로 끝 지점은 길이의 절반
        float chunkEndZ = firstChunk.GetLength() / 2;

        while (GetFinishDistance(chunkEndZ, finishLineOffset, startZ) < targetDistance)
        {
            List<Chunk> candidates = new List<Chunk>();

            foreach (Chunk chunk in randomChunks)
            {
                if (GetFinishDistance(chunkEndZ + chunk.GetLength(), finishLineOffset, startZ) <= maxDistance)
                    candidates.Add(chunk);
            }

            if (candidates.Count == 0)
                break;

            Chunk randomChunk = candidates[Random.Range(0, candidates.Count)];
            levelChunks.Add(randomChunk);
            chunkEndZ += randomChunk.GetLength();
        }

        levelChunks.Add(finishChunk);

        CreateLevel(levelChunks);
    }

    private float GetFinishDistance(float chunkEndZ, float finishLineOffset, float startZ)
    {
        return chunkEndZ + finishChunk.GetLength() / 2 + finishLineOffset - startZ;
    }

    private float GetFinishLineOffset()
    {
        foreach (Transform child in finishChunk.GetComponentsInChildren<Transform>(true))
        {
            if (child.CompareTag("Finish"))
                return child.position.z - finishChunk.transform.position.z;
        }

        return 0f;
    }

    private void CreateLevel(List<Chunk> levelChunks)
    {
        Vector3 chunkPosition = Vector3.zero;

        for (int i = 0; i < levelChunks.Count; i++)
        {
            Chunk chunkToCreate = levelChunks[i];

            if (i > 0)
                chunkPosition.z += chunkToCreate.GetLength() / 2;

            Chunk chunkInstance = Instantiate(chunkToCreate, chunkPosition, Quaternion.identity, transform);

            RemovePlacedEnemyGroups(chunkInstance);

            bool isFinishChunk = i == levelChunks.Count - 1;
            if (i >= firstEnemyChunkIndex && !isFinishChunk)
                SpawnEnemyGroup(chunkInstance);

            chunkPosition.z += chunkInstance.GetLength() / 2;
        }
    }

    // 청크 프리팹에 미리 배치된 적 군체는 쓰지 않고, 청크마다 랜덤으로 다시 배치한다.
    private void RemovePlacedEnemyGroups(Chunk chunk)
    {
        foreach (EnemyGroup enemyGroup in chunk.GetComponentsInChildren<EnemyGroup>(true))
        {
            // 비활성화해서 Start(적 생성)가 실행되지 않게 한 뒤 삭제한다.
            enemyGroup.gameObject.SetActive(false);
            Destroy(enemyGroup.gameObject);
        }
    }

    private void SpawnEnemyGroup(Chunk chunk)
    {
        if (enemyGroupPrefab == null)
            return;

        Doors[] doors = chunk.GetComponentsInChildren<Doors>();
        float centerZ = chunk.transform.position.z;
        float margin = enemyGroupPrefab.GetMaxRadius() + 1f;
        float halfLength = Mathf.Max(0f, chunk.GetLength() / 2 - margin);

        float spawnZ = centerZ;

        // 문과 겹치지 않는 위치를 몇 번 시도해서 찾는다.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            spawnZ = centerZ + Random.Range(-halfLength, halfLength);

            bool isFarFromDoors = true;
            foreach (Doors door in doors)
            {
                if (Mathf.Abs(door.transform.position.z - spawnZ) < enemyDistanceFromDoors)
                {
                    isFarFromDoors = false;
                    break;
                }
            }

            if (isFarFromDoors)
                break;
        }

        Vector3 spawnPosition = new Vector3(Random.Range(-enemySpawnHalfWidth, enemySpawnHalfWidth), 0, spawnZ);

        // 적이 플레이어 쪽(-z)을 바라보도록 회전시킨다.
        EnemyGroup enemyGroup = Instantiate(enemyGroupPrefab, spawnPosition, Quaternion.Euler(0, 180, 0), chunk.transform);

        // 적 생성(Start)은 다음 프레임이라 지금 테마 적으로 바꿔 끼우면 반영된다.
        enemyGroup.SetEnemyPrefabs(themeEnemyPrefabs);
    }

    public float GetFinishZ()
    {
        return finishLine.transform.position.z;
    }

    public int GetLevel()
    {
        return PlayerPrefs.GetInt("level", 0);
    }
}
