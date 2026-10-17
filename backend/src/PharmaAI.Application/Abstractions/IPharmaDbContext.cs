using Microsoft.EntityFrameworkCore;
using PharmaAI.Domain.Entities;

namespace PharmaAI.Application.Abstractions;

public interface IPharmaDbContext
{
    DbSet<AppSetting> AppSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
