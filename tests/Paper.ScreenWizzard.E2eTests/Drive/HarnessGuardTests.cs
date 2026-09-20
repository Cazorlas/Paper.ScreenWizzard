using NUnit.Framework;
using Paper.ScreenWizzard.E2eTests.Support;

namespace Paper.ScreenWizzard.E2eTests.Drive;

/// <summary>The guard that keeps the drive tests away from a copy of the app they did not start.</summary>
[TestFixture]
public sealed class HarnessGuardTests : DriveBase
{
    [Test]
    public void ACopyTheRunDidNotStartIsRefusedAndTheOneItStartedIsNot()
    {
        NewApp();
        StartAndWaitForBar();

        var other = HarnessGuard.Refusal([]);
        var mine = HarnessGuard.Refusal([App.Process!.Id]);

        Assert.That(other, Does.Contain("did not start"), "with nothing owned, the running copy counts as somebody else's");
        Assert.That(mine, Is.Null, "the copy this test started is not a reason to refuse");
    }
}
