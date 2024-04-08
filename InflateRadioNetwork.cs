
using Colossal.IO.AssetDatabase;

using ExtendedRadio;

using Newtonsoft.Json.Linq;

using System.Collections.Generic;
using System.IO;
using System.Linq;

using static Game.Audio.Radio.Radio;

#nullable enable
namespace SimCityRadio {

    public class InflateRadioNetwork {
        private readonly string _basePath;
        public readonly RadioNetwork network;
        public readonly List<SimCityRadioChannel> channels;

        public void Deconstruct(out RadioNetwork network, out List<SimCityRadioChannel> channels) {
            network = this.network;
            channels = this.channels;
        }

        public InflateRadioNetwork(string json, string basePath) {
            JToken jtoken = JToken.Parse(json);
            _basePath = basePath;
            network = jtoken.ToObject<RadioNetwork>() ?? new RadioNetwork();
            network.descriptionId ??= network.description;
            network.nameId ??= network.name;
            channels = jtoken["channels"]?.MapToArray(ParseChannel).ToList() ?? [];
        }

        private SimCityRadioChannel ParseChannel(JToken jtoken) {
            JToken? programs = jtoken["programs"];
            programs?.Parent?.Remove();
            SimCityRadioChannel channel = jtoken.ToObject<SimCityRadioChannel>() ?? new();
            channel.network = network.name;
            channel.allowGameClips = jtoken.Get("allowGameClips", false);
            channel.programs = programs?.MapToArray((p) => ParseProgram(p, channel.name));
            return channel;
        }

        private Program ParseProgram(JToken jtoken, string? channel) {
            JToken? segments = jtoken["segments"];
            segments?.Parent?.Remove();
            Program program = jtoken.ToObject<Program>() ?? new();
            program.endTime ??= "00:00";
            program.startTime ??= "00:00";
            program.loopProgram = jtoken.Get("loopProgram", true);
            program.segments = segments?.MapToArray((s) => ParseSegment(s, channel));
            return program;
        }

        private Segment ParseSegment(JToken jtoken, string? channelName) {
            JToken? unnormalized = jtoken.Get<JToken?>("clips", null);
            unnormalized?.Parent?.Remove();
            IEnumerable<(string?, JsonAudioAsset?)>? normalized = null;
            if (unnormalized != null && unnormalized.Type != JTokenType.Null) {
                if (unnormalized is JObject) {
                    normalized = (unnormalized?.ToObject<Dictionary<string?, JsonAudioAsset?>>().ToList().Map(p => (p.Key, p.Value)));
                } else {
                    normalized = (unnormalized?.Map(c => c.Type == JTokenType.String ? (c.ToString(), null) : (c.Get("filename"), c.ToObject<JsonAudioAsset>())));
                }
            }

            // TODO add some SegmentType/tags helpers=
            Segment segment = jtoken.ToObject<Segment>() ?? new();
            AudioAsset[] clips = normalized?.MapToArray(
            c => MyMusicLoader.LoadAudioFile(
                Path.Combine(_basePath, c.Item1),
                    segment.type,
                    network.name,
                    channelName,
                    c.Item2
                )
            ) ?? [];
            segment.clips ??= clips;

            return segment;
        }
    }
}
