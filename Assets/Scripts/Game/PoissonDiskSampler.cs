using System.Collections.Generic;
using UnityEngine;

// Bridson 푸아송 디스크 샘플링. 원 안을 어떤 두 점도 최소 거리보다 가깝지 않게, 더 놓을 자리가 없을 때까지 고르게 채운다.
// 격자 : 칸 크기를 최소 거리 / √2 로 두면 한 칸에 점이 하나만 들어간다. 그래서 가까운 점 검사는 주변 칸(앞뒤 2칸)만 보면 된다.
//   이미 놓은 모든 점과 비교하는 방식(점 하나마다 O(n))과 달리 점 하나마다 O(1) 이라, 전체가 놓은 점 수에 비례한다.
// 활성 목록 : 주변에 새 점을 놓을 수 있을지 모르는 점들. 활성 점 하나를 골라 그 주변 고리(최소 거리 ~ 2배)에서 후보를 정해진 횟수만큼 뽑고,
//   하나라도 놓이면 새 점도 활성 목록에 넣는다. 모두 실패하면 그 점은 주변이 찼다고 보고 활성 목록에서 뺀다. 목록이 비면 끝난다.
// 결과는 원의 중심 기준 좌표다. 월드에 놓을 때는 x 를 x, y 를 z 로 쓴다.
public static class PoissonDiskSampler
{
    // 활성 점 하나에서 후보를 뽑는 횟수. Bridson 논문의 기본값이다.
    private const int DEFAULT_ATTEMPTS = 30;

    // radius 원 안을 minDistance 간격으로 채운 점 목록을 돌려준다.
    public static List<Vector2> SampleCircle(float radius, float minDistance, int attempts = DEFAULT_ATTEMPTS)
    {
        List<Vector2> points = new List<Vector2>();
        List<int> activeIndices = new List<int>();
        float cellSize = minDistance / Mathf.Sqrt(2f);
        int gridSize = Mathf.CeilToInt(radius * 2f / cellSize);
        // 칸에 든 점의 번호 + 1. 0 이면 빈 칸이다.
        int[,] grid = new int[gridSize, gridSize];

        AddPoint(Random.insideUnitCircle * radius, points, activeIndices, grid, radius, cellSize);

        while(activeIndices.Count > 0)
        {
            int activeSlot = Random.Range(0, activeIndices.Count);
            Vector2 origin = points[activeIndices[activeSlot]];

            if(TryPlaceAround(origin, points, activeIndices, grid, radius, minDistance, cellSize, attempts)) continue;

            // 순서는 상관없으므로 마지막 항목과 바꿔 지워 목록을 당기지 않는다.
            activeIndices[activeSlot] = activeIndices[activeIndices.Count - 1];
            activeIndices.RemoveAt(activeIndices.Count - 1);
        }

        return points;
    }

    // origin 주변 고리에서 후보를 뽑아 놓을 수 있는 첫 후보를 놓는다. 하나도 못 놓으면 false 다.
    private static bool TryPlaceAround(Vector2 origin, List<Vector2> points, List<int> activeIndices, int[,] grid, float radius, float minDistance, float cellSize, int attempts)
    {
        for(int i = 0; i < attempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(minDistance, minDistance * 2f);
            Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            if(!CanPlace(candidate, points, grid, radius, minDistance, cellSize)) continue;

            AddPoint(candidate, points, activeIndices, grid, radius, cellSize);
            return true;
        }

        return false;
    }

    // 원 안에 있고, 주변 칸의 어떤 점과도 최소 거리 이상 떨어져 있으면 놓을 수 있다.
    private static bool CanPlace(Vector2 candidate, List<Vector2> points, int[,] grid, float radius, float minDistance, float cellSize)
    {
        if(candidate.sqrMagnitude > radius * radius) return false;

        Vector2Int cell = ToCell(candidate, radius, cellSize);
        int gridSize = grid.GetLength(0);
        float sqrMinDistance = minDistance * minDistance;

        for(int x = Mathf.Max(0, cell.x - 2); x <= Mathf.Min(gridSize - 1, cell.x + 2); x++)
        {
            for(int y = Mathf.Max(0, cell.y - 2); y <= Mathf.Min(gridSize - 1, cell.y + 2); y++)
            {
                int pointNumber = grid[x, y];

                if(pointNumber > 0 && (points[pointNumber - 1] - candidate).sqrMagnitude < sqrMinDistance) return false;
            }
        }

        return true;
    }

    private static void AddPoint(Vector2 point, List<Vector2> points, List<int> activeIndices, int[,] grid, float radius, float cellSize)
    {
        Vector2Int cell = ToCell(point, radius, cellSize);

        points.Add(point);
        activeIndices.Add(points.Count - 1);
        grid[cell.x, cell.y] = points.Count;
    }

    // 원의 중심 기준 좌표를 격자 칸 번호로 바꾼다. 격자는 원을 감싼 정사각형의 왼쪽 아래 모서리에서 시작한다.
    // 원의 경계에 딱 걸친 점이 격자 밖으로 나가지 않도록 마지막 칸으로 맞춘다.
    private static Vector2Int ToCell(Vector2 point, float radius, float cellSize)
    {
        int last = Mathf.CeilToInt(radius * 2f / cellSize) - 1;
        int x = Mathf.Min(last, (int)((point.x + radius) / cellSize));
        int y = Mathf.Min(last, (int)((point.y + radius) / cellSize));

        return new Vector2Int(x, y);
    }
}
