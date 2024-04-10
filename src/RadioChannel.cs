using static Game.Audio.Radio.Radio;

namespace SimCityRadio {

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
}
