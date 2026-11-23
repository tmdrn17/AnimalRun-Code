using UnityEngine;
using UnityEngine.Serialization;

public class EnemyGroup : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private Transform enemiesParent;

    [Header(" Settings ")]
    [SerializeField] private int minAmount = 0;
    [FormerlySerializedAs("amountRange")]
    [SerializeField] private int maxAmount = 5;
    [SerializeField] private float radius;
    [SerializeField] private float angle;

    private int amount;
    private Enemy[] enemyPrefabOverrides;

    /// <summary>
    /// 테마 적 목록으로 바꿔 낀다. Start(적 생성) 전에 불러야 한다. 비어 있으면 기본 enemyPrefab을 쓴다.
    /// </summary>
    public void SetEnemyPrefabs(Enemy[] prefabs)
    {
        enemyPrefabOverrides = prefabs;
    }

    private Enemy PickEnemyPrefab()
    {
        if (enemyPrefabOverrides == null || enemyPrefabOverrides.Length == 0)
            return enemyPrefab;

        Enemy picked = enemyPrefabOverrides[Random.Range(0, enemyPrefabOverrides.Length)];
        return picked != null ? picked : enemyPrefab;
    }

    // Start is called before the first frame update
    private void Start()
    {
        // 최솟값 ~ 최댓값(포함) 사이의 랜덤한 수로 군체를 만든다.
        amount = Random.Range(minAmount, maxAmount + 1);

        GenerateEnemies();
    }

    private void GenerateEnemies()
    {
        for (int i = 0; i < amount; i++)
        {
            Vector3 enemyLocalPosition = GetRunnerLocalPosition(i);

            Vector3 enemyWorldPosition = enemiesParent.TransformPoint(enemyLocalPosition);

            Instantiate(PickEnemyPrefab(), enemyWorldPosition, enemiesParent.rotation, enemiesParent);
        }
    }

    public float GetMaxRadius()
    {
        return radius * Mathf.Sqrt(maxAmount);
    }

    private Vector3 GetRunnerLocalPosition(int index)
    {
        float x = radius * Mathf.Sqrt(index) * Mathf.Cos(Mathf.Deg2Rad * index * angle);
        float z = radius * Mathf.Sqrt(index) * Mathf.Sin(Mathf.Deg2Rad * index * angle);

        return new Vector3(x, 0, z);
    }
}
