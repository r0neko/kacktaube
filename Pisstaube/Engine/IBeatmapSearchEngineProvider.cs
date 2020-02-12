using System.Collections.Generic;
using osu.Game.Beatmaps;
using Pisstaube.Database.Models;

namespace Pisstaube.Engine
{
    public interface IBeatmapSearchEngineProvider
    {
        void Index(IEnumerable<BeatmapSet> sets);

        IEnumerable<BeatmapSet> Search(string query,
            int amount = 50,
            int offset = 0,
            BeatmapSetOnlineStatus? rankedStatus = null,
            PlayMode mode = PlayMode.All);
    }
}