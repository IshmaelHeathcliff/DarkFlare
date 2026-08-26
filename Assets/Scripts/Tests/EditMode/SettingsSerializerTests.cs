using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class SettingsSerializerTests
    {
        readonly NewtonsoftSettingsSerializer _serializer = new NewtonsoftSettingsSerializer();

        [Test]
        public void Serialize_IsDeterministicHashesPayloadAndDoesNotMutateInput()
        {
            UserSettingsDocumentDto source = SettingsDataContractTests.CreateValidDocument(
                UserLanguagePreference.SimplifiedChinese);

            SettingsSerializationResult first = _serializer.Serialize(source);
            SettingsSerializationResult second = _serializer.Serialize(source);

            Assert.IsTrue(first.Succeeded, Describe(first));
            Assert.IsTrue(second.Succeeded, Describe(second));
            CollectionAssert.AreEqual(first.Bytes, second.Bytes);
            Assert.IsEmpty(source.PayloadSha256);
            Assert.AreEqual(64, first.Document.PayloadSha256.Length);
            StringAssert.DoesNotContain("\r", Encoding.UTF8.GetString(first.Bytes));

            SettingsSerializationResult restored = _serializer.Deserialize(first.Bytes);
            Assert.IsTrue(restored.Succeeded, Describe(restored));
            Assert.AreEqual(
                UserLanguagePreference.SimplifiedChinese,
                restored.Document.Payload.Language);
        }

        [Test]
        public void Deserialize_RejectsTamperingFutureSchemaDuplicatesAndOversizedInput()
        {
            SettingsSerializationResult serialized = _serializer.Serialize(
                SettingsDataContractTests.CreateValidDocument());
            Assert.IsTrue(serialized.Succeeded, Describe(serialized));
            JObject tamperedRoot = JObject.Parse(Encoding.UTF8.GetString(serialized.Bytes));
            tamperedRoot["payload"]["audio"]["masterVolume"] = 0.5f;

            SettingsSerializationResult tampered = _serializer.Deserialize(
                Encoding.UTF8.GetBytes(tamperedRoot.ToString(Formatting.None)));
            Assert.AreEqual(SettingsSerializationCode.ChecksumMismatch, tampered.Code);

            JObject futureRoot = JObject.Parse(Encoding.UTF8.GetString(serialized.Bytes));
            futureRoot["settingsSchemaVersion"] = SettingsSchemaVersion.Current.Value + 1;
            SettingsSerializationResult future = _serializer.Deserialize(
                Encoding.UTF8.GetBytes(futureRoot.ToString(Formatting.None)));
            Assert.AreEqual(SettingsSerializationCode.SchemaUnsupported, future.Code);

            string validJson = Encoding.UTF8.GetString(serialized.Bytes);
            string duplicateJson = validJson.Replace(
                "\"formatId\":\"darkflare-settings\"",
                "\"formatId\":\"darkflare-settings\",\"formatId\":\"darkflare-settings\"");
            SettingsSerializationResult duplicate = _serializer.Deserialize(
                Encoding.UTF8.GetBytes(duplicateJson));
            Assert.AreEqual(SettingsSerializationCode.InvalidJson, duplicate.Code);

            byte[] oversized = new byte[LocalSettingsFormat.MaximumDocumentBytes + 1];
            SettingsSerializationResult oversizedResult = _serializer.Deserialize(oversized);
            Assert.AreEqual(SettingsSerializationCode.DocumentTooLarge, oversizedResult.Code);
        }

        [Test]
        public void Deserialize_MigratesLegacyLanguageDocumentAndFillsFutureDomains()
        {
            string json = MigrationFixtureUtility.ReadUtf8("settings-v0-language.json");

            SettingsSerializationResult result = _serializer.Deserialize(Encoding.UTF8.GetBytes(json));

            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.IsTrue(result.WasMigrated);
            Assert.AreEqual(SettingsSchemaVersion.Current.Value, result.Document.SettingsSchemaVersion);
            Assert.AreEqual(UserLanguagePreference.English, result.Document.Payload.Language);
            Assert.NotNull(result.Document.Payload.Audio);
            Assert.NotNull(result.Document.Payload.Input);
            Assert.NotNull(result.Document.Payload.Display);
            Assert.NotNull(result.Document.Payload.Accessibility);
        }

        static string Describe(SettingsSerializationResult result)
        {
            return result.Exception?.ToString()
                ?? string.Join("\n", result.ValidationIssues.Select(issue => issue.Message));
        }
    }
}
