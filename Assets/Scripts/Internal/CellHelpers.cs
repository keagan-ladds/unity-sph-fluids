using System.Runtime.CompilerServices;
using UnityEngine;

static class CellHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong CellHash(int x, int y, int z)
    {
        return (ulong)((x * 73856093) ^ (y * 19349663) ^ (z * 83492791));
    }

    public static ulong PositionToCellHash(Vector3 position, float gridCellSize)
    {
        var x = (int)Unity.Mathematics.math.floor(position.x / gridCellSize);
        var y = (int)Unity.Mathematics.math.floor(position.y / gridCellSize);
        var z = (int)Unity.Mathematics.math.floor(position.z / gridCellSize);

        return CellHash(x, y, z);
    }
}