using Microsoft.EntityFrameworkCore;
using PharmaAI.Infrastructure.Backup;
using PharmaAI.Infrastructure.Persistence;

namespace PharmaAI.Api.Endpoints;

internal static class BackupEndpoints
{
    public static RouteGroupBuilder MapBackupEndpoints(this RouteGroupBuilder api)
    {
        var backups = api.MapGroup("/backups");

        backups.MapGet("/", (DatabaseBackups store) => Results.Ok(store.List()));

        backups.MapPost("/", async (DatabaseBackups store, CancellationToken ct) => Results.Ok(await store.CreateAsync(ct)));

        backups.MapGet("/{name}", (string name, DatabaseBackups store) =>
            store.PathOf(name) is { } path
                ? Results.File(path, "application/vnd.sqlite3", name)
                : Results.NotFound());

        backups.MapPost("/{name}/restore", async (string name, DatabaseBackups store, PharmaDbContext db, CancellationToken ct) =>
        {
            if (store.PathOf(name) is null)
            {
                return Results.NotFound();
            }

            var safety = await store.RestoreAsync(name, ct);
            // A backup from an older version is brought up to the current schema.
            await db.Database.MigrateAsync(ct);
            return Results.Ok(new { restored = name, safetyBackup = safety.Name });
        });

        return api;
    }
}
