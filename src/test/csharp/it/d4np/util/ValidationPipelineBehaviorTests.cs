using FluentValidation;

using It.D4np.Util.Application;

using MediatR;

namespace It.D4np.Util.Tests;

public class ValidationPipelineBehaviorTests
{
    private sealed record CreateUser(string Email) : IRequest<string>;

    private sealed class CreateUserValidator : AbstractValidator<CreateUser>
    {
        public CreateUserValidator() => RuleFor(request => request.Email).NotEmpty().EmailAddress();
    }

    private sealed class AlwaysFailsValidator : AbstractValidator<CreateUser>
    {
        public AlwaysFailsValidator() =>
            RuleFor(request => request.Email).Must(_ => false).WithMessage("always fails");
    }

    private static ValidationPipelineBehavior<CreateUser, string> Behavior(params IValidator<CreateUser>[] validators) =>
        new(validators);

    [Fact]
    public async Task Invokes_the_next_step_when_validation_passes()
    {
        var nextCalled = false;
        Task<string> Next(CancellationToken _)
        {
            nextCalled = true;
            return Task.FromResult("created");
        }

        ValidationPipelineBehavior<CreateUser, string> behavior = Behavior(new CreateUserValidator());
        string response = await behavior.Handle(new CreateUser("user@example.com"), Next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("created", response);
    }

    [Fact]
    public async Task Short_circuits_with_a_ValidationException_when_validation_fails()
    {
        var nextCalled = false;
        Task<string> Next(CancellationToken _)
        {
            nextCalled = true;
            return Task.FromResult("created");
        }

        ValidationPipelineBehavior<CreateUser, string> behavior = Behavior(new CreateUserValidator());

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(new CreateUser(string.Empty), Next, CancellationToken.None));

        Assert.False(nextCalled); // the downstream handler must not run
        Assert.NotEmpty(exception.Errors);
    }

    [Fact]
    public async Task Aggregates_failures_from_every_registered_validator()
    {
        ValidationPipelineBehavior<CreateUser, string> behavior =
            Behavior(new CreateUserValidator(), new AlwaysFailsValidator());

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(new CreateUser(string.Empty), _ => Task.FromResult("created"), CancellationToken.None));

        Assert.Contains(exception.Errors, failure => failure.PropertyName == nameof(CreateUser.Email));
        Assert.Contains(exception.Errors, failure => failure.ErrorMessage == "always fails");
    }

    [Fact]
    public async Task Passes_through_when_no_validators_are_registered()
    {
        ValidationPipelineBehavior<CreateUser, string> behavior = Behavior();

        string response = await behavior.Handle(
            new CreateUser(string.Empty), _ => Task.FromResult("created"), CancellationToken.None);

        Assert.Equal("created", response);
    }

    [Fact]
    public void Constructor_rejects_null_validators()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ValidationPipelineBehavior<CreateUser, string>(null!));
    }

    [Fact]
    public async Task Handle_rejects_a_null_next_delegate()
    {
        // Use a validator that would pass for a valid request, so the ArgumentNullException can only
        // come from the guard on `next` — never from the empty-validators pass-through path.
        ValidationPipelineBehavior<CreateUser, string> behavior = Behavior(new CreateUserValidator());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => behavior.Handle(new CreateUser("user@example.com"), null!, CancellationToken.None));
    }
}
