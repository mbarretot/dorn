global using CleanArchWorkerService.Application.Todos.ProcessPendingTodoItems;
global using CleanArchWorkerService.Domain.Entities;
global using CleanArchWorkerService.Infrastructure.DependencyInjection;
global using CleanArchWorkerService.Infrastructure.Persistence;
global using CleanArchWorkerService.Worker;
global using CleanArchWorkerService.Worker.DependencyInjection;
global using Dorn.Messaging;
global using FluentValidation;
global using Microsoft.Data.Sqlite;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Options;
global using Microsoft.Extensions.Time.Testing;
global using Xunit;
// The instance property below is named Host; alias the static factory to avoid the name collision.
global using HostFactory = Microsoft.Extensions.Hosting.Host;
