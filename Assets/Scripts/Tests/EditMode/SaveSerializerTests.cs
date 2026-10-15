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
        public void BehaviorMigration_PreservesReleasedNavigationState_AndInitializesFrozenActors()
        {
            byte[] bytes = System.IO.File.ReadAllBytes("Assets/Scripts/Tests/Fixtures/Migration/save-v5-navigation.json");
            JObject historical = JObject.Parse(Encoding.UTF8.GetString(bytes));
            SaveDeserializationResult restored = _serializer.Deserialize(bytes);
            Assert.IsTrue(restored.Succeeded, Describe(restored));
            SaveSerializationResult serialized = _serializer.Serialize(restored.Document);
            Assert.IsTrue(serialized.Succeeded, Describe(serialized));
            JObject current = JObject.Parse(Encoding.UTF8.GetString(serialized.Bytes));
            foreach (JToken monster in current["payload"]["run"]["monsters"])
            {
                Assert.IsTrue(JToken.DeepEquals(monster["position"], monster["behavior"]["anchor"]));
                Assert.AreEqual(0, (int)monster["behavior"]["attackPhase"]);
                Assert.AreEqual(0, (long)monster["behavior"]["randomSequence"]);
                ((JObject)monster).Remove("behavior");
            }
            Assert.AreEqual((int)BossEncounterStatus.LegacyAbsent, (int)current["payload"]["run"]["boss"]["status"]);
            StripLaterMigrationFields(current, historical);
            Assert.IsTrue(JToken.DeepEquals(historical["payload"], current["payload"]), "行为迁移不得改变导航、资源、冷却、身份或随机流");
            JObject frozen = (JObject)historical.DeepClone();
            frozen["payload"]["run"]["frozenMaps"][0]["monsters"] = frozen["payload"]["run"]["monsters"].DeepClone();
            new MonsterBehaviorSaveMigration().Apply(frozen);
            foreach (JToken monster in frozen["payload"]["run"]["frozenMaps"][0]["monsters"])
            { Assert.IsTrue(JToken.DeepEquals(monster["position"], monster["behavior"]["anchor"])); }
        }

        [Test]
        public void BehaviorState_RoundTripsActionAndRejectsUnknownVersionOrForeignTarget()
        {
            SaveDocumentDto document = SaveDataContractTests.CreateValidDocument();
            MonsterBehaviorStateDto state = document.Payload.Run.Monsters[0].Behavior;
            state.Mode = MonsterBehaviorMode.Attacking; state.AttackPhase = MonsterAttackPhase.Recovery;
            state.ActionId = 12; state.HitResolved = true; state.PhaseRemaining = .21f;
            state.RandomSequence = 77; state.TargetId = "player:" + document.Payload.Profile.PlayerId;
            SaveSerializationResult serialized = _serializer.Serialize(document);
            Assert.IsTrue(serialized.Succeeded, Describe(serialized));
            SaveDeserializationResult restored = _serializer.Deserialize(serialized.Bytes);
            Assert.IsTrue(restored.Succeeded, Describe(restored));
            Assert.IsTrue(restored.Document.Payload.Run.Monsters[0].Behavior.HitResolved);
            Assert.AreEqual(77, restored.Document.Payload.Run.Monsters[0].Behavior.RandomSequence);
            state.Version = 99; Assert.IsFalse(_serializer.Serialize(document).Succeeded);
            state.Version = 1; state.TargetId = "player:foreign"; Assert.IsFalse(_serializer.Serialize(document).Succeeded);
            state.TargetId = null; Assert.IsFalse(_serializer.Serialize(document).Succeeded);
        }

        [Test]
        public void NavigationMigration_PreservesReleasedMapSaveAndAddsFrozenDefaults()
        {
            byte[] bytes = System.IO.File.ReadAllBytes("Assets/Scripts/Tests/Fixtures/Migration/save-v4-world-maps.json");
            JObject historical = JObject.Parse(Encoding.UTF8.GetString(bytes));
            SaveDeserializationResult result = _serializer.Deserialize(bytes);
            Assert.IsTrue(result.Succeeded, Describe(result));
            SaveSerializationResult serialized = _serializer.Serialize(result.Document);
            Assert.IsTrue(serialized.Succeeded, Describe(serialized));
            JObject current = JObject.Parse(Encoding.UTF8.GetString(serialized.Bytes));
            foreach (JToken monster in current["payload"]["run"]["monsters"])
            { Assert.AreEqual(0, (int)monster["navigation"]["target"]); ((JObject)monster).Remove("navigation"); ((JObject)monster).Remove("behavior"); }
            StripLaterMigrationFields(current, historical);
            Assert.IsTrue(JToken.DeepEquals(historical["payload"], current["payload"]), "Released state must not be rerolled or normalized by navigation migration");
            JObject frozen = (JObject)historical.DeepClone();
            frozen["payload"]["run"]["frozenMaps"][0]["monsters"] = frozen["payload"]["run"]["monsters"].DeepClone();
            new NavigationSaveMigration().Apply(frozen);
            foreach (JToken monster in frozen["payload"]["run"]["frozenMaps"][0]["monsters"])
            { Assert.AreEqual(0, (int)monster["navigation"]["target"]); Assert.AreEqual(JTokenType.Null, monster["navigation"]["mode"].Type); }
        }

        [Test]
        public void NavigationIntent_RoundTripsAndRejectsUnknownModeOrCrossMapTarget()
        {
            SaveDocumentDto source = SaveDataContractTests.CreateValidDocument();
            MonsterRunStateDto monster = source.Payload.Run.Monsters[0];
            monster.Navigation = new NavigationIntentDto { Mode = NavigationMode.AStar, Target = NavigationTargetKind.Point,
                MapId = source.Payload.Run.ActiveMapId, Point = new Vector3Dto { X = 5, Y = 8 }, StopDistance = .2f };
            SaveSerializationResult bytes = _serializer.Serialize(source);
            Assert.IsTrue(bytes.Succeeded, Describe(bytes));
            SaveDeserializationResult restored = _serializer.Deserialize(bytes.Bytes);
            Assert.IsTrue(restored.Succeeded, Describe(restored));
            Assert.AreEqual(NavigationMode.AStar, restored.Document.Payload.Run.Monsters[0].Navigation.Mode);
            Assert.AreEqual(8, restored.Document.Payload.Run.Monsters[0].Navigation.Point.Y);
            monster.Navigation.Mode = (NavigationMode)99;
            Assert.IsFalse(_serializer.Serialize(source).Succeeded);
            monster.Navigation.Mode = NavigationMode.FlowField;
            monster.Navigation.MapId = "map:other";
            Assert.IsFalse(_serializer.Serialize(source).Succeeded);
            RunSaveData run = source.Payload.Run;
            MonsterRunStateDto frozenTarget = MapStateData.Copy(monster);
            frozenTarget.InstanceId = run.InstanceIds.RunId + ":monster:3";
            frozenTarget.Navigation = new NavigationIntentDto();
            run.InstanceIds.NextMonsterSequence = 4;
            var frozenMap = new MapStateDto { MapId = "map:other", Random = MapStateData.Copy(run.Random) };
            frozenMap.Monsters.Add(frozenTarget);
            run.FrozenMaps.Add(frozenMap);
            monster.Navigation = new NavigationIntentDto();
            Assert.IsTrue(_serializer.Serialize(source).Succeeded, "目标引用负例必须基于合法双地图存档");
            monster.Navigation = new NavigationIntentDto { Target = NavigationTargetKind.Actor,
                MapId = run.ActiveMapId, ActorId = frozenTarget.InstanceId };
            Assert.IsFalse(_serializer.Serialize(source).Succeeded, "伪装成当前 MapId 的冻结图目标也必须拒绝");
        }

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
            foreach (string field in new[] { "activeMapId", "mapRevision", "mapRandom", "frozenMaps" })
            { ((JObject)current["payload"]["run"]).Remove(field); }
            foreach (JToken monster in current["payload"]["run"]["monsters"]) { ((JObject)monster).Remove("navigation"); ((JObject)monster).Remove("behavior"); }
            StripLaterMigrationFields(current, historical);

            Assert.AreEqual("map:wilderness", result.Document.Payload.Run.ActiveMapId);
            Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(result.Document.Payload.Run.Random), JToken.FromObject(result.Document.Payload.Run.MapRandom)));
            Assert.IsTrue(JToken.DeepEquals(historical["payload"], current["payload"]), "迁移不能改变物品、资源、随机数和运行时数据");
        }

        [Test]
        public void GrowthMigration_PreservesEntireSchemaTenPayloadAndOnlyAddsEmptyOwnedGrowth()
        {
            JObject legacy = JObject.Parse(MigrationFixtureUtility.ReadUtf8("save-v9-migrated-player.json"));
            // 来源是真实发布档经历史迁移导出的 Schema 9；在内存中经原 9→10 链构造源合同。
            new SkillBoardSaveMigration().Apply(legacy);
            JToken before = legacy["payload"].DeepClone();
            new PlayerGrowthSaveMigration().Apply(legacy);
            PlayerGrowthDto dto = legacy["payload"]["profile"]["growth"].ToObject<PlayerGrowthDto>();
            PlayerGrowthState state = RuntimeStateMapper.ReadPlayerGrowth(dto, out string error);
            Assert.That(error, Is.Null);
            Assert.That(state.EarnedPoints, Is.Zero);
            Assert.That(state.Energy, Is.Zero);
            Assert.That(state.Rules, Is.EqualTo(GrowthRules.Default));
            Assert.That(dto.ActorInstanceId, Is.EqualTo(legacy["payload"]["profile"]["playerId"].Value<string>()));
            ((JObject)legacy["payload"]["profile"]).Remove("growth");
            Assert.That(JToken.DeepEquals(before, legacy["payload"]), Is.True,
                "成长迁移不得改写原棋盘、瓶、技能冷却、资源、历史实例或随机状态");
        }

        static void StripLaterMigrationFields(JObject document, JObject historical)
        {
            JObject profile = (JObject)document["payload"]["profile"];
            if (historical["payload"]["profile"]["growth"] == null)
            {
                PlayerGrowthDto growth = profile["growth"].ToObject<PlayerGrowthDto>();
                PlayerGrowthState state = RuntimeStateMapper.ReadPlayerGrowth(growth, out string error);
                Assert.That(error, Is.Null);
                Assert.That(growth.ActorInstanceId, Is.EqualTo(profile["playerId"].Value<string>()));
                Assert.That(state.EarnedPoints, Is.Zero);
                Assert.That(state.Energy, Is.Zero);
                Assert.That(state.Activations, Is.Empty);
                Assert.That(state.Rules, Is.EqualTo(GrowthRules.Default));
                profile.Remove("growth");
            }
            if (historical["payload"]["profile"]["skillBoard"] == null)
            {
                JToken board = profile["skillBoard"];
                Assert.That(board["actorInstanceId"].Value<string>(), Is.EqualTo(profile["playerId"].Value<string>()));
                Assert.That((JArray)board["firewoods"], Is.Empty);
                Assert.That((JArray)board["embeddings"], Is.Empty);
                Assert.That((JArray)board["connections"], Is.Empty);
                Assert.That(board["energyBottleInstanceId"].Value<string>(), Is.Empty);
                profile.Remove("skillBoard");
                foreach (JObject item in document["payload"]["items"])
                { Assert.That(item["skillBoard"].Type, Is.EqualTo(JTokenType.Null)); item.Remove("skillBoard"); }
            }
            var run = (JObject)document["payload"]["run"];
            var oldRun = (JObject)historical["payload"]["run"];
            run.Remove("activeMapEpoch");
            run.Remove("boss");
            StripFiniteSpawnFields((JObject)run["spawner"], (JObject)oldRun["spawner"]);
            foreach (JToken monster in run["monsters"]) { ((JObject)monster).Remove("boss"); }
            if (run["frozenMaps"] is not JArray maps) { return; }
            for (int i = 0; i < maps.Count; i++)
            {
                JToken map = maps[i];
                ((JObject)map).Remove("boss");
                ((JObject)map).Remove("instanceEpoch");
                StripFiniteSpawnFields((JObject)map["spawner"], (JObject)oldRun["frozenMaps"][i]["spawner"]);
                foreach (JToken monster in map["monsters"]) { ((JObject)monster).Remove("boss"); }
            }
        }

        static void StripFiniteSpawnFields(JObject spawner, JObject historical)
        {
            spawner.Remove("mode");
            spawner.Remove("planSeed");
            spawner.Remove("batches");
            spawner.Remove("slots");
            spawner["isRunning"] = historical["isRunning"].DeepClone();
            spawner["nextSpawnRemainingSeconds"] = historical["nextSpawnRemainingSeconds"].DeepClone();
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

        [TestCase("1.2.3-alpha.10+build.001")]
        [TestCase("999.0.0-beta")]
        [TestCase("legacy-build")]
        public void GameVersion_RoundTripsOriginalHeaderWithoutControllingDataCompatibility(string value)
        {
            SaveDocumentDto source = SaveDataContractTests.CreateValidDocument();
            source.Header.GameVersion = value;

            SaveSerializationResult serialized = _serializer.Serialize(source);
            Assert.IsTrue(serialized.Succeeded, Describe(serialized));
            SaveDeserializationResult restored = _serializer.Deserialize(serialized.Bytes);
            Assert.IsTrue(restored.Succeeded, Describe(restored));
            Assert.AreEqual(value, restored.Document.Header.GameVersion);
            Assert.AreEqual(source.Header.SaveSchemaVersion, restored.Document.Header.SaveSchemaVersion);
            Assert.AreEqual(source.Header.ContentVersion, restored.Document.Header.ContentVersion);
            Assert.IsTrue(JToken.DeepEquals(
                JToken.FromObject(source.Payload),
                JToken.FromObject(restored.Document.Payload)));
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
