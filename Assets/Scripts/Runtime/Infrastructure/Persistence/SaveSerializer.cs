using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace DarkFlare
{
    public enum SaveSerializationCode
    {
        Success,
        InputMissing,
        DocumentTooLarge,
        InvalidJson,
        FormatUnsupported,
        HeaderInvalid,
        SlotMismatch,
        CommitSequenceMismatch,
        ChecksumMismatch,
        SchemaUnsupported,
        MigrationFailed,
        ValidationFailed,
        SerializationFailed,
    }

    public sealed class SaveSerializationResult
    {
        public SaveSerializationCode Code { get; }

        public byte[] Bytes { get; }

        public SaveDocumentDto Document { get; }

        public IReadOnlyList<SaveDataIssue> ValidationIssues { get; }

        public Exception Exception { get; }

        public bool WasMigrated { get; }

        public bool Succeeded => Code == SaveSerializationCode.Success
            && Bytes != null
            && Document != null;

        internal SaveSerializationResult(
            SaveSerializationCode code,
            byte[] bytes,
            SaveDocumentDto document,
            IReadOnlyList<SaveDataIssue> validationIssues,
            Exception exception,
            bool wasMigrated)
        {
            Code = code;
            Bytes = bytes;
            Document = document;
            ValidationIssues = validationIssues ?? Array.Empty<SaveDataIssue>();
            Exception = exception;
            WasMigrated = wasMigrated;
        }
    }

    public sealed class SaveDeserializationResult
    {
        public SaveSerializationCode Code { get; }

        public SaveDocumentDto Document { get; }

        public IReadOnlyList<SaveDataIssue> ValidationIssues { get; }

        public Exception Exception { get; }

        public bool WasMigrated { get; }

        public bool Succeeded => Code == SaveSerializationCode.Success && Document != null;

        internal SaveDeserializationResult(
            SaveSerializationCode code,
            SaveDocumentDto document,
            IReadOnlyList<SaveDataIssue> validationIssues,
            Exception exception,
            bool wasMigrated)
        {
            Code = code;
            Document = document;
            ValidationIssues = validationIssues ?? Array.Empty<SaveDataIssue>();
            Exception = exception;
            WasMigrated = wasMigrated;
        }
    }

    public interface ISaveSerializer
    {
        SaveSerializationResult Serialize(SaveDocumentDto document);

        SaveDeserializationResult Deserialize(
            byte[] bytes,
            SaveSlotId? expectedSlotId = null,
            long? expectedCommitSequence = null);
    }

    public sealed class NewtonsoftSaveSerializer : ISaveSerializer
    {
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        readonly JsonSerializer _serializer;
        readonly JsonMigrationPipeline _migrationPipeline;

        public NewtonsoftSaveSerializer(JsonMigrationRegistry migrationRegistry = null)
        {
            JsonMigrationRegistry registry = migrationRegistry ?? CreateDefaultMigrationRegistry();
            _migrationPipeline = new JsonMigrationPipeline(registry, "header.saveSchemaVersion");
            _serializer = JsonSerializer.Create(CreateSettings());
        }

        public SaveSerializationResult Serialize(SaveDocumentDto document)
        {
            if (document == null)
            {
                return SerializationFailure(SaveSerializationCode.InputMissing);
            }

            try
            {
                JObject root = JObject.FromObject(document, _serializer);

                if (root["header"] is not JObject header || root["payload"] is not JObject payload)
                {
                    return SerializationFailure(SaveSerializationCode.HeaderInvalid);
                }

                int schemaVersion = header.Value<int?>("saveSchemaVersion") ?? -1;

                if (schemaVersion != SaveSchemaVersion.Current.Value)
                {
                    return SerializationFailure(SaveSerializationCode.SchemaUnsupported);
                }

                header["payloadSha256"] = ComputePayloadSha256(payload);
                JObject canonicalRoot = CanonicalizeObject(root);
                SaveDocumentDto normalized = canonicalRoot.ToObject<SaveDocumentDto>(_serializer);
                SaveDataValidationResult validation = SaveDataValidator.ValidateDocument(normalized);

                if (!validation.Succeeded)
                {
                    return SerializationFailure(
                        SaveSerializationCode.ValidationFailed,
                        validation.Issues);
                }

                byte[] bytes = WriteCanonicalUtf8(canonicalRoot);

                if (bytes.Length > LocalSaveFormat.MaximumDocumentBytes)
                {
                    return SerializationFailure(SaveSerializationCode.DocumentTooLarge);
                }

                return new SaveSerializationResult(
                    SaveSerializationCode.Success,
                    bytes,
                    normalized,
                    Array.Empty<SaveDataIssue>(),
                    null,
                    false);
            }
            catch (Exception exception) when (
                exception is JsonException
                || exception is InvalidOperationException
                || exception is FormatException
                || exception is EncoderFallbackException)
            {
                return SerializationFailure(
                    SaveSerializationCode.SerializationFailed,
                    exception: exception);
            }
        }

        public SaveDeserializationResult Deserialize(
            byte[] bytes,
            SaveSlotId? expectedSlotId = null,
            long? expectedCommitSequence = null)
        {
            if (bytes == null)
            {
                return DeserializationFailure(SaveSerializationCode.InputMissing);
            }

            if (bytes.Length > LocalSaveFormat.MaximumDocumentBytes)
            {
                return DeserializationFailure(SaveSerializationCode.DocumentTooLarge);
            }

            JObject root;

            try
            {
                root = ParseRoot(bytes);
            }
            catch (Exception exception) when (
                exception is JsonException
                || exception is DecoderFallbackException
                || exception is InvalidDataException)
            {
                return DeserializationFailure(
                    SaveSerializationCode.InvalidJson,
                    exception: exception);
            }

            if (root["header"] is not JObject header || root["payload"] is not JObject payload)
            {
                return DeserializationFailure(SaveSerializationCode.HeaderInvalid);
            }

            string formatId = header.Value<string>("formatId");
            int? formatVersion = ReadInteger(header["formatVersion"]);

            if (!string.Equals(formatId, LocalSaveFormat.FormatId, StringComparison.Ordinal)
                || formatVersion != LocalSaveFormat.FormatVersion)
            {
                return DeserializationFailure(SaveSerializationCode.FormatUnsupported);
            }

            string rawSlotId = header.Value<string>("slotId");

            if (!SaveSlotId.TryParse(rawSlotId, out SaveSlotId slotId))
            {
                return DeserializationFailure(SaveSerializationCode.HeaderInvalid);
            }

            if (expectedSlotId.HasValue && expectedSlotId.Value != slotId)
            {
                return DeserializationFailure(SaveSerializationCode.SlotMismatch);
            }

            long? commitSequence = ReadLong(header["commitSequence"]);

            if (!commitSequence.HasValue || commitSequence.Value <= 0)
            {
                return DeserializationFailure(SaveSerializationCode.HeaderInvalid);
            }

            if (expectedCommitSequence.HasValue
                && expectedCommitSequence.Value != commitSequence.Value)
            {
                return DeserializationFailure(SaveSerializationCode.CommitSequenceMismatch);
            }

            string expectedChecksum = header.Value<string>("payloadSha256");

            if (!IsLowerHex(expectedChecksum, 64)
                || !string.Equals(
                    expectedChecksum,
                    ComputePayloadSha256(payload),
                    StringComparison.Ordinal))
            {
                return DeserializationFailure(SaveSerializationCode.ChecksumMismatch);
            }

            int? sourceSchemaVersion = ReadInteger(header["saveSchemaVersion"]);

            if (!sourceSchemaVersion.HasValue || sourceSchemaVersion.Value < 0)
            {
                return DeserializationFailure(SaveSerializationCode.HeaderInvalid);
            }

            if (sourceSchemaVersion.Value > SaveSchemaVersion.Current.Value)
            {
                return DeserializationFailure(SaveSerializationCode.SchemaUnsupported);
            }

            JsonMigrationResult migration = _migrationPipeline.Migrate(root, sourceSchemaVersion.Value);

            if (!migration.Succeeded)
            {
                return DeserializationFailure(
                    migration.Code == MigrationResultCode.FutureVersionUnsupported
                        ? SaveSerializationCode.SchemaUnsupported
                        : SaveSerializationCode.MigrationFailed,
                    exception: migration.Exception);
            }

            SaveDocumentDto document;

            try
            {
                document = migration.Document.ToObject<SaveDocumentDto>(_serializer);
            }
            catch (Exception exception) when (
                exception is JsonException
                || exception is InvalidOperationException
                || exception is FormatException)
            {
                return DeserializationFailure(
                    SaveSerializationCode.InvalidJson,
                    exception: exception);
            }

            SaveDataValidationResult validation = SaveDataValidator.ValidateDocument(document);

            if (!validation.Succeeded)
            {
                return DeserializationFailure(
                    SaveSerializationCode.ValidationFailed,
                    validation.Issues);
            }

            return new SaveDeserializationResult(
                SaveSerializationCode.Success,
                document,
                Array.Empty<SaveDataIssue>(),
                null,
                sourceSchemaVersion.Value != SaveSchemaVersion.Current.Value);
        }

        static JsonSerializerSettings CreateSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new CanonicalContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateParseHandling = DateParseHandling.None,
                FloatFormatHandling = FloatFormatHandling.DefaultValue,
                FloatParseHandling = FloatParseHandling.Double,
                Formatting = Formatting.None,
                MaxDepth = LocalSaveFormat.MaximumJsonDepth,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Include,
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                TypeNameHandling = TypeNameHandling.None,
            };
        }

        static JsonMigrationRegistry CreateDefaultMigrationRegistry()
        {
            JsonMigrationRegistryBuildResult result = JsonMigrationRegistry.Build(
                MigrationDataDomain.Save,
                SaveSchemaVersion.Current.Value,
                new IJsonMigrationStep[] { new LegacySaveV0ToV1Migration(), new ItemQuantitySaveMigration() });

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "无法建立 Save Schema 迁移链："
                    + string.Join("; ", result.Issues.Select(issue => issue.Message)));
            }

            return result.Registry;
        }

        static JObject ParseRoot(byte[] bytes)
        {
            string json = StrictUtf8.GetString(bytes);

            using (StringReader stringReader = new StringReader(json))
            using (JsonTextReader jsonReader = new JsonTextReader(stringReader))
            {
                jsonReader.CloseInput = false;
                jsonReader.Culture = CultureInfo.InvariantCulture;
                jsonReader.DateParseHandling = DateParseHandling.None;
                jsonReader.FloatParseHandling = FloatParseHandling.Double;
                jsonReader.MaxDepth = LocalSaveFormat.MaximumJsonDepth;
                JToken token = JToken.Load(
                    jsonReader,
                    new JsonLoadSettings
                    {
                        CommentHandling = CommentHandling.Ignore,
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        LineInfoHandling = LineInfoHandling.Ignore,
                    });

                if (jsonReader.Read())
                {
                    throw new JsonReaderException("存档 JSON 根对象之后存在额外内容");
                }

                if (token is not JObject root)
                {
                    throw new JsonReaderException("存档 JSON 根节点必须是对象");
                }

                return root;
            }
        }

        static string ComputePayloadSha256(JToken payload)
        {
            byte[] bytes = WriteCanonicalUtf8(CanonicalizeToken(payload));

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder result = new StringBuilder(hash.Length * 2);

                for (int i = 0; i < hash.Length; i++)
                {
                    result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        static byte[] WriteCanonicalUtf8(JToken token)
        {
            StringBuilder builder = new StringBuilder();

            using (StringWriter stringWriter = new StringWriter(builder, CultureInfo.InvariantCulture))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.CloseOutput = false;
                jsonWriter.Culture = CultureInfo.InvariantCulture;
                jsonWriter.Formatting = Formatting.None;
                jsonWriter.StringEscapeHandling = StringEscapeHandling.EscapeNonAscii;
                token.WriteTo(jsonWriter);
                jsonWriter.Flush();
            }

            return StrictUtf8.GetBytes(builder.ToString());
        }

        static JToken CanonicalizeToken(JToken token)
        {
            if (token is JObject objectToken)
            {
                return CanonicalizeObject(objectToken);
            }

            if (token is JArray arrayToken)
            {
                JArray result = new JArray();

                for (int i = 0; i < arrayToken.Count; i++)
                {
                    result.Add(CanonicalizeToken(arrayToken[i]));
                }

                return result;
            }

            if (token is JValue valueToken
                && valueToken.Type == JTokenType.Float)
            {
                return new JValue(Convert.ToDouble(
                    valueToken.Value,
                    CultureInfo.InvariantCulture));
            }

            return token.DeepClone();
        }

        static JObject CanonicalizeObject(JObject value)
        {
            JObject result = new JObject();

            foreach (JProperty property in value.Properties().OrderBy(
                property => property.Name,
                StringComparer.Ordinal))
            {
                result.Add(property.Name, CanonicalizeToken(property.Value));
            }

            return result;
        }

        static int? ReadInteger(JToken token)
        {
            if (token?.Type != JTokenType.Integer)
            {
                return null;
            }

            try
            {
                return token.Value<int>();
            }
            catch (Exception exception) when (
                exception is OverflowException
                || exception is FormatException)
            {
                return null;
            }
        }

        static long? ReadLong(JToken token)
        {
            if (token?.Type != JTokenType.Integer)
            {
                return null;
            }

            try
            {
                return token.Value<long>();
            }
            catch (Exception exception) when (
                exception is OverflowException
                || exception is FormatException)
            {
                return null;
            }
        }

        static bool IsLowerHex(string value, int length)
        {
            if (string.IsNullOrEmpty(value) || value.Length != length)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (!char.IsDigit(character) && (character < 'a' || character > 'f'))
                {
                    return false;
                }
            }

            return true;
        }

        static SaveSerializationResult SerializationFailure(
            SaveSerializationCode code,
            IReadOnlyList<SaveDataIssue> validationIssues = null,
            Exception exception = null)
        {
            return new SaveSerializationResult(
                code,
                null,
                null,
                validationIssues,
                exception,
                false);
        }

        static SaveDeserializationResult DeserializationFailure(
            SaveSerializationCode code,
            IReadOnlyList<SaveDataIssue> validationIssues = null,
            Exception exception = null)
        {
            return new SaveDeserializationResult(
                code,
                null,
                validationIssues,
                exception,
                false);
        }

        sealed class CanonicalContractResolver : DefaultContractResolver
        {
            public CanonicalContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy(
                    processDictionaryKeys: true,
                    overrideSpecifiedNames: false);
            }

            protected override IList<JsonProperty> CreateProperties(
                Type type,
                MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization)
                    .OrderBy(property => property.PropertyName, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }
}
