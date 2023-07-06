using System.Collections.Generic;
using UnityEngine;

class Particle
{
    private Vector3 _size;
    private Vector3 _position;
    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            _trsMatrix = Matrix4x4.TRS(Position, Quaternion.identity, _size);
        }
    }

    private Matrix4x4 _trsMatrix;

    public Vector3 Velocity { get; set; }

    public float Radius { get; set; }
    public float Mass { get; set; }

    public float Pressure { get; set; }

    public List<int> Neighbours { get; set; }

    public ulong CellHash { get; set; }


    public Matrix4x4 TrsMatrix
    {
        get => _trsMatrix;
    }

    public Particle(Vector3 position, float radius, float mass, float density)
    {
        Position = position;
        Velocity = Vector3.zero;
        Radius = radius;
        Mass = mass;
        Density = density;
        Neighbours = new List<int>();
        Pressure = 0f;

        _size = new Vector3(Radius, Radius, Radius);
    }

    public float Density { get; set; }
}
