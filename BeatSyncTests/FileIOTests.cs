﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BeatSync;
using BeatSync.Playlists;
using System.IO;
using BeatSync.Utilities;

namespace BeatSyncTests
{
    [TestClass]
    public class FileIO_Tests
    {
        [TestMethod]
        public void GetFilePath_Test()
        {
            var test = FileIO.GetPlaylistFilePath("BeatSyncBSaberBookmarks");
            var sep = Path.DirectorySeparatorChar;
            var alt = Path.AltDirectorySeparatorChar;
        }
    }
}
