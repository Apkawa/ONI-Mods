using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using PeterHan.PLib.Core;
using PeterHan.PLib.UI;


// Namespace keeps the `OxygenNotIncluded` walk-up so unqualified game types
// (ToolParameterMenu, Strings, Element, ElementLoader) resolve without extra usings.
namespace OxygenNotIncluded.Mods
{
    /// <summary>
    /// Replacement category list for the vanilla "Схема материалов" overlay
    /// (F4, OverlayModes.TileMode). Vanilla builds 11 category checkboxes with
    /// ToolParameterMenu from TileMode.CreateDefaultFilters(); this panel replaces
    /// that filter section for TileMode only.
    ///
    /// One row per category: a "+" expand button (visible only on the selected row),
    /// the localized category name, and a radio button (a PCheckBox restyled with a
    /// solid white disc marker; the selected row gets a black dot in its center,
    /// like the vanilla legend). Rows span the full panel width: the label
    /// is left-aligned and the radio sits in a right column. Selection is
    /// single-select (exactly one radio checked), starting at ALL, matching vanilla
    /// behaviour.
    ///
    /// The "+" of a selected category opens a per-material sub-filter tree under
    /// that row: one indented row per material of the category, each with an
    /// independent PCheckBox (multi-select). Closing the tree hides the rows and
    /// resets their checkboxes; switching the category does the same for any open
    /// tree.
    /// </summary>
    public class LegendPanel
    {
        /// <summary>
        /// Single panel per level (OverlayLegend is recreated per level).
        /// </summary>
        public static LegendPanel Instance;

        /// <summary>
        /// The root GameObject of the realized panel (set by <see cref="Build"/>);
        /// destroy it to remove the whole panel from the screen.
        /// </summary>
        public GameObject Root { get; private set; }

        /// <summary>
        /// Left indent (px) of the material tree rows under their category row.
        /// </summary>
        private const int TREE_INDENT = 20;

        /// <summary>
        /// Strips the &lt;link="ID"&gt;…&lt;/link&gt; markup from a localized
        /// Element.name so it can be used as a plain row label.
        /// </summary>
        private static readonly Regex LinkMarkup = new("<[^>]+>");

        /// <summary>
        /// The 11 material categories, in the same order as vanilla
        /// OverlayModes.TileMode.CreateDefaultFilters().
        /// </summary>
        private static readonly string[] CATEGORIES =
        {
            ToolParameterMenu.FILTERLAYERS.ALL,
            ToolParameterMenu.FILTERLAYERS.METAL,
            ToolParameterMenu.FILTERLAYERS.BUILDABLE,
            ToolParameterMenu.FILTERLAYERS.FILTER,
            ToolParameterMenu.FILTERLAYERS.CONSUMABLEORE,
            ToolParameterMenu.FILTERLAYERS.ORGANICS,
            ToolParameterMenu.FILTERLAYERS.FARMABLE,
            ToolParameterMenu.FILTERLAYERS.LIQUIFIABLE,
            ToolParameterMenu.FILTERLAYERS.GAS,
            ToolParameterMenu.FILTERLAYERS.LIQUID,
            ToolParameterMenu.FILTERLAYERS.MISC,
        };

        /// <summary>
        /// Category → material category tags, mirroring the tag list each category
        /// contributes in vanilla TileMode.OnFiltersChanged(). ALL is intentionally
        /// absent (it has no tree).
        /// </summary>
        private static readonly Dictionary<string, List<Tag>> CategoryTags = new()
        {
            [ToolParameterMenu.FILTERLAYERS.METAL] = new() { GameTags.Metal, GameTags.RefinedMetal },
            [ToolParameterMenu.FILTERLAYERS.BUILDABLE] = new() { GameTags.BuildableRaw, GameTags.BuildableProcessed },
            [ToolParameterMenu.FILTERLAYERS.FILTER] = new() { GameTags.Filter },
            [ToolParameterMenu.FILTERLAYERS.CONSUMABLEORE] = new() { GameTags.ConsumableOre, GameTags.Sublimating },
            [ToolParameterMenu.FILTERLAYERS.ORGANICS] = new() { GameTags.Organics },
            [ToolParameterMenu.FILTERLAYERS.FARMABLE] = new() { GameTags.Farmable, GameTags.Agriculture },
            [ToolParameterMenu.FILTERLAYERS.LIQUIFIABLE] = new() { GameTags.Liquifiable },
            [ToolParameterMenu.FILTERLAYERS.GAS] = new() { GameTags.Breathable, GameTags.Unbreathable },
            [ToolParameterMenu.FILTERLAYERS.LIQUID] = new() { GameTags.Liquid },
            [ToolParameterMenu.FILTERLAYERS.MISC] = new() { GameTags.Other },
        };

        private sealed class MaterialRow
        {
            public Element Element = null!;

            public GameObject CheckBoxGo = null!;

            /// <summary>
            /// The realized material row GameObject; used to make the whole row
            /// (not just the checkbox square) clickable.
            /// </summary>
            public GameObject RowGo = null!;
        }

        private sealed class CategoryRow
        {
            public string Name = null!;

            public GameObject RowGo = null!;

            public GameObject ExpandButtonGo = null!;

            public GameObject CheckBoxGo = null!;

            /// <summary>
            /// The realized tree panel under this row; null for ALL (and for any
            /// category with no matching elements).
            /// </summary>
            public GameObject? TreePanelGo = null;

            /// <summary>
            /// The materials of this category, in row order (sorted by name).
            /// </summary>
            public List<MaterialRow> Materials = new();

            public bool Expanded;
        }

        /// <summary>
        /// Left-click handler for a whole category row (see
        /// <see cref="MakeRowsClickable"/>). A plain IPointerClickHandler on the
        /// row GameObject: no hover/press visuals, and the EventSystem routes to
        /// it only when no closer child widget (the "+" button, the checkbox
        /// square) handled the click.
        /// </summary>
        private sealed class RowClickHandler : MonoBehaviour, IPointerClickHandler
        {
            public System.Action OnClick = null!;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    OnClick?.Invoke();
                }
            }
        }

        private readonly List<CategoryRow> rows = new();
        private string selectedCategory = ToolParameterMenu.FILTERLAYERS.ALL;

        /// <summary>
        /// Raised when the user clicks the "+" button of a selected row, toggling its
        /// expanded state. The argument is the category name.
        /// </summary>
        public event Action<string> OnExpandToggled;

        /// <summary>
        /// Raised whenever the effective filter selection changes: the selected
        /// category changes, a material tree checkbox toggles, or an open tree
        /// collapses (resetting its material sub-selection). Subscribers recompute
        /// the overlay filter from <see cref="SelectedCategory"/> +
        /// <see cref="GetSelectedMaterials"/>.
        /// </summary>
        public event System.Action OnFilterChanged;

        /// <summary>
        /// Categories currently in the expanded state.
        /// </summary>
        public readonly List<string> expandedCategories = new();

        // Radio button sprites for the category rows (generated once at runtime,
        // so the mod needs no extra art assets): a solid white disc (the always-
        // visible marker) and a black dot shown in the center of the selected
        // row. The mark's state color is white, so the black dot renders as-is.
        private static Sprite s_radioBorder;
        private static Sprite s_radioDot;

        /// <summary>
        /// Optional external hook invoked after a tree is toggled:
        /// (categoryName, expanded).
        /// </summary>
        public Action<string, bool>? TreeToggleHook = null;

        /// <summary>
        /// The currently selected (checked) category.
        /// </summary>
        public string SelectedCategory => selectedCategory;

        /// <summary>
        /// The materials whose tree checkboxes are currently checked (for the filter
        /// logic): one element per checked material row across all trees.
        /// </summary>
        public List<Element> GetSelectedMaterials()
        {
            var result = new List<Element>();
            foreach (var row in rows)
            {
                foreach (var material in row.Materials)
                {
                    // Read the toggle's live state directly (the property maps to the
                    // MultiToggle.state field we set in ForceCheckState) rather than
                    // the PCheckBox detour read.
                    var toggle = material.CheckBoxGo?.GetComponent<MultiToggle>();
                    if (toggle != null && toggle.CurrentState == PCheckBox.STATE_CHECKED)
                    {
                        result.Add(material.Element);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// The tags that make the vanilla TileMode highlight for a category,
        /// mirroring the additions in vanilla TileMode.OnFiltersChanged(). For ALL
        /// (which enables every category through Mode.InFilter) this is the union
        /// of all category tags, i.e. everything.
        /// </summary>
        public static List<Tag> GetCategoryTags(string categoryName)
        {
            if (categoryName == ToolParameterMenu.FILTERLAYERS.ALL)
            {
                var all = new List<Tag>();
                foreach (var categoryTags in CategoryTags.Values)
                {
                    all.AddRange(categoryTags);
                }
                return all;
            }
            return CategoryTags.TryGetValue(categoryName, out var tags)
                ? tags
                : new List<Tag>();
        }

        /// <summary>
        /// Builds the whole panel under <paramref name="parent"/>.
        /// <paramref name="menuReference"/> is the RectTransform of the vanilla
        /// ToolParameterMenu this panel replaces; when provided, the root panel is
        /// pinned to that menu's horizontal slot (same left position and width) so
        /// it does not stretch across the whole overlay.
        /// </summary>
        public void Build(Transform parent, RectTransform menuReference = null)
        {
            Instance = this;
            selectedCategory = ToolParameterMenu.FILTERLAYERS.ALL;
            expandedCategories.Clear();
            rows.Clear();

            var rootPanel = new PPanel("MaterialFilterLegend")
            {
                Direction = PanelDirection.Vertical,
                Spacing = 4,
                DynamicSize = true,
                // Padding on all four sides between the content and the blue border.
                Margin = new RectOffset(8, 8, 8, 8),
            };
            rootPanel.SetKleiBlueColor();

            foreach (var name in CATEGORIES)
            {
                var row = new CategoryRow() { Name = name };

                // The "+" expand button, shown only on the selected row (and only if
                // the category has a tree; ALL has none).
                var expandButton = new PButton($"Expand_{name}")
                {
                    Text = "+",
                };
                expandButton.OnClick = _ => OnExpandButtonClicked(row);
                expandButton.AddOnRealize(go => row.ExpandButtonGo = go);

                // Localized category name (vanilla string: STRINGS.UI.TOOLS.FILTERLAYERS.<name>.NAME).
                // FlexSize.x stretches the label across the row so the checkbox always
                // lands in the right column; TextAlignment pins the text to the left
                // edge (vanilla row layout: label left, checkbox right).
                var label = new PLabel($"Label_{name}")
                {
                    Text = Strings.Get($"STRINGS.UI.TOOLS.FILTERLAYERS.{name}.NAME"),
                    FlexSize = new Vector2(1f, 0f),
                    TextAlignment = TextAnchor.MiddleLeft,
                };

                // Single-select checkbox, vanilla pink style; ALL starts checked.
                // Restyled as a radio button (see MakeRadioStyle) since exactly one
                // category can be selected at a time.
                var checkBox = new PCheckBox($"Filter_{name}")
                {
                    InitialState = name == ToolParameterMenu.FILTERLAYERS.ALL
                        ? PCheckBox.STATE_CHECKED
                        : PCheckBox.STATE_UNCHECKED,
                    // A radio dot reads better at a slightly smaller check size
                    // inside the border than the 16px checkmark.
                    CheckSize = new Vector2(12, 12),
                    OnChecked = (go, state) => OnCheckBoxToggled(row, state),
                };
                checkBox.SetKleiPinkStyle();
                checkBox.AddOnRealize(go => row.CheckBoxGo = go);
                checkBox.AddOnRealize(go => MakeRadioStyle(go));

                var rowPanel = new PPanel($"Row_{name}")
                {
                    Direction = PanelDirection.Horizontal,
                    Spacing = 4,
                    DynamicSize = true,
                    // Stretch the row across the full panel width. Without this the
                    // row stays at its content width and the root panel's
                    // MiddleCenter alignment packs the row content into the middle
                    // of the panel instead of spanning it.
                    FlexSize = new Vector2(1f, 0f),
                };
                rowPanel.AddChild(expandButton);
                rowPanel.AddChild(label);
                rowPanel.AddChild(checkBox);
                rowPanel.AddOnRealize(go => row.RowGo = go);
                rootPanel.AddChild(rowPanel);
                rows.Add(row);
            }

            var rootGo = rootPanel.Build();
            Root = rootGo;
            // Add the root PPanel as a child of parent, first sibling (like vanilla).
            rootGo.transform.SetParent(parent);
            rootGo.transform.SetAsFirstSibling();
            // CreateUI leaves the root stretched across the entire parent (the whole
            // overlay, menu + diagrams), which made the rows overflow the right
            // border. Pin it to the vanilla menu's horizontal slot instead.
            ApplyMenuLayout(rootGo, menuReference);

            MakeRowsClickable();
            BuildMaterialTrees(rootGo);
            MakeMaterialRowsClickable();
            ApplySelection();
        }

        /// <summary>
        /// Pins <paramref name="rootGo"/> horizontally to the vanilla
        /// ToolParameterMenu it replaces. The root panel and the menu are siblings
        /// under the same parent, so copying the menu's horizontal anchors, pivot,
        /// position and width places the panel in exactly the menu's slot (the menu
        /// itself is hidden, but its RectTransform is still valid). The vertical
        /// axis is left stretched to the parent: the overlay's height already matches
        /// the content, and the root panel's Margin supplies the 4-side padding.
        /// </summary>
        private static void ApplyMenuLayout(GameObject rootGo, RectTransform menuReference)
        {
            if (menuReference == null)
            {
                return;
            }
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(menuReference.anchorMin.x, 0f);
            rt.anchorMax = new Vector2(menuReference.anchorMax.x, 1f);
            rt.pivot = new Vector2(menuReference.pivot.x, 0.5f);
            rt.anchoredPosition = new Vector2(menuReference.anchoredPosition.x, 0f);
            rt.sizeDelta = new Vector2(menuReference.sizeDelta.x, 0f);
        }

        /// <summary>
        /// Makes each whole category row a click target, matching vanilla where the
        /// filter row widget is clickable as a whole (not just the checkbox square).
        /// A transparent raycast-target Image covers the row area; left-clicks on the
        /// label or the empty row area then select the category through the same
        /// path as a checkbox click. Child widgets sit in front of the row Image and
        /// keep their own handlers (the EventSystem stops at the nearest one), so the
        /// "+" button and the checkbox square behave exactly as before.
        /// </summary>
        private void MakeRowsClickable()
        {
            foreach (var row in rows)
            {
                var back = row.RowGo.AddComponent<UnityEngine.UI.Image>();
                back.color = Color.clear;
                back.raycastTarget = true;
                var clicker = row.RowGo.AddComponent<RowClickHandler>();
                clicker.OnClick = () =>
                {
#if DEBUG
                    PUtil.LogDebug("category row clicked: {0}".F(row.Name));
#endif
                    SelectCategory(row);
                };
            }
        }

        /// <summary>
        /// Makes each whole material row in the trees a click target, so clicking a
        /// material name (not just its small checkbox square) toggles it, matching
        /// how the category rows behave. Same transparent raycast-target Image trick
        /// as the category rows; the checkbox square sits in front and keeps its own
        /// handler.
        /// </summary>
        private void MakeMaterialRowsClickable()
        {
            foreach (var row in rows)
            {
                foreach (var material in row.Materials)
                {
                    if (material.RowGo == null || material.CheckBoxGo == null)
                    {
                        continue;
                    }
                    var back = material.RowGo.AddComponent<UnityEngine.UI.Image>();
                    back.color = Color.clear;
                    back.raycastTarget = true;
                    var clicker = material.RowGo.AddComponent<RowClickHandler>();
                    clicker.OnClick = () => ToggleMaterial(material);
                }
            }
        }

        /// <summary>
        /// Toggles a single material's checkbox (multi-select) when its row is
        /// clicked, then raises the filter change.
        /// </summary>
        private void ToggleMaterial(MaterialRow material)
        {
            var toggle = material.CheckBoxGo?.GetComponent<MultiToggle>();
            if (toggle == null)
            {
                return;
            }
#if DEBUG
            PUtil.LogDebug("material row clicked: {0} (state {1})".F(StripMarkup(material.Element.name), toggle.CurrentState));
#endif
            ForceCheckState(material.CheckBoxGo, toggle.CurrentState == PCheckBox.STATE_CHECKED
                ? PCheckBox.STATE_UNCHECKED
                : PCheckBox.STATE_CHECKED);
            OnFilterChanged?.Invoke();
        }

        /// <summary>
        /// Realizes the per-material tree for every category that has one and
        /// inserts it under the root panel directly below its category row, hidden
        /// until the category's "+" is clicked.
        /// ElementLoader.elements is populated at game load, and this panel is built
        /// at runtime after load, so enumerating it here is safe.
        /// </summary>
        private void BuildMaterialTrees(GameObject rootGo)
        {
            var elements = ElementLoader.elements ?? new List<Element>();
            // Iterate rows in REVERSE order: inserting from last to first means each
            // SetSiblingIndex(i + 1) placement never shifts an already-placed
            // higher-index tree, so every tree lands directly below its own row.
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                var row = rows[i];
                if (!CategoryTags.TryGetValue(row.Name, out var tags))
                {
                    continue; // ALL (or an unknown category): no tree.
                }
                // One tree row per element whose materialCategory matches, sorted by
                // the localized display name (ordinal, culture-invariant).
                var materials = elements
                    .Where(e => tags.Contains(e.materialCategory))
                    .OrderBy(e => StripMarkup(e.name), StringComparer.Ordinal)
                    .ToList();
                if (materials.Count == 0)
                {
                    continue; // Nothing to expand: no tree, so no "+" for this row.
                }
                var treePanel = new PPanel($"Tree_{row.Name}")
                {
                    Direction = PanelDirection.Vertical,
                    Spacing = 2,
                    // Left margin indents the whole tree under the category row.
                    Margin = new RectOffset(TREE_INDENT, 0, 0, 0),
                    DynamicSize = true,
                    // Stretch across the full panel width (minus the indent) so the
                    // material rows below can span it too.
                    FlexSize = new Vector2(1f, 0f),
                };
                foreach (var element in materials)
                {
                    var materialRow = new MaterialRow() { Element = element };
                    // Independent checkbox: no single-select logic inside the tree.
                    var checkBox = new PCheckBox($"Material_{row.Name}_{element.idx}")
                    {
                        InitialState = PCheckBox.STATE_UNCHECKED,
                        // User toggle narrows (or widens) the overlay filter to the
                        // checked materials. PCheckBox only raises OnChecked on
                        // real clicks, so programmatic resets in CollapseTree do
                        // not double-fire it.
                        OnChecked = (go, state) =>
                        {
#if DEBUG
                            PUtil.LogDebug("material checkbox clicked: {0}".F(StripMarkup(element.name)));
#endif
                            ForceCheckState(go, state == PCheckBox.STATE_CHECKED
                                ? PCheckBox.STATE_UNCHECKED
                                : PCheckBox.STATE_CHECKED);
                            OnFilterChanged?.Invoke();
                        },
                    };
                    checkBox.SetKleiPinkStyle();
                    checkBox.AddOnRealize(go => materialRow.CheckBoxGo = go);
                    // Same stretch as the category label: the label fills the row and
                    // pins its text left, so the checkbox sits in the same right
                    // column as the category checkboxes.
                    var label = new PLabel($"MaterialLabel_{row.Name}_{element.idx}")
                    {
                        Text = StripMarkup(element.name),
                        FlexSize = new Vector2(1f, 0f),
                        TextAlignment = TextAnchor.MiddleLeft,
                    };
                    var rowPanel = new PPanel($"MaterialRow_{row.Name}_{element.idx}")
                    {
                        Direction = PanelDirection.Horizontal,
                        Spacing = 4,
                        DynamicSize = true,
                        // Stretch across the tree width so the label fills the row
                        // and the checkbox sits in the same right column as the
                        // category radios.
                        FlexSize = new Vector2(1f, 0f),
                    };
                    rowPanel.AddChild(label);
                    rowPanel.AddChild(checkBox);
                    rowPanel.AddOnRealize(go => materialRow.RowGo = go);
                    treePanel.AddChild(rowPanel);
                    row.Materials.Add(materialRow);
                }
                var treeGo = treePanel.Build();
                // Nest the realized tree right below its category row, hidden.
                treeGo.transform.SetParent(rootGo.transform, false);
                treeGo.transform.SetSiblingIndex(i + 1);
                treeGo.SetActive(false);
                row.TreePanelGo = treeGo;
            }
        }

        /// <summary>
        /// Moves the single selection to <paramref name="categoryName"/> (used externally
        /// for reset logic); updates checkbox states and the "+" button visibility.
        /// Closing any other open tree resets its sub-selection.
        /// </summary>
        public void SetSelection(string categoryName)
        {
            if (!rows.Exists(r => r.Name == categoryName))
            {
                PUtil.LogWarning("SetSelection: неизвестная категория {0}".F(categoryName));
                return;
            }
            if (selectedCategory == categoryName)
            {
                return;
            }
            selectedCategory = categoryName;
            // ApplySelection closes (and resets) any open foreign tree, so the
            // sub-selection state is final before we raise the filter change.
            ApplySelection();
            OnFilterChanged?.Invoke();
        }

        private void OnCheckBoxToggled(CategoryRow row, int state)
        {
#if DEBUG
            PUtil.LogDebug("category checkbox clicked: {0} (pre-state {1})".F(row.Name, state));
#endif
            SelectCategory(row);
        }

        /// <summary>
        /// Single-select: moves the category selection to <paramref name="row"/>.
        /// Shared by the checkbox click and the whole-row click. The selection is
        /// independent of the checkbox's pre-click state: PCheckBox.OnChecked
        /// delivers the PRE-CLICK state and nothing else advances the visual state
        /// of our custom widgets, so any click on a category row always means
        /// "user selects this row".
        /// </summary>
        private void SelectCategory(CategoryRow row)
        {
            if (row.Name == selectedCategory)
            {
                return; // zero selection is not allowed: keep current selection
            }
            selectedCategory = row.Name;
            // ApplySelection checks this row, unchecks the rest, and closes
            // (and resets) any open foreign tree, so the sub-selection state is
            // final before we raise the filter change.
            ApplySelection();
            OnFilterChanged?.Invoke();
        }

        private void OnExpandButtonClicked(CategoryRow row)
        {
#if DEBUG
            PUtil.LogDebug("expand button clicked: {0} (was expanded: {1})".F(row.Name, row.Expanded));
#endif
            if (row.TreePanelGo == null)
            {
                return; // No tree for this category (ALL / no materials).
            }
            if (row.Expanded)
            {
                CollapseTree(row);
                // Flip the realized button label between "-" and "+". PUIElements.SetText
                // resolves the TextMeshProUGUI internally (that assembly is not
                // referenced by this project, so we cannot touch TMP types directly).
                PUIElements.SetText(row.ExpandButtonGo, "+");
                OnExpandToggled?.Invoke(row.Name);
                TreeToggleHook?.Invoke(row.Name, false);
                // The sub-selection was reset by CollapseTree: the filter falls
                // back to the whole (still selected) category.
                OnFilterChanged?.Invoke();
            }
            else
            {
                row.Expanded = true;
                expandedCategories.Add(row.Name);
                PUIElements.SetText(row.ExpandButtonGo, "-");
                // Opening the tree: reveal the (already reset) material rows.
                row.TreePanelGo.SetActive(true);
                OnExpandToggled?.Invoke(row.Name);
                TreeToggleHook?.Invoke(row.Name, true);
            }
        }

        /// <summary>
        /// Closes <paramref name="row"/>'s tree: resets all of its material
        /// checkboxes and hides the tree rows.
        /// </summary>
        private void CollapseTree(CategoryRow row)
        {
            if (!row.Expanded)
            {
                return;
            }
            row.Expanded = false;
            expandedCategories.Remove(row.Name);
            if (row.TreePanelGo != null)
            {
                foreach (var material in row.Materials)
                {
                    ForceCheckState(material.CheckBoxGo, PCheckBox.STATE_UNCHECKED);
                }
                row.TreePanelGo.SetActive(false);
            }
        }

        /// <summary>
        /// Forces a checkbox's <see cref="MultiToggle"/> to <paramref name="state"/>
        /// directly. <see cref="PCheckBox.SetCheckState"/> routes through UIDetours
        /// that target the MultiToggle.CurrentState *property* and its ChangeState
        /// method; at runtime those detours resolve to nothing, so the call is a
        /// silent no-op and the visual state never changes. Calling the game's
        /// MultiToggle.ChangeState (compile-time type, with forceRefreshState)
        /// re-applies the state sprite to the mark image immediately, so the
        /// radio/checkbox actually updates.
        /// </summary>
        private static void ForceCheckState(GameObject go, int state)
        {
            if (go == null)
            {
                return;
            }
            var toggle = go.GetComponent<MultiToggle>();
            if (toggle == null || toggle.states == null || state < 0 || state >= toggle.states.Length)
            {
                return;
            }
            // Update the internal state (so CurrentState / OnChecked agree) and
            // request a refresh.
            toggle.ChangeState(state, forceRefreshState: true);
            // Belt-and-suspenders: also apply the state's sprite and color to the
            // mark image directly. In practice ChangeState's refresh has proven
            // unreliable on live toggles (the radio stayed stuck on its
            // build-time state), so the visual is forced here regardless.
            if (toggle.toggle_image != null)
            {
                toggle.toggle_image.sprite = toggle.states[state].sprite;
                toggle.toggle_image.color = toggle.states[state].color;
            }
#if DEBUG
            PUtil.LogDebug("ForceCheckState -> state={0} sprite={1}".F(state, toggle.toggle_image?.sprite?.name));
#endif
        }

        /// <summary>
        /// Restyles a realized category PCheckBox as a radio button: a solid
        /// white disc marker instead of the square border, and a black dot in
        /// the center instead of the checkmark in the checked state. PCheckBox
        /// builds a MultiToggle whose ChangeState re-applies the per-state sprite
        /// to the mark image on every
        /// state change, so the checked/partial state sprites in the toggle's
        /// state array are swapped for the dot as well; the current state is then
        /// forced to refresh so the new sprites show immediately.
        /// </summary>
        private static void MakeRadioStyle(GameObject checkBoxGo)
        {
            var toggle = checkBoxGo.GetComponent<MultiToggle>();
            var borderGo = checkBoxGo.transform.Find("CheckBox/CheckBorder");
            if (toggle == null || borderGo == null)
            {
                return;
            }
            var borderImage = borderGo.GetComponent<Image>();
            if (borderImage == null)
            {
                return;
            }
            var dot = GetRadioDotSprite();
            // The vanilla border is a 9-sliced square frame; the generated disc
            // is a plain sprite.
            borderImage.sprite = GetRadioBorderSprite();
            borderImage.type = Image.Type.Simple;
            borderImage.preserveAspect = true;
            var states = toggle.states;
            for (int i = 0; i < states.Length; i++)
            {
                var state = states[i];
                if (i == PCheckBox.STATE_CHECKED || i == PCheckBox.STATE_PARTIAL)
                {
                    state.sprite = dot;
                }
                states[i] = state;
            }
            toggle.states = states;
            // MultiToggle.ChangeState re-applies states[i].sprite to the mark
            // image, so force a refresh of the current state.
            toggle.ChangeState(PCheckBox.GetCheckState(checkBoxGo), forceRefreshState: true);
        }

        private static Sprite GetRadioBorderSprite()
        {
            if (s_radioBorder == null)
            {
                s_radioBorder = MakeCircleSprite(hollow: false);
            }
            return s_radioBorder;
        }

        private static Sprite GetRadioDotSprite()
        {
            if (s_radioDot == null)
            {
                s_radioDot = MakeDotSprite(32);
            }
            return s_radioDot;
        }

        /// <summary>
        /// Draws a solid black dot into a runtime texture on a transparent
        /// background: the mark shown in the center of the selected radio.
        /// Being black, it survives PCheckBox's white state tinting.
        /// </summary>
        private static Sprite MakeDotSprite(int size)
        {
            var texture = new Texture2D(size, size);
            float center = size / 2f;
            float dotRadius = size / 3f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(dotRadius - dist + 0.5f);
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Draws a white circle into a runtime texture: a ring (hollow) or a
        /// filled disc (the always-visible white radio marker). PCheckBox tints
        /// the result with its check color.
        /// </summary>
        private static Sprite MakeCircleSprite(bool hollow)
        {
            const int size = 32;
            var texture = new Texture2D(size, size);
            float center = size / 2f;
            float outerRadius = size / 2f - 1f;
            float innerRadius = hollow ? outerRadius * 0.55f : 0f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float outerAlpha = Mathf.Clamp01(outerRadius - dist + 0.5f);
                    float innerAlpha = hollow ? Mathf.Clamp01(dist - innerRadius + 0.5f) : 1f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Min(outerAlpha, innerAlpha)));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Strips the link markup from a localized element name.
        /// </summary>
        private static string StripMarkup(string name)
        {
            return LinkMarkup.Replace(name ?? string.Empty, string.Empty);
        }

        private void ApplySelection()
        {
            foreach (var row in rows)
            {
                var selected = row.Name == selectedCategory;
                // Switching the category closes any other open tree and resets
                // its material sub-selection.
                if (!selected && row.Expanded)
                {
                    CollapseTree(row);
                }
                ForceCheckState(row.CheckBoxGo, selected
                    ? PCheckBox.STATE_CHECKED
                    : PCheckBox.STATE_UNCHECKED);
                // The "+" button is enabled/visible only on the selected row and
                // only if the category has a tree.
                var showExpand = selected && row.TreePanelGo != null;
                row.ExpandButtonGo.SetActive(showExpand);
                PButton.SetButtonEnabled(row.ExpandButtonGo, showExpand);
            }
        }
    }
}
