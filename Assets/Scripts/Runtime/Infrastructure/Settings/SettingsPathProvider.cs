using System;
using System.IO;
using UnityEngine;

namespace DarkFlare
{
    public interface ISettingsPathProvider
    {
        string RootPath { get; }
    }

    public sealed class PersistentSettingsPathProvider : ISettingsPathProvider
    {
        public string RootPath { get; }

        public PersistentSettingsPathProvider()
        {
            string persistentDataPath = Application.persistentDataPath;

            if (string.IsNullOrWhiteSpace(persistentDataPath))
            {
                throw new InvalidOperationException("Application.persistentDataPath 不可用");
            }

            RootPath = Path.Combine(persistentDataPath, "DarkFlare", "Settings");
        }
    }
}
