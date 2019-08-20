﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BeatSync;
using System.IO;
using System.Threading.Tasks;

namespace BeatSyncTests.SongHasher_Tests
{

    [TestClass]
    public class GetSongHashData_Tests
    {
        private static readonly string SongCoreCachePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"Low\Hyperbolic Magnetism\Beat Saber\SongHashData.dat";
        private static readonly string TestCacheDir = Path.GetFullPath(Path.Combine("Data", "SongHashData"));
        private static readonly string TestSongsDir = Path.GetFullPath(Path.Combine("Data", "Songs"));

        static GetSongHashData_Tests()
        {
            TestSetup.Initialize();
        }

        [TestMethod]
        public void ValidDir()
        {
            var hasher = new SongHasher(@"Data\Songs");
            var songDir = @"Data\Songs\5d02 (Sail - baxter395)";
            var expectedHash = "d6f3f15484fe169f4593718f50ef6d049fcaa72e".ToUpper();
            var hashData = hasher.GetSongHashDataAsync(songDir).Result;
            Assert.AreEqual(hashData.songHash, expectedHash);
        }

        [TestMethod]
        public void MissingInfoDat()
        {
            var hasher = new SongHasher();
            var songDir = @"Data\Songs\0 (Missing Info.dat)";
            var hashData = hasher.GetSongHashDataAsync(songDir).Result;
            Assert.IsNull(hashData.songHash);
        }

        [TestMethod]
        public void MissingExpectedDifficultyFile()
        {
            var hasher = new SongHasher();
            var songDir = @"Data\Songs\0 (Missing ExpectedDiff)";
            var hashData = hasher.GetSongHashDataAsync(songDir).Result;
            Assert.IsNotNull(hashData.songHash);
        }

        [TestMethod]
        public void DirectoryDoesntExist()
        {
            var songDir = Path.GetFullPath(@"Data\DoesntExistSongs");
            var hasher = new SongHasher();

            var test = Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(async () => await hasher.GetSongHashDataAsync(songDir).ConfigureAwait(false)).Result;
        }
    }
}
