using UnityEngine;

public class FluidEmitter : MonoBehaviour
{
    public GameObject FluidTarget;
    // Start is called before the first frame update
    public float InitialVelocity = 1f;
    public float EmitterSize = 1f;
    public float ParticlesPerSecond = 1f;

    private FluidSimulation _fluid;
    private Vector3 _direction => transform.TransformDirection(Vector3.forward);
    private float _elapsedTime = 0f;

    void Start()
    {
        if (FluidTarget != null)
        {
            _fluid = FluidTarget.GetComponent<FluidSimulation>();

            if (_fluid == null)
                Debug.LogError("Could not find referenced fluid");
        }

    }

    // Update is called once per frame
    void Update()
    {
        _elapsedTime += Time.deltaTime;

        if (Mathf.FloorToInt(_elapsedTime * ParticlesPerSecond) > 0)
        {
            for (int x = 0; x < Mathf.FloorToInt(_elapsedTime * ParticlesPerSecond); x++)
            {
                _fluid.AddParticleFromEmitter(EmitterSize * Random.insideUnitSphere + transform.position, InitialVelocity * _direction);
            }

            _elapsedTime = 0;
        }
        
    }

    void OnDrawGizmos()
    {
        // Draw a yellow sphere at the transform's position
        Gizmos.color = Color.yellow;

        Gizmos.DrawRay(transform.position, _direction * 10f);


        
    }
}
