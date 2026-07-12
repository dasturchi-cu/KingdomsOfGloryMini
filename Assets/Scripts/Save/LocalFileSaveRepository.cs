using System;
using System.IO;
using UnityEngine;

namespace KoG.MiniMvp.Save
{
    /// <summary>Atomic-ish JSON file store under Application.persistentDataPath.</summary>
    public sealed class LocalFileSaveRepository : ISaveRepository
    {
        readonly string _root;

        public LocalFileSaveRepository(string rootFolderName = SaveSchema.RootFolderName)
        {
            _root = Path.Combine(Application.persistentDataPath, rootFolderName ?? SaveSchema.RootFolderName);
            if (!Directory.Exists(_root))
                Directory.CreateDirectory(_root);
        }

        public string RootPath => _root;

        public bool Exists(string slotKey) => File.Exists(ResolvePath(slotKey));

        public void Delete(string slotKey)
        {
            var path = ResolvePath(slotKey);
            if (File.Exists(path))
                File.Delete(path);
        }

        public SaveResult Write(string slotKey, SaveEnvelope envelope)
        {
            if (envelope == null)
                return SaveResult.Fail(SaveResultKind.IoError, "null envelope");
            try
            {
                var path = ResolvePath(slotKey);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonUtility.ToJson(envelope, false);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
                return SaveResult.Ok(envelope);
            }
            catch (Exception e)
            {
                return SaveResult.Fail(SaveResultKind.IoError, e.Message);
            }
        }

        public SaveResult Read(string slotKey)
        {
            var path = ResolvePath(slotKey);
            if (!File.Exists(path))
                return SaveResult.Fail(SaveResultKind.Missing, "missing");
            try
            {
                var json = File.ReadAllText(path);
                var envelope = JsonUtility.FromJson<SaveEnvelope>(json);
                if (envelope == null || string.IsNullOrEmpty(envelope.payloadType))
                    return SaveResult.Fail(SaveResultKind.Corrupt, "bad envelope");
                return SaveResult.Ok(envelope);
            }
            catch (Exception e)
            {
                return SaveResult.Fail(SaveResultKind.Corrupt, e.Message);
            }
        }

        string ResolvePath(string slotKey)
        {
            var safe = Sanitize(slotKey);
            return Path.Combine(_root, safe + ".json");
        }

        static string Sanitize(string key)
        {
            if (string.IsNullOrEmpty(key)) return "default";
            var chars = key.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                    chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
