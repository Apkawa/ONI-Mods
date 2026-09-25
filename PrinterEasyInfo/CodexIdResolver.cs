using STRINGS;
using UnityEngine;
using PeterHan.PLib.Core;

namespace PrinterEasyInfo
{
    /// <summary>
    /// Resolves a care-package item id string (CarePackageInfo.id) to a codex
    /// (tech database) entry id, mirroring the game's two-way id resolution.
    /// </summary>
    public static class CodexIdResolver
    {
        /// <summary>
        /// Resolves the codex entry id for the given item id.
        /// Branching mirrors <c>CarePackageContainer.SetAnimator()</c> (prefab first
        /// via <c>Assets.TryGetPrefab(id.ToTag())</c>, element fallback via
        /// <c>ElementLoader.GetElement(id.ToTag())</c>); codex id computation mirrors
        /// <c>DetailsScreen.CodexEntryButton_GetCodexId()</c>, including its final
        /// codex-existence check.
        /// </summary>
        /// <param name="itemId">
        /// The item id string: either a prefab/config id (e.g. "FieldRation")
        /// or an element tag (e.g. "Water").
        /// </param>
        /// <returns>
        /// The codex entry id string, or "" when the id resolves to neither a
        /// prefab nor an element, or when the candidate id has no codex entry
        /// (top-level entry or sub-entry, see DetailsScreen.CodexEntryButton_GetCodexId()).
        /// </returns>
        public static string ResolveCodexId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return "";
            }

            string codexId = "";

            // Prefab branch first, exactly like CarePackageContainer.SetAnimator():
            //   GameObject prefab = Assets.GetPrefab(info.id.ToTag());
            //   if (prefab != null) { ... }
            // Assets.TryGetPrefab is the silent variant of Assets.GetPrefab (no
            // "Missing prefab" warning), so element ids don't spam the game log;
            // behavior is otherwise identical.
            GameObject prefab = Assets.TryGetPrefab(itemId.ToTag());
            if (prefab != null)
            {
                // DetailsScreen.CodexEntryButton_GetCodexId():
                //   text = UI.ExtractLinkID(component.GetProperName());
                //   if (string.IsNullOrEmpty(text))
                //       text = CodexCache.FormatLinkID(component.PrefabID().ToString());
                codexId = UI.ExtractLinkID(prefab.GetProperName());
                if (string.IsNullOrEmpty(codexId))
                {
                    codexId = CodexCache.FormatLinkID(prefab.PrefabID().ToString());
                }
                return HasCodexEntry(itemId, codexId) ? codexId : "";
            }

            // Element fallback, exactly like the SetAnimator() else-branch:
            //   ElementLoader.GetElement(info.id.ToTag())
            Element element = ElementLoader.GetElement(itemId.ToTag());
            if (element == null)
            {
#if DEBUG
                PUtil.LogDebug("ResolveCodexId: item id {0} — ни prefab, ни элемент, codex id нет".F(itemId));
#endif
                return "";
            }

            // DetailsScreen.CodexEntryButton_GetCodexId():
            //   text = CodexCache.FormatLinkID(component6.Element.id.ToString());
            codexId = CodexCache.FormatLinkID(element.id.ToString());
            return HasCodexEntry(itemId, codexId) ? codexId : "";
        }

        /// <summary>
        /// Codex-existence validation mirroring the final check in
        /// <c>DetailsScreen.CodexEntryButton_GetCodexId()</c> (DetailsScreen.cs:542):
        /// the candidate id is accepted only if it is a top-level codex entry
        /// (<c>CodexCache.entries</c>, a <c>Dictionary&lt;string, CodexEntry&gt;</c>,
        /// CodexCache.cs:12) or a sub-entry (<c>CodexCache.FindSubEntry(string)</c>,
        /// CodexCache.cs:204).
        /// </summary>
        private static bool HasCodexEntry(string itemId, string codexId)
        {
            if (CodexCache.entries == null)
            {
                // Rare internal failure: the codex cache is not (yet) initialized.
                PUtil.LogWarning("ResolveCodexId: CodexCache.entries == null, codex cache недоступен");
                return false;
            }

            bool exists = CodexCache.entries.ContainsKey(codexId) || CodexCache.FindSubEntry(codexId) != null;
            if (!exists)
            {
#if DEBUG
                PUtil.LogDebug("ResolveCodexId: item id {0} → codex id {1} — записи в codex нет".F(itemId, codexId));
#endif
            }
            return exists;
        }
    }
}
