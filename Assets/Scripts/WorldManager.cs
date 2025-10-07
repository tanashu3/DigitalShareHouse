using MicroWorldNS;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour // クラス名はWorldManagerのままとします
{
    [Header("追跡対象")]
    [Tooltip("地形生成の中心となるオブジェクト（船など）")]
    [SerializeField] private Transform trackingTarget;
    [Tooltip("船の移動を管理するShipMovementスクリプト")]
    [SerializeField] private ShipMovement shipMovement;

    [Header("島プレハブ設定")]
    [SerializeField] private List<MicroWorld> islandPrefabs;

    [Header("海プレハブ設定")]
    [SerializeField] private GameObject oceanTilePrefab;

    [Header("ワールド生成設定")]
    [SerializeField] private float islandBaseHeight = 30f;
    [SerializeField] private float oceanBaseHeight = 0f;
    [SerializeField] private float oceanTileSize = 150f;

    [Header("島 生成・削除ルール")]
    [SerializeField] private float generationDistance = 100f;
    // Tooltipを分かりやすく変更
    [Tooltip("この距離だけ船の『後方』になった島を削除します")]
    [SerializeField] private float deletionDistance = 200f; // デフォルト値を200に変更

    [Header("海の管理設定")]
    [Tooltip("船からこの半径の外側にある海のタイルを削除します")]
    [SerializeField] private float oceanKeepRadius = 150f;

    // --- 内部変数 ---
    private MicroWorld currentIsland;
    private bool isSwitchingWorlds = false;
    private Dictionary<Vector2Int, GameObject> oceanTilesByCoords = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int currentTargetCoords;
    private bool initialSpawnComplete = false; // 最初の生成が完了したかを管理するフラグ

    private void Start()
    {
        if (trackingTarget == null || shipMovement == null || islandPrefabs == null || islandPrefabs.Count == 0 || oceanTilePrefab == null)
        {
            Debug.LogError("WorldManagerに必要な設定がされていません。インスペクターを確認してください。");
            return;
        }

        Debug.Log("--- ゲーム開始: 最初の島を生成します ---");
        Vector3 initialSpawnPos = trackingTarget.TransformPoint(new Vector3(0, 0, generationDistance));
        // ここでは生成を開始するだけで、完了を待たない
        StartCoroutine(SpawnNewIslandRoutine(initialSpawnPos));
    }

    private void Update()
    {
        // 最初の生成が完了するまではUpdateの判定も行わない
        if (!initialSpawnComplete || isSwitchingWorlds || currentIsland == null || !currentIsland.IsBuilt)
        {
            return;
        }

        Vector3 toIsland = currentIsland.transform.position - trackingTarget.position;
        float distanceBehind = Vector3.Dot(toIsland, -trackingTarget.forward);

        if (distanceBehind > deletionDistance)
        {
            StartCoroutine(SwitchIslandRoutine());
        }
    }

    private IEnumerator SwitchIslandRoutine()
    {
        isSwitchingWorlds = true;
        Destroy(currentIsland.gameObject);
        currentIsland = null;

        Vector3 newIslandPos = trackingTarget.TransformPoint(new Vector3(0, 0, generationDistance));
        yield return StartCoroutine(SpawnNewIslandRoutine(newIslandPos));

        isSwitchingWorlds = false;
    }

    private IEnumerator SpawnNewIslandRoutine(Vector3 position)
    {
        MicroWorld prefabToBuild = islandPrefabs[Random.Range(0, islandPrefabs.Count)];

        // プレハブは一旦、原点に生成します（位置は直後にBuildAtPositionAsyncで指定するため）
        currentIsland = Instantiate(prefabToBuild, Vector3.zero, Quaternion.identity);

        // Y座標だけ設定し直す
        Vector3 spawnPosition = new Vector3(position.x, islandBaseHeight, position.z);

        currentIsland.Seed = (int)(spawnPosition.x + spawnPosition.z);

        // 新しく作った命令を使い、「この座標に地形を作れ」と明確に指示します
        currentIsland.BuildAtPositionAsync(spawnPosition, null, false);

        while (currentIsland != null && !currentIsland.IsBuilt)
        {
            yield return null;
        }

        if (currentIsland != null)
        {
            if (currentIsland.Terrain != null)
            {
                currentIsland.Terrain.gameObject.SetActive(true);
            }

            // --- ★★★ここが重要★★★ ---
            // もし最初の生成がまだ完了していなければ、ここで船と海を始動させる
            if (!initialSpawnComplete)
            {
                initialSpawnComplete = true;
                Debug.Log("最初の島の準備完了。船の移動と海の管理を開始します。");
                shipMovement.StartMoving();
                StartCoroutine(UpdateOceanRoutine());
            }
        }
    }
    private IEnumerator UpdateOceanRoutine()
    {
        while (true)
        {
            UpdateOceanGrid();
            yield return new WaitForSeconds(1.0f);
        }
    }

    private Vector2Int GetCoordsFromPosition(Vector3 position)
    {
        int x = Mathf.RoundToInt(position.x / oceanTileSize);
        int z = Mathf.RoundToInt(position.z / oceanTileSize);
        return new Vector2Int(x, z);
    }

    private void UpdateOceanGrid()
    {
        // --- ★★★海の削除ロジックを修正★★★ ---
        List<Vector2Int> oceansToRemove = new List<Vector2Int>();
        foreach (var kvp in oceanTilesByCoords)
        {
            Vector3 tileWorldPos = kvp.Value.transform.position;
            // 船の現在地からの「実際の距離」を計算
            float distanceToShip = Vector3.Distance(new Vector3(tileWorldPos.x, 0, tileWorldPos.z), new Vector3(trackingTarget.position.x, 0, trackingTarget.position.z));

            // 距離が指定した半径を超えたら削除リストに追加
            if (distanceToShip > oceanKeepRadius)
            {
                oceansToRemove.Add(kvp.Key);
            }
        }

        foreach (var coord in oceansToRemove)
        {
            if (oceanTilesByCoords.ContainsKey(coord) && oceanTilesByCoords[coord] != null)
            {
                Destroy(oceanTilesByCoords[coord]);
                oceanTilesByCoords.Remove(coord);
            }
        }

        // 新しい海のタイル生成ロジック（グリッドベースで範囲をチェック）
        Vector2Int currentTargetCoords = GetCoordsFromPosition(trackingTarget.position);
        int oceanGridRadius = Mathf.CeilToInt(oceanKeepRadius / oceanTileSize); // 半径からグリッド数を計算

        for (int x = -oceanGridRadius; x <= oceanGridRadius; x++)
        {
            for (int z = -oceanGridRadius; z <= oceanGridRadius; z++)
            {
                Vector2Int coord = new Vector2Int(currentTargetCoords.x + x, currentTargetCoords.y + z);
                Vector3 tileWorldPos = new Vector3(coord.x * oceanTileSize, oceanBaseHeight, coord.y * oceanTileSize);
                float distanceToShip = Vector3.Distance(new Vector3(tileWorldPos.x, 0, tileWorldPos.z), new Vector3(trackingTarget.position.x, 0, trackingTarget.position.z));

                // 既にタイルがなく、かつ円形範囲の内側なら生成
                if (!oceanTilesByCoords.ContainsKey(coord) && distanceToShip <= oceanKeepRadius)
                {
                    GameObject oceanTile = Instantiate(oceanTilePrefab);
                    oceanTile.transform.position = tileWorldPos;
                    oceanTilesByCoords[coord] = oceanTile;
                }
            }
        }
    }
}