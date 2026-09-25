using System;
using System.Collections.Generic;
using PeterHan.PLib.Core;
using UnityEngine;

namespace OxygenNotIncluded.Mods
{
    /// <summary>
    /// Per-second, per-(world, tag) cycle-stats state.
    ///
    /// A "cycle" is 600 game-seconds (one game-day, STRINGS.UI.FORMATDAY). One
    /// CycleStatsSampler instance per world container (ISim1000ms, auto-registered
    /// by KMonoBehaviour.Spawn) samples every world resource once per game-second
    /// and accumulates how much of each tag was produced (amount went up) and
    /// consumed (amount went down) in the current cycle and in the previous
    /// completed one. All state lives here, statically, keyed by world id and
    /// tag, and is exposed through the public API below
    /// (IsReady / TryGetCurrent / TryGetPrevious).
    ///
    /// The first SETTLE_GATE game-seconds after the reference time TRef are
    /// skipped: at world-gen / save-load time the inventory is still being
    /// filled and its initial spike must not be counted as consumption. TRef
    /// is captured by Game.OnLoad (save load) or by the first unpause
    /// (PauseChanged, new game); a sampler first-tick pins it as a safety net.
    ///
    /// Every entry point is null-guarded and never throws into the game loop;
    /// an unexpected tick failure logs one PUtil error line and disables
    /// the sampler component (it stops ticking permanently).
    ///
    /// Game-build note (Aquatic 731233): there is no prefab-collection API
    /// (no Components.Prefabs / PrefabCollection in Assembly-CSharp), so the
    /// sampler is a plain KMonoBehaviour on a plain GameObject parented under
    /// the world container — KMonoBehaviour.Start() auto-registers its
    /// ISim1000ms ticks with SimAndRenderScheduler and OnLoadLevel removes it.
    /// </summary>
    public static class CycleStats
    {
        /// <summary>Length of one cycle in game-seconds.</summary>
        public const float CYCLE_SECONDS = 600f;

        /// <summary>Seconds after TRef before sampling is allowed to start.</summary>
        public const float SETTLE_GATE = 30f;

        /// <summary>
        /// A bucket's current and previous-cycle figures. A class on purpose:
        /// the sampler mutates bucket fields in place (Mono/net48 bumps the
        /// Dictionary version on every indexer assignment — even for an
        /// existing key — so a write-back while enumerating would throw).
        /// </summary>
        public class Bucket
        {
            // Current (in-progress) cycle.
            public float Produced;
            public float Consumed;
            /// <summary>World-inventory amount at the last sample (baseline for the next delta).</summary>
            public float LastValue;
            /// <summary>(GetTime() / CYCLE_SECONDS) of the cycle currently being accumulated.</summary>
            public int CycleIndex;
            /// <summary>Game-time of the last sample; 0 until the first sample.</summary>
            public float LastSampleTime;
            /// <summary>True once the baseline sample has been taken.</summary>
            public bool Sampled;

            // Previous completed cycle (valid while PrevComplete).
            public float PrevProduced;
            public float PrevConsumed;
            /// <summary>World-inventory amount at the last sample before the boundary flip.</summary>
            public float PrevBoundaryAmount;
            public bool PrevComplete;
        }

        /// <summary>
        /// Game-time (GameClock.GetTime) at which sampling is allowed to start:
        /// accumulation is skipped while t &lt; TRef + SETTLE_GATE.
        /// float.NegativeInfinity = not captured yet; the first sampler tick
        /// pins it as a safety net.
        /// </summary>
        public static float TRef = float.NegativeInfinity;

        // worldId -> tag -> bucket.
        private static readonly Dictionary<int, Dictionary<Tag, Bucket>> Buckets = new Dictionary<int, Dictionary<Tag, Bucket>>();
        // World container ids that currently have a live sampler instance.
        private static readonly HashSet<int> Worlds = new HashSet<int>();
        // The Game instance the event subscriptions live on (null = none yet).
        private static Game subscribedGame;
        private static bool errorLogged;

        // -----------------------------------------------------------------
        // Public read API (consumed by the tooltip code).
        // -----------------------------------------------------------------

        /// <summary>
        /// True when the settle gate has passed, this world+tag has been
        /// sampled at least once, and the last sample is less than 5
        /// game-seconds old (i.e. the sim is running and the sampler is live).
        /// </summary>
        public static bool IsReady(int worldId, Tag tag)
        {
            if (!HasTRef)
            {
                return false;
            }
            float t = Now();
            if (t < 0f || t < TRef + SETTLE_GATE)
            {
                return false;
            }
            return TryGetBucket(worldId, tag, out Bucket b) && b.Sampled && t - b.LastSampleTime < 5f;
        }

        /// <summary>
        /// Produced/consumed amounts accumulated in the current (in-progress)
        /// cycle, plus how many game-seconds of that cycle have elapsed
        /// (0..CYCLE_SECONDS). False until the first baseline sample.
        /// </summary>
        public static bool TryGetCurrent(int worldId, Tag tag, out float produced, out float consumed, out float elapsedSeconds)
        {
            produced = 0f;
            consumed = 0f;
            elapsedSeconds = 0f;
            if (!TryGetBucket(worldId, tag, out Bucket b) || !b.Sampled)
            {
                return false;
            }
            float t = Now();
            produced = b.Produced;
            consumed = b.Consumed;
            elapsedSeconds = Math.Min(t - b.CycleIndex * CYCLE_SECONDS, CYCLE_SECONDS);
            if (elapsedSeconds < 0f)
            {
                elapsedSeconds = 0f;
            }
            return true;
        }

        /// <summary>
        /// Produced/consumed amounts of the previous completed cycle plus the
        /// inventory amount at the boundary. False until a cycle boundary has
        /// been crossed (PrevComplete).
        /// </summary>
        public static bool TryGetPrevious(int worldId, Tag tag, out float produced, out float consumed, out float boundaryAmount)
        {
            produced = 0f;
            consumed = 0f;
            boundaryAmount = 0f;
            if (!TryGetBucket(worldId, tag, out Bucket b) || !b.PrevComplete)
            {
                return false;
            }
            produced = b.PrevProduced;
            consumed = b.PrevConsumed;
            boundaryAmount = b.PrevBoundaryAmount;
            return true;
        }

        // -----------------------------------------------------------------
        // Registration / lifecycle (driven from Mod.cs and the sampler).
        // -----------------------------------------------------------------

        /// <summary>
        /// Idempotent bootstrap: capture TRef hooks (Game.OnLoad for save
        /// loads, GameHashes.PauseChanged for new games), subscribe
        /// GameHashes.WorldAdded for worlds added later (rocket interiors),
        /// and make sure the active world has a sampler instance. Safe to
        /// call any number of times; it is a no-op until the game scene
        /// exists and resubscribes on a new Game instance after each load.
        /// </summary>
        public static void EnsureStarted()
        {
            Game game = Game.Instance;
            if (game == null)
            {
                return;
            }
            if (subscribedGame == game)
            {
                SpawnForActiveWorld();
                return;
            }
            subscribedGame = game;
            try
            {
                game.OnLoad += OnGameLoad;
                // GameHashes is an ENUM in this build — cast to the int hash.
                game.Subscribe((int)GameHashes.PauseChanged, OnPauseChanged);
                ClusterManager cluster = ClusterManager.Instance;
                if (cluster != null)
                {
                    cluster.Subscribe((int)GameHashes.WorldAdded, OnWorldAdded);
                }
                SpawnForActiveWorld();
#if DEBUG
                PUtil.LogDebug("cycle stats sampler registered");
#endif
            }
            catch (System.Exception e)
            {
                LogOnce("failed to register the cycle stats sampler: " + e, e);
            }
        }

        /// <summary>
        /// Called from the WorldContainer.OnSpawn postfix (Mod.cs): every
        /// spawned world container gets its own sampler instance.
        /// </summary>
        public static void OnWorldContainerSpawned(WorldContainer wc)
        {
            try
            {
                EnsureStarted();
                SpawnForWorld(wc);
            }
            catch (System.Exception e)
            {
                LogOnce("failed to start the cycle stats sampler for a world: " + e, e);
            }
        }

        /// <summary>
        /// Spawns this mod's sampler under the given world container (once).
        /// There is no prefab registry in this build: a plain GameObject with
        /// the KMonoBehaviour on it is enough — Start() auto-registers the
        /// ISim1000ms ticks and OnLoadLevel removes them.
        /// </summary>
        public static void SpawnForWorld(WorldContainer wc)
        {
            try
            {
                if (wc == null || wc.id < 0 || wc.gameObject == null)
                {
                    return;
                }
                if (!Worlds.Add(wc.id))
                {
                    return; // this world already has a sampler.
                }
                GameObject go = new GameObject("ResourceRemainCycleStats");
                go.transform.SetParent(wc.transform, false);
                CycleStatsSampler sampler = go.AddComponent<CycleStatsSampler>();
                sampler.WorldId = wc.id;
#if DEBUG
                PUtil.LogDebug("cycle stats sampler spawned for world {0}".F(wc.id));
#endif
            }
            catch (System.Exception e)
            {
                if (wc != null)
                {
                    Worlds.Remove(wc.id);
                }
                LogOnce("failed to spawn the cycle stats sampler: " + e, e);
            }
        }

        /// <summary>
        /// Defensive: make sure the active world has a sampler instance
        /// (covers any bootstrap race, e.g. the world container spawning
        /// before ClusterManager/Game are ready).
        /// </summary>
        public static void SpawnForActiveWorld()
        {
            try
            {
                ClusterManager cluster = ClusterManager.Instance;
                if (cluster == null)
            {
                return;
            }
            SpawnForWorld(cluster.activeWorld);
        }
        catch (System.Exception e)
        {
            LogOnce("failed to spawn the cycle stats sampler for the active world: " + e, e);
        }
    }

        /// <summary>
        /// Called from the sampler's OnLoadLevel: the world is gone, forget it
        /// so a future world with the same id can get a fresh instance.
        /// </summary>
        public static void ForgetWorld(int worldId)
        {
            Worlds.Remove(worldId);
            Buckets.Remove(worldId);
        }

        /// <summary>
        /// True once the active world (ClusterManager.activeWorldId) is tracked.
        /// Used by the sampler's defensive first-tick spawn.
        /// </summary>
        public static bool HasWorld(int worldId)
        {
            return Worlds.Contains(worldId);
        }

        /// <summary>
        /// The sampler's per-tick hook: returns the (created if needed)
        /// per-world bucket dictionary.
        /// </summary>
        public static Dictionary<Tag, Bucket> GetOrCreateWorldBuckets(int worldId)
        {
            if (!Buckets.TryGetValue(worldId, out Dictionary<Tag, Bucket> tags))
            {
                tags = new Dictionary<Tag, Bucket>();
                Buckets[worldId] = tags;
            }
            return tags;
        }

        // -----------------------------------------------------------------
        // Event handlers.
        // -----------------------------------------------------------------

        // Fired by Game.Load (SaveLoader.cs:440) after the save's GameSaveData is
        // read: for a save load the GameClock is already restored by then
        // (ISaveLoadable, deserialized during saveManager.Load before
        // Game.Instance.Load), so TRef = load time; the settle gate then skips
        // the first 30 game-seconds. A new game does NOT go through Game.Load,
        // so for those the PauseChanged fallback below captures TRef instead.
        // The old Game object is destroyed with the old scene, so the
        // subscriptions die with it: subscribedGame is cleared and the new
        // Game is (re)subscribed on its first world container's OnSpawn.
        private static void OnGameLoad(Game.GameSaveData saveData)
        {
            try
            {
                Buckets.Clear();
                Worlds.Clear();
                subscribedGame = null;
                // Capture TRef here: on a save load the GameClock is already
                // restored by this point (ISaveLoadable, deserialized during
                // saveManager.Load before Game.Instance.Load). If the clock
                // is somehow not ready yet, leave it unset — the PauseChanged
                // / first-tick pins below cover that case.
                GameClock clock = GameClock.Instance;
                TRef = clock != null ? clock.GetTime() : float.NegativeInfinity;
#if DEBUG
                PUtil.LogDebug("cycle stats state reset on save load, TRef = {0:F1}".F(TRef));
#endif
            }
            catch (System.Exception e)
            {
                LogOnce("failed to reset the cycle stats state on save load: " + e, e);
            }
        }

        // GameHashes.PauseChanged (Game.LateUpdate) delivers Boxed<bool> IsPaused:
        // value == false means the game just UNPAUSED. On a new game the first
        // unpause happens after world gen finished, so it is the right moment
        // to pin TRef (Game.OnLoad never fires on the new-game path).
        private static void OnPauseChanged(object data)
        {
            try
            {
                if (data is Boxed<bool> boxed && !boxed.value && !HasTRef)
                {
                    GameClock clock = GameClock.Instance;
                    if (clock != null)
                    {
                        TRef = clock.GetTime();
#if DEBUG
                        PUtil.LogDebug("cycle stats TRef captured on first unpause: {0:F1}".F(TRef));
#endif
                    }
                }
            }
            catch (System.Exception e)
            {
                LogOnce("failed on the PauseChanged handler: " + e, e);
            }
        }

        // GameHashes.WorldAdded (ClusterManager.CreateRocketInteriorWorld)
        // delivers Boxed<int> — the new WorldContainer.id. In this build it is
        // the only WorldAdded fire site (the main world on save load / new game
        // does NOT fire it — those come from the WorldContainer.OnSpawn
        // postfix in Mod.cs).
        private static void OnWorldAdded(object data)
        {
            try
            {
                if (data is Boxed<int> boxed)
                {
                    ClusterManager cluster = ClusterManager.Instance;
                    WorldContainer wc = cluster != null ? cluster.GetWorld(boxed.value) : null;
                    if (wc != null)
                    {
                        SpawnForWorld(wc);
                    }
                }
            }
            catch (System.Exception e)
            {
                LogOnce("failed on the WorldAdded handler: " + e, e);
            }
        }

        // -----------------------------------------------------------------
        // Helpers.
        // -----------------------------------------------------------------

        private static bool HasTRef
        {
            get
            {
                return !float.IsNaN(TRef) && !float.IsInfinity(TRef) && TRef >= 0f;
            }
        }

        private static float Now()
        {
            GameClock clock = GameClock.Instance;
            return clock == null ? float.NegativeInfinity : clock.GetTime();
        }

        private static bool TryGetBucket(int worldId, Tag tag, out Bucket bucket)
        {
            // bucket is null when the world/tag has no bucket (Bucket is a class
            // now); callers only dereference it after TryGetBucket returned true.
            bucket = null!;
            return Buckets.TryGetValue(worldId, out Dictionary<Tag, Bucket> tags) && tags.TryGetValue(tag, out bucket);
        }

        private static void LogOnce(string message, System.Exception e)
        {
            if (!errorLogged)
            {
                errorLogged = true;
                PUtil.LogError(message);
            }
        }
    }

    /// <summary>
    /// One instance per world container: samples every world resource once
    /// per game-second (ISim1000ms) and feeds the static CycleStats state.
    /// The component is added to a plain GameObject parented under the world
    /// container (see CycleStats.SpawnForWorld); KMonoBehaviour.Start()
    /// auto-registers the sim ticks and OnLoadLevel removes them.
    /// </summary>
    public class CycleStatsSampler : KMonoBehaviour, ISim1000ms
    {
        /// <summary>World container id this instance samples (set by CycleStats.SpawnForWorld).</summary>
        public int WorldId;

        private WorldContainer worldContainer;
        private readonly List<Tag> tagScratch = new List<Tag>();
        private readonly HashSet<Tag> tagSeen = new HashSet<Tag>();
        private readonly List<Tag> finalizeScratch = new List<Tag>();

        // OnSpawn/OnLoadLevel are public in the publicized game assemblies
        // (AssemblyPublicizer rewrites protected virtual → public virtual).
        public override void OnSpawn()
        {
            base.OnSpawn();
            if (WorldId < 0)
            {
                WorldContainer wc = GetComponentInParent<WorldContainer>();
                WorldId = wc != null ? wc.id : -1;
            }
            worldContainer = GetComponentInParent<WorldContainer>();
        }

        public override void OnLoadLevel()
        {
            CycleStats.ForgetWorld(WorldId);
            base.OnLoadLevel();
        }

        public void Sim1000ms(float dt)
        {
            try
            {
                if (WorldId < 0 || !CycleStats.HasWorld(WorldId))
                {
                    return; // stale instance (e.g. old world after a save load).
                }
                // First-tick safety: pin TRef if no event captured it yet, so
                // the settle gate cannot block forever.
                if (!IsTRefSet())
                {
                    GameClock clock0 = GameClock.Instance;
                    if (clock0 != null)
                    {
                        CycleStats.TRef = clock0.GetTime();
#if DEBUG
                        PUtil.LogDebug("cycle stats TRef pinned on first tick: {0:F1}".F(CycleStats.TRef));
#endif
                    }
                    return; // start accumulating from the next tick.
                }
                GameClock clock = GameClock.Instance;
                WorldInventory inventory = worldContainer != null ? worldContainer.worldInventory : null;
                if (clock == null || inventory == null || inventory.Inventory == null)
                {
                    return;
                }

                float t = clock.GetTime();
                int ci = (int)(t / CycleStats.CYCLE_SECONDS);
                Dictionary<Tag, CycleStats.Bucket> tags = CycleStats.GetOrCreateWorldBuckets(WorldId);

                // 1) Finalize every existing bucket that crossed a cycle boundary
                //    (even tags no longer present in the inventory this tick).
                //    Two phases on the same tick: first collect the keys that
                //    need finalization, then mutate the buckets in place —
                //    assigning to the dictionary while enumerating it would
                //    throw (Mono/net48 bumps the version even on existing keys).
                finalizeScratch.Clear();
                foreach (KeyValuePair<Tag, CycleStats.Bucket> kv in tags)
                {
                    if (kv.Value != null && kv.Value.CycleIndex == ci)
                    {
                        continue;
                    }
                    finalizeScratch.Add(kv.Key);
                }
                foreach (Tag tag in finalizeScratch)
                {
                    CycleStats.Bucket b;
                    if (!tags.TryGetValue(tag, out b) || b == null)
                    {
                        continue; // defensive: gone between the two phases
                    }
                    b.PrevProduced = b.Produced;
                    b.PrevConsumed = b.Consumed;
                    b.PrevBoundaryAmount = b.LastValue;
                    b.PrevComplete = true;
                    b.Produced = 0f;
                    b.Consumed = 0f;
                    b.CycleIndex = ci;
                }

                // 2) Settle gate: skip accumulation while the world inventory
                //    is still filling in (first 30 game-seconds after TRef).
                if (t < CycleStats.TRef + CycleStats.SETTLE_GATE)
                {
                    return;
                }

                // 3) Sample the current tag set: union of the world inventory's
                //    own tag keys (Inventory) and its derived amount map
                //    (accessibleAmounts). World has no "resources" set in this
                //    build — the inventory IS the world's resource set.
                tagSeen.Clear();
                foreach (Tag tag in inventory.Inventory.Keys)
                {
                    tagSeen.Add(tag);
                }
                if (inventory.accessibleAmounts != null)
                {
                    foreach (Tag tag in inventory.accessibleAmounts.Keys)
                    {
                        tagSeen.Add(tag);
                    }
                }

                tagScratch.Clear();
                tagScratch.AddRange(tagSeen);

                foreach (Tag tag in tagScratch)
                {
                    float amount = inventory.GetAmount(tag, false);
                    if (amount < 0f)
                    {
                        amount = 0f;
                    }
                    if (!tags.TryGetValue(tag, out CycleStats.Bucket b) || !b.Sampled)
                    {
                        // First sample after the gate: baseline only, no delta.
                        // A fresh bucket is stored once; existing buckets are
                        // mutated in place below (no dictionary write needed).
                        b = new CycleStats.Bucket
                        {
                            CycleIndex = ci,
                            LastValue = amount,
                            LastSampleTime = t,
                            Sampled = true
                        };
                        tags[tag] = b;
                    }
                    else
                    {
                        float delta = amount - b.LastValue;
                        if (delta >= 0f)
                        {
                            b.Produced += delta;
                        }
                        else
                        {
                            b.Consumed += -delta;
                        }
                        b.LastValue = amount;
                        b.LastSampleTime = t;
                    }
                }
            }
            catch (System.Exception e)
            {
                // One-shot error, then the sampler really stops ticking.
                if (!errorLogged)
                {
                    errorLogged = true;
                    PUtil.LogError("cycle stats tick failed, sampler disabled: " + e);
                }
                this.enabled = false;
            }
        }

        private static bool errorLogged;

        private static bool IsTRefSet()
        {
            return !float.IsNaN(CycleStats.TRef) && !float.IsInfinity(CycleStats.TRef) && CycleStats.TRef >= 0f;
        }
    }
}
