using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
struct DensityCalculationJob : IJobParallelFor
{
    const float M_PI = 3.141592653589793f;
    const float M1_PI = 1f / M_PI;
    const float KERNEL_FACTOR = (2f / 3f) * M1_PI;

    [ReadOnly]
    public NativeArray<Vector3> Position;
    [ReadOnly]
    public NativeHashMap<ulong, Cell> Cells;


    public NativeArray<float> Density;
    public NativeArray<float> Pressure;

    public float FluidDensity;
    public float FluidStiffness;

    public float GridCellSize;
    public int GridSizeX;
    public int GridSizeY;
    public int GridSizeZ;
    public float ParticleSize;
    public float ParticleMass;

    private float _h;
    private float _h1;
    private float _fac;
    private float _2h;
    private float _2h_sqr;

    public DensityCalculationJob(NativeArray<Vector3> position, NativeHashMap<ulong, Cell> cells, NativeArray<float> density, NativeArray<float> pressure, float fluidDensity, float fluidStiffness,
        float gridCellSize, int gridSizeX, int gridSizeY, int gridSizeZ, float particleSize, float particleMass)
    {
        Position = position;
        Cells = cells;
        Density = density;
        Pressure = pressure;
        FluidDensity = fluidDensity;
        FluidStiffness = fluidStiffness;
        GridCellSize = gridCellSize;
        GridSizeX = gridSizeX;
        GridSizeY = gridSizeY;
        GridSizeZ = gridSizeZ;
        ParticleMass = particleMass;
        ParticleSize = particleSize;

        _h = ParticleSize;
        _h1 = 1f / _h;
        _fac = KERNEL_FACTOR * _h1 * _h1 * _h1;
        _2h = 2f * _h;
        _2h_sqr = _2h * _2h;
    }



    public void Execute(int index)
    {
        var position = Position[index];
        var density = FluidDensity;

        var cellX = (int)Unity.Mathematics.math.floor(position.x / GridCellSize);
        var cellY = (int)Unity.Mathematics.math.floor(position.y / GridCellSize);
        var cellZ = (int)Unity.Mathematics.math.floor(position.z / GridCellSize);


        for (int x = Max(0, cellX - 1); x < Min(cellX + 2, GridSizeX); x++)
        {
            for (int y = Max(0, cellY - 1); y < Min(cellY + 2, GridSizeY); y++)
            {
                for (int z = Max(0, cellZ - 1); z < Min(cellZ + 2, GridSizeZ); z++)
                {
                    if (Cells.TryGetValue(CellHelpers.CellHash(x, y, z), out var cell) && cell.ParticleCount > 0)
                    {
                        for (int j = cell.ParticleStartIndex; j <= cell.ParticleEndIndex; j++)
                        {
                            // Make sure to not include ourselves when looking for neighbours
                            if (j != index)
                            {
                                var neighbourPosition = Position[j];
                                var deltaPosition = position - neighbourPosition;

                                if (deltaPosition.sqrMagnitude < _2h_sqr && !Mathf.Approximately(deltaPosition.sqrMagnitude, 0f))
                                {
                                    density += ParticleMass * CubicSpline(deltaPosition.magnitude);
                                }
                            }
                        }
                    }
                }
            }
        }


        Density[index] = density;
        Pressure[index] = FluidStiffness * (density - FluidDensity);
    }


    static int Min(int i, int j) => i > j ? j : i;

    static int Max(int i, int j) => i < j ? j : i;




    private float CubicSpline(float rij)
    {
        var q = rij * _h1;


        var val = 0.0f;

        var tmp2 = 2f - q;

        if (q > 2.0)
            val = 0.0f;

        else if (q > 1.0)
            val = 0.25f * tmp2 * tmp2 * tmp2;
        else
            val = 1 - 1.5f * q * q * (1 - 0.5f * q);

        return val * _fac;

    }
}