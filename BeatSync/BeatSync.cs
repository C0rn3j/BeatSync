﻿using BeatSync.Configs;
using BeatSync.Downloader;
using BeatSync.Playlists;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSync
{
    public class BeatSync : MonoBehaviour
    {
        public static BeatSync Instance { get; set; }
        private static bool _paused;
        public static bool IsRunning { get; private set; }
        public static bool Paused
        {
            get
            {
                return _paused;
            }
            set
            {
                if (_paused == value)
                {
                    return;
                }

                _paused = value;
                if (_paused)
                {
                    SongFeedReaders.Utilities.Pause();
                    if (IsRunning)
                        Logger.log?.Info("Pausing BeatSync.");
                }
                else
                {
                    SongFeedReaders.Utilities.UnPause();
                    if (IsRunning)
                        Logger.log?.Info("Resuming BeatSync.");
                }
            }
        }

        private static WaitUntil WaitForUnPause = new WaitUntil(() => !Paused);
        private SongDownloader Downloader;
        public SongHasher SongHasher;
        public HistoryManager HistoryManager;


        public void Awake()
        {
            if (Instance != null)
                GameObject.DestroyImmediate(this);
            Instance = this;
            //FinishedHashing += OnHashingFinished;
        }


        public void Start()
        {
            Logger.log?.Debug("BeatSync Start()");
            IsRunning = true;
            SongHasher = new SongHasher(Plugin.CustomLevelsPath, Plugin.CachedHashDataPath);
            HistoryManager = new HistoryManager(Path.Combine(Plugin.UserDataPath, "BeatSyncHistory.json"));
            Task.Run(() => HistoryManager.Initialize());
            Downloader = new SongDownloader(Plugin.config.Value, HistoryManager, SongHasher, CustomLevelPathHelper.customLevelsDirectoryPath);
            //LoadCachedSongHashesAsync(Plugin.CachedHashDataPath);
            //Logger.log?.Critical($"Read {HashDictionary.Count} cached songs.");
            //var hashTask = Task.Run(() => AddMissingHashes());
            //Logger.log?.Info("Converting legacy playlists.");
            //PlaylistManager.ConvertLegacyPlaylists();
            var recentPlaylist = Plugin.config.Value.RecentPlaylistDays > 0 ? PlaylistManager.GetPlaylist(BuiltInPlaylist.BeatSyncRecent) : null;
            if (recentPlaylist != null && Plugin.config.Value.RecentPlaylistDays > 0)
            {
                var minDate = DateTime.Now - new TimeSpan(Plugin.config.Value.RecentPlaylistDays, 0, 0, 0);
                int removedCount = recentPlaylist.Songs.RemoveAll(s => s.DateAdded < minDate);
                if (removedCount > 0)
                {
                    Logger.log?.Info($"Removed {removedCount} old songs from the RecentPlaylist.");
                }
                else
                    Logger.log?.Info("Didn't remove any songs from RecentPlaylist.");
                
            }
            StartCoroutine(HashSongsCoroutine());
            FavoriteMappers.Initialize();
        }

        public IEnumerator<WaitUntil> HashSongsCoroutine()
        {
            SongHasher.LoadCachedSongHashes(false);
            yield return WaitForUnPause;
            var hashingTask = Task.Run(() => SongHasher.AddMissingHashes());
            var hashWait = new WaitUntil(() => hashingTask.IsCompleted);
            yield return hashWait;
            yield return WaitForUnPause;
            StartCoroutine(ScrapeSongsCoroutine());
        }


        public IEnumerator<WaitUntil> ScrapeSongsCoroutine()
        {
            Logger.log?.Debug("Starting ScrapeSongsCoroutine");
            var readTask = Downloader.RunReaders();
            var readWait = new WaitUntil(() => readTask.IsCompleted);
            yield return readWait;
            var downloadTask = Downloader.RunDownloaderAsync(Plugin.config.Value.MaxConcurrentDownloads);
            var downloadWait = new WaitUntil(() => downloadTask.IsCompleted);
            yield return downloadWait;
            PlaylistManager.WriteAllPlaylists();
            HistoryManager.WriteToFile();
            int numDownloads = downloadTask.Result.Count;
            IsRunning = false;
            Logger.log?.Info($"BeatSync finished reading feeds, downloaded {(numDownloads == 1 ? "1 song" : numDownloads + " songs")}.");
            StartCoroutine(UpdateLevelPacks());
        }

        

        public IEnumerator<WaitUntil> UpdateLevelPacks()
        {
            yield return WaitForUnPause;
            BeatSaverDownloader.Misc.PlaylistsCollection.ReloadPlaylists(true);
            if (!SongCore.Loader.AreSongsLoaded && SongCore.Loader.AreSongsLoading)
            {
                while (SongCore.Loader.AreSongsLoading)
                    yield return null;
            }
            else
            {
                SongCore.Loader.Instance?.RefreshLevelPacks();
                SongCore.Loader.Instance?.RefreshSongs(true);
            }
        }
    }
}

