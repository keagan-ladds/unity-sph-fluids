using UnityEngine;

struct Cell
{
    public Vector3 Position { get; }

    public int ParticleStartIndex { get; set; }
    public int ParticleEndIndex { get; set; }

    public int ParticleCount
    {
        get
        {
            if (ParticleStartIndex == -1)
                return 0;

            return ParticleEndIndex - ParticleStartIndex + 1;
        }
    }

    public Cell(Vector3 position)
    {
        Position = position;
        ParticleStartIndex = -1;
        ParticleEndIndex = -1;
    }

    public void AddIndex(int index)
    {
        if (ParticleStartIndex == -1)
        {
            ParticleStartIndex = index;
            ParticleEndIndex = index;
        }
        else
        {
            if (index > ParticleEndIndex)
                ParticleEndIndex = index;
            else if (index < ParticleStartIndex)
                ParticleStartIndex = index;
        }
    }
}
