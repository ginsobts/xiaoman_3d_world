using UnityEngine;
using VN.World;

namespace VN.Multiplayer
{
    /// <summary>联网乐器：任意玩家 E/Q 演奏，空间中的所有本地和远程玩家都会听见。</summary>
    [RequireComponent(typeof(Collider))]
    public class PlayableInstrument : Interactable
    {
        public int instrumentId = 1;
        public int noteCount = 8;
        public InstrumentAudio.Timbre timbre = InstrumentAudio.Timbre.Xylophone;

        private int _nextNote;
        private AudioSource _audio;
        private MultiplayerSession _session;

        public override string Prompt => GameLanguage.IsEnglish
            ? "[E / Q] Play " + (string.IsNullOrEmpty(displayName) ? "instrument" : displayName)
            : "[E / Q] 演奏" + (string.IsNullOrEmpty(displayName) ? "乐器" : displayName);

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance = 2f;
            _audio.maxDistance = 28f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
        }

        private void Start()
        {
            _session = MultiplayerSession.Instance;
            if (_session != null) _session.MessageReceived += OnNetworkMessage;
        }

        public override void Interact(WorldController world)
        {
            var player = world.LocalMultiplayer?.PrimaryPlayer;
            if (player != null) PlayBy(player);
        }

        public void PlayBy(MultiplayerAvatar player)
        {
            if (player == null) return;
            int note = _nextNote++ % Mathf.Max(1, noteCount);
            if (_session != null)
                _session.EmitInstrument(player.playerId, instrumentId, note);
            else
                PlayLocal(note);
        }

        private void OnNetworkMessage(MultiplayerMessage message)
        {
            if (message.type != MultiplayerProtocol.Instrument || message.objectId != instrumentId) return;
            PlayLocal(Mathf.RoundToInt(message.value));
        }

        private void PlayLocal(int note)
        {
            if (_audio == null) return;
            _audio.PlayOneShot(InstrumentAudio.GetNote(note, timbre), 1f);
            FairytaleVFX.Spawn("GoldenSparkle", transform.position + Vector3.up * 1.5f, 1.6f, 0.8f);
        }

        private void OnDestroy()
        {
            if (_session != null) _session.MessageReceived -= OnNetworkMessage;
        }
    }
}
