using Colossal;
using Colossal.IO.AssetDatabase;
using Colossal.Json;

using Game.Audio.Radio;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using YamlDotNet.Core;
using YamlDotNet.Serialization;

using static Game.Audio.Radio.Radio;

using AudioDB = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<Game.Audio.Radio.Radio.SegmentType, System.Collections.Generic.List<Colossal.IO.AssetDatabase.AudioAsset>>>>>;

#nullable enable

namespace SimCityRadio {
    internal static class Utils {
        public static string ToJsonFromYaml(string yaml) {
            object? parsedYml = new Deserializer().Deserialize(new MergingParser(new Parser(new StringReader(yaml))));
            string json = new SerializerBuilder().DisableAliases().JsonCompatible().Build().Serialize(parsedYml);
            return json;
        }
    }

    public static class PatchUtils {

        public static AudioDB audioDB = [];

        public static List<AudioAsset> GetAudioAssetsFromAudioDataBase(Radio radio, SegmentType segmentType) => audioDB.Get(radio.currentChannel.network)?.Get(radio.currentChannel.name)?.Get(radio.currentChannel.currentProgram.name)?.Get(segmentType) ?? [];

        public static List<AudioAsset> GetAllClips(Radio radio, RuntimeSegment segment) {
            bool isSCRadio = radio.currentChannel is SimCityRuntimeRadioChannel;
            bool canCoalesce = radio.currentChannel is SimCityRuntimeRadioChannel d && d.allowGameClips;
            if (canCoalesce) {
                Mod.log.DebugFormat("{0} {1} merging game and mod clips", radio.currentChannel.name, segment.type.ToString());
            }

            List<AudioAsset> modAssets = GetAudioAssetsFromAudioDataBase(radio, segment.type);
            IEnumerable<AudioAsset> gameAssets = AssetDatabase.global
                .GetAssets(SearchFilter<AudioAsset>
                .ByCondition((AudioAsset asset) => segment.tags.All(asset.ContainsTag)));

            List<AudioAsset> clips = canCoalesce ? ([.. gameAssets, .. modAssets]) : ([.. isSCRadio ? modAssets : gameAssets]);
            // i don't like the randomness of existing radio methods so i'm adding an additional
            // shuffle here. imo, it doesn't matter that this costs more (due to using
            // System.Security.Cryptography) because realistically the shortest interval between
            // calls (creating runtime segments) is either the shortest segment (should be fine) or
            // as fast as a player can spam next on the radio player (who cares, still only a few
            // times a second)
            clips.Shuffle();
            return clips;
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
            RuntimeRadioChannel c = radio.currentChannel;
            RuntimeProgram p = c.currentProgram;
            bool isEmpty = list.Count == 0;
            if (isEmpty) {
                segment.clipsCap = 0;
                Mod.log.DebugFormat("No clips found - skipping {0} {1}", c.name, p.name);
                p.GoToNextSegment();
            }
            return isEmpty;
        }
    }

    internal static class Extensions {
        // nullable chainable dictionary accessor
        public static V? Get<T, V>(this IDictionary<T, V> dict, T key) {
            dict.TryGetValue(key, out V value);
            return value;
        }
        public static IEnumerable<U> Map<T, U>(this IEnumerable<T> collection, Func<T, U> cb) => collection.Select(cb);
        public static IEnumerable<U> Map<T, U>(this IEnumerable<T> collection, Func<T, int, U> cb) => collection.Select(cb);
        public static U[] MapToArray<T, U>(this IEnumerable<T> collection, Func<T, U> cb) => collection.Map(cb).ToArray();
        public static U[] MapToArray<T, U>(this IEnumerable<T> collection, Func<T, int, U> cb) => collection.Map(cb).ToArray();

        public static T Convert<T>(this object arg) => Decoder.Decode(arg.ToJSONString()).Make<T>();

        // 🙏🙏🙏 'grenade' - https://stackoverflow.com/questions/273313/randomize-a-listt
        public static void Shuffle<T>(this IList<T> list) {
            RNGCryptoServiceProvider provider = new();
            int n = list.Count;
            while (n > 1) {
                byte[] box = new byte[1];
                do {
                    provider.GetBytes(box);
                }
                while (!(box[0] < n * (byte.MaxValue / n)));
                int k = box[0] % n;
                n--;
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        public static void Log(RadioNetwork network) {
            Mod.log.Debug("name: " + network.name);
            using (Mod.log.indent.scoped) {
                Mod.log.Verbose("description: " + network.description);
                Mod.log.Verbose("icon: " + network.icon);
                Mod.log.Verbose($"uiPriority: {network.uiPriority}");
                Mod.log.Verbose($"allowAds: {network.allowAds}");
            }
        }

        public static void Log(RuntimeRadioChannel channel) {
            Mod.log.Debug("name: " + channel.name);
            using (Mod.log.indent.scoped) {
                Mod.log.Verbose("description: " + channel.description);
                Mod.log.Verbose("icon: " + channel.icon);
                Mod.log.Verbose($"uiPriority: {channel.uiPriority}");
                Mod.log.Verbose("network: " + channel.network);
                Mod.log.DebugFormat("Programs ({0})", channel.schedule.Length);
                using (Mod.log.indent.scoped) {
                    RuntimeProgram[] schedule = channel.schedule;
                    foreach (RuntimeProgram program in schedule) {
                        Log(program);
                    }
                }
            }
        }

        public static void Log(AudioAsset clip) {
            if (clip == null) {
                Mod.log.Verbose("id: <missing>");
            } else {
                Mod.log.Verbose(string.Format("id: {0} tags: {1} duration: {2}", clip.guid, string.Join(", ", clip.tags), FormatUtils.FormatTimeDebug(clip.durationMs)));
            }
        }

        public static void Log(RuntimeProgram program) {
            Mod.log.Debug("name: " + program.name + " [" + FormatUtils.FormatTimeDebug(program.startTime) + " -> " + FormatUtils.FormatTimeDebug(program.endTime) + "]");
            using (Mod.log.indent.scoped) {
                Mod.log.Verbose("description: " + program.description);
                Mod.log.Verbose($"estimatedStart: {FormatUtils.FormatTimeDebug(program.startTime)} ({program.startTime}s)");
                Mod.log.Verbose($"estimatedEnd: {FormatUtils.FormatTimeDebug(program.endTime)} ({program.endTime}s)");
                Mod.log.Verbose($"loopProgram: {program.loopProgram}");
                Mod.log.Verbose($"estimatedDuration: {FormatUtils.FormatTimeDebug(program.duration)} ({program.duration}s) (realtime at x1: {FormatUtils.FormatTimeDebug((int)(program.duration * 0.0505679026f))})");
                Mod.log.DebugFormat("Segments ({0})", program.segments.Count);
                using (Mod.log.indent.scoped) {
                    foreach (RuntimeSegment segment in program.segments) {
                        Log(segment);
                    }
                }
            }
        }

        public static void Log(RuntimeSegment segment) {
            Mod.log.Debug($"type: {segment.type}");
            using (Mod.log.indent.scoped) {
                if (segment.tags != null) {
                    Mod.log.Debug("tags: " + string.Join(", ", segment.tags));
                }
                if (segment.clips == null) {
                    return;
                }
                Mod.log.Verbose($"duration total: {segment.durationMs}ms ({FormatUtils.FormatTimeDebug(segment.durationMs)})");
                Mod.log.DebugFormat("Clips ({0})", segment.clips.Count);
                using (Mod.log.indent.scoped) {
                    foreach (AudioAsset clip in segment.clips) {
                        Log(clip);
                    }
                }
                Mod.log.DebugFormat("Clips cap: {0}", segment.clipsCap);
            }
        }
    }
}
