using UnityEngine;

// Tuning values for one kind of melee enemy, shared by every instance of it.
[CreateAssetMenu(menuName = "Ascendant/Enemy Stats", fileName = "EnemyStats")]
public sealed class EnemyStats : ScriptableObject
{
    [Header("Movement")]
    public float patrolSpeed = 1f;
    public float chaseSpeed = 2f;
    public float patrolRadius = 2.5f;
    public float patrolWait = 1.2f;

    [Header("Senses")]
    public float sightRange = 6f;
    // Chasing continues out to sightRange * loseSightFactor so the orc does not flicker at the edge.
    public float loseSightFactor = 1.3f;

    [Header("Attack")]
    public float attackRange = 0.9f;
    public float attackCooldown = 1.2f;
    public int damage = 1;
    public float knockbackSpeed = 6f;
    // Pause before the swing starts (a visible wind-up). 0 swings at once.
    public float telegraphTime = 0f;
    // Seconds from the start of the attack animation to the moment the hit lands.
    public float hitDelay = 0.3f;
    public float attackDuration = 0.65f;
    // Radius of the hit check in front of the orc, so wide swings can be tuned.
    public float hitRadius = 0.7f;
    // Time standing still after the swing ends.
    public float recoveryTime = 0.2f;

    [Header("Hurt")]
    public float hurtStun = 0.3f;
    public float hurtKnockbackSpeed = 4f;
    // Chance that a hit does not interrupt an attack in progress; 1 = never interrupted.
    [Range(0f, 1f)] public float staggerResistance = 0f;

    [Header("Death")]
    public float deathDuration = 0.9f;
}
