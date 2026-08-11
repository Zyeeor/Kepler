using UnityEngine;

/// <summary>
/// Per-frame polling context handed to a Controller.
/// Lets a pure-logic AI read the world without depending on MonoBehaviour.
/// </summary>
public struct ActorContext
{
    public Transform Self;
    public Transform PlayerTarget;   // Provided by MonsterActor (cached, replaces per-frame FindGameObjectWithTag)
    public float DeltaTime;
}

/// <summary>
/// A control source: produces a ControlCommand every frame, driven by its host Actor.
/// The essence of the architecture: possession = swapping the Controller, not a new character class.
/// Implementations: PlayerController (input), AIController (enemy AI), NullController (no-op).
/// (Architecture decisions D2/D4.)
/// </summary>
public interface IController
{
    /// <summary>Called on switch-in. Reset decision state (e.g. AI timers).</summary>
    void OnAttached(Actor owner);

    /// <summary>Called every frame by the host Actor. Writes the control intent.</summary>
    void Tick(in ActorContext ctx, ref ControlCommand cmd);

    /// <summary>Called on switch-out. Clean up (e.g. AI drops its target).</summary>
    void OnDetached();
}

/// <summary>
/// Null object: the default controller while a soul is possessed. Unifies the
/// "control intent" model and removes null branches inside Actor. (D4 — pure class.)
/// </summary>
public sealed class NullController : IController
{
    public static readonly NullController Instance = new NullController();

    public void OnAttached(Actor owner) { }
    public void Tick(in ActorContext ctx, ref ControlCommand cmd) { cmd = ControlCommand.Empty; }
    public void OnDetached() { }
}
