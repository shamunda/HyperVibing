using Watchdog.Server.Models;
using Watchdog.Server.Services;

namespace Watchdog.Tests;

public class CorrectionServiceTests
{
    private readonly CorrectionService _sut = new();

    [Theory]
    [InlineData("no, don't do that")]
    [InlineData("stop doing that")]
    [InlineData("undo that")]
    [InlineData("revert this")]
    [InlineData("wrong approach")]
    [InlineData("ignore previous")]
    [InlineData("that was wrong")]
    public void Detect_CorrectionPhrases_ReturnSignal(string input)
    {
        var ev = MakeEvent(input);
        var signal = _sut.Detect(ev);

        Assert.NotNull(signal);
        Assert.Equal("explicit_correction", signal.SignalType);
        Assert.Equal("test-proj", signal.Project);
    }

    [Theory]
    [InlineData("please continue with the current approach")]
    [InlineData("looks good, proceed")]
    [InlineData("run the build")]
    [InlineData("")]
    public void Detect_NormalInput_ReturnsNull(string input)
    {
        var ev = MakeEvent(input);
        var signal = _sut.Detect(ev);

        Assert.Null(signal);
    }

    [Fact]
    public void Detect_LongInput_TruncatesContent()
    {
        var longInput = "no, don't do that " + new string('x', 300);
        var ev = MakeEvent(longInput);
        var signal = _sut.Detect(ev);

        Assert.NotNull(signal);
        Assert.True(signal.Content.Length <= 200);
    }

    private static StreamEvent MakeEvent(string input) => new(
        Ts:           DateTimeOffset.UtcNow,
        SessionId:    "test",
        Project:      "test-proj",
        ToolName:     "bash",
        ToolInput:    input,
        ToolResponse: null,
        Outcome:      "success");
}
