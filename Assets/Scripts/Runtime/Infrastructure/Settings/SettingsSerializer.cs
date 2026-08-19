using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace DarkFlare
{
    public enum SettingsSerializationCode
    {
        Success,
        DocumentMissing,
        DocumentTooLarge,
        InvalidJson,
        FormatMismatch,
        SchemaUnsupported,
        MigrationFailed,
        ChecksumMismatch,
        ValidationFailed,
        SerializationFailed,
    }

    public sealed class SettingsSerializationResult
    {
        public SettingsSerializationCode Code { get; }

        public byte[] Bytes { get; }

        public UserSettingsDocumentDto Document { get; }

        public IReadOnlyList<SettingsDataIssue> ValidationIssues { get; }

        public Exception Exception { get; }

        public bool WasMigrated { get; }

        public bool Succeeded => Code == SettingsSerializationCode.Success;

        internal SettingsSerializationResult(
            SettingsSerializationCode code,
            byte[] bytes,
            UserSettingsDocumentDto document,
            IReadOnlyList<SettingsDataIssue> validationIssues,
            Exception exception,
            bool wasMigrated)
        {
            Code = code;
            Bytes = bytes;
            Document = document;
            ValidationIssues = validationIssues ?? Array.Empty<SettingsDataIssue>();
            Exception = exception;
            WasMigrated = wasMigrated;
        }
    }

    public interface ISettingsSerializer
    {
        SettingsSerializationResult Serialize(UserSettingsDocumentDto document);

        SettingsSerializationResult Deserialize(byte[] bytes);
    }

    public sealed class NewtonsoftSettingsSerializer : ISettingsSerializer
    {
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        readonly JsonSerializer _serializer;
        readonly JsonMigrationPipeline _migrationPipeline;

        public NewtonsoftSettingsSerializer(JsonMigrationRegistry migrationRegistry = null)
        {
            _serializer = JsonSerializer.Create(CreateSettings());
            _migrationPipeline = new JsonMigrationPipeline(
                migrationRegistry ?? CreateDefaultMigrationRegistry(),
                "settingsSchemaVersion");
        }

        public SettingsSerializationResult Serialize(UserSettingsDocumentDto document)
        {
            if (document == null)
            {
                return Failure(SettingsSerializationCode.DocumentMissing);
            }

            try
            {
                JObject root = JObject.FromObject(document, _serializer);
                root["payloadSha256"] = ComputePayloadSha256(root["payload"]);
                UserSettingsDocumentDto canonicalDocument = root.ToObject<UserSettingsDocumentDto>(
                    _serializer);
                SettingsDataValidationResult validation = SettingsDataValidator.ValidateDocument(
                    canonicalDocument);

                if (!validation.Succeeded)
                {
                    return Failure(
                        SettingsSerializationCode.ValidationFailed,
                        validationIssues: validation.Issues);
                }

                byte[] bytes = WriteCanonicalUtf8(root);

                if (bytes.Length > LocalSettingsFormat.MaximumDocumentBytes)
                {
                    return Failure(SettingsSerializationCode.DocumentTooLarge);
                }

                return Success(bytes, canonicalDocument, false);
            }
            catch (Exception exception)
            {
                return Failure(SettingsSerializationCode.SerializationFailed, exception);
            }
        }

        public SettingsSerializationResult Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return Failure(SettingsSerializationCode.DocumentMissing);
            }

            if (bytes.Length > LocalSettingsFormat.MaximumDocumentBytes)
            {
                return Failure(SettingsSerializationCode.DocumentTooLarge);
            }

            try
            {
                JObject root = ParseRoot(bytes);

                if (!string.Equals(
                        root.Value<string>("formatId"),
                        LocalSettingsFormat.FormatId,
                        StringComparison.Ordinal)
                    || root.Value<int?>("formatVersion") != LocalSettingsFormat.FormatVersion)
                {
                    return Failure(SettingsSerializationCode.FormatMismatch);
                }

                int? sourceVersion = ReadInteger(root["settingsSchemaVersion"]);

                if (!sourceVersion.HasValue || sourceVersion.Value < 0)
                {
                    return Failure(SettingsSerializationCode.InvalidJson);
                }

                if (sourceVersion.Value > SettingsSchemaVersion.Current.Value)
                {
                    return Failure(SettingsSerializationCode.SchemaUnsupported);
                }

                JsonMigrationResult migration = _migrationPipeline.Migrate(
                    root,
                    sourceVersion.Value);

                if (!migration.Succeeded)
                {
                    SettingsSerializationCode code = migration.Code
                        == MigrationResultCode.FutureVersionUnsupported
                        ? SettingsSerializationCode.SchemaUnsupported
                        : SettingsSerializationCode.MigrationFailed;
                    return Failure(code, migration.Exception);
                }

                bool wasMigrated = migration.OriginalVersion != migration.FinalVersion;
                JObject migratedRoot = migration.Document;
                JToken payload = migratedRoot["payload"];

                if (payload is not JObject)
                {
                    return Failure(SettingsSerializationCode.InvalidJson);
                }

                if (!wasMigrated)
                {
                    string expectedChecksum = migratedRoot.Value<string>("payloadSha256");

                    if (!IsLowerHex(expectedChecksum, 64)
                        || !string.Equals(
                            expectedChecksum,
                            ComputePayloadSha256(payload),
                            StringComparison.Ordinal))
                    {
                        return Failure(SettingsSerializationCode.ChecksumMismatch);
                    }
                }

                UserSettingsDocumentDto document = migratedRoot.ToObject<UserSettingsDocumentDto>(
                    _serializer);
                SettingsDataValidationResult validation = SettingsDataValidator.ValidateDocument(document);

                if (!validation.Succeeded)
                {
                    return Failure(
                        SettingsSerializationCode.ValidationFailed,
                        validationIssues: validation.Issues);
                }

                document.PayloadSha256 = ComputePayloadSha256(migratedRoot["payload"]);
                return Success(null, document, wasMigrated);
            }
            catch (JsonException exception)
            {
                return Failure(SettingsSerializationCode.InvalidJson, exception);
            }
            catch (DecoderFallbackException exception)
            {
                return Failure(SettingsSerializationCode.InvalidJson, exception);
            }
            catch (Exception exception)
            {
                return Failure(SettingsSerializationCode.SerializationFailed, exception);
            }
        }

        static JsonSerializerSettings CreateSettings()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                DateParseHandling = DateParseHandling.None,
                FloatFormatHandling = FloatFormatHandling.Symbol,
                FloatParseHandling = FloatParseHandling.Double,
                Formatting = Formatting.None,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Include,
            };
            settings.Converters.Add(new StringEnumConverter(new CamelCaseNamingStrategy()));
            return settings;
        }

        static JsonMigrationRegistry CreateDefaultMigrationRegistry()
        {
            JsonMigrationRegistryBuildResult result = JsonMigrationRegistry.Build(
                MigrationDataDomain.Settings,
                SettingsSchemaVersion.Current.Value,
                new IJsonMigrationStep[]
                {
                    new LegacySettingsV0ToV1Migration(),
                });

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "设置迁移注册失败："
                    + string.Join("；", result.Issues.Select(issue => issue.Message)));
            }

            return result.Registry;
        }

        static JObject ParseRoot(byte[] bytes)
        {
            string json = StrictUtf8.GetString(bytes);
            using (JsonTextReader reader = new JsonTextReader(new System.IO.StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;
                reader.MaxDepth = LocalSettingsFormat.MaximumJsonDepth;
                JObject root = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                });

                if (reader.Read())
                {
                    throw new JsonReaderException("设置 JSON 根对象后存在额外内容");
                }

                return root;
            }
        }

        static string ComputePayloadSha256(JToken payload)
        {
            byte[] bytes = WriteCanonicalUtf8(payload ?? JValue.CreateNull());
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);

                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        static byte[] WriteCanonicalUtf8(JToken token)
        {
            JToken canonical = CanonicalizeToken(token);
            string json = canonical.ToString(Formatting.None);
            return StrictUtf8.GetBytes(json);
        }

        static JToken CanonicalizeToken(JToken token)
        {
            if (token is JObject value)
            {
                return new JObject(value.Properties()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => new JProperty(
                        property.Name,
                        CanonicalizeToken(property.Value))));
            }

            if (token is JArray array)
            {
                return new JArray(array.Select(CanonicalizeToken));
            }

            return token?.DeepClone() ?? JValue.CreateNull();
        }

        static int? ReadInteger(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer)
            {
                return null;
            }

            long value = token.Value<long>();
            return value >= int.MinValue && value <= int.MaxValue ? (int)value : null;
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

                if (!(character >= '0' && character <= '9')
                    && !(character >= 'a' && character <= 'f'))
                {
                    return false;
                }
            }

            return true;
        }

        static SettingsSerializationResult Success(
            byte[] bytes,
            UserSettingsDocumentDto document,
            bool wasMigrated)
        {
            return new SettingsSerializationResult(
                SettingsSerializationCode.Success,
                bytes,
                document,
                Array.Empty<SettingsDataIssue>(),
                null,
                wasMigrated);
        }

        static SettingsSerializationResult Failure(
            SettingsSerializationCode code,
            Exception exception = null,
            IReadOnlyList<SettingsDataIssue> validationIssues = null)
        {
            return new SettingsSerializationResult(
                code,
                null,
                null,
                validationIssues ?? Array.Empty<SettingsDataIssue>(),
                exception,
                false);
        }
    }

    public sealed class LegacySettingsV0ToV1Migration : IJsonMigrationStep
    {
        public string Id => "settings_0_to_1_domains";

        public MigrationDataDomain Domain => MigrationDataDomain.Settings;

        public int FromVersion => 0;

        public int ToVersion => 1;

        public void Apply(JObject document)
        {
            JObject payload = document["payload"] as JObject ?? new JObject();

            if (payload["language"] == null && document["language"] != null)
            {
                payload["language"] = document["language"].DeepClone();
            }

            payload["language"] ??= "auto";
            payload["audio"] ??= CreateDefaultAudio();
            payload["input"] ??= CreateDefaultInput();
            payload["display"] ??= CreateDefaultDisplay();
            payload["accessibility"] ??= CreateDefaultAccessibility();
            document["payload"] = payload;
            document["formatId"] = LocalSettingsFormat.FormatId;
            document["formatVersion"] = LocalSettingsFormat.FormatVersion;
            document["updatedUtc"] ??= new DateTimeOffset(
                1970,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero).ToString("O");
            document["payloadSha256"] = string.Empty;
            document.Remove("language");
        }

        static JObject CreateDefaultAudio()
        {
            return new JObject
            {
                ["masterVolume"] = 1f,
                ["musicVolume"] = 1f,
                ["soundEffectsVolume"] = 1f,
                ["uiVolume"] = 1f,
            };
        }

        static JObject CreateDefaultInput()
        {
            return new JObject
            {
                ["bindingOverridesJson"] = string.Empty,
                ["glyphPreference"] = "auto",
            };
        }

        static JObject CreateDefaultDisplay()
        {
            return new JObject
            {
                ["width"] = 1920,
                ["height"] = 1080,
                ["mode"] = "fullscreenWindow",
                ["uiScale"] = 1f,
            };
        }

        static JObject CreateDefaultAccessibility()
        {
            return new JObject
            {
                ["textScale"] = 1f,
                ["reduceMotion"] = false,
                ["screenShakeIntensity"] = 1f,
            };
        }
    }
}
