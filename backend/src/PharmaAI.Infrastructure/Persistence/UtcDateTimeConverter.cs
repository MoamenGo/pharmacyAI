using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PharmaAI.Infrastructure.Persistence;

/// <summary>SQLite stores DateTime as text without a kind; every DateTime in this app is UTC, so mark it on read.</summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
