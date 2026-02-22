using UnityEngine;
using UnityEngine.Audio;
using Genesis.Core;
using Genesis.Data;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using Genesis.Simulation;

namespace Genesis.Presentation.Audio {

    public class AudioManager : Singleton<AudioManager> {

        [Header("Mixer")]
        [SerializeField] private AudioMixerGroup _masterGroup;
        [SerializeField] private AudioMixerGroup _musicGroup;
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [SerializeField] private AudioMixerGroup _ambientGroup;
        [SerializeField] private AudioMixer _mixer;

        [Header("Sound Library")]
        [SerializeField] private SoundLibrary _soundLibrary;

        [Header("Pool Settings")]
        [SerializeField] private int _initialPoolSize = 10;
        [SerializeField] private int _maxPoolSize = 20;

        // Dedicated sources
        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private AudioSource _ambientSourceA;
        private AudioSource _ambientSourceB;
        private bool _musicAIsActive = true;
        private bool _ambientAIsActive = true;

        // SFX Pool
        private List<AudioSource> _sfxPool = new List<AudioSource>();

        // Dedicated footstep source (so we can stop it instantly)
        private AudioSource _footstepSource;

        // Loop SFX tracking (keyed by entity ObjectId)
        private Dictionary<int, AudioSource> _loopSources = new Dictionary<int, AudioSource>();

        // Guard against SyncVar double-fire for zone music
        private AudioClip _currentZoneMusic;

        // ═══════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════

        protected override void Awake() {
            base.Awake();
            InitializeSources();
            InitializePool();
        }

        private IEnumerator Start() {
            // Wait until the end of frame to ensure AudioMixer is fully ready
            yield return new WaitForEndOfFrame();
            LoadVolumeSettings();
        }

        private void OnEnable() {
            // Combat events
            EventBus.Subscribe<FloatingTextData>("OnShowFloatingText", OnFloatingText);
            EventBus.Subscribe<string>("OnCombatError", OnCombatError);
            EventBus.Subscribe<string, bool>("OnStatusApplied", OnStatusApplied);
            EventBus.Subscribe("OnLocalPlayerDied", OnPlayerDied);

            // Progression events
            EventBus.Subscribe<string>("OnQuestAccepted", OnQuestAccepted);
            EventBus.Subscribe<NetworkObject, string>("OnQuestCompleted", OnQuestCompleted);
            EventBus.Subscribe<int>("OnLevelChanged", OnLevelUp);

            // Economy events
            EventBus.Subscribe<bool, string>("OnVendorBuyResult", OnVendorBuy);
            EventBus.Subscribe<int>("OnGoldChanged", OnGoldChanged);

            // Zone music
            EventBus.Subscribe<AudioClip, AudioClip>("OnZoneMusicChanged", OnZoneMusicChanged);

            // Direct SFX from Simulation layer (CastSound, ImpactSound, ApplySound)
            EventBus.Subscribe<AudioClip, Vector3>("OnPlaySFX3D", OnPlaySFX3D);

            // Loop SFX (channel abilities)
            EventBus.Subscribe<AudioClip, Vector3, int>("OnStartLoopSFX3D", OnStartLoopSFX);
            EventBus.Subscribe<int>("OnStopLoopSFX3D", OnStopLoopSFX);

            // Footsteps
            EventBus.Subscribe<Vector3>("OnFootstep", OnFootstep);
            EventBus.Subscribe("OnFootstepStop", OnFootstepStop);

            // Interaction sounds
            EventBus.Subscribe<ILootSource>("OnLootOpened", OnChestOpened);
            EventBus.Subscribe<bool>("OnEquipmentSound", OnEquipmentSound);
            EventBus.Subscribe("OnConsumableUsed", OnConsumableUsed);
            EventBus.Subscribe("OnLocalPlayerRespawned", OnPlayerRespawned);
            EventBus.Subscribe<bool>("OnPlayerZoneChanged", OnZoneChanged);

            // Portal sounds
            EventBus.Subscribe<bool>("OnPortalSound", OnPortalSound);

            // Loot drop
            EventBus.Subscribe<Vector3>("OnLootDropped", OnLootDropped);
        }

        private void OnDisable() {
            EventBus.Unsubscribe<FloatingTextData>("OnShowFloatingText", OnFloatingText);
            EventBus.Unsubscribe<string>("OnCombatError", OnCombatError);
            EventBus.Unsubscribe<string, bool>("OnStatusApplied", OnStatusApplied);
            EventBus.Unsubscribe("OnLocalPlayerDied", OnPlayerDied);

            EventBus.Unsubscribe<string>("OnQuestAccepted", OnQuestAccepted);
            EventBus.Unsubscribe<NetworkObject, string>("OnQuestCompleted", OnQuestCompleted);
            EventBus.Unsubscribe<int>("OnLevelChanged", OnLevelUp);

            EventBus.Unsubscribe<bool, string>("OnVendorBuyResult", OnVendorBuy);
            EventBus.Unsubscribe<int>("OnGoldChanged", OnGoldChanged);

            EventBus.Unsubscribe<AudioClip, AudioClip>("OnZoneMusicChanged", OnZoneMusicChanged);
            EventBus.Unsubscribe<AudioClip, Vector3>("OnPlaySFX3D", OnPlaySFX3D);
            EventBus.Unsubscribe<AudioClip, Vector3, int>("OnStartLoopSFX3D", OnStartLoopSFX);
            EventBus.Unsubscribe<int>("OnStopLoopSFX3D", OnStopLoopSFX);
            EventBus.Unsubscribe<Vector3>("OnFootstep", OnFootstep);
            EventBus.Unsubscribe("OnFootstepStop", OnFootstepStop);

            EventBus.Unsubscribe<ILootSource>("OnLootOpened", OnChestOpened);
            EventBus.Unsubscribe<bool>("OnEquipmentSound", OnEquipmentSound);
            EventBus.Unsubscribe("OnConsumableUsed", OnConsumableUsed);
            EventBus.Unsubscribe("OnLocalPlayerRespawned", OnPlayerRespawned);
            EventBus.Unsubscribe<bool>("OnPlayerZoneChanged", OnZoneChanged);
            EventBus.Unsubscribe<bool>("OnPortalSound", OnPortalSound);
            EventBus.Unsubscribe<Vector3>("OnLootDropped", OnLootDropped);
        }

        // ═══════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════

        private void InitializeSources() {
            _musicSourceA = CreateDedicatedSource("MusicA", _musicGroup, true);
            _musicSourceB = CreateDedicatedSource("MusicB", _musicGroup, true);
            _ambientSourceA = CreateDedicatedSource("AmbientA", _ambientGroup, true);
            _ambientSourceB = CreateDedicatedSource("AmbientB", _ambientGroup, true);
            _footstepSource = CreateDedicatedSource("Footsteps", _sfxGroup, false);
        }

        private AudioSource CreateDedicatedSource(string name, AudioMixerGroup group, bool loop) {
            var go = new GameObject($"AudioSource_{name}");
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = group;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void InitializePool() {
            for (int i = 0; i < _initialPoolSize; i++) {
                CreatePooledSource();
            }
        }

        private AudioSource CreatePooledSource() {
            var go = new GameObject($"SFX_Pool_{_sfxPool.Count}");
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = _sfxGroup;
            source.playOnAwake = false;
            _sfxPool.Add(source);
            return source;
        }

        private AudioSource GetPooledSource() {
            foreach (var source in _sfxPool) {
                if (!source.isPlaying) return source;
            }

            if (_sfxPool.Count < _maxPoolSize) {
                return CreatePooledSource();
            }

            // All busy, steal the oldest
            return _sfxPool[0];
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC API — SFX
        // ═══════════════════════════════════════════════════════

        public void PlaySFX(SoundType type) {
            if (_soundLibrary == null) return;
            SoundEntry entry = _soundLibrary.GetEntry(type);
            if (entry == null || entry.Clip == null) return;

            AudioSource source = GetPooledSource();
            source.clip = entry.Clip;
            source.volume = entry.Volume;
            source.pitch = Random.Range(entry.PitchMin, entry.PitchMax);
            source.spatialBlend = 0f;
            source.Play();
        }

        public void PlaySFX(SoundType type, Vector3 position) {
            if (_soundLibrary == null) return;
            SoundEntry entry = _soundLibrary.GetEntry(type);
            if (entry == null || entry.Clip == null) return;

            AudioSource source = GetPooledSource();
            source.transform.position = position;
            source.clip = entry.Clip;
            source.volume = entry.Volume;
            source.pitch = Random.Range(entry.PitchMin, entry.PitchMax);
            source.spatialBlend = entry.Is3D ? 1f : 0f;
            source.Play();
        }

        public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f) {
            if (clip == null) return;

            float baseVol = volume;
            if (_soundLibrary != null) {
                var entry = _soundLibrary.GetEntryByClip(clip);
                if (entry != null) baseVol = entry.Volume * volume;
            }

            AudioSource source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = baseVol;
            source.pitch = 1f;
            source.spatialBlend = 1f;
            source.Play();
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC API — MUSIC
        // ═══════════════════════════════════════════════════════

        public void PlayMusic(AudioClip clip, float fadeTime = 1f, float targetVolume = 1f) {
            if (clip == null) return;

            float baseVol = targetVolume;
            if (_soundLibrary != null) {
                var entry = _soundLibrary.GetEntryByClip(clip);
                if (entry != null) baseVol = entry.Volume * targetVolume;
            }

            AudioSource incoming = _musicAIsActive ? _musicSourceB : _musicSourceA;
            AudioSource outgoing = _musicAIsActive ? _musicSourceA : _musicSourceB;
            _musicAIsActive = !_musicAIsActive;

            incoming.clip = clip;
            incoming.Play();
            StartCoroutine(CrossfadeCoroutine(outgoing, incoming, fadeTime, baseVol));
        }

        public void StopMusic(float fadeTime = 1f) {
            AudioSource active = _musicAIsActive ? _musicSourceA : _musicSourceB;
            StartCoroutine(FadeOutCoroutine(active, fadeTime));
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC API — AMBIENT
        // ═══════════════════════════════════════════════════════

        public void PlayAmbient(AudioClip clip, float fadeTime = 2f, float targetVolume = 1f) {
            if (clip == null) return;

            float baseVol = targetVolume;
            if (_soundLibrary != null) {
                var entry = _soundLibrary.GetEntryByClip(clip);
                if (entry != null) baseVol = entry.Volume * targetVolume;
            }

            AudioSource incoming = _ambientAIsActive ? _ambientSourceB : _ambientSourceA;
            AudioSource outgoing = _ambientAIsActive ? _ambientSourceA : _ambientSourceB;
            _ambientAIsActive = !_ambientAIsActive;

            incoming.clip = clip;
            incoming.Play();
            StartCoroutine(CrossfadeCoroutine(outgoing, incoming, fadeTime, baseVol));
        }

        public void StopAmbient(float fadeTime = 2f) {
            AudioSource active = _ambientAIsActive ? _ambientSourceA : _ambientSourceB;
            StartCoroutine(FadeOutCoroutine(active, fadeTime));
        }

        // ═══════════════════════════════════════════════════════
        // PUBLIC API — VOLUME
        // ═══════════════════════════════════════════════════════

        public void SetMasterVolume(float normalized) {
            SetMixerVolume("MasterVolume", normalized);
        }

        public void SetMusicVolume(float normalized) {
            SetMixerVolume("MusicVolume", normalized);
        }

        public void SetSFXVolume(float normalized) {
            SetMixerVolume("SFXVolume", normalized);
        }

        public void SetAmbientVolume(float normalized) {
            SetMixerVolume("AmbientVolume", normalized);
        }

        private void SetMixerVolume(string parameter, float normalized) {
            if (_mixer == null) return;
            // High-precision calibration: 50% slider (0.5) = 0dB (pure asset volume)
            // Range: [0.0] -> -80dB, [0.5] -> 0dB, [1.0] -> +6dB
            float volumeScale = normalized * 2f;
            float dB = volumeScale > 0.0001f ? Mathf.Log10(volumeScale) * 20f : -80f;
            _mixer.SetFloat(parameter, dB);
        }

        // ═══════════════════════════════════════════════════════
        // CROSSFADE COROUTINES
        // ═══════════════════════════════════════════════════════

        private IEnumerator CrossfadeCoroutine(AudioSource outgoing, AudioSource incoming, float duration, float targetVolume = 1f) {
            float elapsed = 0f;
            float startVolumeOut = outgoing.volume;

            incoming.volume = 0f;

            while (elapsed < duration) {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                outgoing.volume = Mathf.Lerp(startVolumeOut, 0f, t);
                
                // Note: targetVolume is already calibrated when passed to PlayMusic/PlayAmbient
                incoming.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }

            outgoing.Stop();
            outgoing.volume = 0f;
            incoming.volume = targetVolume;
        }

        private IEnumerator FadeOutCoroutine(AudioSource source, float duration) {
            float startVolume = source.volume;
            float elapsed = 0f;

            while (elapsed < duration) {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            source.Stop();
            source.volume = 0f;
        }

        // ═══════════════════════════════════════════════════════
        // EVENTBUS HANDLERS
        // ═══════════════════════════════════════════════════════

        private void OnFloatingText(FloatingTextData data) {
            switch (data.type) {
                case "damage":
                    PlaySFX(data.isCritical ? SoundType.Combat_CriticalHit : SoundType.Combat_Hit, data.position);
                    break;
                case "heal":
                    PlaySFX(SoundType.Combat_Hit, data.position);
                    break;
                case "gold":
                    PlaySFX(SoundType.Loot_Gold);
                    break;
                case "evade":
                    PlaySFX(SoundType.Combat_Miss, data.position);
                    break;
            }
        }

        private void OnCombatError(string message) {
            PlaySFX(SoundType.UI_Error);
        }

        private void OnStatusApplied(string name, bool isBuff) {
            // Status effect sounds are handled directly via ApplySound in StatusEffectSystem
        }

        private void OnPlayerDied() {
            PlaySFX(SoundType.Combat_Death);
        }

        private void OnQuestAccepted(string questName) {
            PlaySFX(SoundType.Quest_Accept);
        }

        private void OnQuestCompleted(NetworkObject player, string questType) {
            PlaySFX(SoundType.Quest_Complete);
        }

        private void OnLevelUp(int newLevel) {
            PlaySFX(SoundType.Combat_LevelUp);
        }

        private void OnVendorBuy(bool success, string message) {
            PlaySFX(success ? SoundType.Vendor_Buy : SoundType.UI_Error);
        }

        private void OnGoldChanged(int newGold) {
            // Gold change sound handled via FloatingTextData "gold" type
        }

        private void OnZoneMusicChanged(AudioClip music, AudioClip ambient) {
            if (music != null) PlayMusic(music);
            if (ambient != null) PlayAmbient(ambient);
        }

        private void OnPlaySFX3D(AudioClip clip, Vector3 position) {
            PlaySFX(clip, position);
        }

        private void OnFootstep(Vector3 position) {
            if (_soundLibrary == null || _footstepSource == null) return;
            SoundEntry entry = _soundLibrary.GetFootstepEntry();
            if (entry == null || entry.Clip == null) return;

            _footstepSource.transform.position = position;
            _footstepSource.clip = entry.Clip;
            _footstepSource.volume = entry.Volume;
            _footstepSource.pitch = Random.Range(entry.PitchMin, entry.PitchMax);
            _footstepSource.spatialBlend = entry.Is3D ? 1f : 0f;
            _footstepSource.Play();
        }

        private void OnFootstepStop() {
            if (_footstepSource != null && _footstepSource.isPlaying) {
                _footstepSource.Stop();
            }
        }

        // ═══════════════════════════════════════════════════════
        // INTERACTION HANDLERS
        // ═══════════════════════════════════════════════════════

        private void OnChestOpened(ILootSource source) {
            bool isChest = source is ChestController;
            PlaySFX(isChest ? SoundType.Loot_ChestOpen : SoundType.Loot_BagOpen);
        }

        private void OnEquipmentSound(bool isEquip) {
            PlaySFX(isEquip ? SoundType.Equipment_Equip : SoundType.Equipment_Unequip);
        }

        private void OnConsumableUsed() {
            PlaySFX(SoundType.Consumable_Potion);
        }

        private void OnPlayerRespawned() {
            PlaySFX(SoundType.Player_Respawn);
        }

        private void OnZoneChanged(bool isInSafeZone) {
            SyncAudioState(isInSafeZone);
        }

        public void SyncAudioState(bool isInSafeZone) {
            if (_soundLibrary == null) return;
            SoundEntry entry = _soundLibrary.GetEntry(isInSafeZone ? SoundType.Zone_SafeEnter : SoundType.Zone_UnsafeEnter);
            if (entry == null || entry.Clip == null) return;
            if (entry.Clip == _currentZoneMusic) return;
            
            _currentZoneMusic = entry.Clip;
            // Play with targetVolume 1.0, the PlayMusic overload will handle lookup and calibration
            PlayMusic(entry.Clip, 1f, 1f);
            
            Debug.Log($"[AudioManager] SyncAudioState: {(isInSafeZone ? "Safe" : "Unsafe")} | Clip: {entry.Clip.name}");
        }

        // ═══════════════════════════════════════════════════════
        // PORTAL & LOOT SOUNDS
        // ═══════════════════════════════════════════════════════

        private void OnPortalSound(bool isEntering) {
            PlaySFX(isEntering ? SoundType.Portal_Enter : SoundType.Portal_Exit);
        }

        private void OnLootDropped(Vector3 position) {
            PlaySFX(SoundType.Loot_Drop, position);
        }

        // ═══════════════════════════════════════════════════════
        // LOOP SFX (Channel Abilities)
        // ═══════════════════════════════════════════════════════

        private void OnStartLoopSFX(AudioClip clip, Vector3 position, int entityId) {
            if (clip == null) return;

            // Stop existing loop for this entity if any
            OnStopLoopSFX(entityId);

            float baseVol = 1f;
            if (_soundLibrary != null) {
                var entry = _soundLibrary.GetEntryByClip(clip);
                if (entry != null) baseVol = entry.Volume;
            }

            AudioSource source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = baseVol;
            source.pitch = 1f;
            source.spatialBlend = 1f;
            source.loop = true;
            source.Play();

            _loopSources[entityId] = source;
        }

        private void OnStopLoopSFX(int entityId) {
            if (_loopSources.TryGetValue(entityId, out AudioSource source)) {
                if (source != null) {
                    source.Stop();
                    source.loop = false;
                    source.clip = null;
                }
                _loopSources.Remove(entityId);
            }
        }

        // ═══════════════════════════════════════════════════════
        // VOLUME PERSISTENCE
        // ═══════════════════════════════════════════════════════

        private void LoadVolumeSettings() {
            // One-time migration to force 50% for existing users
            if (PlayerPrefs.GetInt("audio_v2_initialized", 0) == 0) {
                PlayerPrefs.SetFloat("vol_master", 0.5f);
                PlayerPrefs.SetFloat("vol_music", 0.5f);
                PlayerPrefs.SetFloat("vol_sfx", 0.5f);
                PlayerPrefs.SetFloat("vol_ambient", 0.5f);
                PlayerPrefs.SetInt("audio_v2_initialized", 1);
                PlayerPrefs.Save();
            }

            SetMasterVolume(PlayerPrefs.GetFloat("vol_master", 0.5f));
            SetMusicVolume(PlayerPrefs.GetFloat("vol_music", 0.5f));
            SetSFXVolume(PlayerPrefs.GetFloat("vol_sfx", 0.5f));
            SetAmbientVolume(PlayerPrefs.GetFloat("vol_ambient", 0.5f));
        }

        public float GetMasterVolume() => PlayerPrefs.GetFloat("vol_master", 0.5f);
        public float GetMusicVolume() => PlayerPrefs.GetFloat("vol_music", 0.5f);
        public float GetSFXVolume() => PlayerPrefs.GetFloat("vol_sfx", 0.5f);
        public float GetAmbientVolume() => PlayerPrefs.GetFloat("vol_ambient", 0.5f);
    }
}
