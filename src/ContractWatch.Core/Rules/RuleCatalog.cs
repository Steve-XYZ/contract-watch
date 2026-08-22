namespace ContractWatch.Core.Rules;

public static class RuleCatalog
{
    public static readonly IReadOnlyList<IContractRule> Default =
    [
        new EndpointRemoved(),
        new OperationRemoved(),
        new RequiredParameterAdded(),
        new RequiredPropertyAdded(),
        new ResponseStatusRemoved(),
        new EndpointAdded(),
        new OptionalParameterAdded(),
        new OptionalPropertyAdded(),
        new ResponseStatusAdded(),
    ];
}
