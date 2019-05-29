using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using opi.v1;
using osu.Game.Beatmaps;
using Pisstaube.Database;
using Pisstaube.Database.Models;

namespace Pisstaube.Tests
{
    public class ElasticBeatmapTests
    {
        [Test]
        public void TestGetElasticBeatmap()
        {
            var sampleSet = new BeatmapSet
            {
                SetId = 1,
                RankedStatus = BeatmapSetOnlineStatus.Ranked,
                ApprovedDate = DateTime.Parse("2007-10-06T17:46:31"),
                LastUpdate = DateTime.Parse("2007-10-06T17:46:31"),
                LastChecked = DateTime.Parse("2019-05-27T07:08:25.900422"),
                Artist = "Kenji Ninuma",
                Title ="DISCO PRINCE",
                Creator = "peppy",
                Source = "",
                Tags = "katamari",
                HasVideo = false,
                Genre = Genre.Game,
                Language = Language.Japanese,
                Favourites = 494,
                ChildrenBeatmaps = new List<ChildrenBeatmap>
                {
                    new ChildrenBeatmap
                    {
                        BeatmapId = 75,
                        ParentSetId = 1,
                        DiffName = "Normal",
                        FileMd5 = "a5b99395a42bd55bc5eb1d2411cbdf8b",
                        Mode = PlayMode.Osu,
                        Bpm = 119.999f,
                        Ar = 6,
                        Od = 6,
                        Cs = 4,
                        Hp = 6,
                        TotalLength = 142,
                        HitLength = 109,
                        Playcount = 360491,
                        Passcount = 45311,
                        MaxCombo = 0,
                        DifficultyRating = 2.4069502353668213
                    }
                }
            };
            sampleSet.ChildrenBeatmaps[0].Parent = sampleSet;
            
            var returnResult = ElasticBeatmap.GetElasticBeatmap(sampleSet);
            var expectedResult = new ElasticBeatmap
            {
                SetId = sampleSet.SetId,
                Artist = sampleSet.Artist,
                Creator = sampleSet.Creator,
                RankedStatus = sampleSet.RankedStatus,
                Mode = sampleSet.ChildrenBeatmaps.Select(cb => cb.Mode).ToList(),
                Tags = sampleSet.Tags.Split(" ").Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
                Title = sampleSet.Title,
                DiffName = sampleSet.ChildrenBeatmaps.Select(cb => cb.DiffName).ToList(),
                ApprovedDate =
                    (ulong) sampleSet.ApprovedDate?.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds
            };
            
            Assert.NotNull(returnResult);
            
            Assert.AreEqual(returnResult.SetId, expectedResult.SetId);
            Assert.AreEqual(returnResult.Artist, expectedResult.Artist);
            Assert.AreEqual(returnResult.Creator, expectedResult.Creator);
            Assert.AreEqual(returnResult.RankedStatus, expectedResult.RankedStatus);
            Assert.AreEqual(returnResult.Title, expectedResult.Title);
            Assert.AreEqual(returnResult.Tags, expectedResult.Tags);
            Assert.AreEqual(returnResult.DiffName, expectedResult.DiffName);
            Assert.AreEqual(returnResult.ApprovedDate, expectedResult.ApprovedDate);
        }
    }
}