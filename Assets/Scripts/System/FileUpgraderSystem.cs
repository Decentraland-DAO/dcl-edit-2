using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Assets.Scripts.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace Assets.Scripts.System
{
    public class FileUpgraderSystem
    {
        public struct Version
        {
            public int major;
            public int minor;
            public int patch;

            public Version(string versionString)
            {
                var parts = versionString.Split('.');
                Assert.AreEqual(3, parts.Length);
                major = int.Parse(parts[0]);
                minor = int.Parse(parts[1]);
                patch = int.Parse(parts[2]);
            }

            public Version(int major, int minor, int patch)
            {
                this.major = major;
                this.minor = minor;
                this.patch = patch;
            }

            public void Deconstruct(out int major, out int minor, out int patch)
            {
                major = this.major;
                minor = this.minor;
                patch = this.patch;
            }

            public static implicit operator Version((int, int, int) value)
            {
                return new Version(value.Item1, value.Item2, value.Item3);
            }

            public override string ToString()
            {
                return $"{major}.{minor}.{patch}";
            }

            public bool Equals(Version other)
            {
                return major == other.major && minor == other.minor && patch == other.patch;
            }

            public override bool Equals(object obj)
            {
                return obj is Version other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (major, minor, patch).GetHashCode();
            }


            public static bool operator <(Version lhs, Version rhs)
            {
                if (lhs.major < rhs.major)
                {
                    return true;
                }

                if (lhs.major == rhs.major && lhs.minor < rhs.minor)
                {
                    return true;
                }

                if (lhs.major == rhs.major && lhs.minor == rhs.minor && lhs.patch < rhs.patch)
                {
                    return true;
                }

                return false;
            }

            public static bool operator >(Version lhs, Version rhs)
            {
                if (lhs.major > rhs.major)
                {
                    return true;
                }

                if (lhs.major == rhs.major && lhs.minor > rhs.minor)
                {
                    return true;
                }

                if (lhs.major == rhs.major && lhs.minor == rhs.minor && lhs.patch > rhs.patch)
                {
                    return true;
                }

                return false;
            }

            public static bool operator ==(Version lhs, Version rhs)
            {
                return lhs.major == rhs.major && lhs.minor == rhs.minor && lhs.patch == rhs.patch;
            }

            public static bool operator !=(Version lhs, Version rhs)
            {
                return !(lhs == rhs);
            }

            public static bool operator <=(Version lhs, Version rhs)
            {
                return lhs < rhs || lhs == rhs;
            }

            public static bool operator >=(Version lhs, Version rhs)
            {
                return lhs > rhs || lhs == rhs;
            }
        }

        [Serializable]
        private struct FileInfo
        {
            // ReSharper disable once UnassignedField.Local
            public string @dclEditVersionNumber;
        }

        public Version GetFileVersion(string path)
        {
            var fileContents = File.ReadAllText(path);
            var fileInfo = JsonConvert.DeserializeObject<FileInfo>(fileContents);
            var versionString = fileInfo.dclEditVersionNumber;
            return new Version(versionString);
        }

        public void SetFileVersion(string path, Version version)
        {
            var fileContents = File.ReadAllText(path);
            var json = JObject.Parse(fileContents);
            json["dclEditVersionNumber"] = (JToken) version.ToString();
            var newFileContents = json.ToString(Formatting.Indented);
            File.WriteAllText(path, newFileContents);
        }

        public void CheckUpgrades(string path)
        {
            var currentVersion = new Version(Application.version);
            var fileVersion = GetFileVersion(path);

            // file version should not be above current version
            if (fileVersion > currentVersion)
            {
                throw new Exception($"The file {path} was saved with a newer version of the editor ({fileVersion}). Please update the editor to the latest version.");
            }

            foreach (var (version, action) in upgradeActions)
            {
                if (version <= fileVersion)
                {
                    continue;
                }

                if (version > currentVersion)
                {
                    break;
                }

                action(path);
            }

            SetFileVersion(path, currentVersion);
        }

        // Utility
        private bool IsDclAssetFile(string path)
        {
            return path.EndsWith(".dclasset");
        }

        private bool IsSceneJsonFile(string path)
        {
            return path.EndsWith("scene.json");
        }

        private bool IsEntityFile(string path)
        {
            /*
             * This pattern matches filenames with the following format:
             * 
             * A human-readable name that can contain any character, followed by a dash (-).
             * A sequence of 8 hexadecimal digits (0-9 and a-f or A-F) followed by a dash (-).
             * A sequence of 3 groups of dash-separated sequences of 4 hexadecimal digits.
             * A final sequence of 12 hexadecimal digits.
             * The extension .json.
             */
            var regex = new Regex("/^.+-\\b[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\\.json$/gm");
            return regex.IsMatch(path);
        }

        // Upgrades
        private readonly SortedDictionary<Version, Action<string>> upgradeActions = new SortedDictionary<Version, Action<string>>( /*Comparer<Version>.Create((l, r) => l > r ? -1 : l < r ? 1 : 0)*/);

        private void SetupUpgrades()
        {
            upgradeActions.Add((1, 0, 1), UpgradeAssetFileNamesToIncludeOriginalFileEnding);
        }

        private void UpgradeAssetFileNamesToIncludeOriginalFileEnding(string path)
        {
            if (!IsDclAssetFile(path))
            {
                return;
            }
            
            try
            {
                var fileContents = File.ReadAllText(path);
                var json = JObject.Parse(fileContents);

                var assetMetaData = json["metadata"];
                
                if (assetMetaData == null)
                {
                    throw new Exception("No metadata in file!");
                }
                
                var newMetaFileName = assetMetaData["assetFilename"] + ".dclasset";
                var newPath = Path.Combine(Path.GetDirectoryName(path), newMetaFileName);
                
                File.WriteAllText(newPath, fileContents);
                
                File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
        }
    }
}
