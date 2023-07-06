using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
struct ForceCalculationJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<Vector3> Position;
    [ReadOnly]
    public NativeArray<Vector3> Velocity;
    [ReadOnly]
    public NativeArray<float> Density;
    [ReadOnly]
    public NativeArray<float> Pressure;

    [ReadOnly]
    public NativeHashMap<ulong, Cell> Cells;
    public NativeArray<Vector3> Force;
    public float GridCellSize;
    public int GridSizeX;
    public int GridSizeY;
    public int GridSizeZ;
    public float Viscosity;
    public float ParticleSize;
    public float ParticleMass;

    const float M_PI = 3.141592653589793f;
    const float M1_PI = 1f / M_PI;
    const float KERNEL_FACTOR = (2f / 3f) * M1_PI;

    private float _h;
    private float _h1;
    private float _fac;
    private float _2h;
    private float _2h_sqr;
    private float _001h2;

    public ForceCalculationJob(NativeArray<Vector3> position, NativeArray<Vector3> velocity, NativeArray<float> density, NativeArray<float> pressure, NativeHashMap<ulong, Cell> cells, NativeArray<Vector3> force,
        float gridCellSize, int gridSizeX, int gridSizeY, int gridSizeZ, float viscosity, float particleSize, float particleMass)
    {
        Position = position;
        Velocity = velocity;
        Density = density;
        Pressure = pressure;
        Cells = cells;
        Force = force;
        Viscosity = viscosity;
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
        _001h2 = 0.01f * _h * _h;
    }

    public void Execute(int index)
    {
        var position = Position[index];
        var velocity = Velocity[index];
        var density = Density[index];
        var pressure = Pressure[index];

        Vector3 pressureForce = Vector3.zero;
        Vector3 viscosityForce = Vector3.zero;

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
                            if (j != index)
                            {
                                var neighbourPosition = Position[j];
                                var neighbourVelocity = Velocity[j];
                                var neighbourDensity = Density[j];
                                var neighbourPressure = Pressure[j];

                                var deltaPosition = position - neighbourPosition;

                                if (deltaPosition.sqrMagnitude < _2h_sqr)
                                {
                                    var deltaVelocity = velocity - neighbourVelocity;
                                    var massDensity = ParticleMass / neighbourDensity;
                                    pressureForce += massDensity * (pressure / (density * density) + neighbourPressure / (neighbourDensity * neighbourDensity)) * (CubicSplineGradient(deltaPosition));
                                    viscosityForce += massDensity * (deltaVelocity) * (Vector3.Dot(deltaPosition, CubicSplineGradient(deltaPosition)) / (deltaPosition.sqrMagnitude + _001h2));
                                }
                            }
                        }
                    }
                }
            }
        }

        pressureForce *= -density;

        viscosityForce *= 2f;
        viscosityForce *= Viscosity;
        viscosityForce *= ParticleMass;

        Force[index] = pressureForce + viscosityForce;
    }



    private float CubicSplineGradient(float rij)
    {
        var q = rij * _h1;


        var val = 0.0f;

        var tmp2 = 2f - q;


        if (q > 2.0f)
            val = 0.0f;
        else if (q > 1.0f)
            val = -0.75f * tmp2 * tmp2;
        else
            val = -3.0f * q * (1f - 0.75f * q);


        return val * _fac;
    }

    private Vector3 CubicSplineGradient(Vector3 vector)
    {
        var tmp = 0.0f;

        if (vector.magnitude > 0f)
        {
            var wdash = CubicSplineGradient(vector.magnitude);
            tmp = wdash * _h1 / vector.magnitude;
        }

        return tmp * vector;
    }




    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int Min(int i, int j) => i > j ? j : i;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int Max(int i, int j) => i < j ? j : i;

}