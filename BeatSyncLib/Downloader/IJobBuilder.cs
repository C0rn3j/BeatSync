﻿using BeatSyncLib.Downloader.Targets;
using SongFeedReaders.Data;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BeatSyncLib.Downloader
{
    public interface IJobBuilder
    {
        IJobBuilder SetDownloadJobFactory(IDownloadJobFactory downloadJobFactory);
        IJobBuilder AddTargetFactory(ISongTargetFactory songTargetFactory);
        IJobBuilder SetDefaultJobFinishedCallback(JobFinishedCallback jobFinishedCallback);

        Job CreateJob(ScrapedSong song, IProgress<JobProgress> progress, JobFinishedCallback finishedCallback = null);
    }

    public interface IJob
    {
        event EventHandler JobStarted;
        event EventHandler<JobProgress> JobProgressChanged;
        event EventHandler<JobResult> JobFinished;
    }

    public struct JobProgress
    {
        public JobProgressType JobProgressType;
        public JobStage JobStage;
        public ProgressValue StageProgress;
        public ProgressValue TotalProgress;
        public IDownloadJob CurrentDownloadJob;
        public ISongTarget CurrentTarget;
    }

    public enum JobStage
    {
        NotStarted = 0,
        Downloading = 1,
        TransferringToTarget = 2,
        Finishing = 4,
        Finished = 5
    }

    public enum JobState
    {
        NotReady = 0,
        Ready = 1,
        Running = 2,
        Finished = 3,
        Cancelled = 4,
        Error = 5
    }

    public enum JobProgressType
    {
        None = 0,
        Progress = 1,
        StageCompletion = 2,
        Error = 3,
        Cancellation = 4,
        Paused = 5
    }
    public delegate Task JobFinishedCallback(JobResult jobResult);
}
