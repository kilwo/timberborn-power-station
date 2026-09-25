namespace RopePower.Ropes
{
    public enum RopeLinkError
    {
        None,
        NoTarget,
        SameStation,
        AlreadyLinked,
        SourceFull,
        TargetFull,
        TooLong,
        TooSteep,
        DifferentDistricts,
        Obstructed
    }
}
