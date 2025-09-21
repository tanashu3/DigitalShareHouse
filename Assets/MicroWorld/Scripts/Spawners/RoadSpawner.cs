using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// Builds roads/paths between cells of specified types.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.mur423cgb1qy")]
    public class RoadSpawner : BaseSpawner, IExclusive, IBuildPhaseHandler
    {
        public string ExclusiveGroup => "RoadSpawner";
        [field: SerializeField]
        [field: Tooltip("Defines the chance of selecting this spawner among all RoadSpawners of MicroWorld instance.")]
        public float Chance { get; set; } = 1;

        public override int Order => 650;
        [field: SerializeField]
        [field: Tooltip("Algorithm for constructing a road network.")]
        public RoadNetworkType NetworkType { get; set; }
        [ShowIf(nameof(NetworkType), RoadNetworkType.Default, Op = DrawIfOp.AllFalse)]
        [Tooltip("A set of properties used for a Network Type other than Default.")]
        public RoadNetworkData Network = new RoadNetworkData();
        [Tooltip("Specifies the types of cells between which roads are built.")]
        [Popup(nameof(ProposedCellTypes), true)]
        public string[] CellTypes = new string[] { "Ruins", "Gate" };
        [Tooltip("Specifies the radius of the road (meters).")]
        public float RoadRadius = 0.7f;
        [Tooltip("Additional radius of the taken area (meters). Сan be positive and negative. In fact, it defines an additional radius in which vegetation will not spawn.")]
        public float TakenAreaPadding = 0f;
        [Tooltip("Defines multiplier of micro noise amplitude for cells where road is spawned.")]
        public float MicroNoiseScale = 0.1f;
        public RoadSpawnerFeatures Features = RoadSpawnerFeatures.LiftUpRoadsToWaterLevel | RoadSpawnerFeatures.MakeMesh | RoadSpawnerFeatures.MakeCollider;

        [ShowIf(nameof(IsLiftUp))]
        [Tooltip("Cell height above water if LiftUpRoadsToWaterLevel or LiftUpCrossToWaterLevel is turned on (meters).")]
        public float HeightAboveWater = 1f;

        [Header("Road Bed")]
        [SerializeField][Tooltip("Elevation of road bed over terrain surface (meters). As a rule it is negative.")]
        float BedOffsetY = -0.3f;
        [SerializeField][Tooltip("Side padding of bed relative to road radius (meters).")]
        float BedPadding = 1.5f;
        const float BedHorizontality = 1f;

        [Header("Road Mesh")]
        [Tooltip("Prefab of road mesh that contains MeshRenderer with settings of rendering and  MeshCollider with settings of collider. Layer of the prefab will be the layer of collider.")]
        public MeshRenderer RoadPrefab;
        [Tooltip("Material of road surface.")]
        public Material Material;
        [Tooltip("Elevation of road mesh surface over bed bottom (meters). It should be positive.")]
        public float OffsetY = 0.01f;
        [Tooltip("Side padding of road mesh surface relative to road radius (meters).")]
        public float MeshPadding = 0.5f;
        [Range(0, 1)] public float SideIncline = 0.3f;
        public float UVSegmentLength = 5f;

        protected HashSet<string> myCellTypes;

        bool IsLiftUp => (Features & (RoadSpawnerFeatures.LiftUpCrossToWaterLevel | RoadSpawnerFeatures.LiftUpRoadsToWaterLevel)) != 0;
        bool IsCollider => Features.HasFlag(RoadSpawnerFeatures.MakeCollider);
        bool IsMesh => Features.HasFlag(RoadSpawnerFeatures.MakeMesh);
        bool IsMeshOrCollider => IsMesh || IsCollider;

        private void OnValidate()
        {
            if (Network == null)
                Network = new RoadNetworkData();
        }

        public IEnumerator OnPhaseCompleted(BuildPhase phase)
        {
            switch (phase)
            {
                case BuildPhase.CellHeightsCreated:
                    yield return BuildRoadNetwork();
                    break;
                case BuildPhase.TerrainHeightMapCreated:
                    yield return MakeBedOnTerrain();
                    break;
            }
        }

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);

            // build mesh
            var mesh = LineMeshHelper.BuildMesh(TakenAreaType.Road, Builder, UVSegmentLength, RoadRadius + MeshPadding, SideIncline, OffsetY);

            if (IsMeshOrCollider)
            {
                if (!RoadPrefab)
                    RoadPrefab = Resources.Load<MeshRenderer>("Road");

                var go = Instantiate(RoadPrefab, Terrain.transform);
                var mf = go.GetOrAddComponent<MeshFilter>();
                mf.sharedMesh = IsMesh ? mesh : null;
                if (Material)
                    go.sharedMaterial = Material;

                var coll = go.GetOrAddComponent<MeshCollider>();
                coll.sharedMesh = IsCollider ? mesh : null;
            }
        }

        private IEnumerator BuildRoadNetwork()
        {
            CheckMapSpawner();
            CheckTerrainSpawner();

            HashSet<Vector2Int> ruinsCells = null;
            HashSet<Vector2Int> roadCells = null;
            Func<Vector2Int, Vector2Int, bool> areConnected = null;

            switch (NetworkType)
            {
                case RoadNetworkType.Default:
                {
                    myCellTypes = CellTypes.ToHashSet();
                    var myCells = Map.AllHex().Where(p => !Map.IsBorderOrOutside(p) || myCellTypes.Contains(Map[p].Type.Name));
                    ruinsCells = myCells.Where(p => myCellTypes.Contains(Map[p].Type.Name)).ToHashSet();
                    areConnected = (a, b) => Map[a].Parent == b || Map[b].Parent == a;
                    var crossCells = FindCrosses(ruinsCells);
                    roadCells = FindRoads(ruinsCells, crossCells);
                    break;
                }
                case RoadNetworkType.SpanningTree:
                {
                    myCellTypes = CellTypes.ToHashSet();
                    var myCells = Map.AllHex().Where(p => !Map.IsBorderOrOutside(p) || myCellTypes.Contains(Map[p].Type.Name));
                    ruinsCells = myCells.Where(p => myCellTypes.Contains(Map[p].Type.Name)).ToHashSet();
                    var allowedCells = Map.AllHex().Where(p => !Map.IsBorderOrOutside(p)).ToHashSet();
                    allowedCells.AddRange(ruinsCells);
                    var cityCellIndicies = ruinsCells.ToArray();
                    var cityPositions = cityCellIndicies.Select(hex => Map.Geometry.Center(hex)).ToArray();
                    var spanningTree = MicroWorldHelper.BuildSpanningTree(cityPositions);
                    var roadEnds = spanningTree.Select(pair => new RoadEnds() {From = cityCellIndicies[pair.Item1], To = cityCellIndicies[pair.Item2]}).ToList();
                    FindRoadsBetweenCustomCells(roadEnds, allowedCells, out roadCells, out areConnected);
                    break;
                }
                case RoadNetworkType.FullyConnected:
                {
                    myCellTypes = CellTypes.ToHashSet();
                    var myCells = Map.AllHex().Where(p => !Map.IsBorderOrOutside(p) || myCellTypes.Contains(Map[p].Type.Name));
                    ruinsCells = myCells.Where(p => myCellTypes.Contains(Map[p].Type.Name)).ToHashSet();
                    var allowedCells = Map.AllHex().Where(p => !Map.IsBorderOrOutside(p)).ToHashSet();
                    allowedCells.AddRange(ruinsCells);
                    var cityCellIndicies = ruinsCells.ToArray();
                    var graph = MicroWorldHelper.BuildFulyConnectedGraph(cityCellIndicies.Length);
                    var roadEnds = graph.Select(pair => new RoadEnds() { From = cityCellIndicies[pair.Item1], To = cityCellIndicies[pair.Item2] }).ToList();
                    FindRoadsBetweenCustomCells(roadEnds, allowedCells, out roadCells, out areConnected);
                    break;
                }
                case RoadNetworkType.Custom:
                {
                    var allowedCells = Map.AllHex().Where(p => !Map.IsBorderOrOutside(p)).ToHashSet();
                    ruinsCells = Network.RoadEnds.SelectMany(c => new Vector2Int[2] { c.From, c.To }).ToHashSet();
                    FindRoadsBetweenCustomCells(Network.RoadEnds, allowedCells, out roadCells, out areConnected);
                    break;
                }    
            }

            // set flag IsRoad in cells and edges
            SetFlags(ruinsCells, roadCells, areConnected);

            yield return null;

            BuildPathsSegments();
        }

        private void FindRoadsBetweenCustomCells(List<RoadEnds> roadEnds, HashSet<Vector2Int> allowedCells, out HashSet<Vector2Int> roadCells, out Func<Vector2Int, Vector2Int, bool> areConnected)
        {
            // calc wall heights and angle on cell edges
            Builder.TerrainSpawner.CalcFormalSlopeAngles();

            //
            roadCells = new HashSet<Vector2Int>();
            HashSet<(Vector2Int, Vector2Int)> connected = new HashSet<(Vector2Int, Vector2Int)>();

            foreach (var pair in roadEnds)
            {
                var path = AStar.FindPath(pair.From, pair.To, Distance, (a) => (a - pair.To).magnitude + Map[a].Type.RoadPenalty, Neighbors);
                if (path == null)
                    continue;// can not find path
                var prev = Vector2Int.zero;
                foreach (var cell in path)
                {
                    if (prev != Vector2Int.zero)
                    {
                        connected.Add((prev, cell));
                        connected.Add((cell, prev));
                    }
                    prev = cell;
                }
                roadCells.AddRange(path);
            }

            areConnected = (a, b) => connected.Contains((a, b));

            IEnumerable<Vector2Int> Neighbors(Vector2Int hex) 
                => Map.Geometry.Neighbors(hex).Where(n => allowedCells.Contains(n) && Network.AllowedCellHeightRange.InRange(Map[n].Height));

            double Distance(Vector2Int from, Vector2Int to)
            {
                var dist = 1f;
                var fromCell = Map[from];
                var toCell = Map[to];
                if (fromCell.Parent == to || toCell.Parent == from)// is passage?
                    return dist;

                var iEdge = CellGeometry.NeighborToEdge(from, to);
                var edge = fromCell.Edges[iEdge];

                var dh = Mathf.Abs(edge.WallHeight);
                if (dh < Preferences.Instance.StepHeight)
                    dh /= 2f;

                dist += dh / Map.Geometry.InnerRadius * Network.HeightDifferencePenalty;
                dist += Mathf.Abs(edge.WallAngle / 90f) * Network.WallAnglePenalty;
                dist += toCell.Type.RoadPenalty;
                return dist;
            }
        }

        private HashSet<Vector2Int> FindCrosses(HashSet<Vector2Int> ruinsCells)
        {
            var cellToEnterCell = new Dictionary<Vector2Int, Vector2Int>();
            foreach (var ruin in ruinsCells)
                cellToEnterCell[ruin] = ruin;

            var roadCrossCells = new HashSet<Vector2Int>();
            foreach (var ruin in ruinsCells)
            {
                var prev = ruin;
                var next = Map[ruin].Parent;
                while (next != Vector2Int.zero)
                {
                    if (prev == next)
                        break;

                    if (cellToEnterCell.TryGetValue(next, out var exists))
                    {
                        if (exists != prev)
                            roadCrossCells.Add(next);
                        break;
                    }
                    cellToEnterCell[next] = prev;

                    //
                    prev = next;
                    next = Map[next].Parent;
                }
            }

            return roadCrossCells;
        }

        private void SetFlags(HashSet<Vector2Int> ruinsCells, HashSet<Vector2Int> roadCells, Func<Vector2Int, Vector2Int, bool> areConnected)
        {
            // set flag "IsRoad" in edges
            foreach (var hex in roadCells)
            {
                //if (ruinsCells.Contains(hex) && !Features.HasFlag(RoadSpawnerFeatures.SpawnRoadsInTargetCells))
                    //continue;

                var cell = Map[hex];
                var edges = cell.Edges;
                for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
                {
                    var n = CellGeometry.Neighbor(hex, iEdge);
                    if (roadCells.Contains(n) || ruinsCells.Contains(n))
                        if (areConnected(hex, n))
                        {
                            if (Features.HasFlag(RoadSpawnerFeatures.SpawnRoadsInTargetCells) || !ruinsCells.Contains(hex))
                                edges[iEdge].IsRoad = true;
                            edges[iEdge].IsPassage = true;
                        }
                }
            }

            // set flag "IsRoad" in cells
            foreach (var n in roadCells)
            {
                var count = Map[n].Edges.Count(e => e.IsRoad);
                Map[n].SetContent(CellContent.IsRoad, count >= 1);

                if (Map[n].HasContent(CellContent.IsRoad))
                {
                    // lift up above water
                    if (Features.HasFlag(RoadSpawnerFeatures.LiftUpRoadsToWaterLevel))
                        Builder.TerrainSpawner.LiftUpCellToWaterLevel(Map[n], HeightAboveWater - BedOffsetY);
                    Map[n].MicroNoiseScale *= MicroNoiseScale;
                }

                // set IsRoadCross flag 
                Map[n].SetContent(CellContent.IsRoadCross, count >= 3);

                if (Map[n].HasContent(CellContent.IsRoadCross) && Features.HasFlag(RoadSpawnerFeatures.LiftUpCrossToWaterLevel))
                    Builder.TerrainSpawner.LiftUpCellToWaterLevel(Map[n], HeightAboveWater - BedOffsetY);
            }
        }

        private HashSet<Vector2Int> FindRoads(HashSet<Vector2Int> ruinsCells, HashSet<Vector2Int> crossCells)
        {
            var roadCells = new HashSet<Vector2Int>();
            roadCells.AddRange(crossCells);
            var queue = new Queue<Vector2Int>();
            foreach (var ruin in ruinsCells)
            {
                queue.Clear();
                queue.Enqueue(ruin);
                var next = Map[ruin].Parent;

                while (next != Vector2Int.zero)
                {
                    if (crossCells.Contains(next))
                        FlushQueue();
                    queue.Enqueue(next);
                    next = Map[next].Parent;
                }

                void FlushQueue()
                {
                    while (queue.Count > 0)
                        roadCells.Add(queue.Dequeue());
                }
            }

            return roadCells;
        }

        private void BuildPathsSegments()
        {
            var neighbors = new List<(Vector2Int n, int iEdge, float height)>();

            foreach (var hex in Map.AllInsideHex())
            {
                var cell  = Map[hex];
                if (!cell.Content.HasFlag(CellContent.IsRoad)) continue;

                var center = Builder.HexToPos(hex).withSetY(cell.Height);

                // find neighbors by roads
                neighbors.Clear();
                for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
                {
                    var n = CellGeometry.Neighbor(hex, iEdge);
                    if (cell.Edges[iEdge].IsRoad)
                    {
                        var nCell = Map[n];
                        var h = (cell.Height * cell.Type.HeightPower + nCell.Height * nCell.Type.HeightPower) / (cell.Type.HeightPower + nCell.Type.HeightPower);
                        neighbors.Add((n, iEdge, h));
                    }
                }

                var takenRadius = RoadRadius + TakenAreaPadding;

                if (cell.Content.HasFlag(CellContent.IsRoadCross))
                {
                    foreach (var pair in neighbors)
                    {
                        var p = CellGeometry.EdgeCenter(hex, pair.iEdge);
                        cell.TakenAreas.Add(new TakenArea(p.ToVector2(), center.ToVector2(), takenRadius, TakenAreaType.Road));
                    }
                }else
                if (neighbors.Count == 2)
                {
                    var pair0 = neighbors[0];
                    var pair1 = neighbors[1];
                    var p0 = CellGeometry.EdgeCenter(hex, pair0.iEdge).withSetY(pair0.height);
                    var p1 = CellGeometry.EdgeCenter(hex, pair1.iEdge).withSetY(pair1.height);
                    var pp0 = ((p0 + center) / 2).withSetY(center.y);
                    var pp1 = ((p1 + center) / 2).withSetY(center.y);
                    cell.TakenAreas.Add(new TakenArea(p0.ToVector2(), pp0.ToVector2(), takenRadius, TakenAreaType.Road));
                    cell.TakenAreas.Add(new TakenArea(pp0.ToVector2(), pp1.ToVector2(), takenRadius, TakenAreaType.Road));
                    cell.TakenAreas.Add(new TakenArea(pp1.ToVector2(), p1.ToVector2(), takenRadius, TakenAreaType.Road));
                }
            }
        }

        protected virtual IEnumerator MakeBedOnTerrain() => MicroWorldHelper.MakeBedOnTerrain(Builder, BedPadding, RoadRadius, BedOffsetY, BedHorizontality, TakenAreaType.Road, Features.HasFlag(RoadSpawnerFeatures.SmoothEnds));

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (UnityEditor.Selection.activeGameObject != gameObject)
                return;

            var builder = GetComponentInParent<MicroWorld>();
            var map = builder?.Map;
            if (builder == null || map == null)
                return;

            foreach (var hex in map.AllInsideHex())
            {
                Gizmos.color = Color.green;
                var cell = map[hex];
                var center = builder.CellGeometry.Center(hex).withSetY(cell.Height + 1);

                for (var i = 0; i < builder.CellGeometry.CornersCount; i++)
                {
                    if (!cell.Edges[i].IsRoad)
                        continue;
                    var n = builder.CellGeometry.Neighbor(hex, i);
                    var nCell = map[n];
                    var nCenter = builder.CellGeometry.Center(n).withSetY(nCell.Height + 1);
                    Gizmos.DrawLine(center, (center + nCenter) / 2);
                }
            }

            Gizmos.color = Color.magenta;

            foreach (var hex in map.AllHex())
                if (map[hex].Content.HasFlag(CellContent.IsRoadCross))
                    Gizmos.DrawSphere(builder.HexToPos(hex) + Vector3.up * 1, 1);

            Gizmos.color = Color.green;

            foreach (var hex in map.AllHex())
                if (map[hex].Content.HasFlag(CellContent.IsRoad) && !map[hex].Content.HasFlag(CellContent.IsRoadCross))
                    Gizmos.DrawSphere(builder.HexToPos(hex) + Vector3.up * 1, 1);
        }
#endif
    }

    [Flags, Serializable]
    public enum RoadSpawnerFeatures
    {
        None = 0x0,
        LiftUpRoadsToWaterLevel = 0x1,
        LiftUpCrossToWaterLevel = 0x2,
        SpawnRoadsInTargetCells = 0x4,
        MakeMesh = 0x40,
        MakeCollider = 0x80,
        SmoothEnds = 0x100,
    }

    [Serializable]
    public enum RoadNetworkType : byte
    {
        Default = 0,
        SpanningTree = 1,
        FullyConnected = 2,
        Custom = 10
    }

    [Serializable]
    public class RoadNetworkData
    {
        [Tooltip("List of custom cells between which roads are created. This property only works for Network Type = Custom.")]
        public List<RoadEnds> RoadEnds = new List<RoadEnds>();

        [Tooltip("This factor determines how important the height difference between cells is when constructing a path.")]
        [Min(0)]
        public int HeightDifferencePenalty = 10;

        [Tooltip("This factor determines how important the wall angle between cells is when constructing a path. This parameter allows you to avoid the steepness of slopes on roads.")]
        [Min(0)]
        public int WallAnglePenalty = 8;

        [Tooltip("Defines a range of formal cell heights where roads can be created (meters).")]
        public RangeFloat AllowedCellHeightRange = new RangeFloat(0, 100);
    }

    [Serializable]
    public struct RoadEnds
    {
        public Vector2Int From;
        public Vector2Int To;
    }
}
