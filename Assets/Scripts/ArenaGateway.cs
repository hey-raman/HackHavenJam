using System.Collections;
using UnityEngine;

/// <summary>
/// Gateway / Portal that unlocks when the arena objective is completed.
/// Transports the player to the next area when entered.
/// </summary>
public class ArenaGateway : MonoBehaviour
{
    [Header("Visuals & FX")]
    public GameObject inactiveVisual;
    public GameObject activeVisual;
    public ParticleSystem portalBeamParticles;
    public Light portalLight;

    [Header("State")]
    public bool isUnlocked = false;

    private Collider2D _triggerCollider;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider2D>();
        SetGatewayState(false);
    }

    public void SetGatewayState(bool unlocked)
    {
        isUnlocked = unlocked;

        if (_triggerCollider != null)
        {
            _triggerCollider.enabled = unlocked;
        }

        if (inactiveVisual != null)
        {
            inactiveVisual.SetActive(!unlocked);
        }

        if (activeVisual != null)
        {
            activeVisual.SetActive(unlocked);
        }

        if (portalBeamParticles != null)
        {
            if (unlocked) portalBeamParticles.Play();
            else portalBeamParticles.Stop();
        }

        if (portalLight != null)
        {
            portalLight.enabled = unlocked;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isUnlocked) return;

        if (other.CompareTag("Player") || other.GetComponent<PlayerMovement>() != null)
        {
            if (GameLoopManager.Instance != null)
            {
                GameLoopManager.Instance.OnGatewayEntered();
            }
        }
    }
}
