using PharmaAI.Domain.Common;
using PharmaAI.Domain.Sync;

namespace PharmaAI.Application.Tests.Domain;

public class OutboxMessageTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc);

    private static OutboxMessage NewMessage() => OutboxMessage.ForEtaReceipt(Guid.NewGuid(), 1, Now);

    [Fact]
    public void Temporary_failures_back_off_from_30_seconds_up_to_an_hour()
    {
        var message = NewMessage();
        var delays = new List<TimeSpan>();

        for (var i = 0; i < 10; i++)
        {
            message.MarkFailedTemporarily("offline", Now);
            delays.Add(message.NextAttemptAtUtc - Now);
        }

        Assert.Equal(
            [30, 60, 120, 240, 480, 960, 1920, 3600, 3600, 3600],
            delays.Select(d => (int)d.TotalSeconds));
        Assert.Equal((OutboxStatus.Pending, 10, "offline"), (message.Status, message.Attempts, message.LastError));
        Assert.False(message.IsDue(Now));
        Assert.True(message.IsDue(Now.AddHours(1)));
    }

    [Fact]
    public void A_sent_message_is_final()
    {
        var message = NewMessage();
        message.MarkSent(Now);

        Assert.Equal("outbox.already_sent", Assert.Throws<DomainException>(() => message.RetryNow(Now, rebuildDocument: false)).Code);
        Assert.Throws<DomainException>(() => message.MarkSent(Now));
        Assert.False(message.IsDue(Now));
    }

    [Fact]
    public void A_rejected_message_waits_for_a_person_then_can_be_rebuilt()
    {
        var message = NewMessage();
        message.AttachDocument("{}", "uuid-1");
        message.MarkRejected("bad item code", Now);
        Assert.False(message.IsDue(Now.AddDays(1)));

        message.RetryNow(Now, rebuildDocument: true);

        Assert.True(message.IsDue(Now));
        Assert.Null(message.Document);
        message.AttachDocument("{}", "uuid-2");
        Assert.Equal("uuid-2", message.ExternalId);
    }

    [Fact]
    public void The_document_is_frozen_once_built() =>
        Assert.Throws<DomainException>(() =>
        {
            var message = NewMessage();
            message.AttachDocument("{}", "uuid-1");
            message.AttachDocument("{}", "uuid-2");
        });

    [Fact]
    public void Long_errors_are_truncated()
    {
        var message = NewMessage();
        message.MarkFailedTemporarily(new string('x', 5000), Now);

        Assert.Equal(OutboxMessage.ErrorMaxLength, message.LastError!.Length);
    }
}
