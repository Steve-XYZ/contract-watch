using System.Collections.Frozen;

namespace ContractWatch.Core.Rules;

public static class RemediationCatalog
{
    private static readonly FrozenDictionary<string, string> Remediations = new Dictionary<string, string>
    {
        ["CW001"] = "Keep the endpoint available during a deprecation period announced in the changelog before removing it. If removal is final, return 410 Gone with migration notes pointing consumers to the replacement.",
        ["CW002"] = "Keep the operation during a deprecation period announced in the changelog before removing it. If removal is final, respond 410 Gone with migration notes describing the replacement.",
        ["CW003"] = "Introduce the parameter as optional with a server-side default and promote it to required only in a major version. Alternatively publish a versioned operation so existing callers keep the previous contract.",
        ["CW004"] = "Introduce the property as optional with a default value and promote it to required only in a major version. Alternatively publish a new versioned operation for callers that can supply it.",
        ["CW005"] = "Accept both the old and the new type during a transition period (a union or a tolerant parser works well) and tighten to the new type in the next major release.",
        ["CW006"] = "Keep accepting the removed legacy values server-side by ignoring or mapping them while consumers migrate, and announce a deprecation date for rejecting them.",
        ["CW007"] = "Keep documenting the status as a possible response even if it is rarely emitted, or state explicitly that it can no longer occur so consumers can drop the branch.",
        ["CW008"] = "Keep emitting the previous type during a transition period, or expose the new shape under an additional property, and change the type definitively only in the next major release.",
        ["CW009"] = "Keep returning the property marked as deprecated, or send it empty/null while consumers migrate, and remove it entirely in a major release.",
        ["CW010"] = "Widening cannot be avoided without breaking exhaustive consumers: announce the new case in the changelog and let consumers handle it before emitting it in production traffic.",
        ["CW011"] = "Add the field as optional first (omitted or null when absent) and make it required only in a major version once registered consumers handle its absence correctly.",
        ["CW012"] = "Keep accepting null during a transition period and enforce non-nullability in the next major release, documenting why null can no longer occur.",
        ["CW013"] = "Nothing is required to stay compatible; announce the new endpoint in the changelog so consumers can start adopting it.",
        ["CW014"] = "Nothing is required to stay compatible; document the new optional parameter so consumers discover it when they need it.",
        ["CW015"] = "Nothing is required to stay compatible; mention the new optional property in the changelog so consumers can adopt it gradually.",
        ["CW016"] = "Nothing is required to stay compatible; document the newly accepted values so consumers know they can start sending them.",
        ["CW017"] = "No technical action is needed for compatibility; document the new status as an expected response so consumers add the corresponding branch.",
        ["CW018"] = "No technical action is required for compatibility; regenerate or sync the affected documentation so it matches the contract.",
    }.ToFrozenDictionary();

    public static string? For(string ruleId) =>
        Remediations.TryGetValue(ruleId, out var suggestion) ? suggestion : null;
}
