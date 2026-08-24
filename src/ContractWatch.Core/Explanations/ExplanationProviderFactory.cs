namespace ContractWatch.Core.Explanations;

public static class ExplanationProviderFactory
{
    public static IExplanationProvider Create(ExplanationSettings settings, Func<string, string?>? lookupEnvironmentVariable = null, HttpMessageHandler? httpHandler = null)
    {
        var lookup = lookupEnvironmentVariable ?? Environment.GetEnvironmentVariable;

        return settings.Provider switch
        {
            ExplanationProviders.Fake => new FakeExplanationProvider(),
            ExplanationProviders.OpenAi => CreateOpenAi(settings, lookup, httpHandler),
            _ => throw new ExplanationConfigurationException($"proveedor de explicación desconocido '{settings.Provider}' (fake|openai)"),
        };
    }

    private static IExplanationProvider CreateOpenAi(ExplanationSettings settings, Func<string, string?> lookup, HttpMessageHandler? httpHandler)
    {
        var key = lookup(ExplanationOptions.KeyEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(key))
            throw new ExplanationConfigurationException(
                $"--explain openai requiere la clave de API en la variable de entorno {ExplanationOptions.KeyEnvironmentVariable}");

        var baseUrl = lookup(ExplanationOptions.BaseUrlEnvironmentVariable);

        if (baseUrl is { } custom && (!Uri.TryCreate(custom, UriKind.Absolute, out _) || !custom.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
            throw new ExplanationConfigurationException(
                $"la variable de entorno {ExplanationOptions.BaseUrlEnvironmentVariable} debe ser una URL absoluta http(s), no '{custom}'");

        return new OpenAiExplanationProvider(httpHandler ?? new HttpClientHandler(), key!, settings.Model, baseUrl);
    }
}
