using STRINGS;
using UnityEngine;
using UnityEngine.UI;
using PeterHan.PLib.Core;

namespace PrinterEasyInfo
{
    // Postfix for CarePackageContainer.GenerateCharacter(bool is_starter)
    // (Assembly-CSharp). CarePackageContainer lives in the GLOBAL namespace in
    // the installed game build (confirmed against the game DLL metadata: empty
    // namespace, same as BuildingDef/Game/BuildTool), so it is referenced
    // unqualified below.
    // NO [HarmonyPatch] attributes on purpose: this class is attached
    // PROGRAMMATICALLY in Mod.OnLoad via PatchUtil.TryPatch, and Harmony's
    // PatchAll (base.OnLoad) ignores types without the attribute.
    //
    // This postfix refreshes the codex (database) book button in the
    // care-package column header: it deep-copies the RenameButton (pencil)
    // from a sibling CharacterContainer column (the selection window always
    // contains duplicant columns when care-package columns exist), swaps its
    // glyph for the database icon, and gives it the "View full entry in
    // database" tooltip. The button is created at the pencil's visual
    // position inside the title bar (absolute layout, localPosition kept).
    public static class CarePackageContainer_GenerateCharacter_Patch
    {
        // Exact node name of the button we create; used to find and destroy the
        // previous instance before recreating (reshuffles re-run this postfix).
        private const string CodexButtonName = "CodexButton";

        // The game's database/codex icon sprite (ManagementMenu.cs:214).
        private const string CodexIconSprite = "OverviewUI_database_icon";

        public static void Postfix(CarePackageContainer? __instance)
        {
            if (__instance == null)
            {
                return;
            }

            CarePackageInfo? info = __instance.Info;
            if (info == null)
            {
#if DEBUG
                PUtil.LogDebug("Postfix ran before Info was set — nothing to do");
#endif
                return;
            }

            string codexId = CodexIdResolver.ResolveCodexId(info.id);

            // Column header: the TitleBar node under the KScreen root
            // (CarePackageContainer → TitleBar → {BG, LabelGroup → Label}).
            Transform? titleBar = FindTitleBar(__instance.transform);
            if (titleBar == null)
            {
                PUtil.LogWarning("Care-package column has no TitleBar header — cannot place codex button");
                return;
            }

            // Reshuffles re-run this postfix with a new item: destroy any
            // previously added button first (the node we create is named
            // exactly CodexButton).
            Transform oldButton = titleBar.Find(CodexButtonName);
            if (oldButton != null)
            {
                UnityEngine.Object.Destroy(oldButton.gameObject);
            }

            if (string.IsNullOrEmpty(codexId))
            {
#if DEBUG
                PUtil.LogDebug("Item {0} has no codex entry — no button".F(info.id));
#endif
                return;
            }

            UpdateCodexButton(__instance, titleBar, codexId);

#if DEBUG
            PUtil.LogDebug("Created codex button for item {0} (codex id {1})".F(info.id, codexId));
#endif
        }

        /// <summary>
        /// Finds the column's title bar: first by name ("TitleBar" under the
        /// column root), with a fallback walk of the root's direct children for
        /// the one containing a "LabelGroup" child (the title-bar layout).
        /// Returns null when neither is found.
        /// </summary>
        private static Transform? FindTitleBar(Transform columnRoot)
        {
            Transform? titleBar = columnRoot.Find("TitleBar");
            if (titleBar != null)
            {
                return titleBar;
            }

            foreach (Transform child in columnRoot)
            {
                if (child.Find("LabelGroup") != null)
                {
                    return child;
                }
            }
            return null;
        }

        /// <summary>
        /// Closes the selection window hosting this care-package column, then
        /// opens the item's codex entry (the same OpenCodexToEntry call the
        /// game makes from DetailsScreen.CodexEntryButton_OnClick,
        /// DetailsScreen.cs:556-563). The game allows only one modal window
        /// at a time, so the window must be closed first — otherwise the
        /// codex screen opens behind it (and ESC closes the window first).
        ///
        /// Window lookup: KScreen has no Parent property in this build
        /// (verified against the full decompiled KScreen.cs), and the
        /// column's direct transform parent is the controller's serialized
        /// "containerParent" node, not the window
        /// (CharacterSelectionController.cs:15, 94). So walk the transform
        /// parents above the column root: the first CharacterSelectionController
        /// found is the window itself.
        ///
        /// Ordering: the close is synchronous — the game's own close path for
        /// this window is OnPressBack, which ends in Show(show: false)
        /// (CharacterSelectionController.cs:103-114; ImmigrantScreen's
        /// close button and Deactivate also do Show(show: false),
        /// ImmigrantScreen.cs:54-57, 120-123). No coroutine is involved, so
        /// the codex is opened right after the close completes.
        /// </summary>
        private static void OpenCodexEntry(CarePackageContainer container, string codexId)
        {
            CharacterSelectionController? window = null;
            for (Transform? node = container.transform.parent; node != null; node = node.parent)
            {
                CharacterSelectionController? controller = node.GetComponent<CharacterSelectionController>();
                if (controller != null)
                {
                    window = controller;
                    break;
                }
            }

            if (window != null)
            {
                window.OnPressBack();
            }
            else
            {
                PUtil.LogWarning("No selection window found above the care-package column — codex entry may open behind it");
            }

            if (ManagementMenu.Instance != null)
            {
                ManagementMenu.Instance.OpenCodexToEntry(codexId);
            }
        }

        /// <summary>
        /// Creates the codex button at the pencil's visual position inside the
        /// title bar. Prefers deep-copying the RenameButton (pencil KButton)
        /// from a sibling CharacterContainer column (CharacterContainer →
        /// TitleBar → {BG, CharacterName, RenameButton, LabelGroup}) so the
        /// button looks and sits exactly like the game's own pencil button.
        /// Falls back to a plain code-created 26x26 button anchored to the
        /// title bar's right edge when no reference exists (should not happen).
        /// </summary>
        private static void UpdateCodexButton(CarePackageContainer container, Transform titleBar, string codexId)
        {
            Transform? refBtn = null;
            if (container.transform.parent != null)
            {
                foreach (Transform sibling in container.transform.parent)
                {
                    if (sibling.GetComponent<CharacterContainer>() != null)
                    {
                        refBtn = sibling.Find("TitleBar/RenameButton");
                        if (refBtn != null)
                        {
                            break;
                        }
                    }
                }
            }

            if (refBtn != null)
            {
                // Deep-copy the pencil button into our title bar.
                // Util.KInstantiateUI (Assembly-CSharp-firstpass Util.cs:381)
                // instantiates with worldPositionStays: false, so the clone
                // keeps the pencil's localPosition/localScale inside the
                // title bar — exactly the absolute layout we need.
                GameObject clone = Util.KInstantiateUI(refBtn.gameObject, titleBar.gameObject);
                if (clone == null)
                {
                    PUtil.LogWarning("KInstantiateUI returned null while cloning the pencil button — codex button not created");
                    return;
                }
                clone.name = CodexButtonName;

                KButton button = clone.GetComponent<KButton>();
                if (button != null)
                {
                    // Detach the prefab's rename listeners. KButton has no
                    // RemoveAllListeners; ClearOnClick (KButton.cs:57) nulls
                    // onClick/onBtnClick/onDoubleClick.
                    button.ClearOnClick();

                    button.onClick += () => OpenCodexEntry(container, codexId);
                }

                SetCodexGlyph(clone.transform);

                ToolTip tooltip = clone.GetComponent<ToolTip>();
                if (tooltip == null)
                {
                    tooltip = clone.AddComponent<ToolTip>();
                }
                // LocString converts implicitly to string (LocString.cs:44);
                // same call the game makes on its own codex button
                // (DetailsScreen.cs:553).
                tooltip.SetSimpleTooltip(UI.TOOLTIPS.OPEN_CODEX_ENTRY);
            }
            else
            {
                // Fallback: build a plain button in code. Should not happen —
                // the selection window always contains duplicant columns.
                PUtil.LogWarning("No CharacterContainer column found in the selection window — using code-created fallback codex button");

                GameObject go = new GameObject(CodexButtonName);
                go.transform.SetParent(titleBar, worldPositionStays: false);

                // A child of a canvas element gets a RectTransform automatically.
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.sizeDelta = new Vector2(26f, 26f);
                rt.anchoredPosition = new Vector2(-14f, -14f);

                KButton button = go.AddComponent<KButton>();
                // KButton's pointer handlers read soundPlayer (KButton.cs:246-252);
                // a code-created button has none, so give it one to avoid an NRE.
                // (ButtonSoundPlayer is a plain [Serializable] data class, not a
                // Component — WidgetSoundPlayer.cs:4 — it is assigned to the
                // KButton's serialized field, not added to the GameObject.)
                button.soundPlayer = new ButtonSoundPlayer();
                button.ClearOnClick();

                button.onClick += () => OpenCodexEntry(container, codexId);

                SetCodexGlyph(go.transform, createImage: true);

                ToolTip tooltip = go.AddComponent<ToolTip>();
                tooltip.SetSimpleTooltip(UI.TOOLTIPS.OPEN_CODEX_ENTRY);
            }
        }

        /// <summary>
        /// Sets the database icon sprite on the button's "Image" child node.
        /// The component is a UnityEngine.UI.Image (or a KImage, which derives
        /// from it — KImage.cs:4), so GetComponent&lt;Image&gt; covers both.
        /// </summary>
        private static void SetCodexGlyph(Transform buttonRoot, bool createImage = false)
        {
            Transform imgNode = buttonRoot.Find("Image");
            if (imgNode == null)
            {
                if (!createImage)
                {
                    return;
                }
                GameObject imgGo = new GameObject("Image");
                imgGo.transform.SetParent(buttonRoot, worldPositionStays: false);
                imgNode = imgGo.transform;
                RectTransform imgRt = imgNode.GetComponent<RectTransform>();
                imgRt.anchorMin = Vector2.zero;
                imgRt.anchorMax = Vector2.one;
                imgRt.offsetMin = Vector2.zero;
                imgRt.offsetMax = Vector2.zero;
            }

            Image image = imgNode.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
            {
                image = imgNode.gameObject.AddComponent<UnityEngine.UI.Image>();
            }
            image.sprite = Assets.GetSprite(CodexIconSprite);
        }
    }
}
