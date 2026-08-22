namespace ContractWatch.Core.Rules;

public static class RuleCatalog
{
    public static readonly IReadOnlyList<IContractRule> Default =
    [
        new EndpointRemoved(),
        new OperationRemoved(),
        new RequiredParameterAdded(),
        new RequiredPropertyAdded(),
        new ParameterTypeChanged(),
        new ResponseStatusRemoved(),
        new ResponsePropertyTypeChanged(),
        new ResponsePropertyRemoved(),
        new EndpointAdded(),
        new OptionalParameterAdded(),
        new OptionalPropertyAdded(),
        new ResponseStatusAdded(),
    ];
}
