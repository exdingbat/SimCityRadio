using ATL;

using Colossal.IO.AssetDatabase;

using ExtendedRadio;

using HarmonyLib;

using System;
using System.Collections.Generic;
using System.IO;

using static Colossal.IO.AssetDatabase.AudioAsset;
using static Game.Audio.Radio.Radio;

#nullable enable
namespace SimCityRadio {
    // same as ExtendedRadio but takes JsonAudioAsset as param instead of reading from file
    public class MyMusicLoader {
        public static AudioAsset? LoadAudioFile(
     string audioFilePath,
     SegmentType segmentType,
     string networkName,
     string radioChannelName,
     string programName,
     JsonAudioAsset? jsAudioAsset = null
   ) {
            jsAudioAsset ??= new JsonAudioAsset();
            AssetDataPath assetDataPath = AssetDataPath.Create(audioFilePath, EscapeStrategy.None);
            AudioAsset audioAsset;

            try {
                IAssetData assetData = AssetDatabase.game.AddAsset(assetDataPath);
                if (assetData is AudioAsset audioAsset1) {
                    audioAsset = audioAsset1;
                } else {
                    return null;
                }

            } catch (Exception e) {
                ExtendedRadioMod.log.Warn(e);
                return null;
            }

            using (Stream writeStream = audioAsset.GetReadStream()) {
                Dictionary<Metatag, string> m_Metatags = [];
                Traverse audioAssetTravers = Traverse.Create(audioAsset);
                Track track = new(audioFilePath, true);
                AddMetaTag(audioAsset, m_Metatags, Metatag.Title, jsAudioAsset.Title ?? track.Title);
                AddMetaTag(audioAsset, m_Metatags, Metatag.Album, jsAudioAsset.Album ?? track.Album);
                AddMetaTag(audioAsset, m_Metatags, Metatag.Artist, jsAudioAsset.Artist ?? track.Artist);
                AddMetaTag(audioAsset, m_Metatags, Metatag.Type, track, "TYPE", jsAudioAsset.Type ?? (segmentType.ToString() == "Playlist" ? "Music" : segmentType.ToString()));
                AddMetaTag(audioAsset, m_Metatags, Metatag.Brand, track, "BRAND", jsAudioAsset.Brand);
                AddMetaTag(audioAsset, m_Metatags, Metatag.RadioStation, track, "RADIO STATION", networkName);
                AddMetaTag(audioAsset, m_Metatags, Metatag.RadioChannel, track, "RADIO CHANNEL", radioChannelName);
                AddMetaTag(audioAsset, m_Metatags, Metatag.PSAType, track, "PSA TYPE", jsAudioAsset.PSAType);
                AddMetaTag(audioAsset, m_Metatags, Metatag.AlertType, track, "ALERT TYPE", jsAudioAsset.AlertType);
                AddMetaTag(audioAsset, m_Metatags, Metatag.NewsType, track, "NEWS TYPE", jsAudioAsset.NewsType);
                AddMetaTag(audioAsset, m_Metatags, Metatag.WeatherType, track, "WEATHER TYPE", jsAudioAsset.WeatherType);
                audioAssetTravers.Field("m_Metatags").SetValue(m_Metatags);
            }
            audioAsset.AddTags(jsAudioAsset.tags);
            audioAsset.AddTags(CustomRadios.FormatTags(segmentType, programName, radioChannelName, networkName));
            return audioAsset;
        }

        internal static void AddMetaTag(AudioAsset audioAsset, Dictionary<Metatag, string> m_Metatags, Metatag tag, string value) {
            audioAsset.AddTag(value);
            m_Metatags[tag] = value;
        }

        internal static void AddMetaTag(AudioAsset audioAsset, Dictionary<Metatag, string> m_Metatags, Metatag tag, Track trackMeta, string oggTag, string? value = null) {
            string? extendedTag = value ?? GetExtendedTag(trackMeta, oggTag);
            if (!string.IsNullOrEmpty(extendedTag) && extendedTag != null) {
                audioAsset.AddTag(oggTag.ToLower() + ":" + extendedTag);
                AddMetaTag(audioAsset, m_Metatags, tag, extendedTag);
            }
        }

        private static string? GetExtendedTag(Track trackMeta, string tag) => trackMeta.AdditionalFields.TryGetValue(tag, out string? value) ? value : null;

    }
}
