using System;
using System.IO;
using Colossal.Logging;
using Colossal.OdinSerializer.Utilities;
using ExtendedRadio;
using Game;
using Game.Modding;
using Game.SceneFlow;
using static ExtendedRadio.ExtendedRadio;
using static Game.Audio.Radio.Radio;
using HarmonyLib;
using System.Linq;
using SimCityRadio.Patches;
using System.Collections.Generic;

namespace SimCityRadio {
    public class Mod : IMod {
        public static ILog log = LogManager.GetLogger($"{nameof(SimCityRadio)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static Dictionary<string, bool> coalesceByProgram;

        private string pathToCfgsFolder;
        private string pathToCustomRadiosFolder;
        private (RadioNetwork, RadioChannel[])[] radioNetworks;
        private Harmony harmony;

        private (RadioNetwork, RadioChannel[])[] GetCustomRadioNetwork() {
            var networkTuples = Directory
              .GetFiles(pathToCfgsFolder, "RadioNetwork*.json")
              .Map(cfg => {
                  var json = File.ReadAllText(cfg);
                  (var network, var channels) = new InflateRadioNetwork(json, pathToCfgsFolder, ref coalesceByProgram);
                  return (network, channels);
              });
            return networkTuples;
        }

        public void RadioLoadHandler() {
            if (!Directory.Exists(pathToCfgsFolder)) {
                log.Error("could not load pathToCfgsFolder");
                return;
            }

            try {
                radioNetworks ??= GetCustomRadioNetwork();
            } catch (Exception e) {
                log.Error(e);
            }

            radioNetworks.ForEach(
              ((RadioNetwork network, RadioChannel[] channels) t) => {
                  try {
                      bool networkResult = CustomRadios.AddRadioNetworkToTheGame(t.network);
                      Extensions.Log(t.network);
                      t.channels.ForEach(channel => {
                          CustomRadios.AddRadioChannelToTheGame(channel, pathToCfgsFolder);
                          Extensions.Log(radio.GetRadioChannel(channel.name));
                      });

                  } catch (Exception e) {
                      log.Error(e);
                  }
              }
            );
        }

        public void OnLoad(UpdateSystem updateSystem) {
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset)) {
                log.Info($"Current mod asset at {asset.path}");
            }
            string dirName = new FileInfo(asset.path).DirectoryName;
            pathToCustomRadiosFolder = Path.Combine(dirName, "CustomRadios");
            pathToCfgsFolder = Path.Combine(dirName, "test");

            harmony = new($"{nameof(SimCityRadio)}.{nameof(Mod)}");
            harmony.PatchAll(typeof(Mod).Assembly);
            var patchedMethods = harmony.GetPatchedMethods();
            log.InfoFormat("Plugin ExtraDetailingTools made patches! Patched methods: {0}", patchedMethods.Count());
            patchedMethods.ForEach(m => log.Info($"Patched method: {m.Module.Name}:{m.Name}"));
            OnRadioLoaded += new onRadioLoaded(RadioLoadHandler);
            // register icon dir

        }

        public void OnDispose() {
            harmony.UnpatchAll($"{nameof(SimCityRadio)}.{nameof(Mod)}");
            //CustomRadios.UnRegisterCustomRadioDirectory(pathToCustomRadiosFolder);
        }
    }
}
