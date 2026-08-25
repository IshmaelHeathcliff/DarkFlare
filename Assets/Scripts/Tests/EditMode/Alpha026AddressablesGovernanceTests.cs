using System.Collections.Generic;
using System.Linq;
using DarkFlare.Editor;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class Alpha026AddressablesGovernanceTests
    {
        [Test]
        public void ManagedContract_HasExpectedShapeAndNoValidationIssues()
        {
            Assert.AreEqual(15, Alpha026AddressablesGovernance.Contracts.Count);
            Assert.AreEqual(
                15,
                Alpha026AddressablesGovernance.Contracts
                    .Select(contract => contract.Guid)
                    .Distinct()
                    .Count());
            Assert.AreEqual(
                4,
                Alpha026AddressablesGovernance.Contracts
                    .Select(contract => contract.Group)
                    .Distinct()
                    .Count());

            List<string> issues = Alpha026AddressablesGovernance.Validate();
            Assert.IsEmpty(issues, string.Join("\n", issues));
        }

        [Test]
        public void PlayerErrors_HaveProductionAndPseudoLocalizationContracts()
        {
            Assert.AreEqual(7, Alpha026PlayerErrorLocalization.RequiredKeys.Count);

            List<string> issues = Alpha026PlayerErrorLocalization.Validate();
            Assert.IsEmpty(issues, string.Join("\n", issues));
        }
    }
}
