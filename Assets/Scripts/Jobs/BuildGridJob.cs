using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
struct BuildGridIndexJob : IJobFor
{
    [ReadOnly]
    public NativeArray<Vector3> Position;

    public NativeHashMap<ulong, Cell> Cells;

    public float GridCellSize;
    public int GridSizeX;
    public int GridSizeY;
    public int GridSizeZ;
    public float ParticleSize;

    const int MaxParticlesPerCell = 8;
    public void Execute(int index)
    {
        var position = Position[index];
        var x = (int)Unity.Mathematics.math.floor(position.x / GridCellSize);
        var y = (int)Unity.Mathematics.math.floor(position.y / GridCellSize);
        var z = (int)Unity.Mathematics.math.floor(position.z / GridCellSize);


        var cellHash = CellHelpers.PositionToCellHash(position, GridCellSize);

        if (!Cells.TryGetValue(cellHash, out var cell))
        {
            cell = new Cell(new Vector3(ParticleSize + (x) * GridCellSize, ParticleSize + (y) * GridCellSize, ParticleSize + (z) * GridCellSize));
            Cells.Add(cellHash, cell);
        }


        if (cell.ParticleCount < MaxParticlesPerCell)
        {
            cell.AddIndex(index);

            Cells[cellHash] = cell;
        }
    }
}