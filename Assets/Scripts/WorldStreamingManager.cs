using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MicroWorldNS
{
    public class WorldStreamingManager : MonoBehaviour
    {
        [Header("対象オブジェクト")]
        [Tooltip("地形生成の中心となる船のTransform")]
        [SerializeField] Transform ShipTransform;

        [Header("島プレハブ設定")]
        [SerializeField] List<MicroWorld> IslandPrefabs;

        [Header("海プレハブ設定")]
        [SerializeField] GameObject OceanTilePrefab;

        [Header("ワールド生成設定")]
        [Tooltip("島が生成される基準のY座標")]
        [SerializeField] float IslandBaseHeight = 30f;
        [Tooltip("海が生成される基準のY座標")]
        [SerializeField] float OceanBaseHeight = 0f;
        [Tooltip("海のタイル1枚のサイズ")]
        [SerializeField] float OceanTileSize = 150f;

        [Header("島 生成・削除ルール")]
        [Tooltip("この距離だけ船の前方に島を生成します")]
        [SerializeField] float GenerationDistance = 100f;
        [Tooltip("この距離だけ船の後方になった島を削除します")]
        [SerializeField] float DeletionDistance = 100f;

        // --- 内部変数 ---
        private MicroWorld currentIsland;
        private Dictionary<Vector2Int, GameObject> oceanTilesByCoords = new Dictionary<Vector2Int, GameObject>();
        private Vector2Int currentShipCoords;


        private void Start()
        {
            if (ShipTransform == null || IslandPrefabs == null || IslandPrefabs.Count == 0 || OceanTilePrefab == null)
            {
                Debug.LogError("必要なコンポーネントが設定されていません。インスペクターを確認してください。");
                return;
            }

            // 最初の島を、船の進行方向の指定された距離に生成
            Vector3 initialSpawnPos = ShipTransform.position + (ShipTransform.forward * GenerationDistance);
            SpawnNewIsland(initialSpawnPos);

            // 海の管理を開始
            StartCoroutine(UpdateOceanRoutine());
        }

        private void Update()
        {
            // 1. 島が存在しない、またはビルド中なら何もしない
            if (currentIsland == null || !currentIsland.IsBuilt)
            {
                return;
            }

            // 2. 船から島へのベクトルと距離を計算
            Vector3 toIsland = currentIsland.transform.position - ShipTransform.position;
            float distance = toIsland.magnitude;

            // 3. 島が船の進行方向に対して「後ろ」にあるか判定
            //    Vector3.Dotは、2つのベクトルがどれくらい同じ方向を向いているかを示す
            //    結果がマイナスなら「逆方向」＝島は船の後ろにある
            float dotProduct = Vector3.Dot(toIsland.normalized, ShipTransform.forward);

            // 4. 島が「後ろ」にあり、かつ「削除距離」を超えたら削除＆新規生成
            if (dotProduct < 0 && distance > DeletionDistance)
            {
                // 古い島を削除
                Destroy(currentIsland.gameObject);
                currentIsland = null;

                // 新しい島を船の前方に生成
                Vector3 newIslandPos = ShipTransform.position + (ShipTransform.forward * GenerationDistance);
                SpawnNewIsland(newIslandPos);
            }
        }

        private void SpawnNewIsland(Vector3 position)
        {
            MicroWorld prefabToBuild = IslandPrefabs[Random.Range(0, IslandPrefabs.Count)];

            // 今回はグリッドにスナップせず、計算通りの正確な位置に生成する
            Vector3 spawnPosition = new Vector3(position.x, IslandBaseHeight, position.z);

            currentIsland = Instantiate(prefabToBuild, spawnPosition, Quaternion.identity);

            // シード値は位置に基づいてランダムに設定
            currentIsland.Seed = (int)(position.x * 100 + position.z * 100);
            currentIsland.BuildAsync();
        }


        // --- 以下、海の管理ロジック ---
        private IEnumerator UpdateOceanRoutine()
        {
            while (true)
            {
                Vector3 shipXZPosition = new Vector3(ShipTransform.position.x, 0, ShipTransform.position.z);
                Vector2Int newShipCoords = GetCoordsFromPosition(shipXZPosition);
                if (newShipCoords != currentShipCoords)
                {
                    currentShipCoords = newShipCoords;
                    UpdateOceanGrid();
                }
                yield return new WaitForSeconds(1.0f);
            }
        }

        private Vector2Int GetCoordsFromPosition(Vector3 position)
        {
            int x = Mathf.RoundToInt(position.x / OceanTileSize);
            int z = Mathf.RoundToInt(position.z / OceanTileSize);
            return new Vector2Int(x, z);
        }

        private void UpdateOceanGrid()
        {
            int oceanKeepRadius = 2;
            List<Vector2Int> oceansToRemove = new List<Vector2Int>();
            foreach (var coord in oceanTilesByCoords.Keys)
            {
                if (Mathf.Abs(coord.x - currentShipCoords.x) > oceanKeepRadius || Mathf.Abs(coord.y - currentShipCoords.y) > oceanKeepRadius)
                {
                    oceansToRemove.Add(coord);
                }
            }
            foreach (var coord in oceansToRemove)
            {
                if (oceanTilesByCoords.ContainsKey(coord))
                {
                    Destroy(oceanTilesByCoords[coord]);
                    oceanTilesByCoords.Remove(coord);
                }
            }
            for (int x = -oceanKeepRadius; x <= oceanKeepRadius; x++)
            {
                for (int z = -oceanKeepRadius; z <= oceanKeepRadius; z++)
                {
                    Vector2Int coord = new Vector2Int(currentShipCoords.x + x, currentShipCoords.y + z);
                    if (!oceanTilesByCoords.ContainsKey(coord))
                    {
                        GameObject oceanTile = Instantiate(OceanTilePrefab);
                        oceanTile.transform.position = new Vector3(coord.x * OceanTileSize, OceanBaseHeight, coord.y * OceanTileSize);
                        oceanTilesByCoords[coord] = oceanTile;
                    }
                }
            }
        }
    }
}