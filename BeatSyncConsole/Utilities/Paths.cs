﻿using BeatSyncConsole.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSyncConsole.Utilities
{
    public static class Paths
    {
        public const string Path_CustomLevels = @"Beat Saber_Data\CustomLevels";
        public const string Path_Playlists = @"Playlists";
        public const string Path_History = @"UserData\BeatSyncHistory.json";
        public static BeatSaberInstallLocation ToSongLocation(this BeatSaberInstall install)
        {
            return new BeatSaberInstallLocation(install.InstallPath);
        }

        public static string ReplaceWorkingDirectory(string fullPath) => fullPath.Replace(Directory.GetCurrentDirectory(), ".");
    }
}
