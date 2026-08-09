var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CleanArchWorkerService_Worker>("worker");

builder.Build().Run();
