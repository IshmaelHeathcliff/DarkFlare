using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class Alpha027ReleaseAcceptanceTests
    {
        const string RecordPath =
            "Assets/Scripts/Tests/EditMode/Alpha027ReleaseAcceptance.json";

        [Test]
        public void ReleaseRecord_RequiresCleanAddressablesAndWindowsPlayerBuild()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("无法解析项目根目录");
            string absolutePath = Path.Combine(
                projectRoot,
                RecordPath.Replace('/', Path.DirectorySeparatorChar));
            string json = File.ReadAllText(absolutePath, Encoding.UTF8);
            ReleaseAcceptanceRecord record =
                JsonUtility.FromJson<ReleaseAcceptanceRecord>(json);

            Assert.IsNotNull(record);
            Assert.AreEqual(1, record.SchemaVersion);
            Assert.AreEqual(Application.version, record.Version);
            Assert.AreEqual("StandaloneWindows64", record.Target);
            Assert.IsTrue(record.Addressables.Succeeded);
            Assert.IsTrue(record.Addressables.CleanBuild);
            Assert.Zero(record.Addressables.ValidationIssues);
            Assert.IsTrue(record.DevelopmentPlayer.Succeeded);
            Assert.IsTrue(record.DevelopmentPlayer.OutputOutsideRepository);
            Assert.Zero(record.DevelopmentPlayer.Errors);
            Assert.Greater(record.DevelopmentPlayer.DurationSeconds, 0f);
            Assert.Greater(record.DevelopmentPlayer.SizeMegabytes, 0f);
            CollectionAssert.AreEquivalent(
                new[] { "DarkFlare.Core.dll", "DarkFlare.Runtime.dll" },
                record.DevelopmentPlayer.RuntimeAssemblies);
            Assert.IsTrue(record.RuntimeSmoke.ColdStartSucceeded);
            Assert.IsTrue(record.RuntimeSmoke.FullPathAutomatedInPlayMode);
            Assert.AreEqual(
                "blocked_by_windows_security_prompt",
                record.RuntimeSmoke.InteractivePlayerStatus);
        }

        [Serializable]
        sealed class ReleaseAcceptanceRecord
        {
            [SerializeField]
            int schemaVersion;

            [SerializeField]
            string version;

            [SerializeField]
            string target;

            [SerializeField]
            AddressablesAcceptance addressables = new AddressablesAcceptance();

            [SerializeField]
            PlayerAcceptance developmentPlayer = new PlayerAcceptance();

            [SerializeField]
            RuntimeSmokeAcceptance runtimeSmoke = new RuntimeSmokeAcceptance();

            public int SchemaVersion => schemaVersion;
            public string Version => version;
            public string Target => target;
            public AddressablesAcceptance Addressables => addressables;
            public PlayerAcceptance DevelopmentPlayer => developmentPlayer;
            public RuntimeSmokeAcceptance RuntimeSmoke => runtimeSmoke;
        }

        [Serializable]
        sealed class AddressablesAcceptance
        {
            [SerializeField]
            bool succeeded;

            [SerializeField]
            bool cleanBuild;

            [SerializeField]
            int validationIssues;

            public bool Succeeded => succeeded;
            public bool CleanBuild => cleanBuild;
            public int ValidationIssues => validationIssues;
        }

        [Serializable]
        sealed class PlayerAcceptance
        {
            [SerializeField]
            bool succeeded;

            [SerializeField]
            bool outputOutsideRepository;

            [SerializeField]
            int errors;

            [SerializeField]
            int warnings;

            [SerializeField]
            float durationSeconds;

            [SerializeField]
            float sizeMegabytes;

            [SerializeField]
            List<string> runtimeAssemblies = new List<string>();

            public bool Succeeded => succeeded;
            public bool OutputOutsideRepository => outputOutsideRepository;
            public int Errors => errors;
            public int Warnings => warnings;
            public float DurationSeconds => durationSeconds;
            public float SizeMegabytes => sizeMegabytes;
            public IReadOnlyList<string> RuntimeAssemblies => runtimeAssemblies;
        }

        [Serializable]
        sealed class RuntimeSmokeAcceptance
        {
            [SerializeField]
            bool coldStartSucceeded;

            [SerializeField]
            bool fullPathAutomatedInPlayMode;

            [SerializeField]
            string interactivePlayerStatus;

            public bool ColdStartSucceeded => coldStartSucceeded;
            public bool FullPathAutomatedInPlayMode => fullPathAutomatedInPlayMode;
            public string InteractivePlayerStatus => interactivePlayerStatus;
        }
    }
}
