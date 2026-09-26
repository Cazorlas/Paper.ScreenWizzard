using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.UnitTests.Shell.Fakes;

/// <summary>The newest release as the test sets it; counts how often it was asked.</summary>
public sealed class FakeReleaseFeed : IReleaseFeed
{
    public ReleaseFeedResult Answer { get; set; } = new(null, "not set");

    public int Calls { get; private set; }

    public Task<ReleaseFeedResult> GetLatestAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Answer);
    }
}

public sealed class FakeBrowser : IBrowser
{
    public List<string> Opened { get; } = [];

    public PortResult Result { get; set; } = PortResult.Ok;

    public PortResult Open(string url)
    {
        Opened.Add(url);
        return Result;
    }
}
