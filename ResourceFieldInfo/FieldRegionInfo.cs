using System.Collections.Generic;

// Same namespace as Mod.cs so unqualified game types (Grid, Element, ...)
// resolve through the `OxygenNotIncluded` walk-up without extra usings.
namespace OxygenNotIncluded.Mods
{
    /// <summary>
    /// Computes a contiguous "resource field": the connected blob of one element
    /// around a hovered cell, measured in cell count and total mass (kg).
    ///
    /// Deliberately does its own 8-neighbor BFS instead of reusing the game's
    /// <c>Grid.FloodFill</c>, which is 4-neighbor only — diagonal contacts are
    /// treated as one field here.
    ///
    /// No memoization: mass flows in and out of fields, so the count/mass must
    /// be fresh on every call. <c>GetField</c> is called at most once per frame
    /// (by the tooltip postfix), so one flood per call is the whole cost.
    /// All state is pooled statics (main-thread only, no locking).
    /// </summary>
    public static class FieldRegionInfo
    {
        // Reused between calls (no per-frame allocation).
        private static readonly HashSet<int> s_visited = new HashSet<int>();
        private static readonly List<int> s_queue = new List<int>();

        /// <summary>
        /// Flood-fills from <paramref name="startCell"/> over 8 neighbors while the
        /// element index matches the start cell's, and sums the per-cell mass.
        /// </summary>
        /// <returns>
        /// (<c>cellCount</c>, <c>totalMass</c>). Returns <c>(0, 0)</c> when the start
        /// cell is invalid, empty, or vacuum.
        /// </returns>
        public static (int cellCount, float totalMass) GetField(int startCell)
        {
            // Guard the start cell before touching any grid indexers.
            if (!Grid.IsValidCell(startCell)
                || Grid.Element[startCell] == null
                || Grid.Element[startCell].IsVacuum)
            {
                return (0, 0f);
            }

            int startElementIdx = Grid.ElementIdx[startCell];

            s_visited.Clear();
            s_queue.Clear();
            s_visited.Add(startCell);
            s_queue.Add(startCell);

            int cellCount = 0;
            float totalMass = 0f;

            // Dequeue by advancing a head index (O(1)) instead of RemoveAt(0).
            int head = 0;
            while (head < s_queue.Count)
            {
                int c = s_queue[head];
                head++;

                cellCount++;
                totalMass += Grid.Mass[c];

                // 8 neighbors: 4 cardinal + 4 diagonal.
                TryVisit(Grid.CellLeft(c), startElementIdx);
                TryVisit(Grid.CellRight(c), startElementIdx);
                TryVisit(Grid.CellAbove(c), startElementIdx);
                TryVisit(Grid.CellBelow(c), startElementIdx);
                TryVisit(Grid.CellUpLeft(c), startElementIdx);
                TryVisit(Grid.CellUpRight(c), startElementIdx);
                TryVisit(Grid.CellDownLeft(c), startElementIdx);
                TryVisit(Grid.CellDownRight(c), startElementIdx);
            }

            return (cellCount, totalMass);
        }

        /// <summary>
        /// Enqueues <paramref name="neighbor"/> if it is a valid cell of the same
        /// element that has not been visited yet.
        ///
        /// <see cref="Grid.IsValidCell"/> is checked FIRST: <c>CellLeft</c>/<c>CellRight</c>
        /// return <c>InvalidCell</c> (-1) at the map edges and the four diagonal
        /// helpers also return <c>InvalidCell</c> when out of bounds, while
        /// <c>CellAbove</c>/<c>CellBelow</c> do no bounds check and can return an
        /// index outside <c>[0, CellCount)</c>. In every case <c>IsValidCell</c>
        /// filters it out before we touch <c>ElementIdx</c>/<c>Mass</c>.
        /// </summary>
        private static void TryVisit(int neighbor, int startElementIdx)
        {
            if (!Grid.IsValidCell(neighbor))
            {
                return;
            }
            if (Grid.ElementIdx[neighbor] != startElementIdx)
            {
                return;
            }
            // Add-before-enqueue: a neighbor reachable from several visited cells
            // is enqueued (and therefore processed) exactly once.
            if (s_visited.Add(neighbor))
            {
                s_queue.Add(neighbor);
            }
        }
    }
}
