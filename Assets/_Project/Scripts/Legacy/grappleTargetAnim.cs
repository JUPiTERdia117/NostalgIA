using NostalgIA.Core.Events;
using NostalgIA.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Código que funciona para la animación del Grapple Target. </summary>
public class grappleTargetAnim : MonoBehaviour
{
    [SerializeField] private Transform core;
    [SerializeField] private Transform orb;
    [Header("Animation")]
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float rotationSpeedOrb = 45f;
    [SerializeField] private float rotationSpeedCore = 90f;
    [SerializeField] private float bobFrequencyOrb = 2f;
    [SerializeField] private float bobFrequencyCore = 3f;

    private Vector3 orbStartLocalPosition;
    private Vector3 coreStartLocalPosition;  

    private void Awake()
    {
        orbStartLocalPosition = orb.localPosition;
        coreStartLocalPosition = core.localPosition;
    }

    private void Update()
    {
        //Animación de ambas partes del Grapple Target: rotación y flotación senoidal.  
        Animation(orb, rotationSpeedOrb, bobHeight, bobFrequencyOrb, orbStartLocalPosition);
        Animation(core, rotationSpeedCore, bobHeight, bobFrequencyCore, coreStartLocalPosition);
    }

    private void Animation(Transform target, float rotationSpeed, float bobHeight, float bobFrequency, Vector3 startPosition)
    {
        // Rotación continua y flotación senoidal: y = y0 + A·sin(ω·t)
        target.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        float offset = Mathf.Sin(Time.time * bobFrequency) * bobHeight;
        Vector3 local = target.localPosition;
        local.y = startPosition.y + offset;
        target.localPosition = local;
    }
}
