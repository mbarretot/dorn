var builder = DistributedApplication.CreateBuilder(args);

#if (UseSqlite)
builder.AddProject<Projects.CleanArchWebApi_WebApi>("webapi");
#elif (UseSqlServer)
var sql = builder.AddSqlServer("sql").AddDatabase("CleanArchWebApi");
builder.AddProject<Projects.CleanArchWebApi_WebApi>("webapi").WithReference(sql);
#elif (UsePostgres)
// Postgres provider wiring lands in Slice B.
#endif

builder.Build().Run();
