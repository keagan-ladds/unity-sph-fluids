using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
struct IntegrationCalculationJob : IJobParallelFor
{
    public Vector3 BoundrySize;
    public float ParticleSize;
    public float ParticleMass;
    public float BoundryCollisionVelocityFactor;
    public float DeltaTime;

    [ReadOnly]
    public NativeArray<Vector3> Force;

    public NativeArray<Vector3> Position;
    public NativeArray<Vector3> Velocity;



    public void Execute(int index)
    {
        Vector3 force = ParticleMass * Physics.gravity; //Vector3.zero; // 

        force += Force[index];

        // Calculate the velocity as a result of this force

        Vector3 velocity = Velocity[index] + (force / ParticleMass) * DeltaTime;

        // Calculate the position as a result of this velocity

        Vector3 pos = Position[index] + velocity * DeltaTime;

        // As a crude first attempt, prevent the position of particle extending further than the simulation bounds

        var previousVelocity = Velocity[index];

        Vector3 finalVelocity = velocity;
        Vector3 finalPos = pos;


        HandleBoundryCollisions(finalVelocity, finalPos, out finalVelocity, out finalPos);

        Position[index] = finalPos;
        Velocity[index] = finalVelocity;

        if (Velocity[index].magnitude > 35f)
        {
            Velocity[index] = Velocity[index].normalized * 35f;
        }
    }



    private void HandleBoundryCollisions(Vector3 velocity, Vector3 position, out Vector3 finalVelocity, out Vector3 finalPosition)

    {
        if (position.x >= BoundrySize.x - ParticleSize)
        {
            position.x = BoundrySize.x - ParticleSize;


            velocity.x *= -1f;
            velocity *= BoundryCollisionVelocityFactor;
        }
        else if (position.x <= 0f + ParticleSize)
        {
            position.x = ParticleSize;

            velocity.x *= -1f;
            velocity *= BoundryCollisionVelocityFactor;
        }

        if (position.y >= BoundrySize.y - ParticleSize)
        {
            position.y = BoundrySize.y - ParticleSize;

            velocity.y *= -1f;
            velocity *= BoundryCollisionVelocityFactor;
        }
        else if (position.y <= 0f + ParticleSize)
        {
            position.y = ParticleSize;

            velocity.y *= -1f;
            velocity *= BoundryCollisionVelocityFactor;
        }

        if (position.z >= BoundrySize.z - ParticleSize)
        {
            position.z = BoundrySize.z - ParticleSize;

            velocity.z *= -1f;
            velocity *= BoundryCollisionVelocityFactor;
        }
        else if (position.z <= 0f + ParticleSize)
        {
            position.z = 0f + ParticleSize;

            velocity.z *= -1f;
            velocity *= BoundryCollisionVelocityFactor;
        }

        finalPosition = position;
        finalVelocity = velocity;

    }
}