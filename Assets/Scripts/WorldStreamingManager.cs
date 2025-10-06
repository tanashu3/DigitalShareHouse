using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MicroWorldNS
{
    public class WorldStreamingManager : MonoBehaviour
    {
        [Header("�ΏۃI�u�W�F�N�g")]
        [Tooltip("�n�`�����̒��S�ƂȂ�D��Transform")]
        [SerializeField] Transform ShipTransform;

        [Header("���v���n�u�ݒ�")]
        [SerializeField] List<MicroWorld> IslandPrefabs;

        [Header("�C�v���n�u�ݒ�")]
        [SerializeField] GameObject OceanTilePrefab;

        [Header("���[���h�����ݒ�")]
        [Tooltip("���������������Y���W")]
        [SerializeField] float IslandBaseHeight = 30f;
        [Tooltip("�C�������������Y���W")]
        [SerializeField] float OceanBaseHeight = 0f;
        [Tooltip("�C�̃^�C��1���̃T�C�Y")]
        [SerializeField] float OceanTileSize = 150f;

        [Header("�� �����E�폜���[��")]
        [Tooltip("���̋��������D�̑O���ɓ��𐶐����܂�")]
        [SerializeField] float GenerationDistance = 100f;
        [Tooltip("���̋��������D�̌���ɂȂ��������폜���܂�")]
        [SerializeField] float DeletionDistance = 100f;

        // --- �����ϐ� ---
        private MicroWorld currentIsland;
        private Dictionary<Vector2Int, GameObject> oceanTilesByCoords = new Dictionary<Vector2Int, GameObject>();
        private Vector2Int currentShipCoords;


        private void Start()
        {
            if (ShipTransform == null || IslandPrefabs == null || IslandPrefabs.Count == 0 || OceanTilePrefab == null)
            {
                Debug.LogError("�K�v�ȃR���|�[�l���g���ݒ肳��Ă��܂���B�C���X�y�N�^�[���m�F���Ă��������B");
                return;
            }

            // �ŏ��̓����A�D�̐i�s�����̎w�肳�ꂽ�����ɐ���
            Vector3 initialSpawnPos = ShipTransform.position + (ShipTransform.forward * GenerationDistance);
            SpawnNewIsland(initialSpawnPos);

            // �C�̊Ǘ����J�n
            StartCoroutine(UpdateOceanRoutine());
        }

        private void Update()
        {
            // 1. �������݂��Ȃ��A�܂��̓r���h���Ȃ牽�����Ȃ�
            if (currentIsland == null || !currentIsland.IsBuilt)
            {
                return;
            }

            // 2. �D���瓇�ւ̃x�N�g���Ƌ������v�Z
            Vector3 toIsland = currentIsland.transform.position - ShipTransform.position;
            float distance = toIsland.magnitude;

            // 3. �����D�̐i�s�����ɑ΂��āu���v�ɂ��邩����
            //    Vector3.Dot�́A2�̃x�N�g�����ǂꂭ�炢���������������Ă��邩������
            //    ���ʂ��}�C�i�X�Ȃ�u�t�����v�����͑D�̌��ɂ���
            float dotProduct = Vector3.Dot(toIsland.normalized, ShipTransform.forward);

            // 4. �����u���v�ɂ���A���u�폜�����v�𒴂�����폜���V�K����
            if (dotProduct < 0 && distance > DeletionDistance)
            {
                // �Â������폜
                Destroy(currentIsland.gameObject);
                currentIsland = null;

                // �V��������D�̑O���ɐ���
                Vector3 newIslandPos = ShipTransform.position + (ShipTransform.forward * GenerationDistance);
                SpawnNewIsland(newIslandPos);
            }
        }

        private void SpawnNewIsland(Vector3 position)
        {
            MicroWorld prefabToBuild = IslandPrefabs[Random.Range(0, IslandPrefabs.Count)];

            // ����̓O���b�h�ɃX�i�b�v�����A�v�Z�ʂ�̐��m�Ȉʒu�ɐ�������
            Vector3 spawnPosition = new Vector3(position.x, IslandBaseHeight, position.z);

            currentIsland = Instantiate(prefabToBuild, spawnPosition, Quaternion.identity);

            // �V�[�h�l�͈ʒu�Ɋ�Â��ă����_���ɐݒ�
            currentIsland.Seed = (int)(position.x * 100 + position.z * 100);
            currentIsland.BuildAsync();
        }


        // --- �ȉ��A�C�̊Ǘ����W�b�N ---
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