using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CleanArchWebApi.Infrastructure.Repositories.Dapper;

public class DapperContext
{
    private readonly string _connectionString;

    public DapperContext(IConfiguration configuration)
    {
#if (UseSqlServer)
        _connectionString = configuration.GetConnectionString("CleanArchWebApi")!;
#else
        _connectionString = configuration.GetConnectionString("Default")!;
#endif
    }

    public IDbConnection CreateConnection()
    {
#if (UseSqlServer)
        return new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
#else
        return new SqliteConnection(_connectionString);
#endif
    }
}
