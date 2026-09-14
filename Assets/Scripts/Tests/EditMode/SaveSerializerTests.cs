using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class SaveSerializerTests
    {
        readonly NewtonsoftSaveSerializer _serializer = new NewtonsoftSaveSerializer();

        [Test]
        public void Deserialize_PreStatusHistoryAddsEmptyClockAndPreservesRuntimeData()
        {
            byte[] bytes = System.IO.File.ReadAllBytes("Assets/Scripts/Tests/Fixtures/Migration/save-v2-without-statuses.json");
            JObject historical = JObject.Parse(Encoding.UTF8.GetString(bytes));
            SaveDeserializationResult result = _serializer.Deserialize(bytes);
            Assert.IsTrue(result.Succeeded, Describe(result));
            Assert.That(result.Document.Payload.Run.Statuses.Time, Is.Zero);
            Assert.That(result.Document.Payload.Run.Statuses.Actors, Is.Empty);
            var serialized = _serializer.Serialize(result.Document);
            Assert.IsTrue(serialized.Succeeded, Describe(serialized));
            JObject current = JObject.Parse(Encoding.UTF8.GetString(serialized.Bytes));
            ((JObject)current["payload"]["run"]).Remove("statuses");
            Assert.IsTrue(JToken.DeepEquals(historical["payload"], current["payload"]), "迁移不能改变物品、资源、随机数和运行时数据");
        }

        [Test]
        public void Serialize_IsDeterministicComputesChecksumAndDoesNotMutateInput()
        {
            SaveDocumentDto source = SaveDataContractTests.CreateValidDocument();
            string originalChecksum = source.Header.PayloadSha256;

            SaveSerializationResult first = _serializer.Serialize(source);
            SaveSerializationResult second = _serializer.Serialize(source);

            Assert.IsTrue(first.Succeeded, Describe(first));
            Assert.IsTrue(second.Succeeded, Describe(second));
            CollectionAssert.AreEqual(first.Bytes, second.Bytes);
            Assert.AreEqual(originalChecksum, source.Header.PayloadSha256);
            Assert.AreNotEqual(originalChecksum, first.Document.Header.PayloadSha256);
            StringAssert.DoesNotContain("\r", Encoding.UTF8.GetString(first.Bytes));

            SaveDeserializationResult restored = _serializer.Deserialize(
                first.Bytes,
                SaveSlotId.Parse(source.Header.SlotId),
                source.Header.CommitSequence);
            Assert.IsTrue(restored.Succeeded, Describe(restored));
            Assert.AreEqual(source.Payload.Profile.Gold, restored.Document.Payload.Profile.Gold);
        }

        [Test]
        public void Deserialize_CanonicalizesPropertyOrderBeforeChecksumVerification()
        {
            SaveSerializationResult serialized = _serializer.Serialize(
                SaveDataContractTests.CreateValidDocument());
            Assert.IsTrue(serialized.Succeeded, Describe(serialized));
            JObject root = JObject.Parse(Encoding.UTF8.GetString(serialized.Bytes));
            JObject payload = (JObject)root["payload"];
            JObject reversedPayload = new JObject(
                payload.Properties().Reverse().Select(property => new JProperty(
                    property.Name,
                    property.Value.DeepClone())));
            root["payload"] = reversedPayload;
            root["header"]["createdUtc"] = serialized.Document.Header.CreatedUtc;
            root["header"]["updatedUtc"] = serialized.Document.Header.UpdatedUtc;
            byte[] reordered = Encoding.UTF8.GetBytes(root.ToString(Formatting.None));

            SaveDeserializationResult result = _serializer.Deserialize(reordered);

            Assert.IsTrue(result.Succeeded, Describe(result));
        }

        [Test]
        public void Serialize_NormalizesSinglePrecisionNumbersBeforeChecksum()
        {
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();
            document.Payload.Run.Player.Position.X =
                System.BitConverter.Int32BitsToSingle(-1032990722);

            SaveSerializationResult serialized = _serializer.Serialize(document);
            Assert.IsTrue(serialized.Succeeded, Describe(serialized));
            SaveDeserializationResult restored = _serializer.Deserialize(serialized.Bytes);

            Assert.IsTrue(restored.Succeeded, Describe(restored));
            Assert.AreEqual(
                document.Payload.Run.Player.Position.X,
                restored.Document.Payload.Run.Player.Position.X);
        }

        [Test]
        public void Deserialize_RejectsTamperingFutureSchemaDuplicatePropertiesAndDeepJson()
        {
            SaveSerializationResult serialized = _serializer.Serialize(
                SaveDataContractTests.CreateValidDocument());
            Assert.IsTrue(serialized.Succeeded, Describe(serialized));
            JObject tamperedRoot = JObject.Parse(Encoding.UTF8.GetString(serialized.Bytes));
            tamperedRoot["payload"]["profile"]["gold"] = 999;

            SaveDeserializationResult tampered = _serializer.Deserialize(
                Encoding.UTF8.GetBytes(tamperedRoot.ToString(Formatting.None)));
            Assert.AreEqual(SaveSerializationCode.ChecksumMismatch, tampered.Code);

            JObject futureRoot = JObject.Parse(Encoding.UTF8.GetString(serialized.Bytes));
            futureRoot["header"]["saveSchemaVersion"] = SaveSchemaVersion.Current.Value + 1;
            SaveDeserializationResult future = _serializer.Deserialize(
                Encoding.UTF8.GetBytes(futureRoot.ToString(Formatting.None)));
            Assert.AreEqual(SaveSerializationCode.SchemaUnsupported, future.Code);

            string validJson = Encoding.UTF8.GetString(serialized.Bytes);
            string duplicateJson = validJson.Replace(
                "\"formatId\":\"darkflare-save\"",
                "\"formatId\":\"darkflare-save\",\"formatId\":\"darkflare-save\"");
            SaveDeserializationResult duplicate = _serializer.Deserialize(
                Encoding.UTF8.GetBytes(duplicateJson));
            Assert.AreEqual(SaveSerializationCode.InvalidJson, duplicate.Code);

            string deepJson = new string('[', LocalSaveFormat.MaximumJsonDepth + 2)
                + "0"
                + new string(']', LocalSaveFormat.MaximumJsonDepth + 2);
            SaveDeserializationResult deep = _serializer.Deserialize(Encoding.UTF8.GetBytes(deepJson));
            Assert.AreEqual(SaveSerializationCode.InvalidJson, deep.Code);
        }

        [Test]
        public void Serialize_RejectsNonFiniteValuesAndOversizedInputIsRejectedBeforeParsing()
        {
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();
            document.Payload.Run.Player.Position.X = float.PositiveInfinity;

            SaveSerializationResult invalid = _serializer.Serialize(document);
            Assert.AreEqual(SaveSerializationCode.ValidationFailed, invalid.Code);
            Assert.That(
                invalid.ValidationIssues.Select(issue => issue.Code),
                Has.Some.EqualTo(SaveDataIssueCode.NonFiniteNumber));

            byte[] oversized = new byte[LocalSaveFormat.MaximumDocumentBytes + 1];
            SaveDeserializationResult result = _serializer.Deserialize(oversized);
            Assert.AreEqual(SaveSerializationCode.DocumentTooLarge, result.Code);
        }

        static string Describe(SaveSerializationResult result)
        {
            return result.Exception?.ToString()
                ?? string.Join("\n", result.ValidationIssues.Select(issue => issue.Message));
        }

        static string Describe(SaveDeserializationResult result)
        {
            return result.Exception?.ToString()
                ?? string.Join("\n", result.ValidationIssues.Select(issue => issue.Message));
        }
    }
}
