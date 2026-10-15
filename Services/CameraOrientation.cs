using MapperIce.Models;

namespace MapperIce.Services;

/// <summary>
/// Ориентация настенной камеры-привязки.
///
/// У камеры четыре направления, заданных поворотом сущности. Поворот 0 соответствует
/// стене с ЮГА от тайла камеры (в СС14 Direction.South = 0), дальше по кругу:
/// восток = 90°, север = 180°, запад = -90° — это ровно тот порядок, который даёт
/// DirectionExtensions.ToAngle в движке и _quarterToDirOrder в рендерере, поэтому
/// спрайт камеры сам выбирает нужный кадр направления.
/// </summary>
public static class CameraOrientation
{
    /// <summary>Насколько далеко (в клетках) ищем стену вокруг камеры.</summary>
    private const int MaxWallSteps = 32;

    /// <summary>Сторона тайла, на которой стоит стена.</summary>
    public enum WallSide
    {
        None,
        South,
        East,
        North,
        West
    }

    /// <summary>Поворот камеры для стены на указанной стороне тайла (в радианах).</summary>
    public static float RotationForSide(WallSide side)
    {
        float rotation = side switch
        {
            WallSide.South => 0f,
            WallSide.East => MathF.PI / 2f,
            WallSide.North => MathF.PI,
            WallSide.West => -MathF.PI / 2f,
            _ => 0f
        };

        // Приводим к диапазону (-π; π] — так же повороты пишет и сама игра:
        // запад в картах это -1.5707963 rad, а не 4.712389 rad.
        if (rotation > MathF.PI) rotation -= MathF.Tau;
        return rotation;
    }

    /// <summary>
    /// Ближайшая стена вокруг позиции камеры: от клетки камеры идём в каждую из
    /// четырёх сторон, пока не упрёмся в стену. Расстояние считается от позиции
    /// камеры до кромки стены. Если несколько стен одинаково близко (например,
    /// камера в одноклеточном коридоре), выигрывает сторона preferred — сторона
    /// уже сохранённого поворота камеры, чтобы направление не «прыгало».
    /// </summary>
    public static WallSide FindNearestWall(Grid? grid, TileGrid? tileGrid, float worldX, float worldY,
        WallSide preferred = WallSide.None)
    {
        int cellX = (int)Math.Floor(worldX);
        int cellY = (int)Math.Floor(worldY);
        float fracX = worldX - cellX;
        float fracY = worldY - cellY;

        var best = WallSide.None;
        float bestDistance = float.MaxValue;

        void Consider(WallSide side, float distance)
        {
            const float tieEpsilon = 0.0001f;

            if (distance < bestDistance - tieEpsilon)
            {
                bestDistance = distance;
                best = side;
            }
            else if (distance <= bestDistance + tieEpsilon && side == preferred)
            {
                bestDistance = distance;
                best = side;
            }
        }

        for (int step = 1; step <= MaxWallSteps; step++)
        {
            if (!IsWallCell(grid, tileGrid, cellX, cellY - step)) continue;
            Consider(WallSide.North, fracY + step - 1);
            break;
        }

        for (int step = 1; step <= MaxWallSteps; step++)
        {
            if (!IsWallCell(grid, tileGrid, cellX, cellY + step)) continue;
            Consider(WallSide.South, step - fracY);
            break;
        }

        for (int step = 1; step <= MaxWallSteps; step++)
        {
            if (!IsWallCell(grid, tileGrid, cellX - step, cellY)) continue;
            Consider(WallSide.West, fracX + step - 1);
            break;
        }

        for (int step = 1; step <= MaxWallSteps; step++)
        {
            if (!IsWallCell(grid, tileGrid, cellX + step, cellY)) continue;
            Consider(WallSide.East, step - fracX);
            break;
        }

        return best;
    }

    /// <summary>
    /// Поворот камеры по ближайшей стене. Если стены вокруг нет, возвращается
    /// fallbackRotation (сохранённый в привязке поворот); при равноудалённых стенах
    /// предпочтение отдаётся стороне, соответствующей fallbackRotation.
    /// </summary>
    public static float ResolveRotation(Grid? grid, TileGrid? tileGrid, float worldX, float worldY, float fallbackRotation)
    {
        var side = FindNearestWall(grid, tileGrid, worldX, worldY, SideForRotation(fallbackRotation));
        return side == WallSide.None ? fallbackRotation : RotationForSide(side);
    }

    /// <summary>Сторона стены, которой соответствует поворот камеры.</summary>
    public static WallSide SideForRotation(float rotation)
    {
        float normalized = rotation % MathF.Tau;
        if (normalized < 0) normalized += MathF.Tau;

        int quarter = (int)MathF.Round(normalized / (MathF.PI / 2f)) % 4;
        return quarter switch
        {
            1 => WallSide.East,
            2 => WallSide.North,
            3 => WallSide.West,
            _ => WallSide.South
        };
    }

    /// <summary>
    /// Клетка считается стеной, если в ней реальный тайл-стена. Если тайлы ещё не
    /// построены (TileGrid недоступен), стеной считается всё, что лежит вне комнат —
    /// так камера всё равно привяжется к границе своей комнаты.
    /// </summary>
    private static bool IsWallCell(Grid? grid, TileGrid? tileGrid, int x, int y)
    {
        if (tileGrid != null)
            return tileGrid.IsWall(x, y);

        return grid != null && !grid.Rooms.Any(r => r.Contains(x, y));
    }
}
