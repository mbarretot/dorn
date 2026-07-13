var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CleanArchWebApi_WebApi>("webapi");

builder.Build().Run();
