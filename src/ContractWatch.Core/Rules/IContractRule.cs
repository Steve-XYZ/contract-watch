namespace ContractWatch.Core.Rules;

public interface IContractRule
{
    IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current);
}
