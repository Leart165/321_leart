using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;

namespace Analytics.Infrastructure.Bank;

// Zeitlimit, Wiederholung und Circuit Breaker auf dem Weg zur Bank, aufgebaut wie beim
// Auszugsdienst der Bank auf dem Weg zum Kontendienst.
//
// Von aussen nach innen: Zeitlimit für alles, Wiederholung, Kreis, Zeitlimit je Versuch. Der Kreis
// sitzt innerhalb der Wiederholung und zählt so jeden einzelnen Versuch. Ist er offen, wirft er
// sofort, ohne die Bank zu fragen: der Kunde bekommt ohne Wartezeit den gespeicherten Stand.
//
// Gezählt werden nur vorübergehende Fehler: keine Verbindung, Zeitüberschreitung, 408, 429 und
// 5xx. Ein 401 oder 403 ist eine Antwort über einen einzelnen Kunden und öffnet den Kreis nicht,
// sonst könnte ein Kunde ohne Zustimmung die Bank für alle abschalten.
public static class BankResilience
{
    public const string PipelineName = "statements-api";

    public static IServiceCollection AddBankResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline<string, HttpResponseMessage>(PipelineName, (pipeline, context) =>
        {
            BankResilienceOptions options = context.ServiceProvider
                .GetRequiredService<IOptions<BankResilienceOptions>>().Value;
            ILogger logger = context.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(BankResilience));

            pipeline
                .AddTimeout(options.TotalTimeout)
                .AddRetry(Retry(options))
                .AddCircuitBreaker(CircuitBreaker(options, logger))
                .AddTimeout(options.AttemptTimeout);
        });

        return services;
    }

    public static IHttpClientBuilder AddBankResilienceHandler(this IHttpClientBuilder builder)
    {
        return builder.AddHttpMessageHandler(services => new ResilienceHandler(services
            .GetRequiredService<ResiliencePipelineProvider<string>>()
            .GetPipeline<HttpResponseMessage>(PipelineName)));
    }

    // Ob die Bank nach dieser Ausnahme als nicht erreichbar gilt.
    public static bool IsUnavailable(Exception exception)
    {
        return exception is HttpRequestException
            or TaskCanceledException
            or TimeoutException
            or TimeoutRejectedException;
    }

    private static HttpRetryStrategyOptions Retry(BankResilienceOptions options)
    {
        HttpRetryStrategyOptions retry = new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetryAttempts,
            Delay = options.RetryDelay,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,

            // Nennt die Bank mit Retry-After selbst eine Wartezeit, ist sie überlastet und sagt das.
            // Nachfragen ginge gegen ihre Bitte; der Kreis zählt den Fehler trotzdem.
            ShouldHandle = arguments => ValueTask.FromResult(
                HttpClientResiliencePredicates.IsTransient(arguments.Outcome)
                && arguments.Outcome.Result?.Headers.RetryAfter is null)
        };

        retry.DisableForUnsafeHttpMethods();
        return retry;
    }

    private static HttpCircuitBreakerStrategyOptions CircuitBreaker(BankResilienceOptions options, ILogger logger)
    {
        CircuitBreakerStateProvider state = new CircuitBreakerStateProvider();

        return new HttpCircuitBreakerStrategyOptions
        {
            StateProvider = state,

            // Bricht der Kunde genau den Probeaufruf ab, ist offen, ob die Bank zurück ist. Der
            // Abbruch zählt im halb offenen Zustand deshalb als Fehler, und der Kreis bleibt offen.
            ShouldHandle = arguments => ValueTask.FromResult(
                HttpClientResiliencePredicates.IsTransient(arguments.Outcome)
                || (arguments.Outcome.Exception is OperationCanceledException
                    && state.CircuitState == CircuitState.HalfOpen)),
            FailureRatio = options.FailureRatio,
            MinimumThroughput = options.MinimumThroughput,
            SamplingDuration = options.SamplingDuration,
            BreakDuration = options.BreakDuration,
            OnOpened = arguments =>
            {
                logger.LogWarning(
                    "Kreis zur Bank offen für {BreakDuration}, Kunden sehen ihren gespeicherten Stand.",
                    arguments.BreakDuration);
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = _ =>
            {
                logger.LogInformation("Pause vorbei, ein Probeaufruf geht an die Bank.");
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                logger.LogInformation("Kreis zur Bank wieder zu, die Buchungen kommen wieder frisch.");
                return ValueTask.CompletedTask;
            }
        };
    }
}
