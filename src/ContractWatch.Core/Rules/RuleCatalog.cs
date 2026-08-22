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
        new RequestEnumNarrowed(),
        new ResponseStatusRemoved(),
        new ResponsePropertyTypeChanged(),
        new ResponsePropertyRemoved(),
        new ResponseEnumWidened(),
        new RequiredResponsePropertyAdded(),
        new NullableRemoved(),
        new EndpointAdded(),
        new OptionalParameterAdded(),
        new OptionalPropertyAdded(),
        new RequestEnumWidened(),
        new ResponseStatusAdded(),
        new MetadataOnlyChanged(),
    ];
}
