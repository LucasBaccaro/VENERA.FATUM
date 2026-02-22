using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace Genesis.Simulation {

    public class EnemyVisualRandomizer : NetworkBehaviour {

        [Header("Accessories")]
        [SerializeField] private GameObject _chestpads;
        [SerializeField] private GameObject _handsAcc;
        [SerializeField] private GameObject _skirt2;
        [SerializeField] private GameObject _crossbelt;
        [SerializeField] private GameObject _shoulderpads;

        [Header("Hairstyles")]
        [SerializeField] private GameObject _hairstyle1;
        [SerializeField] private GameObject _hairstyle2;
        [SerializeField] private GameObject _hairstyle3;
        [SerializeField] private GameObject _hood;

        private readonly SyncVar<int> _visualSeed = new SyncVar<int>(0);

        public override void OnStartNetwork() {
            base.OnStartNetwork();
            _visualSeed.OnChange += OnVisualSeedChanged;
        }

        public override void OnStartServer() {
            base.OnStartServer();
            _visualSeed.Value = Random.Range(int.MinValue, int.MaxValue);
            ApplyVisuals(_visualSeed.Value);
        }

        public override void OnStartClient() {
            base.OnStartClient();
            if (!base.IsServerInitialized) {
                ApplyVisuals(_visualSeed.Value);
            }
        }

        private void OnVisualSeedChanged(int oldVal, int newVal, bool asServer) {
            if (!asServer) {
                ApplyVisuals(newVal);
            }
        }

        private void ApplyVisuals(int seed) {
            var rng = new System.Random(seed);

            // Toggle accessories (50/50 each) — Hood moved to hairstyle group
            SetActive(_chestpads, rng.Next(2) == 1);
            SetActive(_handsAcc, rng.Next(2) == 1);
            SetActive(_skirt2, rng.Next(2) == 1);
            SetActive(_crossbelt, rng.Next(2) == 1);
            SetActive(_shoulderpads, rng.Next(2) == 1);

            // Pick head style: hairstyle 1/2/3, hood, or bald
            int hairChoice = rng.Next(5);
            SetActive(_hairstyle1, hairChoice == 0);
            SetActive(_hairstyle2, hairChoice == 1);
            SetActive(_hairstyle3, hairChoice == 2);
            SetActive(_hood, hairChoice == 3);
            // hairChoice == 4 → nothing (bald)
        }

        private void SetActive(GameObject obj, bool active) {
            if (obj != null) obj.SetActive(active);
        }
    }
}
