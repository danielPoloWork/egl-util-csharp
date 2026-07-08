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
    public async Task Aggregates_failures_from_validators_running_genuinely_concurrently_over_separate_contexts()
    {
        // Regression for #52: the behavior runs every validator via Task.WhenAll, and a ValidationContext
        // is not safe for concurrent use. Each round exercises genuinely-overlapping async validators (each
        // awaits a barrier until all have entered), so if they shared one context a race would surface as
        // lost or duplicated failures. With a fresh context per validator every distinct failure aggregates
        // exactly once. This guards a data race — against the pre-fix shared-context code the k failures
        // nondeterministically collapse onto one validator's message — so it repeats over many rounds: any
        // single round may miss the race window, but a re-regression is caught with near-certainty across all.
        for (var round = 0; round < 32; round++)
        {
            await AssertConcurrentValidatorsEachAggregateTheirOwnFailure();
        }
    }

    private static async Task AssertConcurrentValidatorsEachAggregateTheirOwnFailure()
    {
        const int k = 8;
        var entered = 0;
        var allEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        IValidator<CreateUser> GatedValidator(int index)
        {
            var validator = new InlineValidator<CreateUser>();
            validator.RuleFor(request => request.Email).MustAsync(async (_, _) =>
            {
                if (Interlocked.Increment(ref entered) == k)
                {
                    allEntered.SetResult(); // release only once all k validators are concurrently in-flight
                }

                await allEntered.Task.ConfigureAwait(false);
                return false; // every validator fails, with its own distinct message
            }).WithMessage("fail-" + index);
            return validator;
        }

        IValidator<CreateUser>[] validators = Enumerable.Range(0, k).Select(GatedValidator).ToArray();
        var behavior = new ValidationPipelineBehavior<CreateUser, string>(validators);

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(new CreateUser("user@example.com"), _ => Task.FromResult("created"), CancellationToken.None));

        // All k validators overlapped, and every distinct failure survived aggregation over its own context.
        Assert.Equal(k, entered);
        Assert.Equal(
            Enumerable.Range(0, k).Select(index => "fail-" + index).OrderBy(message => message),
            exception.Errors.Select(failure => failure.ErrorMessage).OrderBy(message => message));
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
