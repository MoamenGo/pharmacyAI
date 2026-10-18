using MediatR;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Sync;

namespace PharmaAI.Application.Sync.Commands.RetryOutboxMessage;

/// <summary>Sends one message again now; a rejected receipt is rebuilt as a correction of the rejected one.</summary>
public sealed record RetryOutboxMessageCommand(Guid Id) : IRequest;

public sealed class RetryOutboxMessageCommandHandler(IPharmaDbContext db, TimeProvider clock, ISyncTrigger trigger)
    : IRequestHandler<RetryOutboxMessageCommand>
{
    public async Task Handle(RetryOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.SingleOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(OutboxMessage), request.Id);

        message.RetryNow(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
        trigger.RequestSync();
    }
}
