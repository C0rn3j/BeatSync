﻿using BeatSync.Configs;
using BeatSync.Downloader;
using BeatSync.Playlists;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static BeatSync.Utilities.Util;

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
        private bool _destroying;
        private SongDownloader Downloader;
        public SongHasher SongHasher;
        public HistoryManager HistoryManager;
        public CancellationToken CancelAllToken { get; set; }

        public void Awake()
        {
            //Logger.log?.Debug("BeatSync Awake()");
            if (Instance != null)
            {
                if (!Instance._destroying)
                {
                    Logger.log?.Debug("BeatSync component already exists, destroying this one.");
                    GameObject.DestroyImmediate(this);
                }
                else
                    Logger.log?.Warn($"Creating a new BeatSync controller before the old finished destroying itself.");
            }
            Instance = this;
            _destroying = false;
            var instances = GameObject.FindObjectsOfType<BeatSync>().ToList();
            Logger.log?.Critical($"Number of controllers: {instances.Count}");
            //FinishedHashing += OnHashingFinished;
        }


        public void Start()
        {
            Logger.log?.Debug("BeatSync Start()");
            IsRunning = true;
            var recentPlaylist = Plugin.config.Value.RecentPlaylistDays > 0 ? PlaylistManager.GetPlaylist(BuiltInPlaylist.BeatSyncRecent) : null;
            if (recentPlaylist != null && Plugin.config.Value.RecentPlaylistDays > 0)
            {
                var minDate = DateTime.Now - new TimeSpan(Plugin.config.Value.RecentPlaylistDays, 0, 0, 0);
                int removedCount = recentPlaylist.Songs.RemoveAll(s => s.DateAdded < minDate);

                if (removedCount > 0)
                {
                    if (!recentPlaylist.TryWriteFile(out Exception ex))
                    {
                        Logger.log?.Warn($"Unable to write {recentPlaylist.FileName}: {ex.Message}");
                        Logger.log?.Debug(ex);
                    }
                    else
                        Logger.log?.Info($"Removed {removedCount} old songs from the RecentPlaylist.");
                }
                else
                    Logger.log?.Info("Didn't remove any songs from RecentPlaylist.");

            }
            var syncInterval = new TimeSpan(Plugin.config.Value.TimeBetweenSyncs.Hours, Plugin.config.Value.TimeBetweenSyncs.Minutes, 0);
            var nowTime = DateTime.Now;
            if (Plugin.config.Value.LastRun + syncInterval <= nowTime)
            {
                if (Plugin.config.Value.LastRun != DateTime.MinValue)
                    Logger.log?.Info($"BeatSync ran {TimeSpanToString(nowTime - Plugin.config.Value.LastRun)} ago");
                SongHasher = new SongHasher(Plugin.CustomLevelsPath, Plugin.CachedHashDataPath);
                HistoryManager = new HistoryManager(Path.Combine(Plugin.UserDataPath, "BeatSyncHistory.json"));
                Task.Run(() => HistoryManager.Initialize());
                Downloader = new SongDownloader(Plugin.config.Value, HistoryManager, SongHasher, CustomLevelPathHelper.customLevelsDirectoryPath)
                {
                    StatusManager = Plugin.StatusController
                };
                StartCoroutine(HashSongsCoroutine());
            }
            else
            {
                Logger.log?.Info($"BeatSync ran {TimeSpanToString(nowTime - Plugin.config.Value.LastRun)} ago, skipping because TimeBetweenSyncs is {Plugin.config.Value.TimeBetweenSyncs}");
            }
        }

        public IEnumerator<WaitUntil> HashSongsCoroutine()
        {
            SongHasher.LoadCachedSongHashes(false);
            yield return WaitForUnPause;
            var hashingTask = Task.Run(() => SongHasher.AddMissingHashes());
            var hashWait = new WaitUntil(() => hashingTask.IsCompleted);
            yield return hashWait;
            yield return WaitForUnPause;
            var historyUpdate = Task.Run(() => UpdateHistory());
            var historyWait = new WaitUntil(() => historyUpdate.IsCompleted);
            yield return historyWait;
            StartCoroutine(ScrapeSongsCoroutine());
        }


        public IEnumerator<WaitUntil> ScrapeSongsCoroutine()
        {
            Logger.log?.Debug("Starting ScrapeSongsCoroutine");
            var readTask = Downloader.RunReaders(CancelAllToken);
            var readWait = new WaitUntil(() => readTask.IsCompleted);
            yield return readWait;
            var downloadTask = Downloader.WaitDownloadCompletionAsync(CancelAllToken);
            var downloadWait = new WaitUntil(() => downloadTask.IsCompleted);
            if (!downloadTask.IsCompleted)
                Logger.log?.Info("Waiting for downloads to finish.");
            yield return downloadWait;
            PlaylistManager.WriteAllPlaylists();
            HistoryManager.TryWriteToFile();
            int numDownloads = 0;
            try
            {
                numDownloads = downloadTask.Result.Count;
                Logger.log?.Info($"BeatSync finished downloading songs, downloaded {(numDownloads == 1 ? "1 song" : numDownloads + " songs")}.");
            }
            catch (TaskCanceledException ex)
            {
                if (ex.Task is Task<List<IDownloadJob>> downloads)
                {
                    numDownloads = downloads.Result.Count;
                    Logger.log?.Info($"BeatSync was cancelled while downloading songs, downloaded {(numDownloads == 1 ? "1 song" : numDownloads + " songs")}.");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.log?.Info($"BeatSync was cancelled while downloading songs.");
            }
            HistoryManager.TryWriteToFile();
            Plugin.config.Value.LastRun = DateTime.Now;
            Plugin.configProvider.Store(Plugin.config.Value);
            Plugin.config.Value.ResetFlags();
            StartCoroutine(UpdateLevelPacks());
            Plugin.StatusController?.TriggerFade();
            Logger.log?.Info("BeatSync finished.");
            try
            {
                if (Directory.Exists(DownloadJob.SongTempPath))
                    Directory.Delete(DownloadJob.SongTempPath, true);
            }
            catch (Exception) { }
            IsRunning = false;
        }

        public async Task UpdateHistory()
        {
            await SongFeedReaders.Utilities.WaitUntil(() => HistoryManager.IsInitialized);
            var hashCount = SongHasher.ExistingSongs.Count;
            foreach (var songHash in HistoryManager.GetSongHashes())
            {
                if (!SongHasher.ExistingSongs.ContainsKey(songHash))
                {
                    if (HistoryManager.TryGetValue(songHash, out var entry) &&
                        (entry.Flag == HistoryFlag.Downloaded
                        || entry.Flag == HistoryFlag.PreExisting
                        || entry.Flag == HistoryFlag.Missing))
                    {
                        Logger.log?.Info($"Flagging {entry.SongInfo} as deleted.");
                        entry.Flag = HistoryFlag.Deleted;
                    }
                }
            }
            HistoryManager.TryWriteToFile();
        }

        public IEnumerator<WaitUntil> UpdateLevelPacks()
        {
            yield return WaitForUnPause;
            BeatSaverDownloader.Misc.PlaylistsCollection.ReloadPlaylists(true);
            if (!SongCore.Loader.AreSongsLoaded)
            {
                yield break;
            }
            if (SongCore.Loader.AreSongsLoading)
            {
                while (SongCore.Loader.AreSongsLoading)
                    yield return null;
            }
            SongCore.Loader.Instance?.RefreshLevelPacks();
            SongCore.Loader.Instance?.RefreshSongs(true);
        }

        public IEnumerator<WaitUntil> DestroyAfterFinishing()
        {
            _destroying = true;
            Instance = null;
            Logger.log?.Debug($"Waiting for BeatSyncController to finish.");
            yield return new WaitUntil(() => IsRunning == false);
            Logger.log?.Debug($"Destroying BeatSyncController"); 
            GameObject.Destroy(this);
        }
    }
}

