global using System.Text.RegularExpressions;
global using ArchUnitNET.Domain;
global using static ArchUnitNET.Fluent.ArchRuleDefinition;
global using ArchUnitNET.Loader;
global using ArchUnitNET.xUnit;
global using CleanArchWorkerService.Application.Todos.ProcessPendingTodoItems;
global using CleanArchWorkerService.Domain.Entities;
global using CleanArchWorkerService.Infrastructure.Persistence;
global using Dorn.Messaging.Contracts;
global using Xunit;
// "Architecture" collides with this project's own namespace segment
// (CleanArchWorkerService.Architecture.Tests) — alias to the ArchUnitNET model type explicitly.
global using ArchitectureModel = ArchUnitNET.Domain.Architecture;
