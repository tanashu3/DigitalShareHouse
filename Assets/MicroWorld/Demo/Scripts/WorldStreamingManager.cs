using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MicroWorldNS
{
    public class WorldStreamingManager : MonoBehaviour
    {
        [Header("対象オブジェクト")]
        [SerializeField] Transform PlayerTransform;

        [Header("島プレハブ設定")]
        [SerializeField] List<MicroWorld> IslandPrefabs;

        [Header("海プレハブ設定")]
        [SerializeField] GameObject OceanTilePrefab;

        [Header("ワールド生成設定")]
        [Tooltip("ワールド1マスあたりのサイズ（メートル）")]
        [SerializeField] float WorldSize = 150f;

        [Tooltip("島が生成される基準のY座標")]
        [SerializeField] float IslandBaseHeight = 5f;

        [Tooltip("海が生成される基準のY座標")]
        [SerializeField] float OceanBaseHeight = 0f;

        [Header("島 生成・削除ルール")]
        [Tooltip("プレイヤーが現在いる島からこの距離だけ離れたら、次の島を生成します")]
        [SerializeField] float GenerationTriggerDistance = 100f;

        [Tooltip("生成する島を、プレイヤーの前方どれくらいの位置に出現させるか")]
        [SerializeField] float GenerationForwardDistance = 150f;

        // --- 内部変数 ---
        private MicroWorld currentIsland;
        private Vector3 currentIslandCenter;

        private Dictionary<Vector2Int, GameObject> oceanTilesByCoords = new Dictionary<Vector2Int, GameObject>();
        private Vector2Int currentPlayerCoords;
        private bool isGeneratingIsland = false;


        private void Start()
        {
            if (PlayerTransform == null || IslandPrefabs == null || IslandPrefabs.Count == 0 || OceanTilePrefab == null)
            {
                Debug.LogError("必要なコンポーネントが設定されていません。インスペクターを確認してください。");
                return;
            }

            // 最初の島をプレイヤーの真下に生成
            SpawnNewIsland(PlayerTransform.position);

            // 海の管理を開始
            StartCoroutine(UpdateOceanRoutine());
        }

        private void Update()
        {
            // 現在の島が存在し、かつ新しい島を生成中でない場合
            if (currentIsland != null && !isGeneratingIsland)
            {
                // 島とプレイヤーの水平距離を計算
                float distance = Vector3.Distance(new Vector3(PlayerTransform.position.x, 0, PlayerTransform.position.z), new Vector3(currentIslandCenter.x, 0, currentIslandCenter.z));

                // プレイヤーが島から一定距離離れたら、次の島の生成をトリガー
                if (distance > GenerationTriggerDistance)
                {
                    // 以前の島を破棄
                    Destroy(currentIsland.gameObject);
                    currentIsland = null;

                    // プレイヤーの進行方向に新しい島を生成
                    Vector3 newIslandPos = PlayerTransform.position + (PlayerTransform.forward * GenerationForwardDistance);
                    SpawnNewIsland(newIslandPos);
                }
            }
        }

        private void SpawnNewIsland(Vector3 position)
        {
            isGeneratingIsland = true;

            // プレハブをランダムに選択
            MicroWorld prefabToBuild = IslandPrefabs[Random.Range(0, IslandPrefabs.Count)];

            // ワールドサイズに合わせて座標をスナップ（グリッド化）
            Vector2Int coords = GetCoordsFromPosition(position);
            Vector3 spawnPosition = new Vector3(coords.x * WorldSize, IslandBaseHeight, coords.y * WorldSize);

            // 生成
            currentIsland = Instantiate(prefabToBuild, spawnPosition, Quaternion.identity);
            currentIsland.Seed = coords.x * 10000 + coords.y;
            currentIsland.BuildAsync();
            currentIslandCenter = spawnPosition;

            // 生成完了を待ってフラグをリセット
            StartCoroutine(WaitForIslandBuild());
        }

        private IEnumerator WaitForIslandBuild()
        {
            while (!currentIsland.IsBuilt)
            {
                yield return null;
            }
            isGeneratingIsland = false;
        }

        // --- ここから下は海の管理 ---
        private IEnumerator UpdateOceanRoutine()
        {
            while (true)
            {
                Vector3 playerXZPosition = new Vector3(PlayerTransform.position.x, 0, PlayerTransform.position.z);
                Vector2Int newPlayerCoords = GetCoordsFromPosition(playerXZPosition);

                if (newPlayerCoords != currentPlayerCoords)
                {
                    currentPlayerCoords = newPlayerCoords;
                    UpdateOceanGrid();
                }
                yield return new WaitForSeconds(1.0f);
            }
        }

        private void UpdateOceanGrid()
        {
            int oceanKeepRadius = 2; // 海は5x5の範囲で維持する

            // 不要な海タイルを削除
            List<Vector2Int> oceansToRemove = new List<Vector2Int>();
            foreach (var coord in oceanTilesByCoords.Keys)
            {
                if (Mathf.Abs(coord.x - currentPlayerCoords.x) > oceanKeepRadius || Mathf.Abs(coord.y - currentPlayerCoords.y) > oceanKeepRadius)
                {
                    oceansToRemove.Add(coord);
                }
            }
            foreach (var coord in oceansToRemove)
            {
                Destroy(oceanTilesByCoords[coord]);
                oceanTilesByCoords.Remove(coord);
            }

            // 新しい海タイルを生成
            for (int x = -oceanKeepRadius; x <= oceanKeepRadius; x++)
            {
                for (int z = -oceanKeepRadius; z <= oceanKeepRadius; z++)
                {
                    Vector2Int coord = new Vector2Int(currentPlayerCoords.x + x, currentPlayerCoords.y + z);
                    if (!oceanTilesByCoords.ContainsKey(coord))
                    {
                        GameObject oceanTile = Instantiate(OceanTilePrefab);
                        oceanTile.transform.position = new Vector3(coord.x * WorldSize, OceanBaseHeight, coord.y * WorldSize);
                        oceanTilesByCoords[coord] = oceanTile;
                    }
                }
            }
        }

        private Vector2Int GetCoordsFromPosition(Vector3 position)
        {
            int x = Mathf.RoundToInt(position.x / WorldSize);
            int z = Mathf.RoundToInt(position.z / WorldSize);
            return new Vector2Int(x, z);
        }
    }
}