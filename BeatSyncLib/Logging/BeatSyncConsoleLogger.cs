﻿using System;

namespace BeatSyncLib.Logging
{
    public class BeatSyncConsoleLogger : BeatSyncLoggerBase
    {
        public override void Log(string message, LogLevel logLevel)
        {
            if (LoggingLevel > logLevel)
                return;
            Console.WriteLine(message);
        }

        public override void Log(Exception ex, LogLevel logLevel)
        {
            if (LoggingLevel > logLevel)
                return;
            Console.WriteLine(ex);
        }
    }
}
