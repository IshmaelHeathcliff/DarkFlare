using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class SemanticVersionTests
    {
        [TestCase("0.0.0")]
        [TestCase("1.2.3-alpha")]
        [TestCase("1.2.3-alpha.0.2")]
        [TestCase("1.2.3-01a.01-.x-y")]
        [TestCase("1.2.3--")]
        [TestCase("1.2.3+001.sha.abc")]
        [TestCase("1.2.3-alpha.2+001.sha.abc")]
        [TestCase("1.2.3-RC.1+Build-01")]
        public void Parse_ValidVersionRoundTripsWithoutNormalization(string value)
        {
            Assert.IsTrue(SemanticVersion.TryParse(value, out SemanticVersion version));
            Assert.AreEqual(value, version.ToString());
            Assert.AreEqual(version, SemanticVersion.Parse(value));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("1.2")]
        [TestCase("1.2.3.4")]
        [TestCase("v1.2.3")]
        [TestCase(" 1.2.3")]
        [TestCase("1.2.3\n")]
        [TestCase("01.2.3")]
        [TestCase("1.02.3")]
        [TestCase("1.2.03")]
        [TestCase("-1.2.3")]
        [TestCase("1..3")]
        [TestCase("a.2.3")]
        [TestCase("1.2.3-")]
        [TestCase("1.2.3+")]
        [TestCase("1.2.3-alpha.01")]
        [TestCase("1.2.3-00")]
        [TestCase("1.2.3-alpha..1")]
        [TestCase("1.2.3-alpha.")]
        [TestCase("1.2.3-.alpha")]
        [TestCase("1.2.3+build..1")]
        [TestCase("1.2.3+build.")]
        [TestCase("1.2.3+build+1")]
        [TestCase("1.2.3-alpha_1")]
        [TestCase("1.2.3+build_1")]
        [TestCase("1.2.3-测试")]
        [TestCase("１.2.3")]
        [TestCase("1.2.3-١")]
        public void Parse_InvalidVersionFailsWithoutReturningPartialValue(string value)
        {
            Assert.IsFalse(SemanticVersion.TryParse(value, out SemanticVersion version));
            Assert.IsNull(version);
            Assert.Throws<FormatException>(() => SemanticVersion.Parse(value));
        }

        [Test]
        public void Components_KeepCanonicalNumbersAndCompleteIdentifiers()
        {
            SemanticVersion version = SemanticVersion.Parse("12.34.56-alpha.2+001.sha.abc");
            Assert.AreEqual("12", version.Major);
            Assert.AreEqual("34", version.Minor);
            Assert.AreEqual("56", version.Patch);
            Assert.AreEqual("alpha.2", version.Prerelease);
            Assert.AreEqual("001.sha.abc", version.BuildMetadata);
            SemanticVersion stable = SemanticVersion.Parse("0.0.0");
            Assert.AreEqual(string.Empty, stable.Prerelease);
            Assert.AreEqual(string.Empty, stable.BuildMetadata);
        }

        [TestCase("1.2.3", "1.2.4-alpha")]
        [TestCase("1.2.999", "1.3.0")]
        [TestCase("1.999.999", "2.0.0-alpha")]
        [TestCase("1.0.0-alpha.2", "1.0.0-alpha.10")]
        [TestCase("1.0.0-9", "1.0.0-a")]
        [TestCase("1.0.0-alpha", "1.0.0-alpha.0")]
        [TestCase("1.0.0-A", "1.0.0-a")]
        [TestCase("1.0.0-alpha.1", "1.0.0-alpha.beta")]
        [TestCase("1.0.0-rc.1", "1.0.0")]
        public void Comparison_UsesNumericCoreAndSemVerIdentifierPrecedence(string lower, string higher)
        {
            SemanticVersion left = SemanticVersion.Parse(lower);
            SemanticVersion right = SemanticVersion.Parse(higher);
            Assert.Less(left.CompareTo(right), 0);
            Assert.Greater(right.CompareTo(left), 0);
            Assert.IsTrue(left < right && left <= right && right > left && right >= left);
            Assert.IsFalse(left > right || left >= right || right < left || right <= left);
        }

        [Test]
        public void Comparison_IsReflexiveAntisymmetricAndTransitiveAcrossReleaseChain()
        {
            string[] chain = { "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta",
                "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0" };
            var versions = new List<SemanticVersion>();
            foreach (string value in chain) { versions.Add(SemanticVersion.Parse(value)); }
            for (int first = 0; first < versions.Count; first++)
            {
                Assert.AreEqual(0, versions[first].CompareTo(versions[first]));
                for (int second = 0; second < versions.Count; second++)
                {
                    int forward = Math.Sign(versions[first].CompareTo(versions[second]));
                    Assert.AreEqual(Math.Sign(first.CompareTo(second)), forward);
                    Assert.AreEqual(-forward, Math.Sign(versions[second].CompareTo(versions[first])));
                    for (int third = second + 1; first < second && third < versions.Count; third++)
                    {
                        Assert.IsTrue(versions[first] < versions[third]);
                    }
                }
            }
            versions.Reverse();
            versions.Sort();
            for (int index = 0; index < chain.Length; index++)
            {
                Assert.AreEqual(chain[index], versions[index].ToString());
            }
        }

        [Test]
        public void Comparison_LongNumbersDoNotOverflowInAnyNumericPosition()
        {
            string smaller = new string('9', 80);
            string larger = "1" + new string('0', 80);
            Assert.IsTrue(SemanticVersion.Parse(smaller + ".0.0") < SemanticVersion.Parse(larger + ".0.0"));
            Assert.IsTrue(SemanticVersion.Parse("1." + smaller + ".0") < SemanticVersion.Parse("1." + larger + ".0"));
            Assert.IsTrue(SemanticVersion.Parse("1.0." + smaller) < SemanticVersion.Parse("1.0." + larger));
            Assert.IsTrue(SemanticVersion.Parse("1.0.0-alpha." + smaller)
                < SemanticVersion.Parse("1.0.0-alpha." + larger));
            Assert.IsTrue(SemanticVersion.Parse("1.0.0-alpha.100000000000000000001")
                > SemanticVersion.Parse("1.0.0-alpha.100000000000000000000"));
        }

        [Test]
        public void BuildMetadata_IsIgnoredInPrecedenceButIncludedInIdentity()
        {
            SemanticVersion first = SemanticVersion.Parse("1.2.3-alpha.2+001");
            SemanticVersion copy = SemanticVersion.Parse("1.2.3-alpha.2+001");
            SemanticVersion second = SemanticVersion.Parse("1.2.3-alpha.2+002");
            SemanticVersion noBuild = SemanticVersion.Parse("1.2.3-alpha.2");
            Assert.AreEqual(0, first.CompareTo(second));
            Assert.AreEqual(0, first.CompareTo(noBuild));
            Assert.IsTrue(first <= second && first >= second);
            Assert.IsFalse(first < second || first > second);
            Assert.IsFalse(first.Equals(second));
            Assert.IsFalse(first == second);
            Assert.IsTrue(first != noBuild);
            Assert.IsTrue(first == copy);
            Assert.AreEqual(first.GetHashCode(), copy.GetHashCode());
            Assert.IsTrue(first.Equals((object)copy));
            Assert.IsFalse(first.Equals((object)"1.2.3-alpha.2+001"));
            Assert.AreEqual(2, new HashSet<SemanticVersion> { first, copy, second }.Count);
        }

        [Test]
        public void NullComparison_FollowsComparableConventionAndEqualityIsSafe()
        {
            SemanticVersion empty = null;
            SemanticVersion otherEmpty = null;
            SemanticVersion version = SemanticVersion.Parse("1.0.0");
            Assert.IsTrue(empty == otherEmpty);
            Assert.IsFalse(version == empty);
            Assert.IsTrue(empty < version && empty <= version && version > empty && version >= empty);
            Assert.IsTrue(empty <= otherEmpty && empty >= otherEmpty);
            Assert.IsFalse(empty < otherEmpty || empty > otherEmpty);
            Assert.IsFalse(version.Equals(empty));
            Assert.IsFalse(version.Equals((object)null));
            Assert.Greater(version.CompareTo(null), 0);
        }

        [Test]
        public void ParsingComparisonAndHash_AreIndependentOfCulture()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                SemanticVersion baseline = SemanticVersion.Parse("1.2.3-I.2+BUILD");
                foreach (string culture in new[] { "tr-TR", "ar-SA", "en-US" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    SemanticVersion parsed = SemanticVersion.Parse("1.2.3-I.2+BUILD");
                    Assert.AreEqual(baseline, parsed);
                    Assert.AreEqual(baseline.GetHashCode(), parsed.GetHashCode());
                    Assert.IsTrue(parsed < SemanticVersion.Parse("1.2.3-i.2+BUILD"));
                    Assert.IsTrue(parsed < SemanticVersion.Parse("1.2.3-I.10+BUILD"));
                    Assert.IsFalse(SemanticVersion.TryParse("١.2.3", out _));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }
    }
}

