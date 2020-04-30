﻿using BeatSyncConsole.Configs;
using BeatSyncConsole.Utilities;
using BeatSyncLib.Configs;
using BeatSyncLib.Downloader;
using BeatSyncLib.Downloader.Downloading;
using BeatSyncLib.Downloader.Targets;
using BeatSyncLib.Hashing;
using BeatSyncLib.History;
using BeatSyncLib.Playlists;
using Newtonsoft.Json;
using SongFeedReaders.Data;
using SongFeedReaders.Readers;
using SongFeedReaders.Readers.BeatSaver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebUtilities;

namespace BeatSyncConsole
{
    class Program
    {
        internal static readonly string ConfigPath = "config.json";
        internal static readonly string HistoryPath = "history.json";
        internal static readonly string ConfigBackupPath = "config.json.bak";

        internal static List<HistoryManager> HistoryManagers = new List<HistoryManager>();
        internal static List<PlaylistManager> PlaylistManagers = new List<PlaylistManager>();
        internal static JobManager manager = new JobManager(1);
        internal static IJobBuilder JobBuilder;
        internal static Config Config;
        public static IJobBuilder CreateJobBuilder()
        {
            string tempDirectory = "Temp";
            Directory.CreateDirectory(tempDirectory);
            IDownloadJobFactory downloadJobFactory = new DownloadJobFactory(song =>
            {
                // return new DownloadMemoryContainer();
                return new FileDownloadContainer(Path.Combine(tempDirectory, (song.Key ?? song.Hash) + ".zip"));
            });
            IJobBuilder jobBuilder = new JobBuilder().SetDownloadJobFactory(downloadJobFactory);
            foreach (SongLocation location in Config.CustomSongsPaths.Where(l => l.Enabled).ToArray())
            {
                bool overwriteTarget = false;
                HistoryManager? historyManager = null;
                SongHasher? songHasher = null;
                PlaylistManager? playlistManager = null;
                if (!string.IsNullOrEmpty(location.HistoryPath))
                {
                    string historyPath = location.HistoryPath;
                    if (!Path.IsPathFullyQualified(historyPath))
                        historyPath = Path.Combine(location.BasePath, historyPath);
                    string historyDirectory = Path.GetDirectoryName(historyPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(historyPath));
                    historyManager = new HistoryManager(historyPath);
                    historyManager.Initialize();
                }
                if (!string.IsNullOrEmpty(location.PlaylistDirectory))
                {
                    string playlistDirectory = location.PlaylistDirectory;
                    if (!Path.IsPathFullyQualified(playlistDirectory))
                        playlistDirectory = Path.Combine(location.BasePath, playlistDirectory);
                    Directory.CreateDirectory(playlistDirectory);
                    playlistManager = new PlaylistManager(location.PlaylistDirectory);
                }
                string songsDirectory = location.SongsDirectory;
                if (!Path.IsPathFullyQualified(songsDirectory))
                    songsDirectory = Path.Combine(location.BasePath, songsDirectory);
                songHasher = new SongHasher<SongHashData>(songsDirectory);
                Directory.CreateDirectory(songsDirectory);
                SongTarget songTarget = new DirectoryTarget(songsDirectory, overwriteTarget, songHasher, historyManager, playlistManager);
                jobBuilder.AddTarget(songTarget);
            }
            JobFinishedAsyncCallback jobFinishedCallback = new JobFinishedAsyncCallback(async (JobResult c) =>
            {
                if (c.Successful)
                {
                    Console.WriteLine($"Job completed successfully: {c.Song}");
                }
                else
                {
                    HistoryEntry entry = c.CreateHistoryEntry();
                    foreach (HistoryManager histManager in HistoryManagers)
                    {
                        // Add entry to history, this should only succeed for jobs that didn't get to the targets.
                        histManager.TryAdd(c.Song.Hash, entry);
                    }
                    Console.WriteLine($"Job failed: {c.Song}");
                }
            });
            jobBuilder.SetDefaultJobFinishedAsyncCallback(jobFinishedCallback);
            return jobBuilder;

        }

        public static async Task DownloadSongsAsync(IEnumerable<ISong> songs)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            manager.Start(cts.Token);
            HashSet<IJob> runningJobs = new HashSet<IJob>();
            int completedJobs = 0;
            foreach (ISong songToAdd in songs)
            {
                IJob job = JobBuilder.CreateJob(songToAdd);
                job.JobProgressChanged += (s, p) =>
                {
                    IJob j = (IJob)s;
                    runningJobs.Add(j);

                    //if (stageUpdates > 4)
                    //    cts.Cancel();
                    if (p.JobProgressType == JobProgressType.Finished)
                    {
                        int finished = ++completedJobs;
                        Console.WriteLine($"({finished} finished) Completed {j}: {p}");
                    }
                    else
                        Console.WriteLine($"({runningJobs.Count} jobs seen) Progress on {j}: {p}");
                };
                if (!manager.TryPostJob(job, out IJob j))
                {
                    Console.WriteLine($"Couldn't post duplicate: {j}");
                }
            }

            try
            {
                await manager.CompleteAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }


        static async Task<bool> InitializeConfigAsync()
        {
            bool validConfig = true;
            try
            {
                if (File.Exists(ConfigPath))
                {
                    Config = JsonConvert.DeserializeObject<Config>(await File.ReadAllTextAsync(ConfigPath).ConfigureAwait(false));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Invalid config.json file, using defaults: {ex.Message}");
            }
            if (Config == null)
            {
                Config = new Config();
            }
            Config.FillDefaults();
            if (!Config.CustomSongsPaths.Any(p => p.Enabled))
            {
                if (Config.CustomSongsPaths.Count == 0)
                {
                    Console.WriteLine("No song paths found in BeatSync.json, should I search for game installs? (Y/N): ");
                    string response = Console.ReadLine();
                    if (response == "Y" || response == "y")
                    {
                        Utilities.BeatSaberInstall[] gameInstalls = BeatSaberTools.GetBeatSaberPathsFromRegistry();
                        if (gameInstalls.Length > 0)
                        {
                            if (gameInstalls.Length == 1)
                            {
                                Console.WriteLine($"Found 1 game install, enabling for BeatSyncConsole: {gameInstalls[0]}");
                                SongLocation newLocation = gameInstalls[0].ToSongLocation();
                                newLocation.Enabled = false;
                                Config.CustomSongsPaths.Add(newLocation);
                                Config.SetConfigChanged(true, nameof(Config.CustomSongsPaths));
                            }
                            else
                            {
                                Console.WriteLine($"Found {gameInstalls.Length} game installs:");
                                for (int i = 0; i < gameInstalls.Length; i++)
                                {
                                    Console.WriteLine($"  {i}: {gameInstalls[i]}");
                                    SongLocation newLocation = gameInstalls[i].ToSongLocation();
                                    newLocation.Enabled = false;
                                    Config.CustomSongsPaths.Add(newLocation);
                                }
                                Config.SetConfigChanged(true, nameof(Config.CustomSongsPaths));

                            }
                        }
                    }
                }
                if (Config.CustomSongsPaths.Count > 0
                    && Config.CustomSongsPaths.Where(p => !p.Enabled).Count() == Config.CustomSongsPaths.Count)
                {
                    Console.WriteLine("No locations currently enabled.");
                    for (int i = 0; i < Config.CustomSongsPaths.Count; i++)
                    {
                        Console.WriteLine($"  {i}: {Config.CustomSongsPaths[i]}");
                    }
                    Console.WriteLine($"Enter the numbers of the installs you wish to enable, separated by commas.");
                    string response = Console.ReadLine();
                    string[] selectionResponse = response.Split(',');
                    int[] selectionInts = selectionResponse.Select(r =>
                    {
                        if (int.TryParse(r.Trim(), out int parsed))
                        {
                            return parsed;
                        }
                        return -1;
                    }).ToArray();
                    for (int i = 0; i < selectionInts.Length; i++)
                    {
                        int current = selectionInts[i];
                        if (current > -1 && current < Config.CustomSongsPaths.Count)
                        {
                            Config.CustomSongsPaths[current].Enabled = true;
                            Console.WriteLine($"Enabling {Config.CustomSongsPaths[current]}.");
                            Config.SetConfigChanged(true, nameof(Config.CustomSongsPaths));
                        }
                        else
                            Console.WriteLine($"'{selectionResponse[i]}' is invalid.");
                    }
                }
            }
            if (Config.CustomSongsPaths.Any(p => p.Enabled))
            {
                Console.WriteLine("Using the following targets:");
                foreach (SongLocation enabledLocation in Config.CustomSongsPaths.Where(p => p.Enabled))
                {
                    Console.WriteLine($"  {enabledLocation}");
                }
            }
            else
            {
                Console.WriteLine("No enabled custom songs paths found, please manually enter a target directory for your songs in BeatSync.json.");
                validConfig = false;
            }

            if (Config.ConfigChanged)
            {
                try
                {
                    if (File.Exists(ConfigPath))
                        File.Copy(ConfigPath, ConfigBackupPath);
                    await File.WriteAllTextAsync(ConfigPath, JsonConvert.SerializeObject(Config, Formatting.Indented)).ConfigureAwait(false);
                    if (File.Exists(ConfigBackupPath))
                        File.Delete(ConfigBackupPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error updating config file.");
                    Console.WriteLine(ex);
                }
            }
            return validConfig;
        }

        static async Task GetBeatSaverAsync()
        {
            BeatSyncLib.Configs.BeatSaverConfig config = Config.BeatSyncConfig.BeatSaver;
            BeatSaverReader reader = new SongFeedReaders.Readers.BeatSaver.BeatSaverReader(config.MaxConcurrentPageChecks);
            List<BuiltInPlaylist> playlists = new List<BuiltInPlaylist>();
            if (config.FavoriteMappers.Enabled)
            {
                BeatSaverFeedSettings favoriteMappersSettings = (BeatSaverFeedSettings)config.FavoriteMappers.ToFeedSettings();
                //ProcessResults(await reader.GetSongsFromFeedAsync(config.FavoriteMappers.ToFeedSettings()).ConfigureAwait(false));
            }
            if (config.Hot.Enabled)
            {
                playlists.Clear();
                if (Config.BeatSyncConfig.AllBeatSyncSongsPlaylist)
                    playlists.Add(BuiltInPlaylist.BeatSyncAll);
                if (config.Hot.CreatePlaylist)
                    playlists.Add(BuiltInPlaylist.BeatSaverHot);
                FeedResult results = await reader.GetSongsFromFeedAsync(config.Hot.ToFeedSettings()).ConfigureAwait(false);
                ProcessResults(results, playlists);
            }
            if (config.Downloads.Enabled)
            {
                playlists.Clear();
                if (Config.BeatSyncConfig.AllBeatSyncSongsPlaylist)
                    playlists.Add(BuiltInPlaylist.BeatSyncAll);
                if (config.Downloads.CreatePlaylist)
                    playlists.Add(BuiltInPlaylist.BeatSaverDownloads);
                ProcessResults(await reader.GetSongsFromFeedAsync(config.Downloads.ToFeedSettings()).ConfigureAwait(false), playlists);
            }
            if (config.FavoriteMappers.Enabled)
            {
                playlists.Clear();
                if (Config.BeatSyncConfig.AllBeatSyncSongsPlaylist)
                    playlists.Add(BuiltInPlaylist.BeatSyncAll);
                if (config.FavoriteMappers.CreatePlaylist && !config.FavoriteMappers.SeparateMapperPlaylists)
                    playlists.Add(BuiltInPlaylist.BeatSaverFavoriteMappers);
                //foreach (var author in FavoriteMappers)
                //{

                //}
            }
        }

        public static void AddSongToPlaylists(PlaylistManager playlistManager, IEnumerable<BuiltInPlaylist> playlists, ISong song)
        {
            foreach (BuiltInPlaylist playlist in playlists)
            {
                playlistManager.GetPlaylist(playlist).TryAdd(song.Hash, song.Name, song.Key, song.LevelAuthorName);
            }
        }

        public static void ProcessResults(FeedResult feedResult, IEnumerable<BuiltInPlaylist> playlists)
        {
            if (!feedResult.Successful)
                return;
            if (feedResult.Songs.Count == 0)
            {
                Console.WriteLine("No songs");
                return;
            }
            if (playlists == null)
                playlists = Array.Empty<BuiltInPlaylist>();
            void JobFinishedCallback(object sender, JobResult jobResult)
            {
                if (jobResult.Successful)
                {
                    ISong song = jobResult.Song;
                    Console.WriteLine($"Downloaded {song} successfully.");
                    foreach (BuiltInPlaylist playlist in playlists)
                    {
                        foreach (PlaylistManager playlistManager in PlaylistManagers)
                        {
                            playlistManager.GetPlaylist(playlist).TryAdd(song);
                        }
                    }
                }
                else
                    Console.WriteLine($"Failed to download {jobResult.Song}.");
            }
            foreach (ScrapedSong song in feedResult.Songs.Values)
            {
                //if (HistoryManager.TryGetValue(song.Hash, out HistoryEntry existing))
                //{
                //    if (!existing.AllowRetry)
                //    {
                //        Console.WriteLine($"Skipping song: {song}");
                //        if (existing.Flag == HistoryFlag.Downloaded)
                //        {
                //            foreach (BuiltInPlaylist playlist in playlists)
                //            {
                //                playlist.TryAdd(song.Hash, song.Name, song.Key, song.LevelAuthorName);
                //            }
                //        }
                //        continue;
                //    }
                //}
                Job newJob = JobBuilder.CreateJob(song);
                manager.TryPostJob(newJob, out IJob postedJob);
                postedJob.JobFinished += JobFinishedCallback; // TODO: Race condition here, might run callback twice.
                if (postedJob.JobState == JobState.Finished)
                {
                    postedJob.JobFinished -= JobFinishedCallback;
                    JobFinishedCallback(postedJob, postedJob.Result);
                }
            }
        }

        static async Task Main(string[] args)
        {
            bool validConfig = await InitializeConfigAsync().ConfigureAwait(false);
            if (!validConfig)
            {
                Console.WriteLine("BeatSyncConsole cannot run without a valid config, exiting.");
                Console.WriteLine("Press any key to continue...");
                Console.Read();
                return;
            }
            SongFeedReaders.WebUtils.Initialize(new WebUtilities.WebWrapper.WebClientWrapper());
            SongFeedReaders.WebUtils.WebClient.SetUserAgent("BeatSyncConsole/0.0.1");
            manager = new JobManager(Config.BeatSyncConfig.MaxConcurrentDownloads);
            manager.Start(CancellationToken.None);
            JobBuilder = CreateJobBuilder();
            foreach (HistoryManager historyManager in HistoryManagers)
            {
                historyManager.Initialize();
            }
            await GetBeatSaverAsync().ConfigureAwait(false);

            await manager.CompleteAsync().ConfigureAwait(false);
            foreach (PlaylistManager playlistManager in PlaylistManagers)
            {
                playlistManager.WriteAllPlaylists();
            }
            foreach (HistoryManager historyManager in HistoryManagers)
            {
                try
                {
                    historyManager.WriteToFile();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to save history at '{historyManager.HistoryPath}': {ex.Message}");
                }
            }
            Console.WriteLine("Press any key to continue...");
            Console.Read();
        }
    }
}
