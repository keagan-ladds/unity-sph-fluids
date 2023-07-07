using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class FluidSimulation : MonoBehaviour
{
    public Vector3 Size = new Vector3(1f, 1f, 1f);
    public Material ParticleMaterial;
    /// <summary>
    /// The maximum number of particles that this simulation will allow
    /// </summary>
    public int MaxParticles = 1;

    public bool CollisionPhysics;
    public float ParticleSize = 0.25f;

    public bool VisualizeGrid = false;
    public bool VisualizeParticles = true;
    public bool GenerateSurfaceMesh = false;
    public int SurfaceMeshResolution = 1;

    private Mesh _particleMesh;

    public float k = 3.5f;
    public float fluidDensity = 0.997f;
    public float fluidViscosity = 1f;
    [Range(0.1f, 2f)]
    public float TimeFactor = 1f;

    private float _boundryCollisionVelocityFactor = 0.33f;
    

    private float GridCellSize;
    private int GridSizeX;
    private int GridSizeY;
    private int GridSizeZ;
    private float _particleSize;

    private float _h;
    private float _h1;
    private float _pi_h3;
    private float _2h;
    private float _2h_sqr;
    private float _001h2;
    private float _m;

    const float M1_PI = 1f / Mathf.PI;

    private Dictionary<long, Cell> _cells;
    private List<Particle> _particles = new List<Particle>();

    NativeArray<Vector3> positions;
    NativeArray<Vector3> velocities;
    NativeArray<float> densities;
    NativeArray<float> pressures;

    NativeArray<Vector3> forces;
    NativeHashMap<ulong, Cell> cells;


    JobHandle simulationJobHandle;

    BuildGridIndexJob indexJob;
    DensityCalculationJob densityJob;
    ForceCalculationJob job;
    IntegrationCalculationJob integrationJob;

    private MeshFilter meshFilter;

    private Queue<QueuedParticle> _newParticles = new Queue<QueuedParticle>();

    // Start is called before the first frame update
    void Start()
    {
        _particleSize = Mathf.Max(ParticleSize, 0.01f);

        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _particleMesh = Instantiate(obj.GetComponent<MeshFilter>().mesh);
        Destroy(obj);

        


        // If we assume some things can't change after the simulation has started, we can pre-compute some values as constants.
        GridCellSize = 2f * _particleSize;
        GridSizeX = Mathf.FloorToInt(Size.x / GridCellSize);
        GridSizeY = Mathf.FloorToInt(Size.y / GridCellSize);
        GridSizeZ = Mathf.FloorToInt(Size.z / GridCellSize);


        _h = _particleSize;
        _h1 = 1f / _h;
        _pi_h3 = M1_PI * _h1 * _h1 * _h1;
        _2h = 2f * _h;
        _2h_sqr = _2h * _2h;
        _001h2 = 0.01f * _h * _h;

        var tmp = (2f / 3f) * _h;
        _m = (tmp * tmp * tmp) * fluidDensity;

        _cells = new Dictionary<long, Cell>();


        meshFilter = GetComponent<MeshFilter>();


        positions = new NativeArray<Vector3>(MaxParticles, Allocator.Persistent);
        velocities = new NativeArray<Vector3>(MaxParticles, Allocator.Persistent);
        densities = new NativeArray<float>(MaxParticles, Allocator.Persistent);
        pressures = new NativeArray<float>(MaxParticles, Allocator.Persistent);

        forces = new NativeArray<Vector3>(MaxParticles, Allocator.Persistent);
        cells = new NativeHashMap<ulong, Cell>(_cells.Count, Allocator.Persistent);
    }

    public void AddParticleFromEmitter(Vector3 position, Vector3 velocity)
    {
        _newParticles.Enqueue(new QueuedParticle(position, velocity));
    }

    private void AddParticle(Vector3 position, Vector3 velocity = default)
    {
        if (_particles.Count < MaxParticles)
        {
            _particles.Add(new Particle(position, _particleSize, _m, fluidDensity)
            {
                Velocity = velocity,
                CellHash = CellHelpers.PositionToCellHash(position, GridCellSize)
            });
        }
    }

    private void LateUpdate()
    {
        
        simulationJobHandle.Complete();


        for (int i = 0; i < _particles.Count; i++)
        {
            // Calculate forces acting on the particle
            var particle = _particles[i];

            var position = integrationJob.Position[i];
            var velocity = integrationJob.Velocity[i];

            if (CollisionPhysics)
                HandleColliders(velocity, position, out velocity, out position);

            particle.Position = position;
            particle.Velocity = velocity;
            particle.CellHash = CellHelpers.PositionToCellHash(position, GridCellSize);
        }

        



       

        
    }

    private void Update()
    {
        if (VisualizeParticles)
        {
            foreach (var batch in BatchParticleDraws())
            {
                Graphics.DrawMeshInstanced(_particleMesh, 0, ParticleMaterial, batch);
            }
        }
    }

    private void FixedUpdate()
    {
        var deltaTime = Time.fixedDeltaTime * TimeFactor;

        if (simulationJobHandle.IsCompleted)
        {
            simulationJobHandle.Complete();
            // Check if there are new particles that need to be added
            if (_newParticles.Count > 0)
            {
                while(_newParticles.TryDequeue(out var queuedParticle))
                {
                    AddParticle(queuedParticle.Position, queuedParticle.Velocity);
                }
            }

            _particles.Sort((a, b) => a.CellHash.CompareTo(b.CellHash));


            for (int i = 0; i < _particles.Count; i++)
            {
                positions[i] = _particles[i].Position;
                velocities[i] = _particles[i].Velocity;
            }


            cells.Clear();

            indexJob = new BuildGridIndexJob
            {
                Position = positions,
                Cells = cells,
                GridCellSize = GridCellSize,
                GridSizeX = GridSizeX,
                GridSizeY = GridSizeY,
                GridSizeZ = GridSizeZ,
                ParticleSize = _h
            };

            var baseHandle = new JobHandle();
            var indexJobHandle = indexJob.Schedule(_particles.Count, baseHandle);


            densityJob = new DensityCalculationJob(positions, cells, densities, pressures, fluidDensity, k, GridCellSize, GridSizeX, GridSizeY, GridSizeZ, _h, _m);

             
            JobHandle densityJobHandle = densityJob.Schedule(_particles.Count, 32, indexJobHandle);


            job = new ForceCalculationJob(positions, velocities, densities, pressures, cells, forces, GridCellSize, GridSizeX, GridSizeY, GridSizeZ, fluidViscosity, _h, _m);

            //job.Run(_particles.Count);
            JobHandle handle = job.Schedule(_particles.Count, 32, densityJobHandle);

            integrationJob = new IntegrationCalculationJob
            {
                Position = positions,
                Velocity = velocities,
                Force = forces,
                ParticleSize = _h,
                ParticleMass = _m,
                BoundryCollisionVelocityFactor = _boundryCollisionVelocityFactor,
                BoundrySize = Size,
                DeltaTime = deltaTime
            };

            simulationJobHandle = integrationJob.Schedule(_particles.Count, 64, handle);
        }
       
    }


    private void OnDestroy()
    {
        positions.Dispose();
        velocities.Dispose();
        densities.Dispose();
        pressures.Dispose();
        forces.Dispose();
        cells.Dispose();
    }

    List<Matrix4x4> currentBatch = new List<Matrix4x4>(1000);

    private IEnumerable<List<Matrix4x4>> BatchParticleDraws()
    {
        
        
        currentBatch.Clear();

        int batchCount = 0;
        for (int i = 0; i < _particles.Count; i++)
        {
            if (batchCount < 1000)
            {
                currentBatch.Add(_particles[i].TrsMatrix * transform.localToWorldMatrix);
                batchCount++;
            }
            else
            {
                batchCount = 0;
                yield return currentBatch;
                currentBatch.Clear();
            }
        }

        if (currentBatch.Any())
        {
            yield return currentBatch;
        }

    }









    void OnDrawGizmos()
    {
        // Draw a yellow sphere at the transform's position
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + 0.5f * Size, Size);


        if (VisualizeGrid)
        {
            foreach (var kvp in cells)
            {
                if (kvp.Value.ParticleCount > 0)
                    Gizmos.DrawWireCube(kvp.Value.Position, new Vector3(2f * _particleSize, 2f * _particleSize, 2f * _particleSize));
            }
        }
    }

    void HandleColliders(Vector3 velocity, Vector3 position, out Vector3 finalVelocity, out Vector3 finalPosition)
    {
        if (Physics.Raycast(position, velocity.normalized, out var hitInfo, ParticleSize))
        {
            Vector3 u = position - hitInfo.point;
            float d = Vector3.Dot(u, hitInfo.normal);

            if (_particleSize - d > 0f)
            {
                position = position + (_particleSize - d) * hitInfo.normal;

                if (hitInfo.collider.attachedRigidbody != null)
                {
                    var force = velocity * 0.02f;
                    hitInfo.collider.attachedRigidbody.AddForce(force, ForceMode.Impulse);
                }


                Vector3 perpendicular = Vector3.Project(velocity, hitInfo.normal);
                Vector3 parallel = velocity - perpendicular;
                velocity = parallel - 0.77f * perpendicular;
            }
        }

        finalVelocity = velocity;
        finalPosition = position;

    }    
}


class QueuedParticle
{
    public Vector3 Position { get; }
    public Vector3 Velocity { get; }

    public QueuedParticle(Vector3 position, Vector3 velocity)
    {
        Position = position;
        Velocity = velocity;
    }
}




