namespace EmbeddingShift.ConsoleEval.Domains;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central list of available domain packs for ConsoleEval.
/// Keep this intentionally small and explicit (RAZOR).
/// </summary>
internal static class DomainPackRegistry
{
    private static readonly IDomainPack[] Packs =
    {
        new MiniInsuranceDomainPack()
    };

    public static IReadOnlyList<IDomainPack> All => Packs;

    public static IDomainPack? TryGet(string? domainId)
    {
        if (string.IsNullOrWhiteSpace(domainId))
            return null;

        return Packs.FirstOrDefault(p =>
            string.Equals(p.DomainId, domainId, StringComparison.OrdinalIgnoreCase));
    }
}