using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// This spawner defines map size, cell size, cell geometry, cell types
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.c14p1l2sm1ni")]
    public class MapSpawner : BaseSpawner, IExclusive
    {
        public string ExclusiveGroup => "MapSpawner";
        [field: SerializeField]
        [field: Tooltip("Defines the chance of selecting this spawner among all MapSpawners of MicroWorld instance.")] 
        public float Chance { get; set; } = 1;
        public override int Order => 200;

        [Header("Map")]
        [Tooltip("Map size (width and same height) measured in cells. This value does not include border cells.")]
        [SerializeField] public ParticleSystem.MinMaxCurve MapSize = 15;
        [Tooltip("Cell size defines the size of a cell (meters).")]
        [SerializeField] public ParticleSystem.MinMaxCurve CellSize = 10;
        [Tooltip("Specifies the thickness of the border, measured in cells.")]
        [SerializeField, Range(1, 3)] int BorderPadding = 1;
        [Tooltip("Specifies shape of cells.")]
        [SerializeField] CellShape Geometry = CellShape.Hex;

        [Header("Cell Types")]
        [Tooltip("Defines the frequency of cell types noise. The higher the value, the smaller the type islands.")]
        [SerializeField] ParticleSystem.MinMaxCurve CellTypeFrequency = 1;
        [Tooltip("This flag makes the distribution of cell types more uniform, avoiding mixing of types.")]
        [SerializeField] public bool CellTypeUniformity = false;
        [Tooltip("List of cell types defined by user.")]
        [SerializeField] public CellType[] CellTypes;

        [Header("Predefined Cell Types")]
        [SerializeField] public CellType BorderCellType = new CellType { Name = "Border", HeightSharpness = 3, HeightPower = 1f };
        [SerializeField] public CellType GateCellType = new CellType { Name = "Gate", MicroNoiseScale = 0, HeightPower = 5, HeightSharpness = 10 };
        [SerializeField] public CellType WaterCellType = new CellType { Name = "Water" };
        [SerializeField, HideInInspector] public CellType FallbackCellType = new CellType { Name = "Fallback" };

        public IEnumerable<CellType> AllCellTypes => CellTypes.Union(new CellType[] { BorderCellType, GateCellType, WaterCellType, FallbackCellType });

        public override IEnumerator Prepare(MicroWorld builder)
        {
            yield return base.Prepare(builder);
            builder.MapSpawner = this;

            FixVariableParams();

            switch (Geometry)
            {
                case CellShape.Square:
                    CellGeometry = new RectCellGeometry(CellSize_fixed, Builder.transform.position + new Vector3(CellSize_fixed, 0, CellSize_fixed));
                    break;
                case CellShape.Hex:
                    CellGeometry = new HexCellGeometry(CellSize_fixed, Builder.transform.position + new Vector3(CellSize_fixed, 0, Mathf.Sqrt(3) * CellSize_fixed / 2));
                    break;
            }

            builder.Map = new Map(MapSize_fixed + BorderPadding * 2, BorderPadding, CellGeometry);

            if (CellTypes == null || CellTypes.Length == 0)
            {
                CellTypes = new CellType[]
                {
                    new CellType{ Name = "Forest" },
                    new CellType{ Name = "Field" }
                };
            }

            BorderCellType.Features |= CellTypeFeatures.NoPassage;
        }

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);

            // assign OneCellPerTerrain cell types
            CalcOnePerTerrainCellTypes(rootRnd.GetBranch(122));

            // calc cell types
            CalcCellTypes();
        }

        private void CalcCellTypes()
        {
            var allowedCellTypes = new List<CellType>(CellTypes.Where(t => t.Chance > float.Epsilon && !t.Features.HasFlag(CellTypeFeatures.OneCellPerTerrain)));

            // prepare cell type list
            var rndMapper = new RndMapper(allowedCellTypes.Select(t => t.Chance).ToArray());

            // calc cell types
            foreach (var hex in Map.AllInsideHex())
            {
                if (Map[hex].Type != null) continue;
                var p = Vector2Int.RoundToInt(CellGeometry.Center(hex).ToVector2() * CellTypeFrequency_fixed);
                p += islandRndOffset;
                var classId = CellTypeUniformity ? Noises.SmoothedIslands[p] : Noises.Islands[p];
                Map[hex].Type = allowedCellTypes[rndMapper.Generate(classId)];
            }

            // create gate cells
            CreateGateCells();

            // assign border cell type
            foreach (var hex in Map.AllHex().Where(Map.IsBorderOrOutside))
                if (Map[hex].Type == null)
                    Map[hex].Type = BorderCellType;
        }

        private void CalcOnePerTerrainCellTypes(Rnd rnd)
        {
            Vector2Int[] shuffledInsideHexes = null;

            var oneCellTypes = CellTypes.Where(c => c.Chance > 0 && c.Features.HasFlag(CellTypeFeatures.OneCellPerTerrain));

            //OneCellPerTerrain
            foreach (var type in oneCellTypes)
            {
                if (!rnd.Bool(type.Chance))
                    continue;

                if (shuffledInsideHexes == null)
                    shuffledInsideHexes = rnd.Shuffle(Map.AllInsideHex()).ToArray();

                foreach (var p in shuffledInsideHexes)
                {
                    var cell = Map[p];
                    if (cell.Type == null)
                    {
                        cell.Type = type;
                        cell.LiftUpToWaterLevel = true;// auto lift up above water
                        break;
                    }
                }
            };
        }

        private void CreateGateCells()
        {
            if (Builder.Gates.Count == 0)
                return;
            var candidates = GetGateCandidates().Where(pair => Map[pair.Item1].Type == null).ToList();
            var rnd = rootRnd.GetBranch(37872);
            rnd.ShuffleFisherYates(candidates);

            foreach (var gate in Builder.Gates)
            {
                var hex = default(Vector2Int);
                if (gate.WorldSide == WorldSide.Custom)
                {
                    hex = gate.Cell;
                }
                else
                {
                    var cand = candidates.FirstOrDefault(p => p.Item2 == gate.WorldSide);
                    candidates.Remove(cand);
                    hex = gate.Cell = cand.Item1;
                }

                Map[hex].Type = GateCellType;

                var maxH = CellGeometry.Neighbors(hex).Where(h => !Map.IsBorderOrOutside(h)).Select(h => Map[h].Height).DefaultIfEmpty(0).Max();
                Map[hex].Height = maxH + rnd.Float(0.001f);
                var parent = CellGeometry.Neighbors(hex).Where(h => !Map.IsBorderOrOutside(h)).FirstOrDefault(h => Map[h].Height == maxH);
                Map[hex].Parent = parent;

                var iEdge = Map.Geometry.NeighborToEdge(hex, parent);
                Map[hex].Edges[iEdge].IsPassage = true;
                Map[parent].Edges[Map.Geometry.OppositeCorner(iEdge)].IsPassage = true;
            }
        }

        public IEnumerable<(Vector2Int, WorldSide)> GetGateCandidates()
        {
            var delta = 1;
            switch (Map.Size)
            {
                case > 7: delta = 3; break;
                case > 5: delta = 2; break;
            }

            var from = Map.LeftBorder;
            var to = Map.RightBorder;
            var From = from + delta;
            var To = to - delta;
            for (int x = From; x <= To; x++)
            {
                yield return (new Vector2Int(x, from), WorldSide.South);
                yield return (new Vector2Int(x, to), WorldSide.North);
            }

            for (int y = From; y <= To; y++)
            {
                yield return (new Vector2Int(from, y), WorldSide.West);
                yield return (new Vector2Int(to, y), WorldSide.East);
            }
        }

        #region Fix variable params

        int MapSize_fixed;
        int CellSize_fixed;
        internal float CellTypeFrequency_fixed { get; private set; }
        Vector2Int islandRndOffset;

        private void FixVariableParams()
        {
            var minMaxCurveRnd = rootRnd.GetBranch(487454);

            MapSize_fixed = MapSize.IntValue(minMaxCurveRnd);
            CellSize_fixed = CellSize.IntValue(minMaxCurveRnd);
            CellTypeFrequency_fixed = CellTypeFrequency.Value(minMaxCurveRnd);
            islandRndOffset = new Vector2Int(minMaxCurveRnd.Int(200), minMaxCurveRnd.Int(200));
        }
        #endregion

        private void OnValidate()
        {
            MapSize = MapSize.ClampInt(2, 500);
            CellSize = CellSize.ClampInt(1, 300);
            CellTypeFrequency = CellTypeFrequency.Clamp(0, 10);

            if (CellTypes != null)
            for (int i = 0; i < CellTypes.Length; i++)
            {
                var c = CellTypes[i];
                if (c.Name.NotNullOrEmpty() || c.Chance != 0f)
                    continue;
                CellTypes[i] = new CellType();
            }
        }
    }

    public enum CellShape
    {
        Hex = 0, Square = 1
    }

    class RndMapper
    {
        private float[] cumulativeProbabilities;

        public RndMapper(float[] chances)
        {
            cumulativeProbabilities = new float[chances.Length];
            var sum = chances.Sum();
            if (sum <= float.Epsilon)
                return;

            var cumulativeSum = 0f;
            for (int i = 0; i < chances.Length; i++)
            {
                cumulativeSum += chances[i] / sum;
                cumulativeProbabilities[i] = cumulativeSum;
            }
        }

        public int Generate(int randomInput)
        {
            // Bringing a random number to the range [0, 1]
            var rand = new System.Random(randomInput);
            var randomValue = (float)rand.NextDouble();

            // binary searching
            int left = 0;
            int right = cumulativeProbabilities.Length - 1;

            while (left < right)
            {
                int mid = left + (right - left) / 2;
                if (randomValue <= cumulativeProbabilities[mid])
                {
                    right = mid;
                }
                else
                {
                    left = mid + 1;
                }
            }

            return left;
        }
    }
}
