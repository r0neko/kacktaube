using System.Collections.Generic;
using osu.Game.Beatmaps;
using Pisstaube.Database.Models;
using Pisstaube.Utils;

namespace Pisstaube.Engine
{
    public interface IBeatmapSearchEngineProvider
    {
        bool IsConnected { get; }
        
        void Index(IEnumerable<BeatmapSet> sets);

        IEnumerable<BeatmapSet> Search(string query,
            int amount = 100,
            int offset = 0,
            BeatmapSetOnlineStatus? rankedStatus = null,
            PlayMode mode = PlayMode.All,
            MapSearchType search = MapSearchType.Normal);
    }
}