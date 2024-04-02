using ATL.Logging;
using Colossal.IO.AssetDatabase;
using Game.Audio.Radio;
using Game.Prefabs;
using Game.Triggers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using static Colossal.IO.AssetDatabase.AudioAsset;

using static Game.Audio.Radio.Radio;

namespace SimCityRadio.Patches {
    using AudioDB = Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<SegmentType, List<AudioAsset>>>>>;

    public static class PatchUtils {

        public static Traverse<AudioDB> audioDBTraverse = Traverse.Create<ExtendedRadio.ExtendedRadio>().Field<AudioDB>("audioDataBase");

        public static List<AudioAsset> GetAudioAssetsFromAudioDataBase(Radio radio, SegmentType segmentType) {
            var audioDB = audioDBTraverse.Value;

            return audioDB.Get(radio.currentChannel.network)?.Get(radio.currentChannel.name)?.Get(radio.currentChannel.currentProgram.name)?.Get(segmentType) ?? [];
        }

        public static List<AudioAsset> CoalesceWithGameClips(Radio radio, RuntimeSegment segment) {
            var gameAssets = AssetDatabase.global.GetAssets(SearchFilter<AudioAsset>.ByCondition((AudioAsset asset) => segment.tags.All(asset.ContainsTag)));
            var modAssets = GetAudioAssetsFromAudioDataBase(radio, segment.type);
            List<AudioAsset> merged = [.. gameAssets, .. modAssets];
            return merged;
        }

        public static AudioAsset[] GetRandomSelection(List<AudioAsset> list, RuntimeSegment segment) {
            Random rnd = new();
            List<int> list2 = (from x in Enumerable.Range(0, list.Count)
                               orderby rnd.Next()
                               select x).Take(segment.clipsCap).ToList();
            AudioAsset[] randomSelection = new AudioAsset[segment.clipsCap];
            for (int i = 0; i < randomSelection.Length; i++) {
                randomSelection[i] = list[list2[i]];
            }
            return randomSelection;
        }

        public static bool HandleEmptySegment(Radio radio, RuntimeSegment segment, List<AudioAsset> list) {
            var c = radio.currentChannel;
            var p = c.currentProgram;
            bool isEmpty = list.Count == 0;
            if (isEmpty) {
                segment.clipsCap = 0;
                Mod.log.DebugFormat("No clips found - {0}:{1}:{2} skipping segment", c.network, c.name, p.name);
                p.GoToNextSegment();
            }
            return isEmpty;
        }
    }

    [HarmonyPatch(typeof(Radio), "GetPlaylistClips")]
    class Radio_GetPlaylistClips {
        static void Postfix(Radio radio, RuntimeSegment segment) {
            bool isCoalescing = Mod.coalesceByProgram.ContainsKey(radio.currentChannel.network);
            if (!isCoalescing) {
                return;
            }
            List<AudioAsset> list = PatchUtils.CoalesceWithGameClips(radio, segment);
            bool isEmpty = PatchUtils.HandleEmptySegment(radio, segment, list);
            if (isEmpty) {
                return;
            }
            segment.clips = PatchUtils.GetRandomSelection(list, segment);
        }
    }

    [HarmonyPatch(typeof(Radio), "GetCommercialClips")]
    class Radio_GetCommercialClips {
        static void Postfix(Radio __instance, RuntimeSegment segment) {
            var radio = __instance;
            bool isCoalescing = Mod.coalesceByProgram.ContainsKey(radio.currentChannel.network);
            if (!isCoalescing) {
                return;
            }
            var m_Networks = Traverse.Create(radio).Field("m_Networks").GetValue<Dictionary<string, RadioNetwork>>();
            bool disallowAds = !m_Networks.TryGetValue(radio.currentChannel.network, out var value) || !value.allowAds;

            if (disallowAds) {
                Mod.log.DebugFormat("Ads not allowed - {0} skipping commercials", radio.currentChannel.name);
                radio.currentChannel.currentProgram.GoToNextSegment();
                return;
            }
            List<AudioAsset> list = PatchUtils.CoalesceWithGameClips(radio, segment);
            bool isEmpty = PatchUtils.HandleEmptySegment(radio, segment, list);
            if (isEmpty) {
                return;
            }
            segment.clips = PatchUtils.GetRandomSelection(list, segment);
        }
    }
    [HarmonyPatch(typeof(Radio), "QueueNextClip")]
    class Radio_QueueNextClip {
        static bool Prefix(Radio __instance) {
            RuntimeProgram p = __instance.currentChannel?.currentProgram;
            try {
                bool test = p?.currentSegment?.currentClip != null;
            } catch (NullReferenceException) {
                Mod.log.ErrorFormat("Skipping QueueNextClip in {1} segment of {0}", p?.name, p?.currentSegment.type);
                p.GoToNextSegment();
                Mod.log.Error("GoToNextSegment called");

                // segment probably doesn't have any clips and accessing the current clip has failed. skip QueueNextClip.
                return false;
            }
            // everything is working normally. continue with QueueNextClip.
            return true;
        }
    }

    [HarmonyPatch(typeof(Radio), "GetEventClips")]
    class Radio_GetEventClips {
        static void Postfix(Radio __instance, ref List<AudioAsset> __result, RuntimeSegment segment, Metatag metatag, bool newestFirst = false, bool flush = false) {
            var radio = __instance;
            // unclear what tags are used by events so any networks using PSA, NEWS, or WEATHER segments should probably allowGameClips = true
            bool isCoalescing = Mod.coalesceByProgram.ContainsKey(radio.currentChannel.network);
            if (!isCoalescing) {
                return;
            }
            // check channel setting to allow merging with game clips
            RadioTagSystem existingSystemManaged = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<RadioTagSystem>();
            PrefabSystem orCreateSystemManaged = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
            List<AudioAsset> clipQueue = new(segment.clipsCap);
            List<AudioAsset> pool = [];
            while (clipQueue.Count < segment.clipsCap && existingSystemManaged.TryPopEvent(segment.type, newestFirst, out RadioTag radioTag)) {
                pool.Clear();

                List<AudioAsset> list = PatchUtils.CoalesceWithGameClips(radio, segment);
                bool isEmpty = PatchUtils.HandleEmptySegment(radio, segment, list);
                if (isEmpty) {
                    return;
                }

                foreach (AudioAsset asset in list) {
                    if (asset.GetMetaTag(metatag) == orCreateSystemManaged.GetPrefab<PrefabBase>(radioTag.m_Event).name) {
                        pool.Add(asset);
                    }
                }

                if (pool.Count > 0) {
                    clipQueue.Add(pool[new Unity.Mathematics.Random((uint)DateTime.Now.Ticks).NextInt(0, pool.Count)]);
                }
            }

            if (flush) {
                existingSystemManaged.FlushEvents(segment.type);
            }
            __result = clipQueue;
        }
    }

}
