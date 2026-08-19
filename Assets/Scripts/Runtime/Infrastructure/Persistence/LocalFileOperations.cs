using System.Collections.Generic;

namespace DarkFlare
{
    public interface ILocalFileOperations
    {
        bool DirectoryExists(string path);

        void CreateDirectory(string path);

        IReadOnlyList<string> EnumerateFiles(string path);

        bool FileExists(string path);

        long GetFileLength(string path);

        byte[] ReadAllBytes(string path);

        void WriteAllBytesDurable(string path, byte[] bytes);

        void MoveFileNoOverwrite(string sourcePath, string destinationPath);

        void DeleteFile(string path);
    }
}
