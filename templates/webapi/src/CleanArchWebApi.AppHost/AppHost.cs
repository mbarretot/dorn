var builder = DistributedApplication.CreateBuilder(args);

#if (UseSqlServer)
var sql = builder.AddSqlServer("sql").AddDatabase("CleanArchWebApi");
builder.AddProject<Projects.CleanArchWebApi_WebApi>("webapi").WithReference(sql);
#else
builder.AddProject<Projects.CleanArchWebApi_WebApi>("webapi");
#endif

builder.Build().Run();
