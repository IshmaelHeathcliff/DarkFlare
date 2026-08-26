using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class MigrationFixtureContractTests
    {
        [Test]
        public void Manifest_RegistersSaveAndSettingsV0FixturesWithMigrationSummary()
        {
            JObject manifest = JObject.Parse(MigrationFixtureUtility.ReadUtf8("manifest.json"));
            Assert.AreEqual(1, manifest.Value<int>("schemaVersion"));
            JArray fixtures = manifest["fixtures"] as JArray;
            Assert.IsNotNull(fixtures);
            Assert.AreEqual(2, fixtures.Count);
            HashSet<string> domains = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < fixtures.Count; i++)
            {
                JObject fixture = fixtures[i] as JObject;
                Assert.IsNotNull(fixture, $"fixtures[{i}]");
                Assert.AreEqual(0, fixture.Value<int>("sourceSchemaVersion"));
                Assert.IsFalse(string.IsNullOrWhiteSpace(fixture.Value<string>("expectedMigration")));
                string path = fixture.Value<string>("path");
                Assert.DoesNotThrow(() => JObject.Parse(MigrationFixtureUtility.ReadUtf8(path)));
                domains.Add(fixture.Value<string>("domain"));
            }

            CollectionAssert.AreEquivalent(new[] { "save", "settings" }, domains);
        }
    }
}
