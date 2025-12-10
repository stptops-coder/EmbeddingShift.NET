namespace EmbeddingShift.ConsoleEval.MiniInsurance;

using EmbeddingShift.Workflows.Domains;

/// <summary>
/// Describes the Mini-Insurance domain in one place:
/// - domain identifier
/// - scope id
/// - local artifact layout (MiniInsurancePaths)
/// - sample dataset layout (MiniInsuranceDataset).
/// This acts as a blueprint for future domains.
/// </summary>
internal static class MiniInsuranceDomain
{
    /// <summary>
    /// Stable identifier for this domain. Useful for logs, reports,
    /// and later multi-domain support.
    /// </summary>
    public const string DomainId = "mini-insurance";

    /// <summary>
    /// Current default scope id for the Mini-Insurance experiments.
    /// New versions (v2, v3, ...) can be added to MiniInsuranceScopes later.
    /// </summary>
    public static string ScopeId
        => global::EmbeddingShift.ConsoleEval.MiniInsuranceScopes.DefaultScopeId;

    // ---- Local artifact layout (under local/mini-insurance/...) ----

    public static string GetLocalRoot() => MiniInsurancePaths.GetDomainRoot();
    public static string GetRunsRoot() => MiniInsurancePaths.GetRunsRoot();
    public static string GetTrainingRoot() => MiniInsurancePaths.GetTrainingRoot();
    public static string GetAggregatesRoot() => MiniInsurancePaths.GetAggregatesRoot();
    public static string GetInspectRoot() => MiniInsurancePaths.GetInspectRoot();

    // ---- Sample dataset layout (under samples/insurance/... in the repo) ----

    public static string GetSamplesRoot() => MiniInsuranceDataset.ResolveDatasetRoot();

    public static string GetPoliciesDirectory()
        => MiniInsuranceDataset.GetPoliciesDirectory();

    public static string GetQueriesFile()
        => MiniInsuranceDataset.GetQueriesFile();
}
