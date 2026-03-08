using UnityEngine;

namespace Octo.Interaction
{
    /// <summary>
    /// Controls treasure chest open / close animations and coin counting.
    /// Attach to the treasure_chest root GameObject.
    /// Requires an Animator component with states: Idle, Open, Close.
    ///
    /// Add a child GameObject "CoinZone" with a trigger BoxCollider inside the
    /// chest cavity. This script detects ObjectGrabbable items entering/exiting.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class TreasureChest : MonoBehaviour
    {
        [Tooltip("Seconds the chest stays open before auto-closing")]
        [SerializeField] private float autoCloseDelay = 4f;

        [Tooltip("How close the octopus must be to interact")]
        [SerializeField] private float interactionRadius = 20f;

        private Animator animator;
        private bool isOpen;
        private float closeTimer;
        private int coinsInside;
        private CoinDepositVFX depositVFX;

        // Animator parameter hash
        private static readonly int OpenTrigger = Animator.StringToHash("Open");
        private static readonly int CloseTrigger = Animator.StringToHash("Close");

        /// <summary>Number of coins currently inside the chest.</summary>
        public int CoinsInside => coinsInside;

        private ChestTriggerZone coinZone;

        private void Awake()
        {
            animator = GetComponent<Animator>();

            var vfxGo = new GameObject("DepositVFX");
            vfxGo.transform.SetParent(transform, false);
            depositVFX = vfxGo.AddComponent<CoinDepositVFX>();
        }

        private void Start()
        {
            coinZone = GetComponentInChildren<ChestTriggerZone>();
            if (coinZone == null)
            {
                var zone = new GameObject("CoinZone");
                zone.transform.SetParent(transform, false);
                zone.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                var box = zone.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.8f, 0.6f, 0.5f);
                coinZone = zone.AddComponent<ChestTriggerZone>();
                coinZone.Init(this);
                Debug.Log("[TreasureChest] Auto-created CoinZone trigger.");
            }

            // Start with coin zone disabled — only accept coins when open
            SetCoinZoneActive(false);
        }

        private void Update()
        {
            if (isOpen)
            {
                closeTimer -= Time.deltaTime;
                if (closeTimer <= 0f)
                {
                    Close();
                }
            }
        }

        /// <summary>
        /// Opens the chest if it's currently closed.
        /// </summary>
        public void Open()
        {
            if (isOpen) return;
            isOpen = true;
            closeTimer = autoCloseDelay;
            animator.SetTrigger(OpenTrigger);
            SetCoinZoneActive(true);
            Debug.Log("[TreasureChest] Opened!");
        }

        /// <summary>
        /// Closes the chest.
        /// </summary>
        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            animator.SetTrigger(CloseTrigger);
            SetCoinZoneActive(false);
            Debug.Log("[TreasureChest] Closed!");
        }

        private void SetCoinZoneActive(bool active)
        {
            if (coinZone != null)
            {
                var col = coinZone.GetComponent<Collider>();
                if (col != null) col.enabled = active;
            }
        }

        /// <summary>Called by ChestTriggerZone when a coin enters.</summary>
        public void OnCoinEntered(GameObject coin)
        {
            coinsInside++;
            Debug.Log($"[TreasureChest] Coin IN: {coin.name} — total: {coinsInside}");

            if (depositVFX != null)
                depositVFX.Play(coin.transform.position);

            var gm = Octo.GameManager.Instance;
            if (gm != null)
                gm.AddCoinScore();
        }

        /// <summary>Called by ChestTriggerZone when a coin exits.</summary>
        public void OnCoinExited(GameObject coin)
        {
            coinsInside = Mathf.Max(0, coinsInside - 1);
            Debug.Log($"[TreasureChest] Coin OUT: {coin.name} — total: {coinsInside}");
        }

        public bool IsOpen => isOpen;
        public float InteractionRadius => interactionRadius;
    }

    /// <summary>
    /// Place on a child trigger-collider inside the chest.
    /// Detects ObjectGrabbable items entering / exiting.
    /// </summary>
    public class ChestTriggerZone : MonoBehaviour
    {
        private TreasureChest chest;

        public void Init(TreasureChest owner) => chest = owner;

        private void Awake()
        {
            if (chest == null)
                chest = GetComponentInParent<TreasureChest>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var grabbable = other.GetComponent<ObjectGrabbable>();
            if (chest != null && grabbable != null)
            {
                // Force-drop the object if it's still held
                if (grabbable.IsGrabbed)
                    grabbable.Drop();

                chest.OnCoinEntered(other.gameObject);

                // Disable the coin so it "disappears" inside the chest
                other.gameObject.SetActive(false);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (chest != null && other.GetComponent<ObjectGrabbable>() != null)
                chest.OnCoinExited(other.gameObject);
        }
    }
}
