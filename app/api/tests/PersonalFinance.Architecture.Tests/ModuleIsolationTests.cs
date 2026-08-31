using System.Reflection;
using PersonalFinance.Financing;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Ledger;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Subscriptions;
using PersonalFinance.Subscriptions.Contracts;
using Xunit;

namespace PersonalFinance.Architecture.Tests;

/// <summary>
/// A module may only see another module through its <c>.Contracts</c> assembly.
/// </summary>
public class ModuleIsolationTests {
    [Fact]
    public void Financing_module_sees_Ledger_only_through_its_contracts_assembly() {
        AssertModuleOnlyReferencesContracts(
            typeof(FinancingModule).Assembly,
            otherImplName: "PersonalFinance.Ledger",
            otherContractsName: "PersonalFinance.Ledger.Contracts"
        );
    }

    [Fact]
    public void Financing_contracts_assembly_references_no_Ledger_assembly() {
        var referenced = ReferencedAssemblyNames(typeof(IFinancingApi).Assembly);
        Assert.DoesNotContain("PersonalFinance.Ledger", referenced);
        Assert.DoesNotContain("PersonalFinance.Ledger.Contracts", referenced);
    }

    [Fact]
    public void Ledger_module_sees_Financing_only_through_its_contracts_assembly() {
        AssertModuleOnlyReferencesContracts(
            typeof(LedgerModule).Assembly,
            otherImplName: "PersonalFinance.Financing",
            otherContractsName: "PersonalFinance.Financing.Contracts"
        );
    }

    [Fact]
    public void Ledger_contracts_assembly_references_no_Financing_assembly() {
        var referenced = ReferencedAssemblyNames(typeof(ILedgerApi).Assembly);
        Assert.DoesNotContain("PersonalFinance.Financing", referenced);
        Assert.DoesNotContain("PersonalFinance.Financing.Contracts", referenced);
    }

    [Fact]
    public void Subscriptions_module_sees_Ledger_only_through_its_contracts_assembly() {
        AssertModuleOnlyReferencesContracts(
            typeof(SubscriptionsModule).Assembly,
            otherImplName: "PersonalFinance.Ledger",
            otherContractsName: "PersonalFinance.Ledger.Contracts"
        );
    }

    [Fact]
    public void Subscriptions_contracts_assembly_references_no_module_implementation() {
        var referenced = ReferencedAssemblyNames(typeof(ISubscriptionsApi).Assembly);
        Assert.DoesNotContain("PersonalFinance.Ledger", referenced);
        Assert.DoesNotContain("PersonalFinance.Ledger.Contracts", referenced);
        Assert.DoesNotContain("PersonalFinance.Financing", referenced);
        Assert.DoesNotContain("PersonalFinance.Financing.Contracts", referenced);
    }

    [Fact]
    public void Subscriptions_module_does_not_reference_Financing_or_Parties() {
        var referenced = ReferencedAssemblyNames(typeof(SubscriptionsModule).Assembly);
        Assert.DoesNotContain("PersonalFinance.Financing", referenced);
        Assert.DoesNotContain("PersonalFinance.Financing.Contracts", referenced);
        Assert.DoesNotContain("PersonalFinance.Parties", referenced);
        Assert.DoesNotContain("PersonalFinance.Parties.Contracts", referenced);
    }

    private static void AssertModuleOnlyReferencesContracts(Assembly implAssembly, string otherImplName, string otherContractsName) {
        var referenced = ReferencedAssemblyNames(implAssembly);
        Assert.Contains(otherContractsName, referenced);
        Assert.DoesNotContain(otherImplName, referenced);
    }

    private static HashSet<string> ReferencedAssemblyNames(Assembly assembly) {
        return assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
        .ToHashSet(StringComparer.Ordinal);
    }
}
