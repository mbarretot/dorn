var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CleanArchGrpcService_Grpc>("grpc");

builder.Build().Run();
