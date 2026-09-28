namespace CablePowerTransfer.Cables
{
    public enum CableLinkError
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
