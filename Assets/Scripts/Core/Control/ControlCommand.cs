using System;
using UnityEngine;

/// <summary>
/// Bit-mask of command buttons held/pressed this frame.
/// Values mirror the current player bindings:
///   LeftClick = Basic, RightClick = possessed-monster skill,
///   MiddleClick = corpse possession/body switch, Space = mobility, F = release.

/// Interact and Possess are legacy reserved bits and are not emitted by PlayerController.
/// </summary>
[Flags]
public enum CommandButtons : ushort
{
    None = 0, Basic = 1, Skill1 = 2, Skill2 = 4, Skill3 = 8,
    Interact = 16, Possess = 32, Release = 64, Mobility = 128
}

/// <summary>
/// One control intent per frame. Struct: zero GC on the per-frame hot path
/// and value semantics prevent a Controller from mutating an already issued command.
/// (Architecture decision D1 — struct + bit-mask buttons.)
/// </summary>
public struct ControlCommand
{
    public Vector3 MoveDirection;   // World-space XZ. Player 输入恒归一化；AI 可能产出非归一化（模长 0.7~1.3 作速度乘数，见 BTAction_MoveToPlayer）。HasMove=false 时无意义
    public Vector3 AimPoint;        // Ground aim point; meaningless when HasAim=false
    public bool HasMove;
    public bool HasAim;
    public CommandButtons Pressed;  // This frame's edge (GetKeyDown semantics, same as current code)

    /// <summary>An empty command — the default for NullController and inactive actors.</summary>
    public static ControlCommand Empty => default;
}
