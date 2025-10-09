var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.PetitionService_Server>("petitionservice-server");

builder.Build().Run();
