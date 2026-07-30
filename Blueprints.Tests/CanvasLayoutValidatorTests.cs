using Blueprints.Core.Models;
using Blueprints.Core.Services;

namespace Blueprints.Tests;

public sealed class CanvasLayoutValidatorTests
{
    [Fact]
    public void Validate_AcceptsFiniteUniqueWorkspaceNodes()
    {
        var projectId = Guid.NewGuid();
        var layout = CreateLayout(
            projectId,
            [
                new CanvasNodePosition("project", projectId, 20, 30),
                new CanvasNodePosition("version", Guid.NewGuid(), 400, 120),
                new CanvasNodePosition("item", Guid.NewGuid(), 760, 180),
            ]);

        CanvasLayoutValidator.Validate(layout, projectId);
    }

    [Fact]
    public void Validate_RejectsDuplicateNodeIdentity()
    {
        var projectId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var layout = CreateLayout(
            projectId,
            [
                new CanvasNodePosition("version", versionId, 100, 100),
                new CanvasNodePosition("version", versionId, 200, 200),
            ]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => CanvasLayoutValidator.Validate(layout, projectId));

        Assert.Contains("duplicated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(100_001)]
    public void Validate_RejectsUnsafeCoordinates(double coordinate)
    {
        var projectId = Guid.NewGuid();
        var layout = CreateLayout(
            projectId,
            [new CanvasNodePosition("project", projectId, coordinate, 100)]);

        Assert.Throws<InvalidOperationException>(
            () => CanvasLayoutValidator.Validate(layout, projectId));
    }

    [Fact]
    public void Validate_RejectsAnotherWorkspaceIdentity()
    {
        var layout = CreateLayout(Guid.NewGuid(), []);

        var exception = Assert.Throws<InvalidOperationException>(
            () => CanvasLayoutValidator.Validate(layout, Guid.NewGuid()));

        Assert.Contains("does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CanvasLayoutDocument CreateLayout(
        Guid projectId,
        IReadOnlyList<CanvasNodePosition> nodes) =>
        new(
            1,
            projectId,
            1,
            nodes,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "Test User");
}
