﻿using SongFeedReaders.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace BeatSyncPlaylists
{
    public interface IPlaylistSong : ISong, IEquatable<IPlaylistSong>
    {
        DateTime? DateAdded { get; set; }
    }

    public interface IPlaylistSong<T> : IEquatable<T>
    {

    }
}
