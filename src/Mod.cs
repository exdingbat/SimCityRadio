using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Colossal.IO.AssetDatabase.Internal;
using Colossal.Logging;
using Colossal.UI;

using ExtendedRadio;

using Game;
using Game.Modding;
using Game.SceneFlow;

using HarmonyLib;

using Newtonsoft.Json;

using YamlDotNet.Core;

using static ExtendedRadio.ExtendedRadio;
using static Game.Audio.Radio.Radio;

namespace SimCityRadio {
    using NetworkTuples = List<(RadioNetwork, List<SimCityRadioChannel>)>;

    public class SimCityRuntimeRadioChannel : RuntimeRadioChannel {
        public bool allowGameClips;
    }
    public class SimCityRadioChannel : RadioChannel {
        public bool allowGameClips;
        public new RuntimeRadioChannel CreateRuntime(string path) {
            SimCityRuntimeRadioChannel runtimeRadioChannel = new() {
                name = name,
                description = description,
                icon = icon,
                uiPriority = uiPriority,
                network = network,
                allowGameClips = allowGameClips,
            };
            runtimeRadioChannel.Initialize(this, name + " (" + path + ")");
            return runtimeRadioChannel;
        }
    }

    public class Mod : IMod {
        public static ILog log = LogManager.GetLogger($"{nameof(SimCityRadio)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static Dictionary<string, bool> coalesceByNetwork = [];

        public static List<string> RadioConfigJson = [];
        internal static readonly string s_iconsResourceKey = "simcityradio";
        public static readonly string COUIBaseLocation = $"coui://{s_iconsResourceKey}";
        private static readonly string s_modHarmonyId = $"{nameof(SimCityRadio)}.{nameof(Mod)}";
        private string _pathToCustomRadiosFolder;
        private NetworkTuples _networkTuples;
        private Harmony _harmony;
        private FileInfo _modFileInfo;

        private List<string> ReadRadioConfigJson() {
            IEnumerable<string> jsonFromYaml = Directory.GetFiles(_pathToCustomRadiosFolder, "RadioNetwork*.yml").Map(File.ReadAllText).Map(Utils.ToJsonFromYaml);
            IEnumerable<string> json = Directory.GetFiles(_pathToCustomRadiosFolder, "RadioNetwork*.json").Map(File.ReadAllText);
            return json.Concat(jsonFromYaml).ToList();
        }

        private NetworkTuples InflateCustomRadioNetworks() => RadioConfigJson.Map(cfg => {
            (RadioNetwork network, List<SimCityRadioChannel> channels) = new InflateRadioNetwork(cfg, _pathToCustomRadiosFolder);
            return (network, channels);
        }).ToList();

        public void RadioLoadHandler() {
            try {
                // inflate just the once
                _networkTuples ??= InflateCustomRadioNetworks();

                _networkTuples.ForEach(
                  ((RadioNetwork network, List<SimCityRadioChannel> channels) t) => {
                      // m_Networks = ExtendedRadio.radioTravers.Field("m_Networks").GetValue<Dictionary<string, RadioNetwork>>();
                      // m_RadioChannels = ExtendedRadio.radioTravers.Field("m_RadioChannels").GetValue<Dictionary<string, RuntimeRadioChannel>>()
                      CustomRadios.AddRadioNetworkToTheGame(t.network);
                      Extensions.Log(t.network);
                      log.DebugFormat("Channels ({0})", t.channels.Count);
                      t.channels.ForEach(channel => {
                          CustomRadios.AddRadioChannelToTheGame(channel, _pathToCustomRadiosFolder);
                          using (log.indent.scoped) {
                              Extensions.Log(radio.GetRadioChannel(channel.name));
                          }
                      });
                  }
                );
            } catch (Exception e) {
                log.Error(e);
            }
        }

        public void OnLoad(UpdateSystem updateSystem) {
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out Colossal.IO.AssetDatabase.ExecutableAsset asset)) {
                log.Info($"Current mod asset at {asset.path}");
            }

            _modFileInfo = new FileInfo(asset.path);
            _pathToCustomRadiosFolder = Path.Combine(_modFileInfo.DirectoryName, "CustomRadios");

            // eager load config to find json/yaml errors -- do not load mod if this fails.
            try {
                RadioConfigJson = ReadRadioConfigJson();
            } catch (Exception e) when (e is SemanticErrorException or JsonReaderException) {
                log.Error(e, "$Aborting {nameof(SimCityRadio)} -- methods not patched and mod resources not load. Error parsing RadioNetwork.yml or RadioNetwork.json file.");
                return;
            }
            _harmony = new(s_modHarmonyId);
            _harmony.PatchAll(typeof(Mod).Assembly);
            _harmony.GetPatchedMethods().ForEach(m => log.Info($"Patched method: {m.Module.Name}:{m.Name}"));
            OnRadioLoaded += new onRadioLoaded(RadioLoadHandler);
            UIManager.defaultUISystem.AddHostLocation(s_iconsResourceKey, _modFileInfo.Directory.FullName, false);
        }

        public void OnDispose() {
            _harmony.UnpatchAll(s_modHarmonyId);
            UIManager.defaultUISystem.RemoveHostLocation(s_iconsResourceKey, _modFileInfo.Directory.FullName);
        }
    }
}
