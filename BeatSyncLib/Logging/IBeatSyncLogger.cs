﻿using System;

namespace BeatSyncLib.Logging
{
    public interface IBeatSyncLogger
    {
        LogLevel LoggingLevel { get; set; }
        void Log(string message, LogLevel logLevel);
        void Log(Exception ex, LogLevel logLevel);
        void Debug(string message);
        void Debug(Exception ex);
        void Info(string message);
        void Info(Exception ex);
        void Warn(string message);
        void Warn(Exception ex);
        void Error(string message);
        void Error(Exception ex);
        void Critical(string message);
        void Critical(Exception ex);
    }

    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Critical = 3,
        Error = 4,
        Disabled = 5
    }
}
