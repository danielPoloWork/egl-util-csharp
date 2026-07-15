// global:: — the containing namespace It.D4np.Util.MediatR(.FluentValidation) would otherwise shadow the
// MediatR and FluentValidation packages.
using global::FluentValidation;
using global::FluentValidation.Results;

using global::MediatR;

namespace It.D4np.Util.MediatR.FluentValidation;

/// <summary>
/// A MediatR pipeline behavior that runs every registered FluentValidation <see cref="IValidator{T}"/>
/// for <typeparamref name="TRequest"/> before the request reaches its handler. If any validator reports
/// failures, the behavior short-circuits the pipeline by throwing a <see cref="ValidationException"/>
/// carrying the aggregated failures; otherwise it invokes the rest of the pipeline. Pipeline behaviors
/// form a Chain of Responsibility — each decides whether to pass the request to <c>next</c> or stop it.
/// Part of the opt-in <c>It.D4np.Util.MediatR</c> package (#38).
/// </summary>
/// <typeparam name="TRequest">The request type flowing through the pipeline.</typeparam>
/// <typeparam name="TResponse">The response type the downstream handler produces.</typeparam>
public sealed class ValidationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Creates the behavior over the validators registered for <typeparamref name="TRequest"/>.</summary>
    /// <param name="validators">
    /// The validators to run; an empty sequence means the request passes through unvalidated.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="validators"/> is <see langword="null"/>.</exception>
    public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _validators = validators;
    }

    /// <summary>
    /// Validates <paramref name="request"/> with every registered validator and either short-circuits
    /// with a <see cref="ValidationException"/> or delegates to the next step in the pipeline.
    /// </summary>
    /// <remarks>
    /// The validators run concurrently, each against its <b>own</b> <c>ValidationContext</c> — a context
    /// is not safe for concurrent use, so a shared one would let genuinely-async validators corrupt each
    /// other's failures. All reported failures are aggregated into a single <see cref="ValidationException"/>.
    /// </remarks>
    /// <param name="request">The incoming request to validate.</param>
    /// <param name="next">The next delegate in the pipeline; must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel validation and downstream handling.</param>
    /// <returns>The response produced by the rest of the pipeline when validation passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="next"/> is <see langword="null"/>.</exception>
    /// <exception cref="ValidationException">One or more validators reported a failure.</exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (!_validators.Any())
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        // Each validator gets its OWN ValidationContext. The validators run concurrently via
        // Task.WhenAll, and a ValidationContext is not safe for concurrent use — sharing a single instance
        // lets genuinely-async validators corrupt each other's failures via the context's shared state (#52).
        ValidationResult[] results = await Task.WhenAll(
            _validators.Select(validator =>
                validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken))).ConfigureAwait(false);

        List<ValidationFailure> failures = results
            .Where(result => !result.IsValid)
            .SelectMany(result => result.Errors)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
