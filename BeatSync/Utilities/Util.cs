﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace BeatSync.Utilities
{
    public static class Util
    {
        /// <summary>
        /// Attempts to find a resource of type TResource with the given name. An action can be provided to execute when the object is found.
        /// pollRateMillis is the interval in milliseconds to check for the existance of the object.
        /// </summary>
        /// <typeparam name="TResource"></typeparam>
        /// <param name="name"></param>
        /// <param name="action"></param>
        /// <param name="pollRateMillis"></param>
        /// <returns></returns>
        public static IEnumerator<WaitForSeconds> WaitForResource<TResource>(string name, Action<TResource> action = null, int pollRateMillis = 100)
            where TResource : UnityEngine.Object
        {
            Func<bool> waitFunc = () => Resources.FindObjectsOfTypeAll<TResource>().Any(o =>
            {
                if (o.name != name)
                    return false;
                try
                {
                    action?.Invoke(o);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error($"Error invoking action for WaitForResource<{typeof(TResource)}> with name {name}.\n{ex?.Message}\n{ex?.StackTrace}");
                }
                return true;
            });
            var wait = new WaitForSeconds(Math.Max(pollRateMillis / 1000f, .02f));
            while (!waitFunc.Invoke())
            {
                yield return wait;
            }
            //yield return waitFunc;

        }


        /// <summary>
        /// Outputs a TimeSpan in hours, minutes, and seconds.
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public static string TimeSpanToString(TimeSpan timeSpan)
        {
            var sb = new StringBuilder();
            if (timeSpan.Days > 0)
                if (timeSpan.Days == 1)
                    sb.Append("1 day ");
                else
                    sb.Append($"{(int)Math.Floor(timeSpan.TotalDays)} days ");
            if (timeSpan.Minutes > 0)
                if (timeSpan.Minutes == 1)
                    sb.Append("1 min ");
                else
                    sb.Append($"{timeSpan.Minutes} mins ");
            if (timeSpan.Seconds > 0)
                if (timeSpan.Seconds == 1)
                    sb.Append("1 sec ");
                else
                    sb.Append($"{timeSpan.Seconds} secs ");
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Generates a hash for the song and assigns it to the SongHash field. Returns null if info.dat doesn't exist.
        /// Uses Kylemc1413's implementation from SongCore.
        /// TODO: Handle/document exceptions (such as if the files no longer exist when this is called).
        /// https://github.com/Kylemc1413/SongCore
        /// </summary>
        /// <returns>Hash of the song files. Null if the info.dat file doesn't exist</returns>
        public static string GenerateHash(string songDirectory, string existingHash = "")
        {
            byte[] combinedBytes = Array.Empty<byte>();
            string infoFile = Path.Combine(songDirectory, "info.dat");
            if (!File.Exists(infoFile))
                return null;
            combinedBytes = combinedBytes.Concat(File.ReadAllBytes(infoFile)).ToArray();
            var token = JToken.Parse(File.ReadAllText(infoFile));
            var beatMapSets = token["_difficultyBeatmapSets"];
            int numChars = beatMapSets.Children().Count();
            for (int i = 0; i < numChars; i++)
            {
                var diffs = beatMapSets.ElementAt(i);
                int numDiffs = diffs["_difficultyBeatmaps"].Children().Count();
                for (int i2 = 0; i2 < numDiffs; i2++)
                {
                    var diff = diffs["_difficultyBeatmaps"].ElementAt(i2);
                    string beatmapPath = Path.Combine(songDirectory, diff["_beatmapFilename"].Value<string>());
                    if (File.Exists(beatmapPath))
                        combinedBytes = combinedBytes.Concat(File.ReadAllBytes(beatmapPath)).ToArray();
                    else
                        Logger.log?.Debug($"Missing difficulty file {beatmapPath.Split('\\', '/').LastOrDefault()}");
                }
            }

            string hash = CreateSha1FromBytes(combinedBytes.ToArray());
            if (!string.IsNullOrEmpty(existingHash) && existingHash != hash)
                Logger.log?.Warn($"Hash doesn't match the existing hash for {songDirectory}");
            return hash;
        }

        /// <summary>
        /// Returns the Sha1 hash of the provided byte array.
        /// Uses Kylemc1413's implementation from SongCore.
        /// https://github.com/Kylemc1413/SongCore
        /// </summary>
        /// <param name="input">Byte array to hash.</param>
        /// <returns>Sha1 hash of the byte array.</returns>
        public static string CreateSha1FromBytes(byte[] input)
        {
            using (var sha1 = SHA1.Create())
            {
                var inputBytes = input;
                var hashBytes = sha1.ComputeHash(inputBytes);

                return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
            }
        }

        /// <summary>
        /// Generates a quick hash of a directory's contents. Does NOT match SongCore.
        /// Uses most of Kylemc1413's implementation from SongCore.
        /// </summary>
        /// <param name="path"></param>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when path's directory doesn't exist.</exception>
        /// <returns></returns>
        public static long GenerateDirectoryHash(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path), "Path cannot be null or empty for GenerateDirectoryHash");
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (!directoryInfo.Exists)
                throw new DirectoryNotFoundException($"GenerateDirectoryHash couldn't find {path}");
            long dirHash = 0L;
            foreach (var file in directoryInfo.GetFiles())
            {
                dirHash ^= file.CreationTimeUtc.ToFileTimeUtc();
                dirHash ^= file.LastWriteTimeUtc.ToFileTimeUtc();
                dirHash ^= file.Name.GetHashCode();
                //dirHash ^= SumCharacters(file.Name); // Replacement for if GetHashCode stops being predictable.
                dirHash ^= file.Length;
            }
            return dirHash;
        }

        public static string GetSongDirectoryName(string songKey, string songName, string levelAuthorName)
        {
            // BeatSaverDownloader's method of naming the directory.
            string basePath = songKey + " (" + songName + " - " + levelAuthorName + ")";
            basePath = string.Join("", basePath.Trim().Split((Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).ToArray())));
            return basePath;
        }

        private static int SumCharacters(string str)
        {
            unchecked
            {
                int charSum = 0;
                for (int i = 0; i < str.Count(); i++)
                {
                    charSum += str[i];
                }
                return charSum;
            }
        }

        #region Sprite loading (From https://github.com/brian91292/BeatSaber-CustomUI/blob/master/Utilities/Utilities.cs)
        public static Sprite LoadSpriteFromResources(string resourcePath, float PixelsPerUnit = 100.0f)
        {
            return LoadSpriteRaw(GetResource(Assembly.GetCallingAssembly(), resourcePath), PixelsPerUnit);
        }

        public static byte[] GetResource(Assembly asm, string ResourceName)
        {
            System.IO.Stream stream = asm.GetManifestResourceStream(ResourceName);
            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, (int)stream.Length);
            return data;
        }

        public static Sprite LoadSpriteRaw(byte[] image, float PixelsPerUnit = 100.0f)
        {
            return LoadSpriteFromTexture(LoadTextureRaw(image), PixelsPerUnit);
        }

        public static Sprite LoadSpriteFromTexture(Texture2D SpriteTexture, float PixelsPerUnit = 100.0f)
        {
            if (SpriteTexture)
                return Sprite.Create(SpriteTexture, new Rect(0, 0, SpriteTexture.width, SpriteTexture.height), new Vector2(0, 0), PixelsPerUnit);
            return null;
        }

        public static Texture2D LoadTextureRaw(byte[] file)
        {
            if (file.Count() > 0)
            {
                Texture2D Tex2D = new Texture2D(2, 2);
                if (Tex2D.LoadImage(file))
                    return Tex2D;
            }
            return null;
        }


        #endregion
    }
}
