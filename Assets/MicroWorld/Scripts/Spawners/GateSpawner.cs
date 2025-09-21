using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// Spawns Gate prefab in cells of Gate type
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.c3tllnhhbrb9")]
    public class GateSpawner : BaseSpawner
    {
        [Tooltip("Gate prefab. It can be empty.")]
        [SerializeField] GameObject defaultGatePrefab;

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);

            CheckMapSpawner();

            if (defaultGatePrefab == null)
                defaultGatePrefab = Resources.Load<GameObject>("Gate");

            foreach (var hex in Map.AllHex())
                if (Map[hex].Type == Builder.MapSpawner.GateCellType)
                    BuildGate(hex);
        }

        private void BuildGate(Vector2Int hex)
        {
            var cell = Map[hex];
            for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge ++)
            {
                if (CellGeometry.Neighbor(hex, iEdge) != cell.Parent) continue;

                var gateInfo = Builder.Gates.FirstOrDefault(g => g.Cell == hex);
                if (gateInfo == null) continue;

                var p0 = CellGeometry.Corner(hex, iEdge);
                var p1 = CellGeometry.Corner(hex, iEdge + 1);
                var pos = (p0 + p1) / 2;
                var dir = (CellGeometry.Center(hex) - pos).normalized;
                pos.y = cell.Height;

                var prefab = gateInfo.GatePrefab == null ? defaultGatePrefab : gateInfo.GatePrefab;
                var obj = Instantiate(prefab, pos, Quaternion.LookRotation(dir, Vector3.up), Builder.Terrain.transform);
                var gate = obj.GetComponentInChildren<Gate>(true);
                gateInfo.Gate = gate;
                gate.GateInfo = gateInfo;
                gate.World = Builder;
            }
        }
    }
}
