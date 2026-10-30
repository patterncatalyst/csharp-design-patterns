namespace CustomsClearance;

/// <summary>
/// Assembles the standard clearance chain once, so callers never wire the
/// individual stages together themselves.
/// </summary>
public static class CustomsClearancePipeline
{
    public static ClearanceStage Standard()
    {
        var documentCheck = new DocumentCheckStage();
        var dutyCalculation = new DutyCalculationStage();
        var inspection = new InspectionStage();
        var release = new ReleaseStage();

        documentCheck.SetNext(dutyCalculation).SetNext(inspection).SetNext(release);

        return documentCheck;
    }
}
