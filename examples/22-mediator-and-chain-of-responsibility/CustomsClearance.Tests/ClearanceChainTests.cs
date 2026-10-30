using Xunit;

namespace CustomsClearance.Tests;

public class ClearanceChainTests
{
    [Fact]
    public void Handle_runs_all_stages_in_order_and_clears_a_complete_declaration()
    {
        var declaration = new CustomsDeclaration(
            ConsignmentId: "CN-1001",
            DeclaredValue: 1000m,
            DocumentsComplete: true,
            RequiresInspection: false,
            InspectionPassed: false);
        var context = new ClearanceContext(declaration);
        var pipeline = CustomsClearancePipeline.Standard();

        var result = pipeline.Handle(context);

        Assert.True(result.Cleared);
        Assert.Equal(80m, result.Duty);
        Assert.Equal(
            [nameof(DocumentCheckStage), nameof(DutyCalculationStage), nameof(InspectionStage), nameof(ReleaseStage)],
            context.StagesRun);
    }

    [Fact]
    public void Handle_halts_at_the_document_stage_when_documents_are_missing()
    {
        var declaration = new CustomsDeclaration(
            ConsignmentId: "CN-1002",
            DeclaredValue: 500m,
            DocumentsComplete: false,
            RequiresInspection: false,
            InspectionPassed: false);
        var context = new ClearanceContext(declaration);
        var pipeline = CustomsClearancePipeline.Standard();

        var result = pipeline.Handle(context);

        Assert.False(result.Cleared);
        Assert.Equal(nameof(DocumentCheckStage), result.HaltedStage);
        Assert.Equal([nameof(DocumentCheckStage)], context.StagesRun);
        Assert.DoesNotContain(nameof(DutyCalculationStage), context.StagesRun);
        Assert.DoesNotContain(nameof(InspectionStage), context.StagesRun);
        Assert.DoesNotContain(nameof(ReleaseStage), context.StagesRun);
    }

    [Fact]
    public void Handle_halts_before_release_when_a_required_inspection_fails()
    {
        var declaration = new CustomsDeclaration(
            ConsignmentId: "CN-1003",
            DeclaredValue: 2000m,
            DocumentsComplete: true,
            RequiresInspection: true,
            InspectionPassed: false);
        var context = new ClearanceContext(declaration);
        var pipeline = CustomsClearancePipeline.Standard();

        var result = pipeline.Handle(context);

        Assert.False(result.Cleared);
        Assert.Equal(nameof(InspectionStage), result.HaltedStage);
        Assert.Equal(
            [nameof(DocumentCheckStage), nameof(DutyCalculationStage), nameof(InspectionStage)],
            context.StagesRun);
        Assert.DoesNotContain(nameof(ReleaseStage), context.StagesRun);
    }

    [Fact]
    public void Handle_calculates_duty_for_a_passing_declaration()
    {
        var declaration = new CustomsDeclaration(
            ConsignmentId: "CN-1004",
            DeclaredValue: 2500m,
            DocumentsComplete: true,
            RequiresInspection: true,
            InspectionPassed: true);
        var context = new ClearanceContext(declaration);
        var pipeline = CustomsClearancePipeline.Standard();

        var result = pipeline.Handle(context);

        Assert.True(result.Cleared);
        Assert.Equal(200m, result.Duty);
        Assert.Equal(200m, context.Duty);
    }
}
