namespace Auth.Api.Middleware;

public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";

    private const string ItemKey = "CorrelationId";

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            string correlationId = Read(context);
            context.Items[ItemKey] = correlationId;

            context.Response.Headers[HeaderName] = correlationId;

            ILoggerFactory loggers = context.RequestServices.GetRequiredService<ILoggerFactory>();
            ILogger logger = loggers.CreateLogger("Request");
            using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            {
                await next();
            }
        });
    }

    public static string Of(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out object? value) && value is string correlationId)
        {
            return correlationId;
        }

        return Read(context);
    }

    private static string Read(HttpContext context)
    {
        string? provided = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(provided))
        {
            return provided.Trim().Length <= 100 ? provided.Trim() : provided.Trim()[..100];
        }

        return Guid.NewGuid().ToString();
    }
}
