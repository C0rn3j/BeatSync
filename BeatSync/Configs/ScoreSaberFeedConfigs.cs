﻿using BeatSync.Playlists;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSync.Configs
{
    public class ScoreSaberTrending : FeedConfigBase
    {
        #region Defaults
        protected override bool DefaultEnabled => false;

        protected override int DefaultMaxSongs => 20;

        protected override bool DefaultCreatePlaylist => true;

        protected override PlaylistStyle DefaultPlaylistStyle => PlaylistStyle.Append;

        protected override BuiltInPlaylist DefaultFeedPlaylist => BuiltInPlaylist.ScoreSaberTrending;

        protected bool DefaultRankedOnly => false;
        #endregion
        [JsonIgnore]
        private bool? _rankedOnly;

        public bool RankedOnly
        {
            get
            {
                if (_rankedOnly == null)
                {
                    _rankedOnly = DefaultRankedOnly;
                    SetConfigChanged();
                }
                return _rankedOnly ?? DefaultRankedOnly;
            }
            set
            {
                if (_rankedOnly == value)
                    return;
                _rankedOnly = value;
                SetConfigChanged();
            }
        }

        public override void FillDefaults()
        {
            base.FillDefaults();
            var _ = RankedOnly;
        }

        public override bool ConfigMatches(ConfigBase other)
        {
            if (other is ScoreSaberTrending castOther)
            {
                if (!base.ConfigMatches(castOther))
                    return false;
                if (RankedOnly != castOther.RankedOnly)
                    return false;
            }
            else
                return false;
            return true;
        }

        public override SongFeedReaders.IFeedSettings ToFeedSettings()
        {
            return new SongFeedReaders.ScoreSaberFeedSettings((int)SongFeedReaders.ScoreSaberFeed.Trending)
            {
                MaxSongs = this.MaxSongs,
                RankedOnly = this.RankedOnly
            };
        }
    }

    public class ScoreSaberLatestRanked : FeedConfigBase
    {
        #region Defaults
        protected override bool DefaultEnabled => true;

        protected override int DefaultMaxSongs => 20;

        protected override bool DefaultCreatePlaylist => true;

        protected override PlaylistStyle DefaultPlaylistStyle => PlaylistStyle.Replace;

        protected override BuiltInPlaylist DefaultFeedPlaylist => BuiltInPlaylist.ScoreSaberLatestRanked;
        #endregion

        public override SongFeedReaders.IFeedSettings ToFeedSettings()
        {
            return new SongFeedReaders.ScoreSaberFeedSettings((int)SongFeedReaders.ScoreSaberFeed.LatestRanked)
            {
                MaxSongs = this.MaxSongs
            };
        }
    }

    public class ScoreSaberTopPlayed : FeedConfigBase
    {
        #region Defaults
        protected override bool DefaultEnabled => false;

        protected override int DefaultMaxSongs => 20;

        protected override bool DefaultCreatePlaylist => true;

        protected override PlaylistStyle DefaultPlaylistStyle => PlaylistStyle.Append;

        protected override BuiltInPlaylist DefaultFeedPlaylist => BuiltInPlaylist.ScoreSaberTrending;

        protected bool DefaultRankedOnly => false;
        #endregion
        [JsonIgnore]
        private bool? _rankedOnly;

        public bool RankedOnly
        {
            get
            {
                if (_rankedOnly == null)
                {
                    _rankedOnly = DefaultRankedOnly;
                    SetConfigChanged();
                }
                return _rankedOnly ?? DefaultRankedOnly;
            }
            set
            {
                if (_rankedOnly == value)
                    return;
                _rankedOnly = value;
                SetConfigChanged();
            }
        }

        public override void FillDefaults()
        {
            base.FillDefaults();
            var _ = RankedOnly;
        }

        public override bool ConfigMatches(ConfigBase other)
        {
            if (other is ScoreSaberTopPlayed castOther)
            {
                if (!base.ConfigMatches(castOther))
                    return false;
                if (RankedOnly != castOther.RankedOnly)
                    return false;
            }
            else
                return false;
            return true;
        }

        public override SongFeedReaders.IFeedSettings ToFeedSettings()
        {
            return new SongFeedReaders.ScoreSaberFeedSettings((int)SongFeedReaders.ScoreSaberFeed.TopPlayed)
            {
                MaxSongs = this.MaxSongs,
                RankedOnly = this.RankedOnly
            };
        }
    }

    public class ScoreSaberTopRanked : FeedConfigBase
    {
        #region Defaults
        protected override bool DefaultEnabled => true;

        protected override int DefaultMaxSongs => 20;

        protected override bool DefaultCreatePlaylist => true;

        protected override PlaylistStyle DefaultPlaylistStyle => PlaylistStyle.Replace;

        protected override BuiltInPlaylist DefaultFeedPlaylist => BuiltInPlaylist.ScoreSaberTopRanked;
        #endregion

        public override SongFeedReaders.IFeedSettings ToFeedSettings()
        {
            return new SongFeedReaders.ScoreSaberFeedSettings((int)SongFeedReaders.ScoreSaberFeed.TopRanked)
            {
                MaxSongs = this.MaxSongs
            };
        }
    }
}
