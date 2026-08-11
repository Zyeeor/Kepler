using System;
using UnityEngine;

/// <summary>
/// Bit-mask of command buttons held/pressed this frame.
/// Values mirror the current PlayerInputController key bindings:
///   LeftClick = Basic, RightClick = Skill1, Q = Skill2, W = Skill3,
///   E = Interact, Space/F = Possess / Release.
/// </summary>
[Flags]
public enum CommandButtons : ushort
{
    None = 0, Basic = 1, Skill1 = 2, Skill2 = 4, Skill3 = 8,
    Interact = 16, Possess = 32, Release = 64
}

/// <summary>
/// One control intent per frame. Struct: zero GC on the per-frame hot path
/// and value semantics prevent a Controller from mutating an already issued command.
/// (Architecture decision D1 — struct + bit-mask buttons.)
/// </summary>
public struct ControlCommand
{
    public Vector3 MoveDirection;   // World-space XZ, normalized; meaningless when HasMove=false
    public Vector3 AimPoint;        // Ground aim point; meaningless when HasAim=false
    public bool HasMove;
    public bool HasAim;
    public CommandButtons Pressed;  // This frame's edge (GetKeyDown semantics, same as current code)

    /// <summary>An empty command — the default for NullController and inactive actors.</summary>
    public static ControlCommand Empty => default;
}
