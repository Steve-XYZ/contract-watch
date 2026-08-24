namespace ContractWatch.Core.Explanations;

public interface IExplanationProvider
{
    string Name { get; }

    Task<string?> ExplainAsync(ContractChange change, CancellationToken cancellationToken = default);
}

public static class ExplanationProviders
{
    public const string Fake = "fake";
    public const string OpenAi = "openai";

    public static readonly IReadOnlySet<string> Known = new HashSet<string>([Fake, OpenAi], StringComparer.Ordinal);
}

public sealed record ExplanationSettings(string Provider, string? Model);

public sealed class ExplanationConfigurationException : Exception
{
    public ExplanationConfigurationException(string message)
        : base(message)
    {
    }
}

public sealed class ExplanationProviderException : Exception
{
    public ExplanationProviderException(string message)
        : base(message)
    {
    }
}

public static class ExplanationOptions
{
    public const string KeyEnvironmentVariable = "CONTRACTWATCH_AI_KEY";
    public const string BaseUrlEnvironmentVariable = "CONTRACTWATCH_AI_BASE_URL";

    public static ExplanationSettings? Resolve(string? flagProvider, string? policyProvider, string? flagModel, string? policyModel)
    {
        var provider = flagProvider ?? policyProvider;

        return provider is null ? null : new ExplanationSettings(provider, flagModel ?? policyModel);
    }
}
