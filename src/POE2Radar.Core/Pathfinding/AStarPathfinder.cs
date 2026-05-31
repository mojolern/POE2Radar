namespace POE2Radar.Core.Pathfinding;

public static class AStarPathfinder
{
    public readonly record struct PathResult(List<(int X, int Y)> Points, float GridDistance, int NodesVisited);

    public static PathResult? FindPath(byte[] walkable, int width, int height,
        int startX, int startY, int endX, int endY, int maxNodes = 100_000)
    {
        if (width <= 0 || height <= 0) return null;
        startX = Math.Clamp(startX, 0, width - 1);
        startY = Math.Clamp(startY, 0, height - 1);
        endX = Math.Clamp(endX, 0, width - 1);
        endY = Math.Clamp(endY, 0, height - 1);

        if (!IsWalkable(walkable, width, startX, startY))
            (startX, startY) = NearestWalkable(walkable, width, height, startX, startY);
        if (!IsWalkable(walkable, width, endX, endY))
            (endX, endY) = NearestWalkable(walkable, width, height, endX, endY);

        if (startX == endX && startY == endY)
            return new PathResult([(startX, startY)], 0, 1);

        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, float>();
        var open = new PriorityQueue<int, float>();

        var startKey = startY * width + startX;
        var endKey = endY * width + endX;
        gScore[startKey] = 0;
        open.Enqueue(startKey, Heuristic(startX, startY, endX, endY));

        var visited = 0;
        Span<(int dx, int dy, float cost)> neighbors =
        [
            (-1, 0, 1f), (1, 0, 1f), (0, -1, 1f), (0, 1, 1f),
            (-1, -1, 1.414f), (1, -1, 1.414f), (-1, 1, 1.414f), (1, 1, 1.414f),
        ];

        while (open.Count > 0 && visited < maxNodes)
        {
            var current = open.Dequeue();
            visited++;

            if (current == endKey)
                return new PathResult(ReconstructPath(cameFrom, current, width), gScore[current], visited);

            var cx = current % width;
            var cy = current / width;

            foreach (var (dx, dy, cost) in neighbors)
            {
                var nx = cx + dx;
                var ny = cy + dy;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                if (!IsWalkable(walkable, width, nx, ny)) continue;

                if (dx != 0 && dy != 0)
                {
                    if (!IsWalkable(walkable, width, cx + dx, cy) ||
                        !IsWalkable(walkable, width, cx, cy + dy))
                        continue;
                }

                var nKey = ny * width + nx;
                var tentG = gScore[current] + cost;
                if (gScore.TryGetValue(nKey, out var existing) && tentG >= existing) continue;

                cameFrom[nKey] = current;
                gScore[nKey] = tentG;
                open.Enqueue(nKey, tentG + Heuristic(nx, ny, endX, endY));
            }
        }

        return null;
    }

    public static List<(int X, int Y)> Simplify(List<(int X, int Y)> path, int every = 4)
    {
        if (path.Count <= 2) return path;
        var result = new List<(int, int)> { path[0] };
        for (var i = every; i < path.Count - 1; i += every)
            result.Add(path[i]);
        result.Add(path[^1]);
        return result;
    }

    private static float Heuristic(int ax, int ay, int bx, int by)
    {
        var dx = Math.Abs(ax - bx);
        var dy = Math.Abs(ay - by);
        return dx + dy + (1.414f - 2f) * Math.Min(dx, dy);
    }

    private static bool IsWalkable(byte[] grid, int width, int x, int y)
        => grid[y * width + x] != 0;

    private static (int x, int y) NearestWalkable(byte[] grid, int width, int height, int x, int y)
    {
        for (var r = 1; r < 20; r++)
            for (var dy = -r; dy <= r; dy++)
                for (var dx = -r; dx <= r; dx++)
                {
                    var nx = x + dx; var ny = y + dy;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && IsWalkable(grid, width, nx, ny))
                        return (nx, ny);
                }
        return (x, y);
    }

    private static List<(int X, int Y)> ReconstructPath(Dictionary<int, int> cameFrom, int current, int width)
    {
        var path = new List<(int, int)>();
        while (true)
        {
            path.Add((current % width, current / width));
            if (!cameFrom.TryGetValue(current, out var prev)) break;
            current = prev;
        }
        path.Reverse();
        return path;
    }
}
