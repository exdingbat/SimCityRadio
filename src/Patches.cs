
using Colossal.IO.AssetDatabase;

using Game.Audio.Radio;

using HarmonyLib;

using System.Collections.Generic;

using static Game.Audio.Radio.Radio;

#pragma warning disable IDE0051

namespace SimCityRadio.Patches {

    [HarmonyPatch(typeof(RadioChannel), "CreateRuntime")]
    internal class RadioChannel_CreateRuntime {
        private static bool Prefix(RadioChannel __instance, string path, ref RuntimeRadioChannel __result) {
            if (__instance is SimCityRadioChannel modRadioChannel) {
                __result = new SimCityRuntimeRadioChannel() {
                    name = modRadioChannel.name,
                    description = modRadioChannel.description,
                    icon = modRadioChannel.icon,
                    uiPriority = modRadioChannel.uiPriority,
                    network = modRadioChannel.network,
                    allowGameClips = modRadioChannel.allowGameClips,
                };
                __result.Initialize(__instance, __instance.name + " (" + path + ")");
                return false;
            }
            return true;
        }
    }


    [HarmonyPatch(typeof(Radio), "GetCommercialClips")]
    internal class Radio_GetCommercialClips {
        private static void Postfix(Radio __instance, RuntimeSegment segment) {
            if (__instance.currentChannel is not SimCityRuntimeRadioChannel) {
                return; // do not run patched method on normal radio channels
            }

            Dictionary<string, RadioNetwork> m_Networks = Traverse.Create(__instance).Field("m_Networks").GetValue<Dictionary<string, RadioNetwork>>();
            bool disallowAds = !m_Networks.TryGetValue(__instance.currentChannel.network, out RadioNetwork value) || !value.allowAds;

            if (disallowAds) {
                __instance.currentChannel.currentProgram.GoToNextSegment();
                return;
            }
            List<AudioAsset> list = PatchUtils.GetAllClips(__instance, segment);
            bool isEmpty = PatchUtils.HandleEmptySegment(__instance, segment, list);
            if (isEmpty) {
                return;
            }
            segment.clips = PatchUtils.GetRandomSelection(list, segment);
        }
    }

    // [HarmonyPatch(typeof(Radio), "QueueNextClip")]
    // internal class Radio_QueueNextClip {
    //     private static bool Prefix(Radio __instance) {
    //         RuntimeProgram p = __instance.currentChannel?.currentProgram;
    //         try {
    //             bool test = p?.currentSegment?.currentClip != null;
    //         } catch (NullReferenceException) {
    //             Mod.log.DebugFormat("Skipping QueueNextClip in {1} segment of {0}", p?.name, p?.currentSegment.type);
    //             p.GoToNextSegment();
    //             // segment probably doesn't have any clips and accessing the current clip has failed. skip QueueNextClip.
    //             return false;
    //         }
    //         // everything is working normally. continue with QueueNextClip.
    //         return true;
    //     }
    // }
}
