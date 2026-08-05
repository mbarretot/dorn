global using System.Text.RegularExpressions;
global using ArchUnitNET.Domain;
global using static ArchUnitNET.Fluent.ArchRuleDefinition;
global using ArchUnitNET.Loader;
global using ArchUnitNET.xUnit;
global using CleanArchGrpcService.Application.Todos.CreateTodoItem;
global using CleanArchGrpcService.Domain.Entities;
global using CleanArchGrpcService.Infrastructure.Persistence;
global using Dorn.Messaging.Contracts;
global using Xunit;
// "Architecture" collides with this project's own namespace segment
// (CleanArchGrpcService.Architecture.Tests) — alias to the ArchUnitNET model type explicitly.
global using ArchitectureModel = ArchUnitNET.Domain.Architecture;
