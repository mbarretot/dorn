using FluentValidation;

namespace CleanArchWorkerService.Application.Todos.CreateTodoItem;

public sealed class CreateTodoItemCommandValidator : AbstractValidator<CreateTodoItemCommand>
{
    public CreateTodoItemCommandValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
    }
}
