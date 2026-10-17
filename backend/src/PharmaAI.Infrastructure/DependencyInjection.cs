using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaAI.Application.Abstractions;
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

        return services;
    }
}
