using MicroWorldNS;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour // �N���X����WorldManager�̂܂܂Ƃ��܂�
{
    [Header("�ǐՑΏ�")]
    [Tooltip("�n�`�����̒��S�ƂȂ�I�u�W�F�N�g�i�D�Ȃǁj")]
    [SerializeField] private Transform trackingTarget;
    [Tooltip("�D�̈ړ����Ǘ�����ShipMovement�X�N���v�g")]
    [SerializeField] private ShipMovement shipMovement;

    [Header("���v���n�u�ݒ�")]
    [SerializeField] private List<MicroWorld> islandPrefabs;

    [Header("�C�v���n�u�ݒ�")]
    [SerializeField] private GameObject oceanTilePrefab;

    [Header("���[���h�����ݒ�")]
    [SerializeField] private float islandBaseHeight = 30f;
    [SerializeField] private float oceanBaseHeight = 0f;
    [SerializeField] private float oceanTileSize = 150f;

    [Header("�� �����E�폜���[��")]
    [SerializeField] private float generationDistance = 100f;
    // Tooltip�𕪂���₷���ύX
    [Tooltip("���̋��������D�́w����x�ɂȂ��������폜���܂�")]
    [SerializeField] private float deletionDistance = 200f; // �f�t�H���g�l��200�ɕύX

    [Header("�C�̊Ǘ��ݒ�")]
    [Tooltip("�D���炱�̔��a�̊O���ɂ���C�̃^�C�����폜���܂�")]
    [SerializeField] private float oceanKeepRadius = 150f;

    // --- �����ϐ� ---
    private MicroWorld currentIsland;
    private bool isSwitchingWorlds = false;
    private Dictionary<Vector2Int, GameObject> oceanTilesByCoords = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int currentTargetCoords;
    private bool initialSpawnComplete = false; // �ŏ��̐������������������Ǘ�����t���O

    private void Start()
    {
        if (trackingTarget == null || shipMovement == null || islandPrefabs == null || islandPrefabs.Count == 0 || oceanTilePrefab == null)
        {
            Debug.LogError("WorldManager�ɕK�v�Ȑݒ肪����Ă��܂���B�C���X�y�N�^�[���m�F���Ă��������B");
            return;
        }

        Debug.Log("--- �Q�[���J�n: �ŏ��̓��𐶐����܂� ---");
        Vector3 initialSpawnPos = trackingTarget.TransformPoint(new Vector3(0, 0, generationDistance));
        // �����ł͐������J�n���邾���ŁA������҂��Ȃ�
        StartCoroutine(SpawnNewIslandRoutine(initialSpawnPos));
    }

    private void Update()
    {
        // �ŏ��̐�������������܂ł�Update�̔�����s��Ȃ�
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

        // �v���n�u�͈�U�A���_�ɐ������܂��i�ʒu�͒����BuildAtPositionAsync�Ŏw�肷�邽�߁j
        currentIsland = Instantiate(prefabToBuild, Vector3.zero, Quaternion.identity);

        // Y���W�����ݒ肵����
        Vector3 spawnPosition = new Vector3(position.x, islandBaseHeight, position.z);

        currentIsland.Seed = (int)(spawnPosition.x + spawnPosition.z);

        // �V������������߂��g���A�u���̍��W�ɒn�`�����v�Ɩ��m�Ɏw�����܂�
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

            // --- �������������d�v������ ---
            // �����ŏ��̐������܂��������Ă��Ȃ���΁A�����őD�ƊC���n��������
            if (!initialSpawnComplete)
            {
                initialSpawnComplete = true;
                Debug.Log("�ŏ��̓��̏��������B�D�̈ړ��ƊC�̊Ǘ����J�n���܂��B");
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
        // --- �������C�̍폜���W�b�N���C�������� ---
        List<Vector2Int> oceansToRemove = new List<Vector2Int>();
        foreach (var kvp in oceanTilesByCoords)
        {
            Vector3 tileWorldPos = kvp.Value.transform.position;
            // �D�̌��ݒn����́u���ۂ̋����v���v�Z
            float distanceToShip = Vector3.Distance(new Vector3(tileWorldPos.x, 0, tileWorldPos.z), new Vector3(trackingTarget.position.x, 0, trackingTarget.position.z));

            // �������w�肵�����a�𒴂�����폜���X�g�ɒǉ�
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

        // �V�����C�̃^�C���������W�b�N�i�O���b�h�x�[�X�Ŕ͈͂��`�F�b�N�j
        Vector2Int currentTargetCoords = GetCoordsFromPosition(trackingTarget.position);
        int oceanGridRadius = Mathf.CeilToInt(oceanKeepRadius / oceanTileSize); // ���a����O���b�h�����v�Z

        for (int x = -oceanGridRadius; x <= oceanGridRadius; x++)
        {
            for (int z = -oceanGridRadius; z <= oceanGridRadius; z++)
            {
                Vector2Int coord = new Vector2Int(currentTargetCoords.x + x, currentTargetCoords.y + z);
                Vector3 tileWorldPos = new Vector3(coord.x * oceanTileSize, oceanBaseHeight, coord.y * oceanTileSize);
                float distanceToShip = Vector3.Distance(new Vector3(tileWorldPos.x, 0, tileWorldPos.z), new Vector3(trackingTarget.position.x, 0, trackingTarget.position.z));

                // ���Ƀ^�C�����Ȃ��A���~�`�͈͂̓����Ȃ琶��
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