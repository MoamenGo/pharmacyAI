using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaAI.Application.Abstractions;
using PharmaAI.Application.Catalog;
using PharmaAI.Application.Sync;
using PharmaAI.Application.Sync.Eta;
using PharmaAI.Infrastructure.Backup;
using PharmaAI.Infrastructure.Catalog;
using PharmaAI.Infrastructure.Sync;
using PharmaAI.Infrastructure.Persistence;

namespace PharmaAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var databasePath = options.ResolvePath();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = true,
        }.ToString();

        services.AddSingleton<SqlitePragmaInterceptor>();
        services.AddDbContext<PharmaDbContext>((sp, db) =>
            db.UseSqlite(connectionString)
              .AddInterceptors(sp.GetRequiredService<SqlitePragmaInterceptor>()));

        services.AddScoped<IPharmaDbContext>(sp => sp.GetRequiredService<PharmaDbContext>());
        services.AddScoped<IDatabaseDiagnostics, DatabaseDiagnostics>();

        // Parsed on first use (the first catalog search), not at startup.
        services.AddSingleton(_ => EmbeddedDrugCatalog.Load());

        services.AddSingleton(sp => new DatabaseBackups(databasePath, sp.GetRequiredService<TimeProvider>()));
        if (configuration.GetValue("Backup:Automatic", true))
        {
            services.AddHostedService<AutomaticBackupService>();
        }

        services.AddSingleton<SyncSignal>();
        services.AddSingleton<ISyncTrigger>(sp => sp.GetRequiredService<SyncSignal>());
        services.AddSingleton<EtaTokenCache>();
        services.AddHttpClient<IEtaClient, EtaHttpClient>(c => c.Timeout = TimeSpan.FromSeconds(60));
        if (configuration.GetValue("Sync:Worker", true))
        {
            services.AddHostedService<OutboxWorker>();
        }

        return services;
    }
}
