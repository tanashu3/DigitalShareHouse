using MicroWorldNS.Spawners;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace MicroWorldNS
{
    /// <summary>
    /// Builds roads/paths between cells of specified types.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.czccg7kdvsdd")]
    public class RoadSplineSpawner : BaseSpawner
    {
        public override IEnumerator Build(MicroWorld builder)
        {
            // create road segment list
            var segments = new List<(Vector3, Vector3)>();

            foreach (var hex in Map.AllInsideHex())
            {
                foreach (var segment in Map[hex].TakenAreas.Where(ta => ta.Type == TakenAreaType.Road))
                {
                    var p0 = segment.A.ToVector3();
                    p0.y = Builder.Terrain.SampleHeight(p0);
                    var p1 = segment.B.ToVector3();
                    p1.y = Builder.Terrain.SampleHeight(p1);
                    segments.Add((p0, p1));
                }
            }

            // create SplineContainer
            var container = new GameObject("RoadSplines", typeof(SplineContainer)).GetComponent<SplineContainer>();
            container.transform.SetParent(Terrain.transform, false);

            // build splines
            BuildRoads(segments, container);

            yield return null;
        }

        public void BuildRoads(IEnumerable<(Vector3 start, Vector3 end)> segments, SplineContainer container)
        {
            var adjacencyList = BuildAdjacencyList(segments);
            var visited = new HashSet<Vector3>(new Vector3Comparer());

            // get start points (road ends and crosses)
            var startPoints = adjacencyList.Where(k => k.Value.Count != 2);

            // for each start point build separate spline
            foreach (var startPoint in startPoints)
                foreach (var nextPoint in startPoint.Value)
                    if (!visited.Contains(nextPoint))
                    {
                        var spline = BuildRoad(startPoint.Key, nextPoint, visited, adjacencyList);
                        container.AddSpline(spline);
                    }

            // link ends of splines to connect spline into one network
            var endKnots = new Dictionary<Vector3, List<SplineKnotIndex>>(new Vector3Comparer());
            for (var iSpline = 0; iSpline < container.Splines.Count; iSpline++)
            {
                var spline = container.Splines[iSpline];
                if (spline.Count < 2) continue;

                Link(0);
                Link(spline.Count - 1);

                void Link(int iKnot)
                {
                    var knot = spline[iKnot];
                    if (!endKnots.TryGetValue(knot.Position, out var list))
                        list = endKnots[knot.Position] = new List<SplineKnotIndex>();

                    var knotIndex = new SplineKnotIndex(iSpline, iKnot);

                    for (int i = 0; i < list.Count; i++)
                        container.LinkKnots(list[i], knotIndex);

                    list.Add(knotIndex);
                }
            }
        }

        private Spline BuildRoad(Vector3 startPoint, Vector3 nextPoint, HashSet<Vector3> visited, Dictionary<Vector3, List<Vector3>> adjacencyList)
        {
            var spline = new Spline();
            spline.Add(new BezierKnot(startPoint));
            visited.Add(startPoint);

            var stack = new Stack<Vector3>();
            stack.Push(nextPoint);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                // end of road?
                if (current != startPoint && adjacencyList[current].Count != 2)
                {
                    visited.Add(current);
                    spline.Add(new BezierKnot(current), TangentMode.AutoSmooth);
                    break;
                }

                if (!visited.Add(current))
                    continue;

                // add point to spline
                spline.Add(new BezierKnot(current), TangentMode.AutoSmooth);

                // goto next point
                foreach (var p in adjacencyList[current])
                    stack.Push(p);
            }

            return spline;
        }

        private Dictionary<Vector3, List<Vector3>> BuildAdjacencyList(IEnumerable<(Vector3 start, Vector3 end)> segments)
        {
            var graph = new Dictionary<Vector3, List<Vector3>>(new Vector3Comparer());
            foreach (var (start, end) in segments)
            {
                if (!graph.ContainsKey(start)) graph[start] = new();
                if (!graph.ContainsKey(end)) graph[end] = new();

                graph[start].Add(end);
                graph[end].Add(start);
            }
            return graph;
        }

        private class Vector3Comparer : IEqualityComparer<Vector3>
        {
            private const float Epsilon = 0.001f;

            public bool Equals(Vector3 a, Vector3 b)
            {
                return Vector3.SqrMagnitude(a - b) < Epsilon * Epsilon;
            }

            public int GetHashCode(Vector3 obj)
            {
                return HashCode.Combine(
                    Mathf.RoundToInt(obj.x * 1000),
                    Mathf.RoundToInt(obj.y * 1000),
                    Mathf.RoundToInt(obj.z * 1000)
                );
            }
        }
    }
}