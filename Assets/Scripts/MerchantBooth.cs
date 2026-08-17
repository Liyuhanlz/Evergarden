using UnityEngine;

// Wires the booth's gaze-based interact prompt (see GazeInteractable) to
// opening the shop. Trading is only offered once the Merchant has actually
// walked back and settled at the booth -- otherwise the prompt/interaction
// is disabled entirely so the player can't pull him into the shop teleport
// while he's still mid-walk toward them.
//
// Unity setup:
//   1. Add both this script and a GazeInteractable to the booth GameObject
//   2. Tune GazeInteractable's Range/Max Gaze Angle, and drag a "Press A to
//      trade" World Space Canvas into its Prompt Canvas
//   3. Drag the Merchant's MerchantNPC component into Merchant
[RequireComponent(typeof(GazeInteractable))]
public class MerchantBooth : MonoBehaviour
{
    [Tooltip("Trading is only offered once this merchant has finished walking back and settled at the booth")]
    public MerchantNPC merchant;

    GazeInteractable gazeInteractable;

    void Awake()
    {
        gazeInteractable = GetComponent<GazeInteractable>();
        gazeInteractable.onInteractPressed.AddListener(TryEnterShop);
    }

    void Update()
    {
        bool merchantReady = merchant == null || merchant.IsAtBooth;
        bool notShopping = ShopInteractionController.Instance == null || !ShopInteractionController.Instance.IsInShopMode;
        bool shouldBeActive = merchantReady && notShopping;

        if (gazeInteractable.enabled != shouldBeActive)
            gazeInteractable.enabled = shouldBeActive;
    }

    void TryEnterShop()
    {
        if (ShopInteractionController.Instance == null) return;
        if (ShopInteractionController.Instance.IsInShopMode) return;

        ShopInteractionController.Instance.EnterShop();
    }
}
