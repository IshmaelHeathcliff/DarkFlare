using System;
using System.IO;
using UnityEngine;

namespace DarkFlare
{
    public interface ISavePathProvider
    {
        string RootPath { get; }
    }

    public sealed class PersistentSavePathProvider : ISavePathProvider
    {
        public string RootPath { get; }

        public PersistentSavePathProvider()
        {
            string persistentDataPath = Application.persistentDataPath;

            if (string.IsNullOrWhiteSpace(persistentDataPath))
            {
                throw new InvalidOperationException("Application.persistentDataPath 不可用");
            }

            RootPath = Path.Combine(persistentDataPath, "DarkFlare", "Saves");
        }
    }
}
