﻿using SongFeedReaders.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BeatSyncPlaylists
{
    /// <summary>
    /// Interface for a playlist.
    /// </summary>
    public interface IPlaylist : IList<IPlaylistSong>
    {
        /// <summary>
        /// Playlist title.
        /// </summary>
        string Title { get; set; }
        /// <summary>
        /// Playlist author.
        /// </summary>
        string? Author { get; set; }
        /// <summary>
        /// Playlist description.
        /// </summary>
        string? Description { get; set; }
        /// <summary>
        /// Filename without extension, does not include directory path.
        /// </summary>
        string Filename { get; set; }
        /// <summary>
        /// Returns a <see cref="Stream"/> for the playlist cover image.
        /// </summary>
        /// <returns></returns>
        Stream? GetCoverStream();
        /// <summary>
        /// Sets the cover image from a byte array.
        /// </summary>
        /// <param name="coverImage"></param>
        void SetCover(byte[] coverImage);
        /// <summary>
        /// Sets the cover image from a base64 string.
        /// </summary>
        /// <param name="coverImageStr"></param>
        void SetCover(string coverImageStr);
        /// <summary>
        /// Sets the cover image from a <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream"></param>
        void SetCover(Stream stream);
        /// <summary>
        /// True if the <see cref="IPlaylist"/> was changed.
        /// </summary>
        bool IsDirty { get; }
        /// <summary>
        /// Marks the playlist as changed.
        /// </summary>
        void MarkDirty(bool dirty = true);
        /// <summary>
        /// Allow duplicate songs in the playlist.
        /// </summary>
        bool AllowDuplicates { get; set; }
        /// <summary>
        /// Adds the <see cref="ISong"/> to the playlist. 
        /// Does nothing if <see cref="AllowDuplicates"/> is false and the song is already in the playlist. 
        /// Converts the <see cref="ISong"/> if needed.
        /// </summary>
        /// <param name="song"></param>
        void Add(ISong song);
        /// <summary>
        /// Creates a new <see cref="IPlaylistSong"/> and adds it to the playlist.
        /// Does nothing if <see cref="AllowDuplicates"/> is false and the song is already in the playlist. 
        /// </summary>
        /// <param name="songHash"></param>
        /// <param name="songName"></param>
        /// <param name="songKey"></param>
        /// <param name="mapper"></param>
        void Add(string songHash, string? songName, string? songKey, string? mapper);
        /// <summary>
        /// Tries to remove all songs with the given hash from the playlist. Returns true if successful.
        /// </summary>
        /// <param name="songHash"></param>
        /// <returns></returns>
        bool TryRemoveByHash(string songHash);
        /// <summary>
        /// Tries to remove all songs with the given key from the playlist. Returns true if successful.
        /// </summary>
        /// <param name="songKey"></param>
        /// <returns></returns>
        bool TryRemoveByKey(string songKey);
        /// <summary>
        /// Removes all duplicate songs from the playlist.
        /// </summary>
        void RemoveDuplicates();
    }

    public interface IPlaylist<T> : IPlaylist
        where T : IPlaylistSong, new()
    {
        int RemoveAll(Func<T, bool> match);
    }
}
