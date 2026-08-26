using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DarkFlare.Tests
{
    static class MigrationFixtureUtility
    {
        const string FixtureDirectory = "Scripts/Tests/Fixtures/Migration";

        public static string ReadUtf8(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || Path.IsPathRooted(fileName)
                || fileName.Contains("..", StringComparison.Ordinal))
            {
                throw new ArgumentException("迁移夹具名无效", nameof(fileName));
            }

            string path = Path.Combine(Application.dataPath, FixtureDirectory, fileName);
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }
    }
}
