using UnityEngine;

/// <summary>
/// Interface for objects that can receive damage and knockback from arrows.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount, Vector2 hitPoint, Vector2 knockback);
}
