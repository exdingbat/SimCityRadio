using Colossal;
using Colossal.IO.AssetDatabase;
using Colossal.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using System.IO;
using System.Linq;

using static Game.Audio.Radio.Radio;
#nullable enable
namespace SimCityRadio {
    internal static class Utils {
        public static string ToJsonFromYaml(string yaml) {
            object? parsedYml = new Deserializer().Deserialize(new MergingParser(new Parser(new StringReader(yaml))));
            string json = new SerializerBuilder().DisableAliases().JsonCompatible().Build().Serialize(parsedYml);
            return json;
        }
    }

    internal static class Extensions {
        // nullable JToken accessor
        public static T? Get<T>(this JToken token, string key, T defaultValue) =>
             (token[key] == null || token[key]?.Type == JTokenType.Null)
             ? defaultValue
             : token.Value<T>(key);

        public static string? Get(this JToken token, string key) =>
            (string?)token?[key];

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
