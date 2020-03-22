﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BeatSyncLib.Playlists.Blister;
using System.IO;

namespace BeatSyncLibTests.Playlist_Tests
{
    [TestClass]
    public class BlisterTests
    {
        static string PlaylistsPath = Path.Combine("Data", "Playlists");
        [TestMethod]
        public void LoadBlisterPlaylist()
        {
            string playlistFile = Path.Combine(PlaylistsPath, "BeatSyncScoreSaberLatestRanked.blist");
            var playlist = BlisterHandler.Deserialize(File.OpenRead(playlistFile));
        }
    }
}
