using System.Collections.Generic;
using System.IO;
using Colossal.Json;
using ExtendedRadio;
using Newtonsoft.Json.Linq;
using static Game.Audio.Radio.Radio;

namespace SimCityRadio {

    public class InflateRadioNetwork {

        private readonly string basePath;
        public RadioNetwork network;
        public RadioChannel[] channels;

        public void Deconstruct(out RadioNetwork network, out RadioChannel[] channels) {
            network = this.network;
            channels = this.channels;
        }

        public InflateRadioNetwork(string json, string basePath, ref Dictionary<string, bool> coalesceByProgram) {
            JToken node = JToken.Parse(json);
            this.basePath = basePath;
            network = new RadioNetwork {
                name = (string)node["name"],
                nameId = (string)node["name"],
                description = (string)node["description"],
                descriptionId = (string)node["description"],
                icon = (string)node["icon"],
                allowAds = (bool)node["allowAds"]
            };
            coalesceByProgram[network.name] = (bool)node["allowGameClips"];
            channels = node["channels"].Map(ParseChannel);
        }

        private RadioChannel ParseChannel(JToken node) =>
            new RadioChannel() {
                network = network.name,
                name = (string)node["name"],
                nameId = (string)node["name"],
                description = (string)node["description"],
                icon = (string)node["icon"],
                programs = node["programs"].ToObject<JToken[]>().Map((p) => ParseProgram(p, (string)node["name"]))
            };

        private Program ParseProgram(JToken node, string channel) =>
            new Program {
                name = (string)node["name"],
                description = (string)node["description"],
                icon = (string)node["icon"],
                startTime = "00:00",
                endTime = "00:00",
                loopProgram = true,
                pairIntroOutro = false,
                segments = node["segments"].Map((s) => ParseSegment(s, channel))
            };

        private Segment ParseSegment(JToken node, string channelName) =>
            new Segment {
                type = node["type"]!.ToObject<SegmentType>(),
                tags = node["tags"]?.ToObject<List<string>>()?.ToArray(),
                clipsCap = (int)node["clipsCap"],
                clips = node["clips"]?.ToObject<List<JToken>>()?.Map((c) => {
                    var audioFilePath = Path.Combine(basePath, (string)c["filename"]);
                    var jsonAudioAsset = Decoder.Decode(c.ToString()).Make<JsonAudioAsset>();
                    return MyMusicLoader.LoadAudioFile(audioFilePath, node["type"]!.ToObject<SegmentType>(), network.name, channelName, jsonAudioAsset);
                })
            };
        // if network allowAds = true DO NOT ALLOW PSAs
        // auto add tags here?
        // segment.clips ??= [];
    }
}