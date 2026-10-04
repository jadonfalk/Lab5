using System;
using System.Collections.Generic;
using UnityEngine;

namespace AvoiderPlugin
{
    // Bridson sampling, adapted from Gregory Schlomoff's public-domain example.
    // Radius means minimum separation, not grid cell size.
    public sealed class PoissonDiscSampler
    {
        private readonly float width, height, radius;
        public PoissonDiscSampler(float width, float height, float radius)
        {
            if (!(width > 0) || !(height > 0) || !(radius > 0) ||
                float.IsInfinity(width) || float.IsInfinity(height) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException("Sampling dimensions must be finite and positive.");
            this.width = width; this.height = height; this.radius = radius;
        }

        public IEnumerable<Vector2> Samples()
        {
            float cell = radius / Mathf.Sqrt(2f);
            // Fresh state per enumeration; nullable cells allow a legitimate (0,0) sample.
            var grid = new Vector2?[Mathf.CeilToInt(width / cell), Mathf.CeilToInt(height / cell)];
            var active = new List<Vector2>();
            Vector2 first = new Vector2(UnityEngine.Random.value * width * 0.999999f,
                                       UnityEngine.Random.value * height * 0.999999f);
            Add(first, cell, grid, active);
            yield return first;
            while (active.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, active.Count);
                Vector2 center = active[index];
                bool found = false;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                    float distance = radius * Mathf.Sqrt(1f + 3f * UnityEngine.Random.value);
                    Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    if (candidate.x < 0 || candidate.y < 0 || candidate.x >= width || candidate.y >= height)
                        continue;
                    int gx = (int)(candidate.x / cell), gy = (int)(candidate.y / cell);
                    bool valid = true;
                    for (int x = Mathf.Max(0, gx - 2); x <= Mathf.Min(grid.GetLength(0) - 1, gx + 2); x++)
                        for (int y = Mathf.Max(0, gy - 2); y <= Mathf.Min(grid.GetLength(1) - 1, gy + 2); y++)
                            if (grid[x, y].HasValue && (candidate - grid[x, y].Value).sqrMagnitude < radius * radius)
                                valid = false;
                    if (!valid) continue;
                    Add(candidate, cell, grid, active);
                    found = true;
                    yield return candidate;
                    break;
                }
                if (!found)
                {
                    active[index] = active[active.Count - 1];
                    active.RemoveAt(active.Count - 1);
                }
            }
        }

        private static void Add(Vector2 point, float cell, Vector2?[,] grid, List<Vector2> active)
        {
            active.Add(point);
            grid[(int)(point.x / cell), (int)(point.y / cell)] = point;
        }
    }
}
