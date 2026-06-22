namespace POE2Radar.Core.Game;

/// <summary>
/// PoE2 memory offsets — the going-forward source of truth, sourced from the GameHelper2
/// <c>GameOffsets/</c> dump and validated against the live client where marked ✓.
///
/// <para>This is separate from the legacy PoE1-shaped <see cref="KnownOffsets"/> (which the
/// overlay still references and which is being migrated). As each PoE2 structure is validated
/// here, the corresponding overlay reader is rechained to use it.</para>
///
/// Markers: ✓ = confirmed against live PoE2; (GH2) = from GameHelper2, not yet live-checked.
/// </summary>
public static class Poe2
{
    /// <summary>Tile→world = 250, tile→grid = 23 ⇒ world/grid ratio ≈ 10.8696. ✓</summary>
    public const float WorldToGridRatio = 250f / 23f;

    /// <summary>Conservative network-bubble radius in grid units (GH2 uses 150). </summary>
    public const int NetworkBubbleGrid = 150;

    /// <summary>
    /// GameState root — found via the "Game States" AOB pattern (<see cref="AobPatterns"/>).
    /// Holds the array of game-state slots; one of them is InGameState.
    /// </summary>
    public static class GameState
    {
        public const int CurrentStatePtr = 0x08;  // (GH2) StdVector — current state
        public const int States          = 0x48;  // (GH2) inline array of 12 × StdTuple2D<IntPtr> (16 bytes each)
        public const int StateSlotStride = 0x10;   // each slot is StdTuple2D<IntPtr> (ptr + extra)
        public const int StateSlotCount  = 12;
    }

    /// <summary>
    /// InGameState. Resolve it from <c>GameState.CurrentStatePtr</c> (StdVector @ +0x08): the
    /// vector's first element is the active state pointer when in-game. ✓ (matches States[] slot).
    /// </summary>
    public static class InGameState
    {
        public const int AreaInstanceData = 0x290; // ✓ → AreaInstance (validated: target holds the local player)
        public const int UiRoot           = 0x2F0; // ✓ → root UiElement (self-ref; children are UI elements)
        public const int Camera           = 0x368; // ✓ → Camera object (Zoom @ +0x528 == 1.0 confirmed)
        public const int WorldData        = 0x310; // (GH2-drift) → WorldData (area name + camera) — TBD
        public const int UiRootStructPtr  = 0x340; // (GH2-drift) reads 0 here — TBD
    }

    public static class UiRootStruct
    {
        public const int UiRootPtr = 0x5A8; // (GH2)
        public const int GameUiPtr = 0xBF0; // (GH2)
    }

    /// <summary>
    /// The big per-area container: area metadata, player, entity maps, terrain.
    /// <para>⚠ GameHelper2's internal offsets are DRIFTED in this build — confirmed by the live
    /// probe (PlayerInfo moved from GH2's 0xA00 to ~0x580; LocalPlayer at 0x5A0). The values
    /// marked (GH2-drift) below must be re-discovered (see <c>--find-entities</c> / <c>--find-terrain</c>).</para>
    /// </summary>
    public static class AreaInstance
    {
        public const int AreaInfoPtr      = 0x0A0;  // ✓ → AreaInfo; +0x00 → UTF-16 "Code\0Name\0" (Code validated 'G1_town')
        public const int LocalPlayer      = 0x5A0;  // ✓ → player Entity (value-scanned player matched here)
        public const int ServerDataPtr    = 0x580;  // candidate (heap ptr just before the player slot)
        public const int AwakeEntities    = 0x6C0;  // ✓ StdMap of live entities (id→EntityPtr); validated size=378
        public const int SleepingEntities = 0x6D0;  // ✓ StdMap (validated size=58)
        public const int TerrainMetadata  = 0x8A0;  // ✓ TerrainStruct base (GH2's 0xD20 drifted)
        public const int CurrentAreaLevel = 0x0C4;  // ✓ int — per-area, validated 27/32 (GH2's 0xBC drifted)
        public const int CurrentAreaHash  = 0x11C;  // ✓ uint — per-area random hash (GH2's 0xFC drifted; +0x120 paired seed)
    }

    /// <summary>Entity StdMap conventions. Maps live at AreaInstance+0x6C0 (Awake) / +0x6D0 (Sleeping).</summary>
    public static class EntityList
    {
        public const int StdMapSize = 0x10; // each StdMap is {Head ptr, int Size, pad} = 16 bytes
        /// <summary>Entity ids below this are real entities; above are visuals/decorations (GH2 filter). ✓ confirmed live.</summary>
        public const uint VisualIdThreshold = 0x40000000;
    }

    /// <summary>std::map node: Left/Parent/Right ptrs, Color, IsNil byte, then Data{Key,Value} @ +0x20.</summary>
    public static class StdMapNode
    {
        public const int Left   = 0x00;
        public const int Parent = 0x08;
        public const int Right  = 0x10;
        public const int IsNil  = 0x19; // bool
        public const int Data   = 0x20; // Key (EntityNodeKey: uint id + pad = 8 bytes), then Value (IntPtr EntityPtr)
        public const int KeyId  = 0x20; // uint entity id
        public const int ValueEntityPtr = 0x28; // IntPtr
    }

    /// <summary>An Entity object.</summary>
    public static class Entity
    {
        public const int EntityDetailsPtr = 0x08; // ✓ → EntityDetails
        public const int ComponentList    = 0x10; // ✓ StdVector of component pointers (8-byte elems)
        public const int Id               = 0x80; // (GH2) uint  (read 0 for local player — revisit)
        public const int IsValid          = 0x84; // (GH2) byte; valid when bit0 clear
    }

    public static class EntityDetails
    {
        public const int Name              = 0x08; // ✓ StdWString — metadata path (e.g. Metadata/Characters/<Class>/<Variant>)
        public const int ComponentLookUpPtr = 0x28; // ✓ → ComponentLookUp
    }

    /// <summary>ComponentLookUp: a StdBucket of (NamePtr, Index) at +0x28; index → ComponentList[index].</summary>
    public static class ComponentLookUp
    {
        public const int NameAndIndexBucket = 0x28; // ✓ StdBucket; its Data StdVector starts here
        public const int EntryStride        = 0x10; // ✓ {IntPtr NamePtr; int Index; int pad}
    }

    // ── Components (offsets from the component object base) ───────────────────

    /// <summary>Life — ✓ re-validated live 2026-06-04 after the patch (980/980 HP, 427 mana, 274 ES).
    /// The vital blocks slid (each grew ~8 bytes): Health 0x1A8→0x1B0, Mana 0x1F8→0x208, ES 0x230→0x248.
    /// The VitalStruct's internal layout (Max@+0x2C, Current@+0x30) was UNCHANGED — only these
    /// per-vital offsets moved. (Prior build: 442/442 HP, 271 mana, 186/186 ES at 0x1A8/0x1F8/0x230.)</summary>
    public static class Life
    {
        public const int Owner        = 0x008; // ComponentHeader.EntityPtr (back-pointer to entity)
        public const int Health       = 0x1B0; // ✓ VitalStruct (was 0x1A8 pre-patch)
        public const int Mana         = 0x208; // ✓ VitalStruct (was 0x1F8 pre-patch)
        public const int EnergyShield = 0x248; // ✓ VitalStruct (was 0x230 pre-patch)
    }

    /// <summary>VitalStruct — ✓ (Max/Current confirmed). Reuse <see cref="VitalStruct"/> for reads.</summary>
    public static class Vital
    {
        public const int ReservedFlat = 0x10;
        public const int Regen        = 0x28;
        public const int Max          = 0x2C; // ✓
        public const int Current      = 0x30; // ✓
    }

    /// <summary>Render component. Byte setter: sub_1415B9C20 (IDA).</summary>
    public static class Render
    {
        public const int AlwaysShowHover      = 0x77;  // (IDA) byte — entity always highlights on hover
        public const int HideHover            = 0x78;  // (IDA) byte — entity never highlights
        public const int HideMiniLifeBar      = 0x79;  // (IDA) byte — hides in-game HP bar ✓
        public const int HideInfoDisplay      = 0x7A;  // (IDA) byte — hides info tooltip
        public const int NoSelectionBox       = 0x81;  // (IDA) byte — disables click targeting
        public const int DisableRendering     = 0x82;  // (IDA) byte — makes entity invisible
        public const int HideAllBuffVisuals   = 0x83;  // (IDA) byte — strips buff/debuff VFX
        public const int ForceOutline         = 0x85;  // (IDA) byte — force_outline_no_alphatest
        public const int HideTalismanIcon     = 0x7B;  // (IDA) byte — hides overhead icon
        public const int CurrentWorldPosition = 0x138; // ✓ Vector3 (X,Y,Z); grid = XY / WorldToGridRatio
        public const int ModelBounds          = 0x144; // candidate (3 floats right after world pos)
    }

    /// <summary>Player component — character name + level. ✓ validated (name StdWString, level byte 27).</summary>
    public static class PlayerComponent
    {
        public const int Name  = 0x1B0; // ✓ StdWString
        public const int Level = 0x204; // ✓ byte (low byte of a u32 slot)
    }

    /// <summary>Camera object (at InGameState+0x368). Holds the WorldToScreen matrix.</summary>
    public static class Camera
    {
        // The matrix is stored duplicated (two identical 0x40-byte copies back-to-back); the first
        // copy is at +0x1A0. Row-major Matrix4x4; screen = project(world * M). Validated visually.
        public const int WorldToScreenMatrix = 0x1A0;
        public const int Zoom = 0x528; // float, == 1.0 confirmed
    }

    /// <summary>MinimapIcon component — present on entities the game marks as map POIs (waypoints,
    /// checkpoints, league encounters…). <see cref="CompletedState"/> is an int the game flips when a
    /// repeatable encounter is finished: it then FADES the icon rather than removing it. ✓ validated
    /// live on an Expedition2Encounter — 0 while not-started/ready/active/looting, 1 after the reward
    /// was claimed. Read it live (don't cache the value): the component stays put; only the flag flips.</summary>
    public static class MinimapIcon
    {
        public const int CompletedState = 0x10; // ✓ int — 0 = active/shown, non-zero = completed/faded
    }

    /// <summary>
    /// StateMachine component. The primary state-name pointer and parallel value vector were
    /// validated live on Essence and Expedition encounter entities.
    /// </summary>
    public static class StateMachine
    {
        public const int ListenerVec = 0x20; // StdVector of listener-node ptrs; used by Runeshape monolith station lookup.
        public const int StatesPtr = 0x158;
        public const int StateValues = 0x160; // StdVector<long>
    }

    /// <summary>
    /// RuneStation is the heap object behind a Runeshape monolith device. Sikaka v0.14.x uses the
    /// StateMachine listener vector to reach it from an Expedition2Encounter entity and expose
    /// hole count plus anchor rune without opening the Runeshape panel.
    /// </summary>
    public static class RuneStation
    {
        public const int Owner = 0x10;
        public const int AnchorRef = 0x28;
        public const int AnchorHolder = 0x30;
        public const int HoleCount = 0x38;
        public const int AnchorPos = 0x3C;
        public const int ListenerSub = 0x98;
        public const int RuneStride = 0x6C;
        public const int RuneCount = 34;
    }

    /// <summary>ObjectMagicProperties component - monster/chest rarity.</summary>
    public static class ObjectMagicProperties
    {
        // ✓ validated live across 21 monsters (values 0 and 2 seen). Enum: 0=Normal,1=Magic,2=Rare,3=Unique.
        public const int Rarity = 0x144;
        public const int Mods = 0x168;
        public const int ModElemStride = 0x20;
        public const int ModRecordPtr = 0x8;
        public const int ModIdString = 0x0;
    }

    public static class WorldItemComponent
    {
        public const int ItemEntity = 0x28;
    }

    public static class RenderItemComponent
    {
        public const int ResourcePath = 0x28;
    }

    public static class BaseComponent
    {
        public const int NameRow = 0x10;
        public const int RowDisplayName = 0x30;
    }

    public static class ModsComponent
    {
        public const int Rarity = 0x94;
        public const int Identified = 0x90;
        public const int ImplicitMods = 0xA0;
        public const int ExplicitMods = 0xB8;
        public const int EnchantMods = 0xD0;
        public const int ModArrayStride = 0x40;
        public const int ModRecordPtr = 0x28;
        public const int ModRecordIdPtr = 0x00;
    }

    public static class StackComponent
    {
        public const int Count = 0x18;
    }

    public static class ServerData
    {
        public const int PlayerServerDataVec = 0x48;
        public const int PlayerInventoriesVec = 0x320;
        public const int InvArrayStride = 0x18;
        public const int InvArrayId = 0x00;
        public const int InvArrayPtr = 0x08;
    }

    public static class Inventory
    {
        public const int TotalBoxesX = 0x150;
        public const int TotalBoxesY = 0x154;
        public const int ItemListVec = 0x170;
        public const int ServerRequestCounter = 0x1E8;
    }

    public static class InventoryItem
    {
        public const int Item = 0x00;
        public const int SlotStartX = 0x08;
        public const int SlotStartY = 0x0C;
        public const int SlotEndX = 0x10;
        public const int SlotEndY = 0x14;
    }

    public static class StatsComponent
    {
        public const int StatArrayStride = 0x08;
        public const int ItemLocalStatsVec = 0x20;
        public const int StatsChangedByItemsPtr = 0x160;
        public const int StatsStructStatsVec = 0xF8;
    }

    /// <summary>Chest component. ✓ validated live (opened chest = 0, closed = 1 at +0x168).
    /// Byte setter: sub_141CDF2A0 (IDA). Additional fields from .ot file string search.</summary>
    public static class ChestComponent
    {
        public const int OpeningDestroys = 0x20; // (IDA) byte — chest destroyed on open
        public const int Large           = 0x21; // (IDA) byte — large chest flag
        public const int Locked          = 0x25; // (IDA) byte — chest is locked
        public const int OpenState       = 0x168; // ✓ 1 = closed/openable, 0 = opened/used
    }

    /// <summary>Positioned component. Byte setter: sub_141CFE050, int setter: sub_141CFDAD0 (IDA).</summary>
    public static class Positioned
    {
        public const int ObjectSize  = 0x10;  // (IDA) byte — entity collision size
        public const int Team        = 0x16;  // (IDA) word — team id (1 = player)
        public const int Blocking    = 0x1A;  // (IDA) byte — blocks pathing
        public const int DoesNotPushWhenPushed = 0x21; // (IDA) byte
        public const int IgnoreBeingPushed     = 0x22; // (IDA) byte
        public const int PhaseThrough          = 0x1E; // (IDA) byte — phase_through_small_gaps_of_blocking_terrain
        public const int Scale       = 0x74;  // (IDA) float — visual scale (value/100 from .ot)
        // ✓ validated live: player (friendly) = 0x01, hostile MastodonBoss = 0x00.
        public const int Reaction    = 0x1E0;
    }

    /// <summary>Monster component. Byte setter: sub_141CBA7F0 (IDA).</summary>
    public static class MonsterComponent
    {
        public const int IsBoss              = 0x27; // (IDA) byte
        public const int FlipEnabled         = 0x26; // (IDA) byte
        public const int DisableDefaultStats = 0x24; // (IDA) byte
    }

    /// <summary>Targetable component. Byte setter: sub_1417263A0 (IDA).</summary>
    public static class Targetable
    {
        public const int Attackable     = 0x17; // (IDA) byte
        public const int IsTargetable   = 0x18; // (IDA) byte
        public const int ForceTarget    = 0x19; // (IDA) byte
        public const int NoHighlight    = 0x1A; // (IDA) byte
    }

    /// <summary>Pathfinding component. Int setter: sub_141CBD6E0 (IDA).</summary>
    public static class PathfindingComponent
    {
        public const int BaseSpeed       = 0xEC; // (IDA) int — movement speed
        public const int AvoidOthers     = 0xD0; // (IDA) byte
        public const int Flying          = 0xE5; // (IDA) byte
        public const int MaintainHeight  = 0xE8; // (IDA) byte
    }

    /// <summary>AreaTransition component. Float setter: sub_141CD2AA0 (IDA).</summary>
    public static class AreaTransitionComponent
    {
        public const int GracePeriod    = 0x18; // (IDA) float
        public const int TeleportDelay  = 0x1C; // (IDA) float
    }

    /// <summary>InteractionAction component. Byte/Int/Float setters (IDA).</summary>
    public static class InteractionActionComponent
    {
        public const int DistanceOverride        = 0x1C2; // (IDA) byte
        public const int ConsoleDistanceOverride = 0x1C3; // (IDA) byte
        public const int ForceHumanForm          = 0x1BF; // (IDA) byte
        public const int MinionsCanInteract      = 0x1CA; // (IDA) byte
        public const int InteractionDuration     = 0x1E4; // (IDA) float
    }

    /// <summary>Animated component. Byte setter: sub_141CC9620 (IDA).</summary>
    public static class AnimatedComponent
    {
        public const int ContinueAnimations       = 0xC1; // (IDA) byte
        public const int SerialiseAnimProgress     = 0xC2; // (IDA) byte
        public const int AlwaysInterpolateBearing  = 0xC4; // (IDA) byte
    }

    /// <summary>Transitionable component. Int setter: sub_141D290F0 (IDA).</summary>
    public static class TransitionableComponent
    {
        public const int NumStates                = 0xB0; // (IDA) byte (2-127)
        public const int TransitionOnDamageTaken  = 0xB1; // (IDA) byte
    }

    /// <summary>NPC component. Byte setter: sub_141CBEFD0 (IDA).</summary>
    public static class NpcComponent
    {
        public const int MarkerEnabled     = 0x20; // (IDA) byte
    }

    /// <summary>Sockets component. Internal struct — not exposed via .ot scripts.</summary>
    public static class SocketsComponent
    {
        public const int Owner = 0x08;
        public const int SocketedItems = 0x30;
        public const int SocketSlotsBegin = 0x30; // (IDA) StdVector of socket slot pointers
        public const int SocketSlotsEnd   = 0x38; // (IDA)
        public const int LinkedGroupBegin = 0x60; // (IDA) link group data
    }

    /// <summary>Quality component. sub_140A40630 (IDA).</summary>
    public static class QualityComponent
    {
        public const int QualityValue = 0x370; // (IDA) byte — item quality %
    }

    /// <summary>Charges component (flasks/items). Int setter: sub_142577A10 (IDA).</summary>
    public static class ChargesComponent
    {
        public const int MaxCharges       = 0x10; // (IDA) int
        public const int CurrentCharges   = 0x14; // (IDA) int (runtime)
        public const int ChargesPerUse    = 0x18; // (IDA) int
        public const int ChargesPerUseBase = 0x1C; // (IDA) int (base copy)
    }

    /// <summary>
    /// TerrainStruct (base at AreaInstance+0x8A0). Validated live: TotalTiles (54,48) → 2592 tiles
    /// (matches TileDetails count); walkable grid 685584 bytes; BytesPerRow 621 → cellsPerRow 1242;
    /// grid 1242×1104 = (54×23)×(48×23). PoE2 has FOUR grid layers (0xD0/0xE8/0x100/0x118), so
    /// BytesPerRow sits at 0x130 — not GH2's 0x100.
    /// </summary>
    public static class Terrain
    {
        public const int TotalTiles        = 0x18;  // ✓ StdTuple2D<long> (tilesX, tilesY)
        public const int TileDetailsPtr    = 0x28;  // ✓ StdVector of TileStructure (0x38 bytes)
        public const int GridWalkableData  = 0xD0;  // ✓ StdVector — packed walkable grid bytes
        public const int GridLandscapeData = 0xE8;  // ✓ StdVector
        public const int GridLayer3        = 0x100; // ✓ StdVector (extra PoE2 layer)
        public const int GridLayer4        = 0x118; // ✓ StdVector (extra PoE2 layer)
        public const int BytesPerRow       = 0x130; // ✓ int (621 live) — cellsPerRow = ×2
        public const int TileGridCells     = 23;    // tile = 23×23 grid cells
    }

    /// <summary>One entry in Terrain.TileDetailsPtr (0x38 bytes). ✓ validated (TgtPath gives tile names).</summary>
    public const int TileStructureSize = 0x38;
    public static class TileStructure
    {
        public const int SubTileDetailsPtr = 0x00; // pointer
        public const int TgtFilePtr        = 0x08; // ✓ → TgtFileStruct
        public const int TileHeight        = 0x30; // short
        public const int RotationSelector  = 0x36; // byte
    }

    public static class TgtFileStruct
    {
        public const int TgtPath = 0x08; // ✓ StdWString — full tile .tdt path (e.g. .../Feature/arena_01.tdt)
    }

    // ── Map UI — GH2, not yet live-checked ──
    public static class ImportantUi
    {
        public const int MapParentPtr = 0x738; // (GH2) from UiRoot/GameUi
    }

    public static class MapParent
    {
        public const int LargeMapPtr = 0x50; // (GH2)
        public const int MiniMapPtr  = 0x58; // (GH2)
    }

    /// <summary>
    /// MapUiElement (large map + minimap share this class/vtable). ✓ validated live: exactly two
    /// elements carry DefaultShift=(0,-20) with Zoom=0.5. Struct shape matches GH2 (shifted +0x70):
    /// Shift→DefaultShift = 8, DefaultShift→Zoom = 0x38.
    /// </summary>
    public static class MapUiElement
    {
        public const int Shift        = 0x368; // ✓ StdTuple2D<float>
        public const int DefaultShift = 0x370; // ✓ StdTuple2D<float> (0,-20)
        public const int Zoom         = 0x3A8; // ✓ float (0.5 live)
    }

    /// <summary>UiElement base — ✓ validated live (GH2's offsets drifted: Self 0x30→0x8, Flags 0x1B8→0x180).</summary>
    public static class UiElement
    {
        public const int Self           = 0x08;  // ✓ self pointer
        public const int Children       = 0x10;  // ✓ StdVector of child UiElement pointers
        public const int ChildrenEnd    = 0x18;
        public const int Parent         = 0xB8;
        public const int PositionModifier = 0xF0;
        public const int RelativePos    = 0x118;
        public const int LocalScaleMul  = 0x130;
        public const int Flags          = 0x180; // ✓ uint; IsVisibleLocal = bit 0x0B (toggle-diff: 0x2EF1↔0x26F1)
        public const int FlagVisibleBit = 0x0B;  // ✓ visible bit (set when shown)
        public const int FlagModifyPosBit = 0x0A;
        public const int ScaleIndex     = 0x18A;
        public const int SizeW          = 0x288;
        public const int SizeH          = 0x28C;
        public const int Text           = 0x390;
        public const double BaseResW = 2560.0;
        public const double BaseResH = 1600.0;
        // Full visibility is hierarchical: an element is shown iff its own bit 0x0B AND every
        // ancestor's bit are set. Walk Parent up to UiRoot (Parent offset still TBD).
        // TODO: Position/Size offsets needed for pixel-perfect minimap overlay alignment.
        // Need to probe a known UiElement with CE to find screen rect floats.
    }

    public static class Runeforge
    {
        public static readonly uint[] PanelFlagFingerprints =
            [0x00462EF1, 0x00502EF3, 0x00502EF7, 0x00542EF1, 0x00502EF1];
        public const int GateStep = 0;
        public const int ViewportStep = 2;
        public const int ScrollOffset = 0x120;
        public const int NameWString = 0x390;
    }

    public static class Ritual
    {
        public const int TileSlotItem = 0x4F8;
    }

    /// <summary>Atlas/World Map UI fields discovered live by POE2Radar.Research --atlas-probe.</summary>
    public static class AtlasUi
    {
        public const int RootChildIndex = 22;    // UiRoot.Children[22] toggles with Atlas open/closed.
        public const int CombinedScale  = 0x0F0; // float; Atlas panel UI scale, node layer = UI scale * Atlas zoom.
        public const int NodePosition   = 0x118; // float2; pan-adjusted node position within the Atlas layer.
        public const int LayerZoom      = 0x130; // float; live Atlas zoom on the dominant node layer.
        public const int LocalRect      = 0x280; // float4; local element bounds, e.g. node icon 40x40.
        public const int PanelClient    = 0x330; // float4; panel client/display bounds candidate.
        public const int PanelClip      = 0x340; // float4; Atlas content clip rect, usually 16,26 -> 2544,1574.
    }

    public static class AtlasMapRow
    {
        public const int WorldAreaName = 0x08;
    }

    public static class AtlasNode
    {
        public const int MapNodeId = 0x300;
        public const int Content = 0x310;
        public const int State = 0x32C;
        public const int Biome = 0x32E;
        public const int Flags = 0x32F;
        public const int GridPos = 0x320;
        public const int Completion = 0x339;
        public const int ContentVec = 0x350;
    }

    public static class AtlasGraph
    {
        public const int ConnectionsVec = 0x5A8;
        public const int EdgeStride = 20;
        public const int EdgeSourceOff = 0x04;
        public const int EdgeTargetOff = 0x0C;
        public const int CurrentMarkerNodePtr = 0x300;
    }

    public static class HoverTracker
    {
        public const int FromUiRoot = 0x7D8;
        public const int WorldTracker = 0x630;
        public const int HoveredEntity = 0x18;
    }
}
