using PharmaAI.Application.Abstractions;
using PharmaAI.Application.SystemStatus.Queries.GetSystemStatus;
using PharmaAI.Application.Tests.TestSupport;

namespace PharmaAI.Application.Tests.SystemStatus;

public class GetSystemStatusQueryHandlerTests
{
    private sealed class StubDiagnostics(string journalMode) : IDatabaseDiagnostics
    {
        public Task<string> GetJournalModeAsync(CancellationToken cancellationToken) => Task.FromResult(journalMode);
    }

    [Theory]
    [InlineData("wal", "ok")]
    [InlineData("WAL", "ok")]
    [InlineData("delete", "degraded")]
    public async Task Reports_ok_only_when_database_is_in_wal_mode(string journalMode, string expectedStatus)
    {
        var handler = new GetSystemStatusQueryHandler(new StubDiagnostics(journalMode), new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var status = await handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        Assert.Equal(expectedStatus, status.Status);
        Assert.Equal(journalMode, status.JournalMode);
    }
}
