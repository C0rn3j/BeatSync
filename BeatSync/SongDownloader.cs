﻿using BeatSync.Configs;
using BeatSync.Playlists;
using BeatSync.Utilities;
using SongCore.Data;
using SongFeedReaders;
using SongFeedReaders.DataflowAlternative;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSync
{
    internal class SongDownloader
    {
        private SongHasher HashSource { get; set; }
        private const string BeatSaverDownloadUrlBase = "https://beatsaver.com/api/download/hash/";
        private static readonly string SongTempPath = Path.GetFullPath(Path.Combine("UserData", "BeatSyncTemp"));
        private ConcurrentQueue<PlaylistSong> DownloadQueue;
        private PluginConfig Config;

        private TransformBlock<PlaylistSong, PlaylistSong> DownloadBatch;

        public SongDownloader(PluginConfig config)
        {
            DownloadQueue = new ConcurrentQueue<PlaylistSong>();
            Config = config;
        }

        public async Task<List<string>> RunDownloaderAsync()
        {
            DownloadBatch = new TransformBlock<PlaylistSong, PlaylistSong>(DownloadJob, new ExecutionDataflowBlockOptions()
            {
                BoundedCapacity = DownloadQueue.Count + 100,
                MaxDegreeOfParallelism = Config.MaxConcurrentDownloads,
                EnsureOrdered = false
            });
            //Logger.log?.Info($"Starting downloader.");
            var downloadedSongs = new List<string>();
            while (DownloadQueue.TryDequeue(out var song))
            {
                if (DownloadBatch.TryReceiveAll(out var songsCompleted))
                {
                    downloadedSongs.AddRange(songsCompleted.Select(s => s.Hash));
                }
                await DownloadBatch.SendAsync(song).ConfigureAwait(false);
            }
            DownloadBatch.Complete();
            await DownloadBatch.Completion().ConfigureAwait(false);

            if (DownloadBatch.TryReceiveAll(out var songs))
            {
                downloadedSongs.AddRange(songs.Select(s => s.Hash));
            }
            return downloadedSongs;
        }

        public async Task<PlaylistSong> DownloadJob(PlaylistSong song)
        {

            bool directoryCreated = false;
            string tempFile = null;
            bool overwrite = true;
            string extractDirectory = null;
            try
            {
                var songDirPath = Path.GetFullPath(Path.Combine(CustomLevelPathHelper.customLevelsDirectoryPath, song.DirectoryName));
                directoryCreated = !Directory.Exists(songDirPath);
                // Won't remove if it fails, why bother with the HashDictionary TryAdd check if we're overwriting, incrementing folder name
                if (HashSource.HashDictionary.TryAdd(songDirPath, new SongHashData(0, song.Hash)))
                {
                    if(BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var downloadUri = new Uri(BeatSaverDownloadUrlBase + song.Hash.ToLower());
                    var downloadTarget = Path.Combine(SongTempPath, song.Key);
                    tempFile = await FileIO.DownloadFileAsync(downloadUri, downloadTarget, true).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(tempFile))
                    {
                        if (BeatSync.Paused)
                            await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                        extractDirectory = await FileIO.ExtractZipAsync(tempFile, songDirPath, true, overwrite);
                        extractDirectory = Path.GetFullPath(extractDirectory);
                        if (!overwrite && !songDirPath.Equals(extractDirectory))
                        {
                            Logger.log?.Debug($"songDirPath {songDirPath} != {extractDirectory}, updating dictionary.");
                            directoryCreated = true;
                            HashSource.ExistingSongs[song.Hash] = extractDirectory;
                        }
                    }

                }

            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (File.Exists(tempFile))
                    await FileIO.TryDeleteAsync(tempFile).ConfigureAwait(false);
            }


            return song;
        }

        public async Task RunReaders()
        {
            List<Task<Dictionary<string, ScrapedSong>>> readerTasks = new List<Task<Dictionary<string, ScrapedSong>>>();
            var config = Config;
            var beatSyncPlaylist = PlaylistManager.GetPlaylist(BuiltInPlaylist.BeatSyncAll);
            if (config.BeastSaber.Enabled)
            {
                readerTasks.Add(ReadBeastSaber());
            }
            if (config.BeatSaver.Enabled)
            {
                readerTasks.Add(ReadBeatSaver());
            }
            if (config.ScoreSaber.Enabled)
            {
                readerTasks.Add(ReadScoreSaber());
            }
            Dictionary<string, ScrapedSong>[] results = null;
            try
            {
                results = await Task.WhenAll(readerTasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.log?.Error($"Error in reading feeds.\n{ex.Message}\n{ex.StackTrace}");
            }

            Logger.log?.Info($"Finished reading feeds.");
            var songsToDownload = new Dictionary<string, ScrapedSong>();
            foreach (var readTask in readerTasks)
            {
                if (!readTask.DidCompleteSuccessfully())
                {
                    Logger.log?.Warn("Task not successful, skipping.");
                    continue;
                }
                Logger.log?.Debug($"Queuing songs from task.");
                songsToDownload.Merge(await readTask);
            }
            Logger.log?.Info($"Found {songsToDownload.Count} unique songs.");
            var allPlaylist = PlaylistManager.GetPlaylist(BuiltInPlaylist.BeatSyncAll);
            var recentPlaylist = config.RecentPlaylistDays > 0 ? PlaylistManager.GetPlaylist(BuiltInPlaylist.BeatSyncRecent) : null;
            foreach (var scrapedSong in songsToDownload.Values)
            {
                var playlistSong = new PlaylistSong(scrapedSong.Hash, scrapedSong.SongName, scrapedSong.SongKey, scrapedSong.MapperName);
                if (HashSource.ExistingSongs.TryAdd(scrapedSong.Hash, ""))
                {
                    Logger.log?.Info($"Queuing {scrapedSong.SongKey} - {scrapedSong.SongKey} by {scrapedSong.MapperName} for download.");
                    DownloadQueue.Enqueue(playlistSong);
                }

                allPlaylist?.TryAdd(playlistSong);
                recentPlaylist?.TryAdd(playlistSong);
            }
            allPlaylist?.TryWriteFile();
            if (recentPlaylist != null && config.RecentPlaylistDays > 0)
            {
                var minDate = DateTime.Now - new TimeSpan(config.RecentPlaylistDays, 0, 0, 0);
                int removedCount = recentPlaylist.Songs.RemoveAll(s => s.DateAdded < minDate);
                if(removedCount > 0)
                    Logger.log?.Info($"Removed {removedCount} old songs from the RecentPlaylist.");
                recentPlaylist.TryWriteFile();
            }


        }

        public async Task<Dictionary<string, ScrapedSong>> ReadFeed(IFeedReader reader, IFeedSettings settings, Playlist feedPlaylist = null)
        {
            var feedName = reader.GetFeedName(settings);
            Logger.log?.Info($"Getting songs from {feedName} feed.");
            var songs = await reader.GetSongsFromFeedAsync(settings).ConfigureAwait(false) ?? new Dictionary<string, ScrapedSong>();
            foreach (var scrapedSong in songs.Reverse()) // Reverse so the last songs have the oldest DateTime
            {
                if (string.IsNullOrEmpty(scrapedSong.Value.SongKey))
                {
                    try
                    {
                        // ScrapedSong doesn't have a Beat Saver key associated with it, probably scraped from ScoreSaber
                        scrapedSong.Value.UpdateFrom(await BeatSaverReader.GetSongByHashAsync(scrapedSong.Key), false);
                    }
                    catch (ArgumentNullException)
                    {
                        Logger.log?.Warn($"Unable to find {scrapedSong.Value?.SongName} by {scrapedSong.Value?.MapperName} on Beat Saver ({scrapedSong.Key})");
                    }
                }
                var song = new PlaylistSong(scrapedSong.Value.Hash, scrapedSong.Value.SongName, scrapedSong.Value.SongKey, scrapedSong.Value.MapperName);

                feedPlaylist?.TryAdd(song);
            }
            feedPlaylist?.TryWriteFile();
            var pages = songs.Values.Select(s => s.SourceUri.ToString()).Distinct().Count();
            Logger.log?.Info($"Found {songs.Count} songs from {pages} {(pages == 1 ? "page" : "pages")} in the {feedName} feed.");
            return songs;
        }

        #region Feed Read Functions
        public async Task<Dictionary<string, ScrapedSong>> ReadBeastSaber()
        {
            if (BeatSync.Paused)
                await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Logger.log?.Info("Starting BeastSaber reading");

            var config = Config.BeastSaber;
            BeastSaberReader reader = null;
            try
            {
                reader = new BeastSaberReader(config.Username, config.MaxConcurrentPageChecks);
            }
            catch (Exception ex)
            {
                Logger.log?.Error(ex);
                return null;
            }
            var readerSongs = new Dictionary<string, ScrapedSong>();
            if (config.Bookmarks.Enabled)
            {
                try
                {
                    var feedSettings = config.Bookmarks.ToFeedSettings();
                    var feedPlaylist = config.Bookmarks.CreatePlaylist
                        ? PlaylistManager.GetPlaylist(config.Bookmarks.FeedPlaylist)
                        : null;
                    if (BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var songs = await ReadFeed(reader, feedSettings, feedPlaylist).ConfigureAwait(false);
                    readerSongs.Merge(songs);
                }
                catch (ArgumentException ex)
                {
                    Logger.log?.Critical("Exception in BeastSaber Bookmarks: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error("Exception in ReadBeastSaber, Bookmarks.");
                    Logger.log?.Error(ex);
                }


            }
            if (config.Follows.Enabled)
            {
                try
                {
                    var feedSettings = config.Follows.ToFeedSettings();
                    var feedPlaylist = config.Follows.CreatePlaylist
                        ? PlaylistManager.GetPlaylist(config.Follows.FeedPlaylist)
                        : null;
                    if (BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var songs = await ReadFeed(reader, feedSettings, feedPlaylist).ConfigureAwait(false);
                    readerSongs.Merge(songs);
                }
                catch (ArgumentException ex)
                {
                    Logger.log?.Critical("Exception in BeastSaber Follows: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error("Exception in ReadBeastSaber, Follows.");
                    Logger.log?.Error(ex);
                }
            }
            if (config.CuratorRecommended.Enabled)
            {
                try
                {
                    var feedSettings = config.CuratorRecommended.ToFeedSettings();
                    var feedPlaylist = config.CuratorRecommended.CreatePlaylist
                        ? PlaylistManager.GetPlaylist(config.CuratorRecommended.FeedPlaylist)
                        : null;
                    if (BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var songs = await ReadFeed(reader, feedSettings, feedPlaylist).ConfigureAwait(false);
                    readerSongs.Merge(songs);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error("Exception in ReadBeastSaber, Curator Recommended.");
                    Logger.log?.Error(ex);
                }
            }
            if (BeatSync.Paused)
                await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
            sw.Stop();
            var totalPages = readerSongs.Values.Select(s => s.SourceUri.ToString()).Distinct().Count();
            Logger.log?.Info($"Found {readerSongs.Count} songs on {totalPages} {(totalPages == 1 ? "page" : "pages")} from {reader.Name} in {sw.Elapsed.ToString()}");
            return readerSongs;
        }

        public async Task<Dictionary<string, ScrapedSong>> ReadBeatSaver(Playlist allPlaylist = null)
        {
            if (BeatSync.Paused)
                await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Logger.log?.Info("Starting BeatSaver reading");

            var config = Config.BeatSaver;
            BeatSaverReader reader = null;
            try
            {
                reader = new BeatSaverReader();
            }
            catch (Exception ex)
            {
                Logger.log?.Error("Exception creating BeatSaverReader in ReadBeatSaver.");
                Logger.log?.Error(ex);
                return null;
            }
            var readerSongs = new Dictionary<string, ScrapedSong>();

            if (config.FavoriteMappers.Enabled)
            {
                try
                {
                    var feedSettings = config.FavoriteMappers.ToFeedSettings();
                    var feedPlaylist = config.FavoriteMappers.CreatePlaylist
                        ? PlaylistManager.GetPlaylist(config.FavoriteMappers.FeedPlaylist)
                        : null;
                    if (BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var songs = await ReadFeed(reader, feedSettings, feedPlaylist).ConfigureAwait(false);
                    readerSongs.Merge(songs);
                }
                catch (InvalidCastException ex)
                {
                    Logger.log?.Error($"This should never happen in ReadBeatSaver.\n{ex.Message}");
                }
                catch (ArgumentNullException ex)
                {
                    Logger.log?.Critical("Exception in ReadBeatSaver, FavoriteMappers: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error("Exception in ReadBeatSaver, FavoriteMappers.");
                    Logger.log?.Error(ex);
                }


            }
            if (config.Hot.Enabled)
            {
                try
                {
                    var feedSettings = config.Hot.ToFeedSettings();
                    var feedPlaylist = config.Hot.CreatePlaylist
                        ? PlaylistManager.GetPlaylist(config.Hot.FeedPlaylist)
                        : null;
                    if (BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var songs = await ReadFeed(reader, feedSettings, feedPlaylist).ConfigureAwait(false);
                    readerSongs.Merge(songs);
                }
                catch (InvalidCastException ex)
                {
                    Logger.log?.Error($"This should never happen in ReadBeatSaver.\n{ex.Message}");
                }
                catch (ArgumentNullException ex)
                {
                    Logger.log?.Critical("Exception in ReadBeatSaver, Hot: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error("Exception in ReadBeatSaver, Hot.");
                    Logger.log?.Error(ex);
                }
            }
            if (config.Downloads.Enabled)
            {
                try
                {
                    var feedSettings = config.Downloads.ToFeedSettings();
                    var feedPlaylist = config.Downloads.CreatePlaylist
                        ? PlaylistManager.GetPlaylist(config.Downloads.FeedPlaylist)
                        : null;
                    if (BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var songs = await ReadFeed(reader, feedSettings, feedPlaylist).ConfigureAwait(false);
                    readerSongs.Merge(songs);
                }
                catch (InvalidCastException ex)
                {
                    Logger.log?.Error($"This should never happen in ReadBeatSaver.\n{ex.Message}");
                }
                catch (ArgumentNullException ex)
                {
                    Logger.log?.Critical("Exception in ReadBeatSaver, Downloads: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error("Exception in ReadBeatSaver, Downloads.");
                    Logger.log?.Error(ex);
                }
            }
            sw.Stop();
            if (BeatSync.Paused)
                await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
            var totalPages = readerSongs.Values.Select(s => s.SourceUri.ToString()).Distinct().Count();
            Logger.log?.Info($"Found {readerSongs.Count} songs on {totalPages} {(totalPages == 1 ? "page" : "pages")} from {reader.Name} in {sw.Elapsed.ToString()}");
            return readerSongs;
        }

        public async Task<Dictionary<string, ScrapedSong>> ReadScoreSaber(Playlist allPlaylist = null)
        {
            if (BeatSync.Paused)
                await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Logger.log?.Info("Starting ScoreSaber reading");

            var config = Config.ScoreSaber;
            ScoreSaberReader reader = null;
            try
            {
                reader = new ScoreSaberReader();
            }
            catch (Exception ex)
            {
                Logger.log?.Error(ex);
                return null;
            }
            var readerSongs = new Dictionary<string, ScrapedSong>();

            if (config.TopRanked.Enabled)
            {
                try
                {
                    var feedSettings = config.TopRanked.ToFeedSettings();
                    var feedPlaylist = config.TopRanked.CreatePlaylist
                        ? PlaylistManager.GetPlaylist(config.TopRanked.FeedPlaylist)
                        : null;
                    if (BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var songs = await ReadFeed(reader, feedSettings, feedPlaylist).ConfigureAwait(false);
                    readerSongs.Merge(songs);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error("Exception in ReadScoreSaber, Top Ranked.");
                    Logger.log?.Error(ex);
                }
            }

            if (config.Trending.Enabled)
            {
                try
                {
                    var feedSettings = config.Trending.ToFeedSettings();
                    var feedPlaylist = config.Trending.CreatePlaylist
                        ? PlaylistManager.GetPlaylist(config.Trending.FeedPlaylist)
                        : null;
                    if (BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var songs = await ReadFeed(reader, feedSettings, feedPlaylist).ConfigureAwait(false);
                    readerSongs.Merge(songs);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error("Exception in ReadScoreSaber, Trending.");
                    Logger.log?.Error(ex);
                }
            }

            if (config.TopPlayed.Enabled)
            {
                try
                {
                    var feedSettings = config.TopPlayed.ToFeedSettings();
                    var feedPlaylist = config.TopPlayed.CreatePlaylist
                        ? PlaylistManager.GetPlaylist(config.TopPlayed.FeedPlaylist)
                        : null;
                    if (BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var songs = await ReadFeed(reader, feedSettings, feedPlaylist).ConfigureAwait(false);
                    readerSongs.Merge(songs);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error("Exception in ReadScoreSaber, Top Played.");
                    Logger.log?.Error(ex);
                }
            }

            if (config.LatestRanked.Enabled)
            {
                try
                {
                    var feedSettings = config.LatestRanked.ToFeedSettings();
                    var feedPlaylist = config.LatestRanked.CreatePlaylist
                        ? PlaylistManager.GetPlaylist(config.LatestRanked.FeedPlaylist)
                        : null;
                    if (BeatSync.Paused)
                        await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
                    var songs = await ReadFeed(reader, feedSettings, feedPlaylist).ConfigureAwait(false);
                    readerSongs.Merge(songs);
                }
                catch (Exception ex)
                {
                    Logger.log?.Error("Exception in ReadScoreSaber, Latest Ranked.");
                    Logger.log?.Error(ex);
                }
            }

            sw.Stop();
            if (BeatSync.Paused)
                await SongFeedReaders.Utilities.WaitUntil(() => !BeatSync.Paused, 500).ConfigureAwait(false);
            var totalPages = readerSongs.Values.Select(s => s.SourceUri.ToString()).Distinct().Count();
            Logger.log?.Info($"Found {readerSongs.Count} songs on {totalPages} {(totalPages == 1 ? "page" : "pages")} from {reader.Name} in {sw.Elapsed.ToString()}");
            return readerSongs;
        }
        #endregion
    }
}
