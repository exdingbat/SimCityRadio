
using Colossal.IO.AssetDatabase;

using ExtendedRadio;

using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using static Game.Audio.Radio.Radio;

#nullable enable

namespace SimCityRadio {
    using ClipTuple = (string path, JsonAudioAsset data);


    // do all this instead of Decode.Make crap because dynamic types don't seem to work with unity
    // and i decided to support polymorphic config files
    public class InflateRadioNetwork {
        private readonly string _basePath;
        public readonly RadioNetwork network;
        public readonly List<SimCityRadioChannel> channels;

        public void Deconstruct(out RadioNetwork network, out List<SimCityRadioChannel> channels) {
            network = this.network;
            channels = this.channels;
        }

        public InflateRadioNetwork(string json, string basePath) {
            JObject jobject = JObject.Parse(json);
            _basePath = basePath;
            network = jobject.ToObject<RadioNetwork>() ?? new RadioNetwork();
            network.descriptionId ??= network.description;
            network.nameId ??= network.name;
            channels = jobject["channels"]?.MapToArray(ParseChannel).ToList() ?? [];
        }

        private SimCityRadioChannel ParseChannel(JToken jtoken) {
            JToken? programs = jtoken["programs"];
            programs?.Parent?.Remove();
            SimCityRadioChannel channel = jtoken.ToObject<SimCityRadioChannel>() ?? new();
            channel.network = network.name;
            channel.allowGameClips = jtoken.Value<bool?>("allowGameClips") ?? false;
            channel.programs = programs?.MapToArray((p) => ParseProgram(p, channel.name));
            return channel;
        }

        private Program ParseProgram(JToken jtoken, string channel) {
            JToken? segments = jtoken["segments"];
            segments?.Parent?.Remove();
            Program program = jtoken.ToObject<Program>() ?? new();
            program.endTime ??= "00:00";
            program.startTime ??= "00:00";
            program.loopProgram = jtoken.Value<bool?>("loopProgram") ?? true;
            program.segments = segments?.MapToArray((s) => ParseSegment(s, channel));
            return program;
        }

        private string[] MakeTags(SegmentType type, string channel) {
            if (type == SegmentType.PSA) {
                return ["type:Public Service Announcements"];
            } else if (type == SegmentType.Playlist) {
                return ["type:Music", $"radio channel:{channel}"];
            } else {
                return [$"type:{type}"];
            }
        }

        private ClipTuple GetClipTuple(JToken jtoken) =>
            jtoken is JObject jobject
                ? (jobject.Value<string?>("filename") ?? "", jobject.ToObject<JsonAudioAsset>() ?? new())
                : (jtoken.ToString(), new());

        private JArray FlattenJArray(JArray jarray) {
            JArray flattened = [];
            foreach (JToken child in jarray.Children()) {
                if (child is JArray childArray) {
                    flattened.Merge(childArray);
                } else {
                    flattened.Add(child);
                }
            }
            return flattened;
        }

        private IEnumerable<ClipTuple> ParseClips(JToken? jtoken) {
            IEnumerable<ClipTuple> normalizedClips = [];
            if (jtoken is JObject clipsObj) {
                normalizedClips = clipsObj.ToObject<Dictionary<string, JsonAudioAsset>>().ToList().Map(p => (p.Key, p.Value));
            } else if (jtoken is JArray clipsArray) {
                normalizedClips = FlattenJArray(clipsArray).Map(GetClipTuple);
            }
            return normalizedClips;
        }

        private Segment ParseSegment(JToken jtoken, string channel) {
            int clipsCap = jtoken.Value<int>("clipsCap");
            clipsCap = clipsCap == 0 ? 1 : clipsCap;
            string typeString = jtoken.Value<string?>("type") ?? "Playlist";
            SegmentType type = (SegmentType)Enum.Parse(typeof(SegmentType), typeString);
            string[] tags = jtoken["tags"]?.ToObject<string[]?>() ?? [];
            if (tags.Length == 0) {
                tags = MakeTags(type, channel);
            }
            AudioAsset clipToAudio(ClipTuple c) =>
                MyMusicLoader.LoadAudioFile(Path.Combine([_basePath, .. c.path.Split('/')]), type, network.name, channel, c.data);
            AudioAsset[] clips = ParseClips(jtoken["clips"]).MapToArray(clipToAudio);

            return new Segment {
                clips = clips ?? [],
                clipsCap = clipsCap,
                tags = tags,
                type = type
            };
        }
    }
}
