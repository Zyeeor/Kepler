using System;

public enum PossessionGrantReason
{
    InitialAssignment,
    PlayerPossession,
    DeathRelay,
    LoadRestore,
    Debug,
}

[Serializable]
public struct PossessionImprintState
{
    public SinType sin;
    public int stacks;

    public PossessionImprintState(SinType sin, int stacks)
    {
        this.sin = sin;
        this.stacks = stacks;
    }
}
