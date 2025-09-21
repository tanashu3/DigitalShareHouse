using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MicroWorldNS
{
    public class AStar
    {
        /// <summary>
        /// This is the method responsible for finding the shortest path between a Start and Destination cities using the A*
        /// search algorithm.
        /// </summary>
        /// <typeparam name="TNode">The Node type</typeparam>
        /// <param name="start">Start city</param>
        /// <param name="destination">Destination city</param>
        /// <param name="distance">Function which tells us the exact distance between two neighbours.</param>
        /// <param name="estimate">Function which tells us the estimated distance between the last node on a proposed path and the
        /// destination node.</param>
        /// <returns></returns>
        static public Path<TNode> FindPath<TNode>(
            TNode start,
            TNode destination,
            Func<TNode, TNode, double> distance,
            Func<TNode, double> estimate,
            Func<TNode, IEnumerable<TNode>> getNeighbors)
        {
            var closed = new HashSet<TNode>();

            var queue = new PriorityQueue<double, Path<TNode>>();

            queue.Enqueue(0, new Path<TNode>(start));

            while (!queue.IsEmpty)
            {
                var path = queue.Dequeue();

                if (closed.Contains(path.LastStep))
                    continue;

                if (path.LastStep.Equals(destination))
                    return path;

                closed.Add(path.LastStep);

                foreach (TNode n in getNeighbors(path.LastStep))
                {
                    double d = distance(path.LastStep, n);

                    var newPath = path.AddStep(n, d);

                    queue.Enqueue(newPath.TotalCost + estimate(n), newPath);
                }

                //ViewOtherPaths(queue, estimate);
            }

            return null;
        }
    }

    public interface IHasNeighbours<TNode>
    {
        IEnumerable<TNode> Neighbours { get; }
    }

    class PriorityQueue<P, V> : IEnumerable
    {
        private SortedDictionary<P, Queue<V>> list = new SortedDictionary<P, Queue<V>>();

        public void Enqueue(P priority, V value)
        {
            Queue<V> q;

            if (!list.TryGetValue(priority, out q))
            {
                q = new Queue<V>();

                list.Add(priority, q);
            }

            q.Enqueue(value);
        }

        public V Dequeue()
        {
            // will throw if there isn’t any first element!
            var pair = list.First();

            var v = pair.Value.Dequeue();

            if (pair.Value.Count == 0) // nothing left of the top priority.
                list.Remove(pair.Key);

            return v;
        }

        public bool IsEmpty
        {
            get { return !list.Any(); }
        }

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        #endregion
    }

    public class Path<TNode> : IEnumerable<TNode>
    {
        public TNode LastStep { get; private set; }
        public Path<TNode> PreviousSteps { get; private set; }
        public double TotalCost { get; private set; }
        private Path(TNode lastStep, Path<TNode> previousSteps, double totalCost)
        {
            LastStep = lastStep;
            PreviousSteps = previousSteps;
            TotalCost = totalCost;
        }
        public Path(TNode start) : this(start, null, 0) { }
        public Path<TNode> AddStep(TNode step, double stepCost)
        {
            return new Path<TNode>(step, this, TotalCost + stepCost);
        }
        public IEnumerator<TNode> GetEnumerator()
        {
            for (Path<TNode> p = this; p != null; p = p.PreviousSteps)
                yield return p.LastStep;
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
