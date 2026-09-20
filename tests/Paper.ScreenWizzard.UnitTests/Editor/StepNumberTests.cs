using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.UnitTests.Editor.Fakes;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.UnitTests.Editor;

/// <summary>SPEC editor, "Số bước": numbers climb, and a deleted number is not filled in again.</summary>
[TestFixture]
public sealed class StepNumberTests
{
    private EditorFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EditorFixture();

    private static int[] Numbers(IEditorSession session) =>
        session.Document.Annotations.OfType<StepAnnotation>().Select(s => s.Number).ToArray();

    /// <summary>Places three markers the way the window does: each one takes the number the session offers.</summary>
    private IEditorSession WithThreeSteps()
    {
        var session = _fixture.NewSession();
        for (var n = 1; n <= 3; n++)
        {
            session.Add(EditorData.Step(n, session.NextStepNumber, n * 30, 20));
        }

        return session;
    }

    [Test]
    public void AnImageWithNoNumbersStartsAtOneThenTwoThenThree()
    {
        var session = _fixture.NewSession();
        Assert.That(session.NextStepNumber, Is.EqualTo(1));

        session.Add(EditorData.Step(1, session.NextStepNumber));
        Assert.That(session.NextStepNumber, Is.EqualTo(2));

        session.Add(EditorData.Step(2, session.NextStepNumber));
        Assert.That(session.NextStepNumber, Is.EqualTo(3));

        session.Add(EditorData.Step(3, session.NextStepNumber));
        Assert.That(Numbers(session), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(session.NextStepNumber, Is.EqualTo(4));
    }

    [Test]
    public void WithOneTwoThreeDeletingThreeMakesTheNextNumberThree()
    {
        var session = WithThreeSteps();

        session.Delete(EditorData.Id(3));

        Assert.That(Numbers(session), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(session.NextStepNumber, Is.EqualTo(3));
    }

    [Test]
    public void WithOneTwoThreeDeletingTwoLeavesOneAndThreeAndTheNextNumberIsFour()
    {
        var session = WithThreeSteps();

        session.Delete(EditorData.Id(2));

        Assert.That(Numbers(session), Is.EqualTo(new[] { 1, 3 }), "nothing is renumbered");
        Assert.That(session.NextStepNumber, Is.EqualTo(4));
    }

    [Test]
    public void OtherShapesDoNotCountAndUndoingAMarkerGivesItsNumberBack()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(10, 0, 0, 10, 10));
        Assert.That(session.NextStepNumber, Is.EqualTo(1));

        session.Add(EditorData.Step(1, 1));
        session.Add(EditorData.Step(2, 2));
        Assert.That(session.NextStepNumber, Is.EqualTo(3));

        session.Undo();
        Assert.That(session.NextStepNumber, Is.EqualTo(2));
    }
}
