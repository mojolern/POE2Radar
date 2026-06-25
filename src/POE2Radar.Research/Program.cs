using POE2Radar.Core;
using POE2Radar.Core.Cheats;
using POE2Radar.Core.Game;
using System.Runtime.InteropServices;

// POE2Radar.Research — dev-time offset discovery / validation harness.
//
// There is no POEMCP-style oracle for PoE2, so validation here is manual + value-scan based:
//   --hp <N> [--mana <N>]   value-scan for the Life component, then back-walk to IngameData
//                           and dump the resolved chain so offsets can be checked by hand.
//   --dump <hexAddr> [len]  hex-dump a memory region (default 256 bytes) for manual inspection.
//   --aob                   scan for IngameState via the committed AOB patterns (if any).
//
// As PoE2 offsets get discovered, build this out into a per-patch sweep (see CLAUDE.md).

Console.WriteLine("POE2Radar.Research");
Console.WriteLine("==================");

var needsWriteAccess = HasFlag(args, "--confirm-write");
using var process = ProcessHandle.AttachToPoE(includeWriteAccess: needsWriteAccess);
if (process is null)
{
    Console.Error.WriteLine("PoE2 not running (no matching process found).");
    return 1;
}
Console.WriteLine($"Attached to {process.ProcessName} (PID {process.ProcessId})");
Console.WriteLine($"Main module base: 0x{process.MainModuleBase:X16}  size: 0x{process.MainModuleSize:X}");
var reader = new MemoryReader(process);

if (HasFlag(args, "--aob"))
    return RunAobScan(process, reader);

if (HasFlag(args, "--gamestate-aob"))
    return RunGameStateAobProbe(process, reader);

if (HasFlag(args, "--cheat-scan"))
    return RunCheatScanProbe(process, reader);

if (HasFlag(args, "--chain"))
    return RunChainProbe(process, reader);

if (HasFlag(args, "--find-entities"))
    return RunFindEntities(process, reader, TryGetIntArg(args, "--window") ?? 0x4000);

if (HasFlag(args, "--find-terrain"))
    return RunFindTerrain(process, reader, TryGetIntArg(args, "--window") ?? 0x2000);

if (HasFlag(args, "--find-map"))
    return RunFindMap(process, reader);

if (HasFlag(args, "--atlas-probe"))
    return RunAtlasProbe(
        process,
        reader,
        TryGetIntArg(args, "--atlas-child") ?? 22,
        TryGetIntArg(args, "--atlas-max") ?? 60000,
        TryGetIntArg(args, "--atlas-samples") ?? 60,
        TryGetHexArg(args, "--atlas-dump-node") ?? 0);

if (HasFlag(args, "--atlas-snapshot"))
    return RunAtlasSnapshot(process, reader, TryGetIntArg(args, "--atlas-samples") ?? 40);

if (HasFlag(args, "--atlas-rect-scan"))
    return RunAtlasRectScan(process, reader, TryGetIntArg(args, "--atlas-samples") ?? 80);

if (HasFlag(args, "--watch"))
    return RunWatch(process, reader);

if (HasFlag(args, "--tiles"))
    return RunTiles(process, reader);

if (HasFlag(args, "--league"))
    return RunLeague(process, reader, TryGetStringArg(args, "--needle"));

if (HasFlag(args, "--entry-snapshot"))
    return RunEntrySnapshot(
        process,
        reader,
        HasFlag(args, "--entry-all"),
        TryGetIntArg(args, "--max-entities") ?? 3000,
        TryGetIntArg(args, "--ui-max") ?? 6000);

if (HasFlag(args, "--rune-ui-probe"))
    return RunRuneUiProbe(
        process,
        reader,
        TryGetIntArg(args, "--ui-max") ?? 12000,
        TryGetIntArg(args, "--context-children") ?? 12);

if (HasFlag(args, "--runeforge-read"))
    return RunRuneforgeRead(
        process,
        reader,
        TryGetIntArg(args, "--win-w") ?? 1920,
        TryGetIntArg(args, "--win-h") ?? 1080);

if (HasFlag(args, "--runeforge-correlate"))
    return RunRuneforgeCorrelate(
        process,
        reader,
        TryGetIntArg(args, "--win-w") ?? 1920,
        TryGetIntArg(args, "--win-h") ?? 1080,
        TryGetIntArg(args, "--ui-max") ?? 20000);

if (HasFlag(args, "--runeforge-inventory-probe"))
    return RunRuneforgeInventoryProbe(
        process,
        reader,
        TryGetIntArg(args, "--component-window") ?? 0x1000);

if (HasFlag(args, "--runeforge-inventory-block-probe"))
    return RunRuneforgeInventoryBlockProbe(
        process,
        reader,
        TryGetIntArg(args, "--component-window") ?? 0x900,
        TryGetIntArg(args, "--entries") ?? 8,
        TryGetIntArg(args, "--object-window") ?? 0x300);

if (HasFlag(args, "--runeforge-inventory-block-watch"))
    return RunRuneforgeInventoryBlockWatch(
        process,
        reader,
        TryGetIntArg(args, "--timeout") ?? 90,
        TryGetIntArg(args, "--component-window") ?? 0x900,
        TryGetIntArg(args, "--entries") ?? 16,
        TryGetIntArg(args, "--object-window") ?? 0x300);

if (HasFlag(args, "--runeforge-vector-probe"))
    return RunRuneforgeVectorProbe(
        process,
        reader,
        TryGetIntArg(args, "--object-window") ?? 0x500,
        TryGetIntArg(args, "--entries") ?? 8);

if (HasFlag(args, "--runeforge-vector-watch"))
    return RunRuneforgeVectorWatch(
        process,
        reader,
        TryGetIntArg(args, "--timeout") ?? 90,
        TryGetIntArg(args, "--object-window") ?? 0x500);

if (HasFlag(args, "--runeforge-ui-gate-probe"))
    return RunRuneforgeUiGateProbe(
        process,
        reader,
        TryGetIntArg(args, "--max-children") ?? 4000,
        TryGetIntArg(args, "--max-branches") ?? 32);

if (HasFlag(args, "--runeforge-ui-child-watch"))
    return RunRuneforgeUiChildWatch(
        process,
        reader,
        TryGetIntArg(args, "--timeout") ?? 90,
        TryGetIntArg(args, "--max-children") ?? 4000,
        TryGetIntArg(args, "--max-branches") ?? 32,
        TryGetIntArg(args, "--field-window") ?? 0x500);

if (HasFlag(args, "--runeforge-ui-populate-timeline"))
    return RunRuneforgeUiPopulateTimeline(
        process,
        reader,
        TryGetIntArg(args, "--timeout") ?? 90,
        TryGetIntArg(args, "--poll-ms") ?? 25,
        TryGetIntArg(args, "--max-children") ?? 4000,
        TryGetIntArg(args, "--max-branches") ?? 32);

if (HasFlag(args, "--runeforge-ui-latch-write-test"))
    return RunRuneforgeUiLatchWriteTest(
        process,
        reader,
        HasFlag(args, "--confirm-write"),
        TryGetIntArg(args, "--timeout") ?? 10,
        TryGetIntArg(args, "--poll-ms") ?? 10,
        TryGetIntArg(args, "--hold-ms") ?? 1000,
        TryGetIntArg(args, "--max-children") ?? 4000,
        TryGetIntArg(args, "--max-branches") ?? 32);

if (HasFlag(args, "--runeforge-selection-source-probe"))
    return RunRuneforgeSelectionSourceProbe(
        process,
        reader,
        TryGetIntArg(args, "--max-children") ?? 4000,
        TryGetIntArg(args, "--max-branches") ?? 32,
        TryGetIntArg(args, "--field-window") ?? 0x800,
        TryGetIntArg(args, "--max-entities") ?? 20);

if (HasFlag(args, "--runeforge-entry-key-scan"))
    return RunRuneforgeEntryKeyScan(
        process,
        reader,
        TryGetIntArg(args, "--max-entities") ?? 20,
        TryGetIntArg(args, "--component-window") ?? 0x1800,
        TryGetIntArg(args, "--object-window") ?? 0x500,
        TryGetIntArg(args, "--vector-entries") ?? 32);

if (HasFlag(args, "--runeforge-selection-index-watch"))
    return RunRuneforgeSelectionIndexWatch(
        process,
        reader,
        TryGetIntArg(args, "--timeout") ?? 90,
        TryGetIntArg(args, "--max-entities") ?? 20,
        TryGetIntArg(args, "--component-window") ?? 0x1800,
        TryGetIntArg(args, "--max-children") ?? 4000,
        TryGetIntArg(args, "--max-branches") ?? 32);

if (HasFlag(args, "--runeforge-interaction-block-probe"))
    return RunRuneforgeInteractionBlockProbe(
        process,
        reader,
        TryGetIntArg(args, "--timeout") ?? 90,
        TryGetIntArg(args, "--max-entities") ?? 20,
        TryGetIntArg(args, "--component-window") ?? 0x1800,
        TryGetIntArg(args, "--max-children") ?? 4000,
        TryGetIntArg(args, "--max-branches") ?? 32);

if (HasFlag(args, "--runeforge-selection-fingerprint-watch"))
    return RunRuneforgeSelectionFingerprintWatch(
        process,
        reader,
        TryGetIntArg(args, "--timeout") ?? 90,
        TryGetIntArg(args, "--max-entities") ?? 20,
        TryGetIntArg(args, "--component-window") ?? 0x3000,
        TryGetIntArg(args, "--object-window") ?? 0x1000,
        TryGetIntArg(args, "--pointer-depth") ?? 2,
        TryGetIntArg(args, "--max-children") ?? 4000,
        TryGetIntArg(args, "--max-branches") ?? 32);

if (HasFlag(args, "--monolith"))
    return RunMonolith(process, reader);

if (HasFlag(args, "--monolith-listener-scan"))
    return RunMonolithListenerScan(
        process,
        reader,
        TryGetIntArg(args, "--max-entities") ?? 6,
        TryGetIntArg(args, "--entries") ?? 24,
        TryGetIntArg(args, "--scan") ?? 0x300);

if (HasFlag(args, "--ritual-shop"))
    return RunRitualShop(process, reader);

if (HasFlag(args, "--atlas-mapname"))
    return RunAtlasMapName(process, reader, TryGetIntArg(args, "--max") ?? 12);

if (HasFlag(args, "--atlas-graph"))
    return RunAtlasGraph(process, reader);

if (HasFlag(args, "--atlas-current"))
    return RunAtlasCurrent(process, reader);

if (HasFlag(args, "--atlas-marker"))
    return RunAtlasMarker(process, reader);

if (HasFlag(args, "--mechanic-scan"))
    return RunMechanicScan(
        process,
        reader,
        TryGetIntArg(args, "--seconds") ?? 60,
        TryGetIntArg(args, "--interval-ms") ?? 500,
        HasFlag(args, "--mechanic-all"));

if (HasFlag(args, "--component-layout-scan"))
    return RunComponentLayoutScan(
        process,
        reader,
        HasFlag(args, "--mechanic-all"),
        TryGetIntArg(args, "--max-entities") ?? 100);

if (HasFlag(args, "--mechanic-component-probe"))
    return RunMechanicComponentProbe(
        process,
        reader,
        HasFlag(args, "--mechanic-all"),
        TryGetStringArg(args, "--component-filter"),
        TryGetIntArg(args, "--max-entities") ?? 80,
        TryGetIntArg(args, "--component-window") ?? 0x600);

if (HasFlag(args, "--rarity"))
    return RunRarity(process, reader);

if (HasFlag(args, "--info"))
    return RunInfo(process, reader);

if (HasFlag(args, "--camera"))
    return RunCamera(process, reader);

if (TryGetHexArg(args, "--find") is { } needle)
    return RunFindPointer(reader, needle, TryGetHexArg(args, "--near"), TryGetIntArg(args, "--window") ?? 0x2000);

if (TryGetHexArg(args, "--dump") is { } dumpAddr)
    return RunDump(reader, dumpAddr, TryGetIntArg(args, "--dump-len") ?? 256);

if (TryGetHexArg(args, "--entity") is { } entAddr)
    return RunEntityProbe(reader, entAddr);

if (TryGetIntArg(args, "--hp") is { } hp)
    return RunValueScan(reader, hp, TryGetIntArg(args, "--mana"));

Console.WriteLine();
Console.WriteLine("No mode specified. Options:");
Console.WriteLine("  --hp <N> [--mana <N>]      value-scan for the player Life component");
Console.WriteLine("  --dump <hexAddr> [--dump-len <N>]   hex-dump a region for inspection");
Console.WriteLine("  --dump <hexAddr> [--dump-len <N>]   hex-dump a region for inspection");
Console.WriteLine("  --entity <hexAddr>         walk a PoE2 entity: id, metadata path, component map, Render→grid, Life");
Console.WriteLine("  --aob                      scan for IngameState via AOB patterns");
Console.WriteLine("  --gamestate-aob            scan GameState AOB and print chain candidates for patch diagnostics");
Console.WriteLine("  --cheat-scan               read-only scan for startup byte-patch cheat signatures");
Console.WriteLine("  --atlas-probe [--atlas-child N] [--atlas-dump-node 0xADDR]  discover Atlas panel/node UI candidates");
Console.WriteLine("  --atlas-snapshot [--atlas-samples N]  validate the Core Atlas snapshot reader");
Console.WriteLine("  --atlas-rect-scan [--atlas-samples N]  scan Atlas nodes for final screen/client rect offsets");
Console.WriteLine("  --league [--needle str]  read the game league from ServerData and optionally scan nearby strings");
Console.WriteLine("  --entry-snapshot [--entry-all] [--max-entities N] [--ui-max N]");
Console.WriteLine("                             one-shot awake/sleeping/UI/terrain snapshot for map-entry research");
Console.WriteLine("  --rune-ui-probe [--ui-max N] [--context-children N]");
Console.WriteLine("                             dump rune/Expedition UI anchors and local child strings");
Console.WriteLine("  --runeforge-read [--win-w N] [--win-h N]");
Console.WriteLine("                             read visible Runeshape Combinations reward rows via overlay parser");
Console.WriteLine("  --runeforge-correlate [--win-w N] [--win-h N] [--ui-max N]");
Console.WriteLine("                             correlate visible Runeforge rows with active controller/encounter stats");
Console.WriteLine("  --runeforge-inventory-probe [--component-window N]");
Console.WriteLine("                             decode controller Inventories vectors as item/inventory candidates");
Console.WriteLine("  --runeforge-inventory-block-probe [--component-window N] [--entries N]");
Console.WriteLine("                             dump repeated RuneEncounterController Inventories block vectors");
Console.WriteLine("  --runeforge-inventory-block-watch [--timeout N] [--entries N]");
Console.WriteLine("                             diff Inventories blocks before/after opening Runeshape");
Console.WriteLine("  --runeforge-vector-probe [--object-window N] [--entries N]");
Console.WriteLine("                             walk Expedition2Encounter reward-state vector suspects");
Console.WriteLine("  --runeforge-vector-watch [--timeout N] [--object-window N]");
Console.WriteLine("                             capture Expedition2Encounter vector deltas when Runeshape opens");
Console.WriteLine("  --runeforge-ui-gate-probe [--max-children N] [--max-branches N]");
Console.WriteLine("                             read-only Runeshape UI gate/path flags for write feasibility research");
Console.WriteLine("  --runeforge-ui-child-watch [--timeout N] [--max-children N] [--field-window N]");
Console.WriteLine("                             watch the real Runeshape catalog container populate when the panel opens");
Console.WriteLine("  --runeforge-ui-populate-timeline [--timeout N] [--poll-ms N]");
Console.WriteLine("                             timestamp catalog population vs fields +0x2E8/+0x2F0");
Console.WriteLine("  --runeforge-ui-latch-write-test --confirm-write [--timeout N] [--poll-ms N]");
Console.WriteLine("                             guarded write test for catalog +0x2E8/+0x2F0; restores originals");
Console.WriteLine("  --runeforge-selection-source-probe [--field-window N] [--max-entities N]");
Console.WriteLine("                             after Runeshape opens, search UI/entities for selected reward indexes");
Console.WriteLine("  --runeforge-entry-key-scan [--component-window N] [--object-window N]");
Console.WriteLine("                             scan Runeforge entities for recipe-key strings before opening Runeshape");
Console.WriteLine("  --runeforge-selection-index-watch [--timeout N] [--component-window N]");
Console.WriteLine("                             snapshot Runeforge components before open, then test visible row indexes after open");
Console.WriteLine("  --monolith                 validate Sikaka v0.14.x Runeshape monolith station/reward catalog path");
Console.WriteLine("  --monolith-listener-scan [--max-entities N] [--entries N] [--scan N]");
Console.WriteLine("                             scan StateMachine listener vectors for shifted RuneStation owner links");
Console.WriteLine("  --ritual-shop              read visible Ritual tribute-shop reward tiles via UiElement item slot");
Console.WriteLine("  --atlas-mapname [--max N]  verify localized Atlas map names from WorldAreas rows");
Console.WriteLine("  --atlas-graph              validate live Atlas node graph/path data");
Console.WriteLine("  --atlas-current            print the current Atlas node resolved by the marker reader");
Console.WriteLine("  --atlas-marker             alias of --atlas-current for marker validation");
Console.WriteLine("  --mechanic-scan [--seconds N] [--interval-ms N] [--mechanic-all]");
Console.WriteLine("                             watch awake/sleeping mechanic entities and terrain clues");
Console.WriteLine("  --component-layout-scan [--mechanic-all] [--max-entities N]");
Console.WriteLine("                             discover StateMachine tables and magic-property mod vectors");
Console.WriteLine("  --mechanic-component-probe [--component-filter term[,term]] [--component-window N]");
Console.WriteLine("                             dump component addresses plus string/vector clues for mechanic research");
return 0;

// ── PoE2 entity / component-map probe ──────────────────────────────────────
// Validates the GameHelper2 PoE2 layout: Entity{Id@0x80, IsValid@0x84, ItemBase{
//   EntityDetailsPtr@0x08, ComponentList StdVector@0x10}}, EntityDetails{name@0x08,
//   ComponentLookUpPtr@0x28}, ComponentLookUp.StdBucket@0x28 of (NamePtr, Index) →
//   ComponentList[Index]. Render.CurrentWorldPosition@0xB8; grid = world / (250/23).
static int RunEntityProbe(MemoryReader reader, nint entity)
{
    const float WorldToGridRatio = 250f / 23f; // ≈ 10.8696 (GameHelper2 TileStructure)

    Console.WriteLine($"Entity @ 0x{entity:X16}");
    if (!reader.TryReadStruct<uint>(entity + 0x80, out var id) ||
        !reader.TryReadStruct<byte>(entity + 0x84, out var isValid))
    {
        Console.Error.WriteLine("  could not read Entity.Id / IsValid");
        return 1;
    }
    Console.WriteLine($"  Id        : {id} (0x{id:X8})   IsValid byte: 0x{isValid:X2} (valid={(isValid & 1) == 0})");

    var detailsPtr   = reader.ReadPointer(entity + 0x08);
    var componentList = reader.ReadStruct<POE2Radar.Core.Game.StdVector>(entity + 0x10);
    var compCount = ((long)componentList.Last - (long)componentList.First) / 8;
    Console.WriteLine($"  Details   : 0x{detailsPtr:X16}   ComponentList: {compCount} entries");

    if (detailsPtr == 0) { Console.Error.WriteLine("  null details"); return 1; }
    Console.WriteLine($"  Metadata  : {ReadStdWString(reader, detailsPtr + 0x08)}");

    var lookupPtr = reader.ReadPointer(detailsPtr + 0x28);
    if (lookupPtr == 0) { Console.Error.WriteLine("  null component lookup"); return 1; }

    // StdBucket.Data (StdVector) lives at ComponentLookUp + 0x28; element = {IntPtr Name, int Index, int pad} = 16 bytes.
    var bucket = reader.ReadStruct<POE2Radar.Core.Game.StdVector>(lookupPtr + 0x28);
    var entryCount = ((long)bucket.Last - (long)bucket.First) / 16;
    Console.WriteLine($"  Components : {entryCount} named");
    if (entryCount <= 0 || entryCount > 256) { Console.Error.WriteLine("  implausible component count — chain offset likely wrong"); return 1; }

    var byName = new Dictionary<string, nint>(StringComparer.Ordinal);
    for (long i = 0; i < entryCount; i++)
    {
        var entryAddr = bucket.First + (nint)(i * 16);
        var namePtr = reader.ReadPointer(entryAddr);
        if (!reader.TryReadStruct<int>(entryAddr + 8, out var index)) continue;
        var name = reader.ReadStringUtf8(namePtr, 64);
        if (string.IsNullOrEmpty(name) || index < 0 || index >= compCount) continue;
        var compAddr = reader.ReadPointer(componentList.First + (nint)(index * 8));
        byName[name] = compAddr;
        Console.WriteLine($"    [{index,2}] {name,-22} @ 0x{compAddr:X16}");
    }

    // Render.CurrentWorldPosition validated @ +0x138 on live PoE2 (GameHelper2's 0xB8 is stale here).
    if (byName.TryGetValue("Render", out var render) && render != 0 &&
        reader.TryReadStruct<POE2Radar.Core.Game.Vector3>(render + 0x138, out var world))
    {
        Console.WriteLine($"  Render.World : ({world.X:F1}, {world.Y:F1}, {world.Z:F1})");
        Console.WriteLine($"  → Grid       : ({world.X / WorldToGridRatio:F1}, {world.Y / WorldToGridRatio:F1})");
    }
    if (byName.TryGetValue("Life", out var life) && life != 0 &&
        reader.TryReadStruct<POE2Radar.Core.Game.VitalStruct>(life + 0x1A8, out var hp))
    {
        Console.WriteLine($"  Life.Health  : {hp.Current} / {hp.Max}");
    }
    if (byName.TryGetValue("Player", out var pc) && pc != 0)
    {
        // PoE2 Player component char-name offset unknown yet; dump a window to find the character name.
        Console.WriteLine($"  Player comp  @ 0x{pc:X16} (char-name offset TBD — dump to locate)");
    }
    return 0;
}

// Pointer back-search: find 8-byte-aligned locations holding `needle`. With --near <addr>,
// only scans [addr, addr+window) (fast, for locating a field offset within one object);
// otherwise scans all readable private regions (slow). Prints each hit and, when --near is
// given, its offset from the near base.
static int RunFindPointer(MemoryReader reader, nint needle, nint? near, int window)
{
    var target = (long)needle;
    var hits = 0;
    if (near is { } baseAddr)
    {
        Console.WriteLine($"Searching [0x{baseAddr:X}, +0x{window:X}) for 0x{needle:X16}...");
        var buf = new byte[window];
        var n = reader.TryReadBytes(baseAddr, buf);
        for (var i = 0; i + 8 <= n; i += 8)
            if (BitConverter.ToInt64(buf, i) == target)
                { Console.WriteLine($"  hit @ 0x{baseAddr + i:X16}  (base +0x{i:X})"); hits++; }
        Console.WriteLine($"{hits} hit(s).");
        return 0;
    }

    Console.WriteLine($"Scanning all private regions for 0x{needle:X16} (8-byte aligned)...");
    var regions = reader.Process.EnumerateReadableRegions(privateOnly: true).ToArray();
    var chunk = new byte[1 << 20];
    for (var ri = 0; ri < regions.Length && hits < 60; ri++)
    {
        var (regionBase, regionSize) = regions[ri];
        long off = 0;
        while (off < regionSize && hits < 60)
        {
            var toRead = (int)Math.Min(chunk.Length, regionSize - off);
            var read = reader.TryReadBytes(regionBase + (nint)off, chunk.AsSpan(0, toRead));
            if (read == 0) break;
            for (var i = 0; i + 8 <= read; i += 8)
                if (BitConverter.ToInt64(chunk, i) == target)
                    { Console.WriteLine($"  hit @ 0x{regionBase + (nint)(off + i):X16}"); if (++hits >= 60) break; }
            if (read != toRead) break;
            off += toRead;
        }
    }
    Console.WriteLine($"{hits} hit(s){(hits >= 60 ? " (capped)" : "")}.");
    return 0;
}

// ── Camera: find the WorldToScreen 4x4 matrix. Scans pointers reachable from InGameState; for
// each pointed object, treats every 16-float window as a row-major matrix, projects the player's
// world position, and reports any that land the player near screen-center (the camera follows
// the player). Run standing still.
static int RunCamera(ProcessHandle process, MemoryReader reader)
{
    var (_, igs, ai, lp) = ResolveChain(process, reader);   // 2nd element = InGameState
    if (igs == 0) { Console.Error.WriteLine("no chain"); return 1; }
    var render = ResolveComponentAddr(reader, lp, "Render");
    if (render == 0 || !reader.TryReadStruct<POE2Radar.Core.Game.Vector3>(render + 0x138, out var w))
    { Console.Error.WriteLine("no player world pos"); return 1; }
    Win.GetClientRect(Win.GetForegroundWindow(), out var rc);
    int W = rc.right - rc.left, H = rc.bottom - rc.top;
    if (W <= 0) { W = 1920; H = 1080; }
    var cam368 = SafePtr(reader, igs + 0x368);
    Console.WriteLine($"InGameState 0x{igs:X}  Camera(*+0x368) 0x{cam368:X}  player world=({w.X:F1},{w.Y:F1},{w.Z:F1})  window={W}x{H}");
    var monsters = new List<POE2Radar.Core.Game.Vector3>();
    var head = SafePtr(reader, ai + Poe2.AreaInstance.AwakeEntities);
    if (head != 0)
    {
        var q = new Queue<nint>(); q.Enqueue(SafePtr(reader, head + Poe2.StdMapNode.Parent));
        var seen = new HashSet<nint>();
        while (q.Count > 0 && seen.Count < 100000 && monsters.Count < 10)
        {
            var node = q.Dequeue();
            if (node == 0 || node == head || !seen.Add(node)) continue;
            if (!reader.TryReadStruct<byte>(node + Poe2.StdMapNode.IsNil, out var nil) || nil != 0) continue;
            var ent = SafePtr(reader, node + Poe2.StdMapNode.ValueEntityPtr);
            q.Enqueue(SafePtr(reader, node + Poe2.StdMapNode.Left));
            q.Enqueue(SafePtr(reader, node + Poe2.StdMapNode.Right));
            if (ent == 0 || !ReadEntityMetadata(reader, ent).Contains("/Monsters/", StringComparison.Ordinal)) continue;
            var r = ResolveComponentAddr(reader, ent, "Render");
            if (r != 0 && reader.TryReadStruct<POE2Radar.Core.Game.Vector3>(r + 0x138, out var mw)) monsters.Add(mw);
        }
    }
    Console.WriteLine($"validating against {monsters.Count} monster world positions.");

    static (float sx, float sy, float cw) Project(float[] m, POE2Radar.Core.Game.Vector3 v, int W, int H)
    {
        float cx = v.X*m[0]+v.Y*m[4]+v.Z*m[8]+m[12];
        float cy = v.X*m[1]+v.Y*m[5]+v.Z*m[9]+m[13];
        float cw = v.X*m[3]+v.Y*m[7]+v.Z*m[11]+m[15];
        return ((cx/cw/2f + 0.5f) * W, (0.5f - cy/cw/2f) * H, cw);
    }

    // Zoom per the community note (Camera+0x528) — a sanity readout.
    if (cam368 != 0 && reader.TryReadStruct<float>(cam368 + 0x528, out var zoom)) Console.WriteLine($"  Camera.Zoom(*+0x528) = {zoom}");

    // Scan candidate camera objects: the +0x368 camera first, then any pointer in InGameState.
    var objs = new List<(string label, nint addr)>();
    if (cam368 != 0) objs.Add(("Camera+0x368", cam368));
    for (var o = 0; o < 0x600; o += 8) { var p = SafePtr(reader, igs + o); if (p != 0 && p != cam368) objs.Add(($"IGS+0x{o:X3}", p)); }

    var buf = new byte[0x600];
    foreach (var (label, cam) in objs)
    {
        if (reader.TryReadBytes(cam, buf) < buf.Length) continue;
        for (var mo = 0; mo + 64 <= buf.Length; mo += 4)
        {
            var m = new float[16];
            for (var i = 0; i < 16; i++) m[i] = BitConverter.ToSingle(buf, mo + i * 4);
            var (sx, sy, cw) = Project(m, w, W, H);
            if (cw < 1f || cw > 1_000_000f) continue;
            if (sx < W*0.25f || sx > W*0.75f || sy < H*0.25f || sy > H*0.75f) continue; // player ~ center
            int on = 0; float minx = 9e9f, maxx = -9e9f;
            foreach (var mw in monsters)
            {
                var (msx, msy, mcw) = Project(m, mw, W, H);
                if (mcw > 0 && msx >= 0 && msx <= W && msy >= 0 && msy <= H) { on++; minx = Math.Min(minx, msx); maxx = Math.Max(maxx, msx); }
            }
            var need = monsters.Count == 0 ? 0 : Math.Max(1, (int)(monsters.Count * 0.6));
            if (on < need) continue;
            // spreadX = how far apart monsters land horizontally — a real projection spreads them; a
            // degenerate one stacks them near center.
            var spread = on > 1 ? (int)(maxx - minx) : 0;
            Console.WriteLine($"  {label} (0x{cam:X}) matrix@+0x{mo:X3} -> player=({sx:F0},{sy:F0}) w={cw:F1}  onScreen={on}/{monsters.Count} spreadX={spread}");
        }
    }
    Console.WriteLine("Real W2S: from the Camera+0x368 object, player≈center, all monsters on-screen, and a healthy spreadX.");
    return 0;
}

// ── Info: validate the community-note fields reachable from town — area name, character
// name/level, camera/zoom — and dump the camera object so the WorldToScreen matrix can be found.
static int RunInfo(ProcessHandle process, MemoryReader reader)
{
    var (igs, _, ai, lp) = ResolveChain(process, reader);
    if (ai == 0) { Console.Error.WriteLine("Could not resolve chain (in game?)."); return 1; }
    Console.WriteLine($"InGameState 0x{igs:X}  AreaInstance 0x{ai:X}  LocalPlayer 0x{lp:X}");

    // Area name: AreaInstance+0xA0 -> AreaInfo -> +0x00 -> UTF-16 "Code\0Name\0".
    var areaInfo = SafePtr(reader, ai + 0xA0);
    var strPtr = SafePtr(reader, areaInfo);
    var code = reader.ReadStringUtf16(strPtr, 64);
    var name = code.Length > 0 ? reader.ReadStringUtf16(strPtr + (nint)((code.Length + 1) * 2), 64) : "";
    Console.WriteLine($"AreaInfo 0x{areaInfo:X}  Code='{code}'  Name='{name}'");

    // Character: try the Player component, then a 'Character' component if present.
    foreach (var compName in new[] { "Player", "Character", "PlayerClass" })
    {
        var c = ResolveComponentAddr(reader, lp, compName);
        if (c == 0) continue;
        var nm0x1B0 = reader.ReadStringUtf16(c + 0x1B0, 32);
        var nmStd = ReadStdWString(reader, c + 0x1B0);
        reader.TryReadStruct<int>(c + 0x204, out var lvl204);
        reader.TryReadStruct<byte>(c + 0x204, out var lvlByte);
        Console.WriteLine($"  [{compName}] @0x{c:X}  name@0x1B0(raw)='{nm0x1B0}' (std)='{nmStd}'  lvl@0x204 int={lvl204} byte={lvlByte}");
    }

    // Camera: InGameState+0x368 -> Camera; Zoom @ +0x528. Dump +0x000..+0x160 to spot the 4x4 matrix.
    var cam = SafePtr(reader, igs + 0x368);
    Console.WriteLine($"Camera 0x{cam:X}");
    if (cam != 0)
    {
        reader.TryReadStruct<float>(cam + 0x528, out var zoom);
        Console.WriteLine($"  Zoom@0x528 = {zoom}");
        var buf = new byte[0x160];
        if (reader.TryReadBytes(cam, buf) == buf.Length)
            for (var i = 0; i < buf.Length; i += 16)
            {
                var f = string.Join(" ", Enumerable.Range(0, 4).Select(j => BitConverter.ToSingle(buf, i + j * 4).ToString("0.###")));
                Console.WriteLine($"  +0x{i:X3}  {f}");
            }
    }
    return 0;
}

// ── Rarity: find the ObjectMagicProperties rarity offset. Walks all alive monsters, resolves
// each one's ObjectMagicProperties component, and for every 4-byte offset records the set of
// values seen. The rarity field is the offset whose values are all small (0..3) AND vary across
// the sample (white/magic/rare/unique). Run while standing in a mixed pack.
static int RunRarity(ProcessHandle process, MemoryReader reader)
{
    var (_, _, ai, _) = ResolveChain(process, reader);
    if (ai == 0) { Console.Error.WriteLine("Could not resolve chain (in game?)."); return 1; }

    var head = SafePtr(reader, ai + Poe2.AreaInstance.AwakeEntities);
    reader.TryReadStruct<int>(ai + Poe2.AreaInstance.AwakeEntities + 8, out var size);
    if (head == 0 || size <= 0) { Console.Error.WriteLine("no awake entities"); return 1; }

    const int span = 0x180;
    var perOffset = new Dictionary<int, HashSet<int>>();
    var sampled = 0;
    var queue = new Queue<nint>(); queue.Enqueue(SafePtr(reader, head + Poe2.StdMapNode.Parent));
    var visited = new HashSet<nint>();
    var buf = new byte[span];
    while (queue.Count > 0 && visited.Count < 200000 && sampled < 200)
    {
        var node = queue.Dequeue();
        if (node == 0 || node == head || !visited.Add(node)) continue;
        if (!reader.TryReadStruct<byte>(node + Poe2.StdMapNode.IsNil, out var nil) || nil != 0) continue;
        reader.TryReadStruct<uint>(node + Poe2.StdMapNode.KeyId, out var id);
        var entity = SafePtr(reader, node + Poe2.StdMapNode.ValueEntityPtr);
        queue.Enqueue(SafePtr(reader, node + Poe2.StdMapNode.Left));
        queue.Enqueue(SafePtr(reader, node + Poe2.StdMapNode.Right));
        if (entity == 0 || id >= Poe2.EntityList.VisualIdThreshold) continue;
        if (!ReadEntityMetadata(reader, entity).Contains("/Monsters/", StringComparison.Ordinal)) continue;

        var omp = ResolveComponentAddr(reader, entity, "ObjectMagicProperties");
        if (omp == 0 || reader.TryReadBytes(omp, buf) != span) continue;
        sampled++;
        for (var o = 0; o + 4 <= span; o += 4)
        {
            var v = BitConverter.ToInt32(buf, o);
            (perOffset.TryGetValue(o, out var s) ? s : perOffset[o] = new HashSet<int>()).Add(v);
        }
    }
    Console.WriteLine($"sampled {sampled} monsters' ObjectMagicProperties.");
    Console.WriteLine("offsets whose values are all in 0..3 and vary (rarity candidates):");
    foreach (var (o, set) in perOffset.OrderBy(k => k.Key))
        if (set.Count > 1 && set.All(v => v is >= 0 and <= 3))
            Console.WriteLine($"  +0x{o:X3}: values {{{string.Join(",", set.OrderBy(x => x))}}}");
    Console.WriteLine("\n(also showing offsets all in 0..4 with >=3 distinct, in case Unique/special tiers present:)");
    foreach (var (o, set) in perOffset.OrderBy(k => k.Key))
        if (set.Count >= 3 && set.All(v => v is >= 0 and <= 6))
            Console.WriteLine($"  +0x{o:X3}: values {{{string.Join(",", set.OrderBy(x => x))}}}");
    return 0;
}

// Resolve a component address by name (same StdBucket walk as Poe2Live, inline for probes).
static nint ResolveComponentAddr(MemoryReader reader, nint entity, string name)
{
    var details = SafePtr(reader, entity + Poe2.Entity.EntityDetailsPtr);
    if (details == 0) return 0;
    var lookup = SafePtr(reader, details + Poe2.EntityDetails.ComponentLookUpPtr);
    if (lookup == 0) return 0;
    if (!reader.TryReadStruct<POE2Radar.Core.Game.StdVector>(entity + Poe2.Entity.ComponentList, out var cl)) return 0;
    var compCount = ((long)cl.Last - (long)cl.First) / 8;
    if (compCount is <= 0 or > 256) return 0;
    var bFirst = SafePtr(reader, lookup + Poe2.ComponentLookUp.NameAndIndexBucket);
    if (!reader.TryReadStruct<nint>(lookup + Poe2.ComponentLookUp.NameAndIndexBucket + 8, out var bLast)) return 0;
    var entries = ((long)bLast - (long)bFirst) / Poe2.ComponentLookUp.EntryStride;
    if (bFirst == 0 || entries is <= 0 or > 256) return 0;
    for (long i = 0; i < entries; i++)
    {
        var e = bFirst + (nint)(i * Poe2.ComponentLookUp.EntryStride);
        if (!reader.TryReadStruct<int>(e + 8, out var index) || index < 0 || index >= compCount) continue;
        if (reader.ReadStringUtf8(SafePtr(reader, e), 40) != name) continue;
        return SafePtr(reader, cl.First + (nint)(index * 8));
    }
    return 0;
}

// ── Tiles: read the terrain tile grid (GameHelper2 GetTgtFileData) — each tile's TgtPath →
// grid positions. Shows what static tile-based landmarks exist (boss arenas, special rooms,
// waypoints) and whether a per-tile semantic "detail name" is reachable. TerrainStruct @
// AreaInstance+0x8A0: TotalTiles@+0x18, TileDetailsPtr StdVector@+0x28 (TileStructure=0x38);
// TileStructure.TgtFilePtr@+0x8 → TgtFileStruct.TgtPath (StdWString)@+0x8.
static int RunTiles(ProcessHandle process, MemoryReader reader)
{
    var (_, _, ai, _) = ResolveChain(process, reader);
    if (ai == 0) { Console.Error.WriteLine("Could not resolve chain (in game?)."); return 1; }
    var terrain = ai + Poe2.AreaInstance.TerrainMetadata;
    reader.TryReadStruct<long>(terrain + 0x18, out var tilesX);
    reader.TryReadStruct<nint>(terrain + 0x28, out var first);
    reader.TryReadStruct<nint>(terrain + 0x30, out var last);
    var count = first == 0 ? 0 : ((long)last - (long)first) / 0x38;
    Console.WriteLine($"AreaInstance 0x{ai:X}  terrain 0x{terrain:X}  tilesX={tilesX}  tileCount={count}");
    if (count is <= 0 or > 200000) { Console.Error.WriteLine("implausible tile count"); return 1; }

    // Dump the first non-empty tile's TgtFileStruct so we can look for a semantic detail-name ptr.
    var byPath = new Dictionary<string, int>(StringComparer.Ordinal);
    nint sampleTgt = 0;
    for (long i = 0; i < count; i++)
    {
        var tile = first + (nint)(i * 0x38);
        var tgtFile = SafePtr(reader, tile + 0x8);
        if (tgtFile == 0) continue;
        var path = ReadStdWString(reader, tgtFile + 0x8);
        if (path.Length == 0) continue;
        if (sampleTgt == 0) sampleTgt = tgtFile;
        byPath[path] = byPath.GetValueOrDefault(path) + 1;
    }
    Console.WriteLine($"distinct tile paths: {byPath.Count}");
    Console.WriteLine("\n--- paths matching boss/arena/unique/waypoint/mechanic/encounter ---");
    foreach (var kv in byPath.Where(k => k.Key.Contains("oss", StringComparison.OrdinalIgnoreCase)
            || k.Key.Contains("rena", StringComparison.OrdinalIgnoreCase)
            || k.Key.Contains("nique", StringComparison.OrdinalIgnoreCase)
            || k.Key.Contains("aypoint", StringComparison.OrdinalIgnoreCase)
            || k.Key.Contains("ncounter", StringComparison.OrdinalIgnoreCase)
            || k.Key.Contains("itual", StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(k => k.Value))
        Console.WriteLine($"  {kv.Value,4}  {kv.Key}");

    Console.WriteLine("\n--- top 25 tile paths by count ---");
    foreach (var kv in byPath.OrderByDescending(k => k.Value).Take(25))
        Console.WriteLine($"  {kv.Value,4}  {kv.Key}");

    if (sampleTgt != 0)
    {
        Console.WriteLine($"\n--- sample TgtFileStruct @ 0x{sampleTgt:X} (+0x00..+0x60; look for a detail-name ptr) ---");
        var buf = new byte[0x60];
        if (reader.TryReadBytes(sampleTgt, buf) == buf.Length)
            for (var i = 0; i < buf.Length; i += 16)
                Console.WriteLine($"  +0x{i:X2}  {string.Join(' ', Enumerable.Range(0, 16).Select(j => buf[i + j].ToString("X2")))}");
    }
    return 0;
}

static int RunLeague(ProcessHandle process, MemoryReader reader, string? needle)
{
    var (_, _, areaInstance, _) = ResolveChain(process, reader);
    if (areaInstance == 0) { Console.Error.WriteLine("Could not resolve chain (in game?)."); return 1; }

    var serverData = SafePtr(reader, areaInstance + Poe2.AreaInstance.ServerDataPtr);
    var league = serverData == 0 ? "" : ReadStdWString(reader, serverData + Poe2.ServerData.League);

    Console.WriteLine();
    Console.WriteLine("League probe");
    Console.WriteLine("------------");
    Console.WriteLine($"AreaInstance : 0x{areaInstance:X16}");
    Console.WriteLine($"ServerData   : 0x{serverData:X16} (AreaInstance+0x{Poe2.AreaInstance.ServerDataPtr:X})");
    Console.WriteLine($"League       : '{league}' (ServerData+0x{Poe2.ServerData.League:X})");

    if (serverData == 0 || string.IsNullOrWhiteSpace(needle))
        return 0;

    Console.WriteLine();
    Console.WriteLine($"Scanning ServerData for strings containing '{needle}'...");
    var hits = 0;
    for (var offset = 0; offset < 0x3000; offset += 8)
    {
        var direct = ReadStdWString(reader, serverData + offset);
        if (direct.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  std::wstring +0x{offset:X}: '{direct}'");
            hits++;
        }

        var ptr = SafePtr(reader, serverData + offset);
        if (ptr == 0) continue;
        var utf16 = reader.ReadStringUtf16(ptr, 96);
        if (utf16.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  ptr utf16    +0x{offset:X}: 0x{ptr:X16} -> '{utf16}'");
            hits++;
        }
    }

    if (hits == 0)
        Console.WriteLine("  no matching strings found in ServerData scan window");

    return 0;
}

// Mechanic scan: observe entity lifecycle across the awake and sleeping maps.
// Watches both entity maps so mechanic discovery can distinguish data loaded with the area
// from entities that only wake when the player approaches. This is deliberately read-only.
static int RunMechanicScan(
    ProcessHandle process,
    MemoryReader reader,
    int durationSeconds,
    int intervalMs,
    bool includeAll)
{
    intervalMs = Math.Clamp(intervalMs, 100, 10000);
    durationSeconds = Math.Max(0, durationSeconds);

    var stop = false;
    ConsoleCancelEventHandler cancel = (_, e) =>
    {
        e.Cancel = true;
        stop = true;
    };
    Console.CancelKeyPress += cancel;

    Console.WriteLine();
    Console.WriteLine("Mechanic lifecycle scan");
    Console.WriteLine("-----------------------");
    Console.WriteLine($"Duration      : {(durationSeconds == 0 ? "until Ctrl+C" : $"{durationSeconds}s")}");
    Console.WriteLine($"Poll interval : {intervalMs}ms");
    Console.WriteLine($"Filter        : {(includeAll ? "all real entities" : "mechanic metadata keywords")}");
    Console.WriteLine("Legend        : NEW, AWAKE/SLEEPING transition, STATE change, GONE");

    var slot = FindGameStateSlot(process, reader);
    if (slot == 0)
    {
        Console.Error.WriteLine("Could not lock GameState slot (in game?).");
        Console.CancelKeyPress -= cancel;
        return 1;
    }
    var live = new Poe2Live(reader, slot);
    Console.WriteLine($"GameState slot: 0x{slot:X16}");

    const int MissingPollsBeforeGone = 6;
    const int SourcePollsBeforeTransition = 2;
    var known = new Dictionary<uint, MechanicProbeEntity>();
    var missingPolls = new Dictionary<uint, int>();
    var pendingSources = new Dictionary<uint, (string Source, int Polls)>();
    nint previousArea = 0;
    var started = Environment.TickCount64;
    var nextSummary = started;

    try
    {
        while (!stop && (durationSeconds == 0 || Environment.TickCount64 - started < durationSeconds * 1000L))
        {
            if (!live.TryResolve(out _, out var areaInstance, out var localPlayer))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] chain unavailable; waiting...");
                Thread.Sleep(intervalMs);
                continue;
            }

            if (areaInstance != previousArea)
            {
                known.Clear();
                missingPolls.Clear();
                pendingSources.Clear();
                previousArea = areaInstance;
                Console.WriteLine();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] AREA 0x{areaInstance:X16}");
                PrintMechanicTerrainCandidates(reader, areaInstance);
            }

            var playerGrid = ReadEntityGrid(reader, localPlayer);
            var current = ReadMechanicEntitySnapshot(reader, areaInstance, playerGrid, includeAll);
            foreach (var (id, entity) in current.OrderBy(x => x.Value.Source).ThenBy(x => x.Key))
            {
                missingPolls.Remove(id);
                if (!known.TryGetValue(id, out var old))
                {
                    PrintMechanicEvent("NEW", entity);
                    known[id] = entity;
                    continue;
                }

                if (!string.Equals(old.Source, entity.Source, StringComparison.Ordinal))
                {
                    var pending = pendingSources.GetValueOrDefault(id);
                    pending = pending.Source == entity.Source
                        ? (entity.Source, pending.Polls + 1)
                        : (entity.Source, 1);
                    pendingSources[id] = pending;
                    if (pending.Polls >= SourcePollsBeforeTransition)
                    {
                        PrintMechanicEvent(entity.Source.ToUpperInvariant(), entity);
                        pendingSources.Remove(id);
                        old = entity;
                    }
                }
                else
                {
                    pendingSources.Remove(id);
                }

                if (old.IconComplete != entity.IconComplete ||
                    !string.Equals(old.LifeState, entity.LifeState, StringComparison.Ordinal) ||
                    !string.Equals(old.StateValues, entity.StateValues, StringComparison.Ordinal))
                    PrintMechanicEvent("STATE", entity);

                // Heap addresses can change while a sleeping entity keeps the same stable map id.
                // Refresh the observation silently so address rebinding does not become NEW/GONE noise.
                known[id] = entity with { Source = old.Source == entity.Source ? entity.Source : old.Source };
            }

            foreach (var (id, old) in known.ToArray())
                if (!current.ContainsKey(id))
                {
                    var polls = missingPolls.GetValueOrDefault(id) + 1;
                    if (polls < MissingPollsBeforeGone)
                    {
                        missingPolls[id] = polls;
                        continue;
                    }
                    PrintMechanicEvent("GONE", old);
                    known.Remove(id);
                    missingPolls.Remove(id);
                    pendingSources.Remove(id);
                }

            var now = Environment.TickCount64;
            if (now >= nextSummary)
            {
                var awake = current.Values.Count(x => x.Source == "awake");
                var sleeping = current.Values.Count(x => x.Source == "sleeping");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] summary awake={awake} sleeping={sleeping} total={current.Count}");
                nextSummary = now + 5000;
            }
            Thread.Sleep(intervalMs);
        }
    }
    finally
    {
        Console.CancelKeyPress -= cancel;
    }

    Console.WriteLine("Mechanic scan finished.");
    return 0;
}

static Dictionary<uint, MechanicProbeEntity> ReadMechanicEntitySnapshot(
    MemoryReader reader,
    nint areaInstance,
    System.Numerics.Vector2? playerGrid,
    bool includeAll)
{
    var result = new Dictionary<uint, MechanicProbeEntity>();
    ReadMechanicEntityMap(reader, areaInstance, Poe2.AreaInstance.SleepingEntities, "sleeping", playerGrid, includeAll, result);
    ReadMechanicEntityMap(reader, areaInstance, Poe2.AreaInstance.AwakeEntities, "awake", playerGrid, includeAll, result);
    return result;
}

static void ReadMechanicEntityMap(
    MemoryReader reader,
    nint areaInstance,
    int mapOffset,
    string source,
    System.Numerics.Vector2? playerGrid,
    bool includeAll,
    Dictionary<uint, MechanicProbeEntity> result)
{
    var head = SafePtr(reader, areaInstance + mapOffset);
    if (head == 0 ||
        !reader.TryReadStruct<int>(areaInstance + mapOffset + 8, out var size) ||
        size is <= 0 or > 200000)
        return;

    var root = SafePtr(reader, head + Poe2.StdMapNode.Parent);
    var queue = new Queue<nint>();
    var visited = new HashSet<nint>();
    queue.Enqueue(root);
    while (queue.Count > 0 && visited.Count < size + 16)
    {
        var node = queue.Dequeue();
        if (node == 0 || node == head || !visited.Add(node)) continue;
        if (!reader.TryReadStruct<byte>(node + Poe2.StdMapNode.IsNil, out var nil) || nil != 0) continue;

        queue.Enqueue(SafePtr(reader, node + Poe2.StdMapNode.Left));
        queue.Enqueue(SafePtr(reader, node + Poe2.StdMapNode.Right));

        reader.TryReadStruct<uint>(node + Poe2.StdMapNode.KeyId, out var id);
        var address = SafePtr(reader, node + Poe2.StdMapNode.ValueEntityPtr);
        if (address == 0 || id >= Poe2.EntityList.VisualIdThreshold) continue;
        var metadata = ReadEntityMetadata(reader, address);
        if (metadata.Length == 0 || (!includeAll && !LooksLikeMechanic(metadata))) continue;

        var render = ResolveComponentAddr(reader, address, "Render");
        System.Numerics.Vector2? grid = null;
        if (render != 0 &&
            reader.TryReadStruct<System.Numerics.Vector3>(render + Poe2.Render.CurrentWorldPosition, out var world))
            grid = new System.Numerics.Vector2(world.X / Poe2.WorldToGridRatio, world.Y / Poe2.WorldToGridRatio);

        var componentNames = new[]
        {
            "Life", "MinimapIcon", "Targetable", "Chest", "ObjectMagicProperties", "StateMachine"
        };
        var present = new List<string>(componentNames.Length);
        nint icon = 0;
        nint life = 0;
        nint stateMachine = 0;
        foreach (var componentName in componentNames)
        {
            var component = ResolveComponentAddr(reader, address, componentName);
            if (component == 0) continue;
            present.Add(componentName);
            if (componentName == "MinimapIcon") icon = component;
            if (componentName == "Life") life = component;
            if (componentName == "StateMachine") stateMachine = component;
        }

        int? iconComplete = null;
        if (icon != 0 && reader.TryReadStruct<int>(icon + Poe2.MinimapIcon.CompletedState, out var state))
            iconComplete = state;

        var lifeState = life == 0 ? "none" : "unreadable";
        int? hpCur = null;
        int? hpMax = null;
        if (life != 0 &&
            reader.TryReadStruct<VitalStruct>(life + Poe2.Life.Health, out var vital) &&
            vital.LooksValid())
        {
            hpCur = vital.Current;
            hpMax = vital.Max;
            lifeState = vital.Current > 0 ? "alive" : "dead";
        }

        var stateValues = ReadPrimaryStateMachineValues(reader, stateMachine);
        result[id] = new MechanicProbeEntity(
            id,
            address,
            source,
            metadata,
            grid,
            playerGrid.HasValue && grid.HasValue
                ? System.Numerics.Vector2.Distance(playerGrid.Value, grid.Value)
                : null,
            string.Join(',', present),
            iconComplete,
            lifeState,
            hpCur,
            hpMax,
            stateValues);
    }
}

static string ReadPrimaryStateMachineValues(MemoryReader reader, nint component)
{
    const int ValuesOffset = 0x160;
    if (component == 0 ||
        !reader.TryReadStruct<StdVector>(component + ValuesOffset, out var values) ||
        !TryGetVectorCount(values, sizeof(long), 1, 100, out var count))
        return "";

    var sample = new long[Math.Min(count, 16)];
    for (var i = 0; i < sample.Length; i++)
        if (!reader.TryReadStruct<long>(values.First + i * sizeof(long), out sample[i]))
            return "";
    return string.Join(',', sample);
}

static System.Numerics.Vector2? ReadEntityGrid(MemoryReader reader, nint entity)
{
    var render = ResolveComponentAddr(reader, entity, "Render");
    if (render == 0 ||
        !reader.TryReadStruct<System.Numerics.Vector3>(render + Poe2.Render.CurrentWorldPosition, out var world))
        return null;
    return new System.Numerics.Vector2(
        world.X / Poe2.WorldToGridRatio,
        world.Y / Poe2.WorldToGridRatio);
}

static bool LooksLikeMechanic(string value)
{
    if (value.Contains("/Monsters/", StringComparison.OrdinalIgnoreCase) &&
        (value.Contains("Strongbox", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("/Shrines/", StringComparison.OrdinalIgnoreCase)))
        return false;

    ReadOnlySpan<string> terms =
    [
        "Ritual", "Breach", "Brequel", "Essence", "Expedition", "Abyss", "Delirium",
        "RogueExile", "AtlasExile",
        "Strongbox", "Shrine", "Ultimatum", "Legion", "Blight", "Sanctum",
        "Incursion", "Betrayal", "Harvest", "Hellscape", "Delve", "Heist"
    ];
    foreach (var term in terms)
        if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;
    return false;
}

static void PrintMechanicTerrainCandidates(MemoryReader reader, nint areaInstance)
{
    var terrain = areaInstance + Poe2.AreaInstance.TerrainMetadata;
    reader.TryReadStruct<long>(terrain + 0x18, out var tilesX);
    reader.TryReadStruct<nint>(terrain + 0x28, out var first);
    reader.TryReadStruct<nint>(terrain + 0x30, out var last);
    var count = first == 0 ? 0 : ((long)last - (long)first) / 0x38;
    if (tilesX <= 0 || count is <= 0 or > 200000)
    {
        Console.WriteLine("Terrain clues : unavailable");
        return;
    }

    var matches = new Dictionary<string, (int Count, double SumX, double SumY)>(StringComparer.Ordinal);
    for (long i = 0; i < count; i++)
    {
        var tgt = SafePtr(reader, first + (nint)(i * 0x38) + 0x8);
        if (tgt == 0) continue;
        var path = ReadStdWString(reader, tgt + 0x8);
        if (!LooksLikeMechanicTerrain(path)) continue;
        var old = matches.GetValueOrDefault(path);
        var x = (i % tilesX) * 23.0;
        var y = (i / tilesX) * 23.0;
        matches[path] = (old.Count + 1, old.SumX + x, old.SumY + y);
    }

    Console.WriteLine($"Terrain clues : {matches.Count} mechanic path(s)");
    foreach (var (path, hit) in matches.OrderBy(x => x.Key))
        Console.WriteLine($"  TILE count={hit.Count,3} center={hit.SumX / hit.Count,7:F1},{hit.SumY / hit.Count,7:F1}  {path}");
}

static bool LooksLikeMechanicTerrain(string path)
{
    // Words such as "Abyss" also appear in ordinary terrain construction assets
    // (for example PillarTop_Abyss_Fill). League directory segments are the
    // high-confidence signal that a tile belongs to an actual mechanic.
    if (!path.Contains("/Leagues/", StringComparison.OrdinalIgnoreCase) &&
        !path.Contains("/League", StringComparison.OrdinalIgnoreCase))
        return false;
    return LooksLikeMechanic(path);
}

static int RunEntrySnapshot(
    ProcessHandle process,
    MemoryReader reader,
    bool includeAllDetails,
    int maxEntities,
    int uiMax)
{
    maxEntities = Math.Clamp(maxEntities, 100, 50000);
    uiMax = Math.Clamp(uiMax, 0, 50000);

    var (gameState, inGameState, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Map entry snapshot");
    Console.WriteLine("------------------");
    Console.WriteLine($"GameState    : 0x{gameState:X16}");
    Console.WriteLine($"InGameState  : 0x{inGameState:X16}");
    Console.WriteLine($"AreaInstance : 0x{areaInstance:X16}");
    PrintAreaInfo(reader, areaInstance);
    var playerGrid = ReadEntityGrid(reader, localPlayer);
    Console.WriteLine(playerGrid.HasValue
        ? $"Player grid  : {playerGrid.Value.X:F1},{playerGrid.Value.Y:F1}"
        : "Player grid  : unavailable");

    PrintMapHeader(reader, areaInstance, Poe2.AreaInstance.SleepingEntities, "sleeping");
    PrintMapHeader(reader, areaInstance, Poe2.AreaInstance.AwakeEntities, "awake");
    PrintMechanicTerrainCandidates(reader, areaInstance);

    var rows = new List<EntrySnapshotRow>();
    AddEntryRows(reader, areaInstance, Poe2.AreaInstance.SleepingEntities, "sleeping", playerGrid, includeAllDetails, maxEntities, rows);
    AddEntryRows(reader, areaInstance, Poe2.AreaInstance.AwakeEntities, "awake", playerGrid, includeAllDetails, maxEntities, rows);

    Console.WriteLine();
    Console.WriteLine("Entity summary");
    Console.WriteLine("--------------");
    Console.WriteLine($"Rows captured : {rows.Count} ({(includeAllDetails ? "all loaded entities" : "mechanic/rune-looking details")})");
    foreach (var group in rows.GroupBy(r => (r.Source, r.Kind))
                 .OrderBy(g => g.Key.Source)
                 .ThenByDescending(g => g.Count()))
        Console.WriteLine($"  {group.Key.Source,-8} {group.Key.Kind,-14} {group.Count(),4}");

    Console.WriteLine();
    Console.WriteLine("Entity details");
    Console.WriteLine("--------------");
    if (rows.Count == 0)
    {
        Console.WriteLine("No matching entity details. Re-run with --entry-all to dump all loaded entities.");
    }
    else
    {
        foreach (var row in rows
                     .OrderBy(r => r.Source)
                     .ThenBy(r => r.Kind, StringComparer.Ordinal)
                     .ThenBy(r => r.Distance ?? float.MaxValue)
                     .ThenBy(r => r.Id)
                     .Take(maxEntities))
        {
            var grid = row.Grid.HasValue ? $" grid={row.Grid.Value.X:F1},{row.Grid.Value.Y:F1}" : "";
            var dist = row.Distance.HasValue ? $" dist={row.Distance.Value:F1}" : "";
            var state = row.StateValues.Length > 0 ? $" sm=[{row.StateValues}]" : "";
            var icon = row.IconComplete.HasValue ? $" iconState={row.IconComplete}" : "";
            Console.WriteLine(
                $"  {row.Source,-8} {row.Kind,-14} id={row.Id,-10} addr=0x{row.Address:X16}" +
                $"{grid}{dist}{icon}{state} comps=[{row.Components}] {row.Metadata}");
        }
    }

    PrintEntryUiTextClues(reader, inGameState, uiMax);

    Console.WriteLine();
    Console.WriteLine("Suggested validation flow");
    Console.WriteLine("  1. Run immediately after entering the map.");
    Console.WriteLine("  2. Run again after moving near a mechanic.");
    Console.WriteLine("  3. Run again after completing it.");
    Console.WriteLine("Compare ids/source/kind/state to separate preloaded content from radius-spawned content.");
    return 0;
}

static void AddEntryRows(
    MemoryReader reader,
    nint areaInstance,
    int mapOffset,
    string source,
    System.Numerics.Vector2? playerGrid,
    bool includeAllDetails,
    int maxEntities,
    List<EntrySnapshotRow> rows)
{
    var added = 0;
    foreach (var (id, address, metadata) in EnumerateEntityMap(reader, areaInstance, mapOffset))
    {
        if (!includeAllDetails && !LooksLikeEntryInteresting(metadata)) continue;
        if (++added > maxEntities) break;

        var grid = ReadEntityGrid(reader, address);
        var distance = playerGrid.HasValue && grid.HasValue
            ? System.Numerics.Vector2.Distance(playerGrid.Value, grid.Value)
            : (float?)null;

        var components = ReadComponentNames(reader, address);
        var icon = ResolveComponentAddr(reader, address, "MinimapIcon");
        int? iconComplete = null;
        if (icon != 0 && reader.TryReadStruct<int>(icon + Poe2.MinimapIcon.CompletedState, out var iconState))
            iconComplete = iconState;
        var stateMachine = ResolveComponentAddr(reader, address, "StateMachine");
        var stateValues = stateMachine == 0 ? "" : ReadPrimaryStateMachineValues(reader, stateMachine);

        rows.Add(new EntrySnapshotRow(
            source,
            GuessEntryKind(metadata),
            id,
            address,
            metadata,
            grid,
            distance,
            string.Join(',', components),
            iconComplete,
            stateValues));
    }
}

static void PrintMapHeader(MemoryReader reader, nint areaInstance, int mapOffset, string label)
{
    var head = SafePtr(reader, areaInstance + mapOffset);
    var size = 0;
    if (head != 0)
        reader.TryReadStruct<int>(areaInstance + mapOffset + 8, out size);
    Console.WriteLine($"{label,-12}: offset=+0x{mapOffset:X} size={size} head=0x{head:X16}");
}

static void PrintAreaInfo(MemoryReader reader, nint areaInstance)
{
    var (code, name, level, hash) = ReadAreaSnapshot(reader, areaInstance);
    Console.WriteLine($"AreaInfo     : code='{code}' name='{name}' level={level} hash=0x{hash:X8}");
}

static (string Code, string Name, int Level, uint Hash) ReadAreaSnapshot(MemoryReader reader, nint areaInstance)
{
    var areaInfo = SafePtr(reader, areaInstance + Poe2.AreaInstance.AreaInfoPtr);
    var text = SafePtr(reader, areaInfo);
    var code = text == 0 ? "" : reader.ReadStringUtf16(text, 96);
    var name = "";
    if (text != 0 && code.Length > 0)
        name = reader.ReadStringUtf16(text + (nint)((code.Length + 1) * 2), 96);
    reader.TryReadStruct<int>(areaInstance + Poe2.AreaInstance.CurrentAreaLevel, out var level);
    reader.TryReadStruct<uint>(areaInstance + Poe2.AreaInstance.CurrentAreaHash, out var hash);
    return (code, name, level, hash);
}

static IReadOnlyList<string> ReadComponentNames(MemoryReader reader, nint entity)
{
    var result = new List<string>();
    var details = SafePtr(reader, entity + Poe2.Entity.EntityDetailsPtr);
    if (details == 0) return result;
    var lookup = SafePtr(reader, details + Poe2.EntityDetails.ComponentLookUpPtr);
    if (lookup == 0) return result;
    var first = SafePtr(reader, lookup + Poe2.ComponentLookUp.NameAndIndexBucket);
    if (first == 0 ||
        !reader.TryReadStruct<nint>(lookup + Poe2.ComponentLookUp.NameAndIndexBucket + 8, out var last))
        return result;
    var entries = ((long)last - (long)first) / Poe2.ComponentLookUp.EntryStride;
    if (entries is <= 0 or > 256) return result;
    for (long i = 0; i < entries; i++)
    {
        var entry = first + (nint)(i * Poe2.ComponentLookUp.EntryStride);
        var name = reader.ReadStringUtf8(SafePtr(reader, entry), 48);
        if (!string.IsNullOrWhiteSpace(name)) result.Add(name);
    }
    result.Sort(StringComparer.Ordinal);
    return result;
}

static bool LooksLikeEntryInteresting(string value)
{
    if (LooksLikeMechanic(value)) return true;
    ReadOnlySpan<string> terms =
    [
        "Rune", "Runeshape", "Runeforge", "Verisium", "Expedition2Encounter",
        "MapContent", "League", "Encounter", "Reward", "Chest"
    ];
    foreach (var term in terms)
        if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;
    return false;
}

static string GuessEntryKind(string metadata)
{
    ReadOnlySpan<(string Term, string Kind)> terms =
    [
        ("Runeshape", "rune"),
        ("Runeforge", "rune"),
        ("Rune", "rune"),
        ("Verisium", "verisium"),
        ("Expedition", "expedition"),
        ("Ritual", "ritual"),
        ("Breach", "breach"),
        ("Brequel", "breach"),
        ("Abyss", "abyss"),
        ("Essence", "essence"),
        ("Shrine", "shrine"),
        ("Strongbox", "strongbox"),
        ("RogueExile", "rogue-exile"),
        ("AtlasExile", "rogue-exile"),
        ("Delirium", "delirium"),
        ("Encounter", "encounter"),
        ("League", "league"),
        ("Chest", "chest")
    ];
    foreach (var (term, kind) in terms)
        if (metadata.Contains(term, StringComparison.OrdinalIgnoreCase))
            return kind;
    if (metadata.Contains("/Monsters/", StringComparison.OrdinalIgnoreCase)) return "monster";
    if (metadata.Contains("/Terrain/", StringComparison.OrdinalIgnoreCase)) return "terrain";
    return "other";
}

static void PrintEntryUiTextClues(MemoryReader reader, nint inGameState, int maxElements)
{
    if (maxElements <= 0) return;
    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    if (uiRoot == 0)
    {
        Console.WriteLine();
        Console.WriteLine("UI text clues : unavailable");
        return;
    }

    var elements = WalkUiSubtree(reader, uiRoot, maxElements, out _, out _);
    var hits = new Dictionary<string, (int Count, nint Element, int Offset)>(StringComparer.OrdinalIgnoreCase);
    foreach (var el in elements)
    {
        for (var offset = 0; offset <= 0x2C0; offset += 8)
        {
            var text = ReadStdWString(reader, el + offset);
            if (text.Length < 3 || text.Length > 120) continue;
            if (!LooksLikeEntryInteresting(text)) continue;
            var clean = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var old = hits.GetValueOrDefault(clean);
            hits[clean] = (old.Count + 1, old.Element == 0 ? el : old.Element, old.Element == 0 ? offset : old.Offset);
        }
    }

    Console.WriteLine();
    Console.WriteLine("UI text clues");
    Console.WriteLine("-------------");
    Console.WriteLine($"UI elements scanned: {elements.Count}");
    if (hits.Count == 0)
    {
        Console.WriteLine("No rune/mechanic-looking UI text found.");
        return;
    }
    foreach (var (text, hit) in hits
                 .OrderByDescending(x => x.Value.Count)
                 .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                 .Take(80))
        Console.WriteLine($"  count={hit.Count,3} el=0x{hit.Element:X16}+0x{hit.Offset:X3}  {text}");
}

static int RunRuneUiProbe(
    ProcessHandle process,
    MemoryReader reader,
    int maxElements,
    int contextChildren)
{
    maxElements = Math.Clamp(maxElements, 500, 50000);
    contextChildren = Math.Clamp(contextChildren, 0, 64);

    var (_, inGameState, areaInstance, _) = ResolveChain(process, reader);
    if (inGameState == 0 || areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    if (uiRoot == 0)
    {
        Console.Error.WriteLine("UiRoot is unavailable.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Rune / Expedition UI probe");
    Console.WriteLine("--------------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"UiRoot       : 0x{uiRoot:X16}");
    Console.WriteLine($"Scan cap     : {maxElements} UI elements");
    Console.WriteLine("Goal         : find text/data neighborhoods for available map-entry rune rewards.");
    Console.WriteLine();

    var elements = WalkUiSubtree(reader, uiRoot, maxElements, out var parents, out var childIndexes);
    var hits = new List<(nint Element, int Offset, string Text)>();
    foreach (var el in elements)
    {
        foreach (var (offset, text) in ReadUiStrings(reader, el))
        {
            if (IsRuneUiProbeHit(text))
                hits.Add((el, offset, text));
        }
    }

    Console.WriteLine($"Elements scanned : {elements.Count}");
    Console.WriteLine($"Anchor hits      : {hits.Count}");
    if (hits.Count == 0)
    {
        Console.WriteLine("No Expedition/rune UI anchors found. Open/hover the map-content or rune UI and retry.");
        return 0;
    }

    foreach (var hit in hits
                 .GroupBy(h => (h.Element, h.Text), h => h.Offset)
                 .Select(g => (g.Key.Element, Offset: g.Min(), g.Key.Text))
                 .OrderBy(h => h.Text, StringComparer.OrdinalIgnoreCase)
                 .ThenBy(h => h.Element)
                 .Take(24))
    {
        Console.WriteLine();
        Console.WriteLine($"ANCHOR 0x{hit.Element:X16}+0x{hit.Offset:X3}  {hit.Text}");
        Console.WriteLine($"  visible={ReadUiVisible(reader, hit.Element)} children={TryGetUiChildCount(reader, hit.Element)} path={UiPath(hit.Element, parents, childIndexes)}");

        if (parents.TryGetValue(hit.Element, out var parent) && parent != 0)
        {
            Console.WriteLine($"  parent 0x{parent:X16} visible={ReadUiVisible(reader, parent)} children={TryGetUiChildCount(reader, parent)}");
            PrintUiStringBlock(reader, parent, "    parent text");
        }

        PrintUiStringBlock(reader, hit.Element, "    anchor text");

        var children = ReadUiChildren(reader, hit.Element, contextChildren);
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var strings = ReadUiStrings(reader, child).ToArray();
            if (strings.Length == 0 && TryGetUiChildCount(reader, child) == 0) continue;
            Console.WriteLine($"    child[{i}] 0x{child:X16} visible={ReadUiVisible(reader, child)} children={TryGetUiChildCount(reader, child)}");
            foreach (var (offset, text) in strings.Take(16))
                Console.WriteLine($"      +0x{offset:X3} {text}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("Next step: if no reward names appear, rerun while hovering/opening the rune/Expedition reward UI.");
    return 0;
}

static IEnumerable<(int Offset, string Text)> ReadUiStrings(MemoryReader reader, nint element)
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (var offset = 0; offset <= 0x420; offset += 8)
    {
        var text = ReadStdWString(reader, element + offset);
        if (text.Length < 2 || text.Length > 160) continue;
        var clean = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length < 2 || !seen.Add(clean)) continue;
        yield return (offset, clean);
    }
}

static bool IsRuneUiProbeHit(string text)
{
    ReadOnlySpan<string> terms =
    [
        "ExpeditionCurrencySummaryDisplay",
        "verisium_smithing_window",
        "Expedition2Encounter",
        "Runeshape",
        "Runeforge",
        "Rune",
        "Verisium",
        "Broken Circle",
        "Stone Circle",
        "Exotic Coinage",
        "Artifact",
        "Logbook",
    ];
    foreach (var term in terms)
        if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;
    return false;
}

static void PrintUiStringBlock(MemoryReader reader, nint element, string label)
{
    var strings = ReadUiStrings(reader, element).Take(24).ToArray();
    if (strings.Length == 0) return;
    Console.WriteLine(label + ":");
    foreach (var (offset, text) in strings)
        Console.WriteLine($"      +0x{offset:X3} {text}");
}

static string UiPath(
    nint element,
    Dictionary<nint, nint> parents,
    Dictionary<nint, int> childIndexes)
{
    var parts = new List<string>();
    var cur = element;
    var guard = 0;
    while (cur != 0 && guard++ < 24)
    {
        var index = childIndexes.GetValueOrDefault(cur, -1);
        parts.Add(index >= 0 ? index.ToString() : "root");
        if (!parents.TryGetValue(cur, out cur)) break;
    }
    parts.Reverse();
    return string.Join('/', parts);
}

static int RunRuneforgeRead(
    ProcessHandle process,
    MemoryReader reader,
    int winW,
    int winH)
{
    winW = Math.Clamp(winW, 640, 10000);
    winH = Math.Clamp(winH, 480, 10000);

    var (_, inGameState, areaInstance, _) = ResolveChain(process, reader);
    if (inGameState == 0 || areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Runeforge reward reader");
    Console.WriteLine("-----------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Window size : {winW}x{winH}");
    Console.WriteLine("Source      : Poe2Runeforge, visible row kid[0]+0x390");

    var runeforge = new Poe2Runeforge(reader);
    var rewards = runeforge.ReadRewards(inGameState, winW, winH);
    Console.WriteLine($"Panel open  : {runeforge.PanelOpen}");
    Console.WriteLine($"Rows read   : {rewards.Count}");
    if (rewards.Count == 0)
    {
        Console.WriteLine("No rows read. Open the Runeshape Combinations panel and retry.");
        return 0;
    }

    for (var i = 0; i < rewards.Count; i++)
    {
        var r = rewards[i];
        Console.WriteLine(
            $"  row[{i}] count={r.Count,-3} name='{r.Name}' rect=({r.X:F1},{r.Y:F1},{r.W:F1},{r.H:F1})");
    }

    Console.WriteLine();
    Console.WriteLine("If these names match the panel, the overlay can price them when Show Runeforge values is enabled.");
    return 0;
}

static int RunRuneforgeCorrelate(
    ProcessHandle process,
    MemoryReader reader,
    int winW,
    int winH,
    int uiMax)
{
    winW = Math.Clamp(winW, 640, 10000);
    winH = Math.Clamp(winH, 480, 10000);
    uiMax = Math.Clamp(uiMax, 1000, 60000);

    var (_, inGameState, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (inGameState == 0 || areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }
    var area = ReadAreaSnapshot(reader, areaInstance);

    Console.WriteLine();
    Console.WriteLine("Runeforge reward correlation probe");
    Console.WriteLine("----------------------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Window size : {winW}x{winH}");
    Console.WriteLine($"UI scan cap : {uiMax}");
    Console.WriteLine("Goal        : visible reward text -> UI row address -> active controller/encounter stats.");

    var runeforge = new Poe2Runeforge(reader);
    var rewards = runeforge.ReadRewards(inGameState, winW, winH);
    Console.WriteLine();
    Console.WriteLine($"Panel open  : {runeforge.PanelOpen}");
    Console.WriteLine($"Rows read   : {rewards.Count}");
    if (rewards.Count == 0)
    {
        Console.WriteLine("No visible Runeshape rows. Continuing with hidden UI/entity scan for map-entry research.");
        PrintRuneforgeUiTextHints(reader, inGameState, uiMax);
        var closedPanelRows = PrintRuneforgeEntityCorrelation(reader, areaInstance, localPlayer);
        PrintRuneforgeMachineSample(area, rewards, closedPanelRows);
        return 0;
    }

    for (var i = 0; i < rewards.Count; i++)
    {
        var r = rewards[i];
        Console.WriteLine(
            $"  row[{i,2}] count={r.Count,-3} name='{r.Name}' key='{RewardKey(r.Name)}' rect=({r.X:F1},{r.Y:F1},{r.W:F1},{r.H:F1})");
    }

    PrintRuneforgeUiRowMatches(reader, inGameState, rewards, uiMax);
    var compactRows = PrintRuneforgeEntityCorrelation(reader, areaInstance, localPlayer);
    PrintRuneforgeMachineSample(area, rewards, compactRows);
    return 0;
}

static void PrintRuneforgeUiTextHints(MemoryReader reader, nint inGameState, int uiMax)
{
    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    Console.WriteLine();
    Console.WriteLine("Rune-looking UI text while panel is closed/empty");
    Console.WriteLine("-----------------------------------------------");
    if (uiRoot == 0)
    {
        Console.WriteLine("UiRoot unavailable.");
        return;
    }

    var elements = WalkUiSubtree(reader, uiRoot, uiMax, out var parents, out var childIndexes);
    var hits = new List<(nint Element, int Offset, string Text)>();
    foreach (var el in elements)
    {
        foreach (var (offset, text) in ReadUiStrings(reader, el))
        {
            if (LooksLikeRuneforgeRewardText(text))
                hits.Add((el, offset, text));
        }
    }

    Console.WriteLine($"UI elements scanned: {elements.Count}");
    Console.WriteLine($"Candidate strings  : {hits.Count}");
    foreach (var hit in hits
                 .GroupBy(h => h.Text, StringComparer.OrdinalIgnoreCase)
                 .Select(g => g.OrderBy(h => UiPath(h.Element, parents, childIndexes), StringComparer.Ordinal).First())
                 .OrderBy(h => h.Text, StringComparer.OrdinalIgnoreCase)
                 .Take(80))
    {
        Console.WriteLine(
            $"  el=0x{hit.Element:X16}+0x{hit.Offset:X3} visible={ReadUiVisible(reader, hit.Element),-5} " +
            $"children={TryGetUiChildCount(reader, hit.Element),3} path={UiPath(hit.Element, parents, childIndexes)} text='{hit.Text}'");
    }
}

static bool LooksLikeRuneforgeRewardText(string text)
{
    if (text.Length is < 5 or > 120) return false;
    if (text.Contains("Runeshape", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Runeforge", StringComparison.OrdinalIgnoreCase))
        return true;

    if (text.Contains("Rune of ", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Greater ", StringComparison.OrdinalIgnoreCase) && text.Contains(" Orb", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Perfect ", StringComparison.OrdinalIgnoreCase) && text.Contains(" Orb", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Uncut ", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Soul Core", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Alloy", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Flux", StringComparison.OrdinalIgnoreCase))
        return true;

    return false;
}

static void PrintRuneforgeUiRowMatches(
    MemoryReader reader,
    nint inGameState,
    IReadOnlyList<Poe2Runeforge.RuneReward> rewards,
    int uiMax)
{
    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    Console.WriteLine();
    Console.WriteLine("Visible reward UI label candidates");
    Console.WriteLine("----------------------------------");
    if (uiRoot == 0)
    {
        Console.WriteLine("UiRoot unavailable.");
        return;
    }

    var elements = WalkUiSubtree(reader, uiRoot, uiMax, out var parents, out var childIndexes);
    var allText = new List<(nint Element, int Offset, string Text)>();
    foreach (var el in elements)
        foreach (var (offset, text) in ReadUiStrings(reader, el))
            allText.Add((el, offset, text));

    Console.WriteLine($"UI elements scanned: {elements.Count}");
    for (var i = 0; i < rewards.Count; i++)
    {
        var reward = rewards[i];
        var exact = $"{reward.Count}x {reward.Name}";
        var key = RewardKey(reward.Name);
        var exactMatches = allText
            .Where(t => string.Equals(t.Text, exact, StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .ToArray();
        var fuzzyMatches = allText
            .Where(t => !string.Equals(t.Text, exact, StringComparison.OrdinalIgnoreCase) &&
                        RewardKey(t.Text).Contains(key, StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToArray();
        var matches = exactMatches.Concat(fuzzyMatches).ToArray();
        Console.WriteLine($"  row[{i,2}] '{exact}' exact={exactMatches.Length} fuzzy={fuzzyMatches.Length}");
        foreach (var match in matches)
        {
            var parent = parents.GetValueOrDefault(match.Element);
            Console.WriteLine(
                $"    el=0x{match.Element:X16}+0x{match.Offset:X3} visible={ReadUiVisible(reader, match.Element),-5} " +
                $"children={TryGetUiChildCount(reader, match.Element),3} path={UiPath(match.Element, parents, childIndexes)} text='{match.Text}'");
            if (parent != 0)
            {
                var siblingText = ReadUiChildren(reader, parent, 16)
                    .SelectMany(child => ReadUiStrings(reader, child).Take(4).Select(s => s.Text))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8);
                Console.WriteLine($"      parent=0x{parent:X16} siblings=[{string.Join(" | ", siblingText)}]");
            }
        }
    }
}

static IReadOnlyList<(string Source, uint Id, string Kind, string Distance, string Grid, string State, string Local, string Changed, string Metadata)>
    PrintRuneforgeEntityCorrelation(MemoryReader reader, nint areaInstance, nint localPlayer)
{
    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    var rows = new List<(string Source, uint Id, nint Entity, string Metadata, System.Numerics.Vector2? Grid, float? Dist)>();
    foreach (var (source, mapOffset) in new[]
             {
                 ("sleeping", Poe2.AreaInstance.SleepingEntities),
                 ("awake", Poe2.AreaInstance.AwakeEntities)
             })
    {
        foreach (var (id, entity, metadata) in EnumerateEntityMap(reader, areaInstance, mapOffset))
        {
            if (!metadata.Contains("Expedition2", StringComparison.OrdinalIgnoreCase) &&
                !metadata.Contains("LeagueExpeditionNew", StringComparison.OrdinalIgnoreCase) &&
                !metadata.Contains("RuneEncounterController", StringComparison.OrdinalIgnoreCase))
                continue;

            var grid = ReadEntityGrid(reader, entity);
            var dist = playerGrid.HasValue && grid.HasValue
                ? System.Numerics.Vector2.Distance(playerGrid.Value, grid.Value)
                : (float?)null;
            rows.Add((source, id, entity, metadata, grid, dist));
        }
    }

    Console.WriteLine();
    Console.WriteLine("Rune/Expedition entity correlation");
    Console.WriteLine("----------------------------------");
    if (rows.Count == 0)
    {
        Console.WriteLine("No Rune/Expedition entities found.");
        return [];
    }

    var orderedRows = rows.OrderBy(r => r.Dist ?? float.MaxValue).ThenBy(r => r.Id).ToArray();
    var compactRows = PrintRuneforgeCompactSample(reader, orderedRows);

    foreach (var row in orderedRows)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"ENTITY {row.Source,-8} id={row.Id,-10} addr=0x{row.Entity:X16} " +
            $"grid={FormatGrid(row.Grid),-15} dist={FormatDistance(row.Dist),6} {row.Metadata}");

        var components = ReadComponentMap(reader, row.Entity);
        Console.WriteLine($"  components=[{string.Join(", ", components.Select(c => c.Name))}]");

        var stateMachine = components.FirstOrDefault(c => c.Name == "StateMachine").Address;
        if (stateMachine != 0)
        {
            Console.WriteLine($"  StateMachine @ 0x{stateMachine:X16}");
            PrintStateMachineCandidates(reader, stateMachine);
        }

        var stats = components.FirstOrDefault(c => c.Name == "Stats").Address;
        if (stats != 0)
        {
            Console.WriteLine($"  Stats @ 0x{stats:X16}");
            PrintStatsVectorFull(reader, stats + Poe2.StatsComponent.ItemLocalStatsVec, "ItemLocalStats");
            var statsChanged = SafePtr(reader, stats + Poe2.StatsComponent.StatsChangedByItemsPtr);
            if (statsChanged != 0)
                PrintStatsVectorFull(reader, statsChanged + Poe2.StatsComponent.StatsStructStatsVec, "ChangedByItems.Stats");
        }
    }

    return compactRows;
}

static IReadOnlyList<(string Source, uint Id, string Kind, string Distance, string Grid, string State, string Local, string Changed, string Metadata)>
    PrintRuneforgeCompactSample(
    MemoryReader reader,
    IReadOnlyList<(string Source, uint Id, nint Entity, string Metadata, System.Numerics.Vector2? Grid, float? Dist)> rows)
{
    Console.WriteLine();
    Console.WriteLine("Compact stat signatures");
    Console.WriteLine("-----------------------");
    var compactRows = new List<(string Source, uint Id, string Kind, string Distance, string Grid, string State, string Local, string Changed, string Metadata)>();
    foreach (var row in rows)
    {
        var stats = ResolveComponentAddr(reader, row.Entity, "Stats");
        var stateMachine = ResolveComponentAddr(reader, row.Entity, "StateMachine");
        var changed = stats == 0 ? [] : ReadStatsChangedByItems(reader, stats);
        var local = stats == 0 ? [] : ReadStatsLocal(reader, stats);
        var state = stateMachine == 0 ? "" : ReadPrimaryStateMachineValues(reader, stateMachine);
        var kind = CompactKind(row.Metadata);
        var distance = FormatDistance(row.Dist);
        var grid = FormatGrid(row.Grid);
        var localText = FormatStats(local);
        var changedText = FormatStats(changed);
        compactRows.Add((row.Source, row.Id, kind, distance, grid, state, localText, changedText, row.Metadata));
        Console.WriteLine(
            $"  SAMPLE id={row.Id} kind={kind} dist={distance} " +
            $"grid={grid} state=[{state}] local=[{localText}] changed=[{changedText}]");
    }

    return compactRows;
}

static void PrintRuneforgeMachineSample(
    (string Code, string Name, int Level, uint Hash) area,
    IReadOnlyList<Poe2Runeforge.RuneReward> rewards,
    IReadOnlyList<(string Source, uint Id, string Kind, string Distance, string Grid, string State, string Local, string Changed, string Metadata)> compactRows)
{
    Console.WriteLine();
    Console.WriteLine("Copyable correlation sample");
    Console.WriteLine("---------------------------");
    var rewardText = string.Join(" || ", rewards.Select(r => $"{r.Count}x {SampleEscape(r.Name)}"));
    var entityText = string.Join(" || ", compactRows.Select(r =>
        $"{r.Kind}#{r.Id}@{r.Source} dist={r.Distance} grid={SampleEscape(r.Grid)} state=[{r.State}] local=[{r.Local}] changed=[{r.Changed}] meta={SampleEscape(r.Metadata)}"));
    Console.WriteLine(
        $"RUNEFORGE_SAMPLE area={SampleEscape(area.Code)} name={SampleEscape(area.Name)} level={area.Level} hash=0x{area.Hash:X8} " +
        $"rewards=[{rewardText}] entities=[{entityText}]");
}

static string SampleEscape(string value) =>
    value.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("[", "\\[", StringComparison.Ordinal)
         .Replace("]", "\\]", StringComparison.Ordinal)
         .Replace("|", "\\|", StringComparison.Ordinal);

static List<(int Id, int Value)> ReadStatsLocal(MemoryReader reader, nint stats)
{
    if (!reader.TryReadStruct<StdVector>(stats + Poe2.StatsComponent.ItemLocalStatsVec, out var vector) ||
        !TryGetVectorCount(vector, Poe2.StatsComponent.StatArrayStride, 1, 2048, out var count))
        return [];
    return ReadStatsEntries(reader, vector, count);
}

static List<(int Id, int Value)> ReadStatsChangedByItems(MemoryReader reader, nint stats)
{
    var statsChanged = SafePtr(reader, stats + Poe2.StatsComponent.StatsChangedByItemsPtr);
    if (statsChanged == 0 ||
        !reader.TryReadStruct<StdVector>(statsChanged + Poe2.StatsComponent.StatsStructStatsVec, out var vector) ||
        !TryGetVectorCount(vector, Poe2.StatsComponent.StatArrayStride, 1, 2048, out var count))
        return [];
    return ReadStatsEntries(reader, vector, count);
}

static string FormatStats(IReadOnlyList<(int Id, int Value)> stats) =>
    stats.Count == 0 ? "" : string.Join(';', stats.Select(e => $"{e.Id}:{e.Value}"));

static string CompactKind(string metadata)
{
    if (metadata.Contains("RuneEncounterController", StringComparison.OrdinalIgnoreCase)) return "controller";
    if (metadata.Contains("Expedition2Encounter", StringComparison.OrdinalIgnoreCase)) return "encounter";
    if (metadata.Contains("HiddenEncounterChest", StringComparison.OrdinalIgnoreCase)) return "reward-chest";
    return GuessEntryKind(metadata);
}

static void PrintStatsVectorFull(MemoryReader reader, nint vectorAddress, string label)
{
    if (!reader.TryReadStruct<StdVector>(vectorAddress, out var vector) ||
        !TryGetVectorCount(vector, Poe2.StatsComponent.StatArrayStride, 1, 2048, out var count))
        return;

    var entries = ReadStatsEntries(reader, vector, count);
    Console.WriteLine($"    STATS {label,-20} count={entries.Count}");
    foreach (var chunk in entries.Chunk(8))
        Console.WriteLine($"      {string.Join(", ", chunk.Select(e => $"{e.Id}:{e.Value}"))}");
}

static List<(int Id, int Value)> ReadStatsEntries(MemoryReader reader, StdVector vector, int count)
{
    var result = new List<(int Id, int Value)>(Math.Min(count, 2048));
    for (var i = 0; i < count; i++)
    {
        if (!reader.TryReadStruct<int>(vector.First + i * Poe2.StatsComponent.StatArrayStride, out var id) ||
            !reader.TryReadStruct<int>(vector.First + i * Poe2.StatsComponent.StatArrayStride + 4, out var value))
            break;
        result.Add((id, value));
    }
    return result;
}

static string RewardKey(string value)
{
    Span<char> buffer = stackalloc char[Math.Min(value.Length, 160)];
    var n = 0;
    foreach (var ch in value)
    {
        if (n >= buffer.Length) break;
        if (char.IsLetterOrDigit(ch))
            buffer[n++] = char.ToLowerInvariant(ch);
    }
    return new string(buffer[..n]);
}

static void PrintMechanicEvent(string eventName, MechanicProbeEntity entity)
{
    var complete = entity.IconComplete.HasValue ? $" iconState={entity.IconComplete}" : "";
    var grid = entity.Grid.HasValue ? $" grid={entity.Grid.Value.X:F1},{entity.Grid.Value.Y:F1}" : "";
    var distance = entity.Distance.HasValue ? $" dist={entity.Distance:F1}" : "";
    var hp = entity.HpMax.HasValue ? $" hp={entity.HpCur}/{entity.HpMax}" : $" life={entity.LifeState}";
    var state = entity.StateValues.Length > 0 ? $" sm=[{entity.StateValues}]" : "";
    Console.WriteLine(
        $"[{DateTime.Now:HH:mm:ss.fff}] {eventName,-8} {entity.Source,-8} id={entity.Id,-10} " +
        $"addr=0x{entity.Address:X16}{grid}{distance}{complete}{hp}{state} " +
        $"comps=[{entity.Components}] {entity.Metadata}");
}

// Recover selected component layouts without relying on ExileCore2's protected offset table.
// The candidate shapes come from public ExileCore/GameHelper implementations, while every
// reported offset is validated against the current PoE2 process.
static int RunComponentLayoutScan(
    ProcessHandle process,
    MemoryReader reader,
    bool includeAll,
    int maxEntities)
{
    var (_, _, areaInstance, _) = ResolveChain(process, reader);
    if (areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    maxEntities = Math.Clamp(maxEntities, 1, 5000);
    Console.WriteLine();
    Console.WriteLine("Component layout scan");
    Console.WriteLine("---------------------");
    Console.WriteLine($"Area instance : 0x{areaInstance:X16}");
    Console.WriteLine($"Filter        : {(includeAll ? "all real entities" : "mechanic metadata keywords")}");
    Console.WriteLine($"Entity cap    : {maxEntities}");
    Console.WriteLine("Reference     : ExileCore StateMachine pointer/vector geometry; OMP 24+64-byte mod records");

    var seen = new HashSet<uint>();
    var scanned = 0;
    var stateComponents = 0;
    var stateCandidates = 0;
    var magicComponents = 0;
    var magicCandidates = 0;

    foreach (var (source, mapOffset) in new[]
             {
                 ("sleeping", Poe2.AreaInstance.SleepingEntities),
                 ("awake", Poe2.AreaInstance.AwakeEntities)
             })
    {
        foreach (var (id, entity, metadata) in EnumerateEntityMap(reader, areaInstance, mapOffset))
        {
            if (!seen.Add(id) || (!includeAll && !LooksLikeMechanic(metadata))) continue;
            if (++scanned > maxEntities) break;

            var stateMachine = ResolveComponentAddr(reader, entity, "StateMachine");
            var magicProperties = ResolveComponentAddr(reader, entity, "ObjectMagicProperties");
            if (stateMachine == 0 && magicProperties == 0) continue;

            Console.WriteLine();
            Console.WriteLine(
                $"ENTITY {source,-8} id={id,-10} addr=0x{entity:X16} {metadata}");

            if (stateMachine != 0)
            {
                stateComponents++;
                Console.WriteLine($"  StateMachine          : 0x{stateMachine:X16}");
                stateCandidates += PrintStateMachineCandidates(reader, stateMachine);
            }

            if (magicProperties != 0)
            {
                magicComponents++;
                Console.WriteLine($"  ObjectMagicProperties : 0x{magicProperties:X16}");
                magicCandidates += PrintMagicPropertyCandidates(reader, magicProperties);
            }
        }

        if (scanned >= maxEntities) break;
    }

    Console.WriteLine();
    Console.WriteLine(
        $"Scanned {Math.Min(scanned, maxEntities)} entities; " +
        $"StateMachine {stateComponents} component(s), {stateCandidates} candidate(s); " +
        $"ObjectMagicProperties {magicComponents} component(s), {magicCandidates} candidate(s).");
    if (stateComponents > 0 && stateCandidates == 0)
        Console.WriteLine("No StateMachine table decoded. Re-run with --mechanic-all in an active encounter.");
    if (magicComponents > 0 && magicCandidates == 0)
        Console.WriteLine("No 64-byte mod vector decoded. Test near an Essence rare or other modified monster.");
    return 0;
}

static int RunMechanicComponentProbe(
    ProcessHandle process,
    MemoryReader reader,
    bool includeAll,
    string? filterText,
    int maxEntities,
    int componentWindow)
{
    var (_, _, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    maxEntities = Math.Clamp(maxEntities, 1, 5000);
    componentWindow = Math.Clamp(componentWindow, 0x80, 0x4000);
    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    var filters = ParseProbeFilters(filterText);
    var explicitFilter = !string.IsNullOrWhiteSpace(filterText);

    Console.WriteLine();
    Console.WriteLine("Mechanic component probe");
    Console.WriteLine("------------------------");
    Console.WriteLine($"Area instance   : 0x{areaInstance:X16}");
    Console.WriteLine($"Filter          : {(includeAll ? "all real entities" : string.Join(", ", filters))}");
    Console.WriteLine($"Entity cap      : {maxEntities}");
    Console.WriteLine($"Component window: 0x{componentWindow:X} bytes");
    Console.WriteLine("Goal            : compare entry/open/completed logs to find current-map mechanic/reward state.");

    var seen = new HashSet<uint>();
    var scanned = 0;
    foreach (var (source, mapOffset) in new[]
             {
                 ("sleeping", Poe2.AreaInstance.SleepingEntities),
                 ("awake", Poe2.AreaInstance.AwakeEntities)
             })
    {
        foreach (var (id, entity, metadata) in EnumerateEntityMap(reader, areaInstance, mapOffset))
        {
            if (!seen.Add(id)) continue;
            if (!includeAll)
            {
                var matchesFilter = MatchesAnyFilter(metadata, filters);
                if (!matchesFilter && (explicitFilter || !LooksLikeMechanic(metadata))) continue;
            }
            if (++scanned > maxEntities) break;

            var grid = ReadEntityGrid(reader, entity);
            var distance = playerGrid.HasValue && grid.HasValue
                ? System.Numerics.Vector2.Distance(playerGrid.Value, grid.Value)
                : (float?)null;
            var components = ReadComponentMap(reader, entity);

            Console.WriteLine();
            Console.WriteLine(
                $"ENTITY {source,-8} id={id,-10} addr=0x{entity:X16} " +
                $"grid={FormatGrid(grid),-15} dist={FormatDistance(distance),6} {metadata}");
            Console.WriteLine($"  Components ({components.Count}):");
            foreach (var (name, index, address) in components)
                Console.WriteLine($"    [{index,2}] {name,-28} 0x{address:X16}");

            foreach (var (name, _, address) in components)
            {
                if (address == 0) continue;
                Console.WriteLine($"  COMPONENT {name} @ 0x{address:X16}");
                if (name.Equals("StateMachine", StringComparison.Ordinal))
                    PrintStateMachineCandidates(reader, address);
                if (name.Equals("ObjectMagicProperties", StringComparison.Ordinal))
                    PrintMagicPropertyCandidates(reader, address);
                if (name.Equals("Stats", StringComparison.Ordinal))
                    PrintStatsComponentClues(reader, address);
                if (name.Equals("Inventories", StringComparison.Ordinal))
                    PrintInventoryRewardClues(reader, address, componentWindow);

                PrintComponentVectorClues(reader, address, componentWindow);
                PrintComponentStringClues(reader, address, componentWindow);
            }
        }

        if (scanned >= maxEntities) break;
    }

    Console.WriteLine();
    Console.WriteLine($"Scanned {Math.Min(scanned, maxEntities)} entity/entities.");
    Console.WriteLine("Recommended sequence:");
    Console.WriteLine("  1. Run at map entry before touching Runeforge.");
    Console.WriteLine("  2. Run again with Runeshape Combinations open.");
    Console.WriteLine("  3. Run again after claiming/completing, then compare component clues.");
    return 0;
}

static string[] ParseProbeFilters(string? filterText)
{
    if (string.IsNullOrWhiteSpace(filterText))
        return
        [
            "Expedition2",
            "LeagueExpeditionNew",
            "Rune",
            "Runeshape",
            "Runeforge",
            "Verisium"
        ];

    return filterText
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => x.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static bool MatchesAnyFilter(string value, IReadOnlyList<string> filters)
{
    foreach (var filter in filters)
        if (value.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;
    return false;
}

static IReadOnlyList<(string Name, int Index, nint Address)> ReadComponentMap(MemoryReader reader, nint entity)
{
    var result = new List<(string Name, int Index, nint Address)>();
    var details = SafePtr(reader, entity + Poe2.Entity.EntityDetailsPtr);
    if (details == 0) return result;
    var lookup = SafePtr(reader, details + Poe2.EntityDetails.ComponentLookUpPtr);
    if (lookup == 0) return result;
    if (!reader.TryReadStruct<StdVector>(entity + Poe2.Entity.ComponentList, out var componentList) ||
        !TryGetVectorCount(componentList, 8, 1, 256, out var componentCount))
        return result;

    var first = SafePtr(reader, lookup + Poe2.ComponentLookUp.NameAndIndexBucket);
    if (first == 0 ||
        !reader.TryReadStruct<nint>(lookup + Poe2.ComponentLookUp.NameAndIndexBucket + 8, out var last))
        return result;
    var entries = ((long)last - (long)first) / Poe2.ComponentLookUp.EntryStride;
    if (entries is <= 0 or > 256) return result;

    for (long i = 0; i < entries; i++)
    {
        var entry = first + (nint)(i * Poe2.ComponentLookUp.EntryStride);
        var name = reader.ReadStringUtf8(SafePtr(reader, entry), 64);
        if (!reader.TryReadStruct<int>(entry + 8, out var index) ||
            string.IsNullOrWhiteSpace(name) ||
            index < 0 ||
            index >= componentCount)
            continue;

        var address = SafePtr(reader, componentList.First + (nint)(index * 8));
        result.Add((name, index, address));
    }

    result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
    return result;
}

static void PrintStatsComponentClues(MemoryReader reader, nint component)
{
    PrintStatsVector(reader, component + Poe2.StatsComponent.ItemLocalStatsVec, "ItemLocalStats");
    var statsChanged = SafePtr(reader, component + Poe2.StatsComponent.StatsChangedByItemsPtr);
    if (statsChanged != 0)
        PrintStatsVector(reader, statsChanged + Poe2.StatsComponent.StatsStructStatsVec, "ChangedByItems.Stats");
}

static void PrintStatsVector(MemoryReader reader, nint vectorAddress, string label)
{
    if (!reader.TryReadStruct<StdVector>(vectorAddress, out var vector) ||
        !TryGetVectorCount(vector, Poe2.StatsComponent.StatArrayStride, 1, 1024, out var count))
        return;

    var sample = new List<string>();
    for (var i = 0; i < Math.Min(count, 8); i++)
    {
        if (!reader.TryReadStruct<int>(vector.First + i * Poe2.StatsComponent.StatArrayStride, out var id) ||
            !reader.TryReadStruct<int>(vector.First + i * Poe2.StatsComponent.StatArrayStride + 4, out var value))
            break;
        sample.Add($"{id}:{value}");
    }
    Console.WriteLine($"    STATS {label,-20} count={count} sample=[{string.Join(", ", sample)}]");
}

static void PrintInventoryRewardClues(MemoryReader reader, nint component, int componentWindow)
{
    var hits = new List<(int Offset, string Kind, string Text)>();
    for (var offset = 0; offset <= componentWindow - 8; offset += 8)
    {
        foreach (var clue in ReadStringCluesAt(reader, component, offset))
        {
            if (LooksLikeRuneforgeRewardText(clue.Text) || LooksLikeRuneforgeModText(clue.Text))
                hits.Add((offset, clue.Kind, clue.Text));
        }
    }

    if (hits.Count == 0) return;

    Console.WriteLine("    INVENTORY reward/mod text clues:");
    foreach (var hit in hits
                 .GroupBy(h => h.Text, StringComparer.OrdinalIgnoreCase)
                 .Select(g => g.OrderBy(h => h.Offset).First())
                 .OrderBy(h => h.Offset)
                 .Take(48))
        Console.WriteLine($"      +0x{hit.Offset:X3} {hit.Kind,-12} {hit.Text}");
}

static bool LooksLikeRuneforgeModText(string text)
{
    if (text.Length is < 8 or > 220) return false;
    return text.Contains("[", StringComparison.Ordinal) && text.Contains("]", StringComparison.Ordinal) ||
           text.Contains("Damage", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("Life", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("Mana", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("Resistance", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("Attributes", StringComparison.OrdinalIgnoreCase);
}

static int RunRuneforgeInventoryProbe(ProcessHandle process, MemoryReader reader, int componentWindow)
{
    componentWindow = Math.Clamp(componentWindow, 0x80, 0x4000);
    var (_, _, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    Console.WriteLine();
    Console.WriteLine("Runeforge inventory probe");
    Console.WriteLine("-------------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Component window: 0x{componentWindow:X}");
    Console.WriteLine("Goal            : test RuneEncounterController Inventories vectors as inventory/item containers.");

    var controllers = new List<(string Source, uint Id, nint Entity, string Metadata, System.Numerics.Vector2? Grid, float? Dist)>();
    foreach (var (source, mapOffset) in new[] { ("sleeping", Poe2.AreaInstance.SleepingEntities), ("awake", Poe2.AreaInstance.AwakeEntities) })
    {
        foreach (var (id, entity, metadata) in EnumerateEntityMap(reader, areaInstance, mapOffset))
        {
            if (!metadata.Contains("RuneEncounterController", StringComparison.OrdinalIgnoreCase)) continue;
            var grid = ReadEntityGrid(reader, entity);
            var dist = playerGrid.HasValue && grid.HasValue
                ? System.Numerics.Vector2.Distance(playerGrid.Value, grid.Value)
                : (float?)null;
            controllers.Add((source, id, entity, metadata, grid, dist));
        }
    }

    if (controllers.Count == 0)
    {
        Console.WriteLine("No RuneEncounterController found. Open/approach the Runeshape panel and rerun.");
        return 0;
    }

    foreach (var controller in controllers.OrderBy(c => c.Dist ?? float.MaxValue))
    {
        Console.WriteLine();
        Console.WriteLine(
            $"CONTROLLER {controller.Source,-8} id={controller.Id} addr=0x{controller.Entity:X16} " +
            $"grid={FormatGrid(controller.Grid)} dist={FormatDistance(controller.Dist)} {controller.Metadata}");

        var inventories = ResolveComponentAddr(reader, controller.Entity, "Inventories");
        if (inventories == 0)
        {
            Console.WriteLine("  Inventories component not found.");
            continue;
        }

        Console.WriteLine($"  Inventories @ 0x{inventories:X16}");
        PrintInventoryComponentDecode(reader, inventories, componentWindow);
    }

    return 0;
}

static void PrintInventoryComponentDecode(MemoryReader reader, nint inventoriesComponent, int componentWindow)
{
    var tested = new HashSet<nint>();
    var printed = 0;
    for (var offset = 0; offset <= componentWindow - 0x18; offset += 8)
    {
        if (!reader.TryReadStruct<StdVector>(inventoriesComponent + offset, out var vector) ||
            !TryGetVectorByteSize(vector, 8, 0x4000, out var bytes))
            continue;

        var count8 = Math.Min(bytes / 8, 128);
        var vectorPrinted = false;
        for (var i = 0; i < count8; i++)
        {
            var ptr = SafePtr(reader, vector.First + i * 8);
            if (ptr == 0 || !tested.Add(ptr)) continue;

            var decodedInventory = TryPrintInventoryContainer(reader, ptr, $"    vector+0x{offset:X3}[{i}]");
            var decodedItem = TryPrintItemEntity(reader, ptr, $"    vector+0x{offset:X3}[{i}] direct-item");
            if (decodedInventory || decodedItem)
            {
                if (!vectorPrinted)
                {
                    Console.WriteLine($"  VECTOR +0x{offset:X3} bytes=0x{bytes:X} count8={bytes / 8}");
                    vectorPrinted = true;
                }
                printed++;
            }
        }
    }

    if (printed == 0)
        Console.WriteLine("  No inventory/item containers decoded from Inventories vectors.");
}

static int RunRuneforgeInventoryBlockProbe(ProcessHandle process, MemoryReader reader, int componentWindow, int entryLimit, int objectWindow)
{
    componentWindow = Math.Clamp(componentWindow, 0x150, 0x4000);
    entryLimit = Math.Clamp(entryLimit, 1, 64);
    objectWindow = Math.Clamp(objectWindow, 0x80, 0x2000);
    var (_, _, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    Console.WriteLine();
    Console.WriteLine("Runeforge inventory block probe");
    Console.WriteLine("-------------------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Component window: 0x{componentWindow:X}");
    Console.WriteLine($"Entry limit     : {entryLimit}");
    Console.WriteLine($"Object window   : 0x{objectWindow:X}");
    Console.WriteLine("Goal            : inspect repeated Inventories blocks that appear on RuneEncounterController.");

    var controllers = EnumerateRuneforgeSourceEntities(reader, areaInstance, playerGrid)
        .Where(e => e.Metadata.Contains("RuneEncounterController", StringComparison.OrdinalIgnoreCase))
        .OrderBy(e => e.Distance ?? float.MaxValue)
        .ToArray();

    if (controllers.Length == 0)
    {
        Console.WriteLine("No RuneEncounterController found.");
        return 0;
    }

    foreach (var controller in controllers)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"CONTROLLER {controller.Source,-8} id={controller.Id,-8} addr=0x{controller.Entity:X16} " +
            $"dist={FormatDistance(controller.Distance)} grid={FormatGrid(controller.Grid)} {controller.Metadata}");
        var inventories = ResolveComponentAddr(reader, controller.Entity, "Inventories");
        if (inventories == 0)
        {
            Console.WriteLine("  Inventories component not found.");
            continue;
        }

        Console.WriteLine($"  Inventories @ 0x{inventories:X16}");
        PrintRuneforgeInventoryBlocks(reader, inventories, componentWindow, entryLimit, objectWindow);
    }

    Console.WriteLine();
    Console.WriteLine("Compare before-open vs after-open:");
    Console.WriteLine("  - A block/vector that changes only on the active nearby controller is likely the reward selection source.");
    Console.WriteLine("  - Blocks that are identical on every sleeping controller are probably template inventory/layout data.");
    return 0;
}

static int RunRuneforgeInventoryBlockWatch(ProcessHandle process, MemoryReader reader, int timeoutSeconds, int componentWindow, int entryLimit, int objectWindow)
{
    timeoutSeconds = Math.Clamp(timeoutSeconds, 5, 600);
    componentWindow = Math.Clamp(componentWindow, 0x150, 0x4000);
    entryLimit = Math.Clamp(entryLimit, 1, 64);
    objectWindow = Math.Clamp(objectWindow, 0x80, 0x2000);
    var (_, inGameState, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0 || inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Runeforge inventory block watch");
    Console.WriteLine("-------------------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Component window: 0x{componentWindow:X}");
    Console.WriteLine($"Entry limit     : {entryLimit}");
    Console.WriteLine($"Object window   : 0x{objectWindow:X}");
    Console.WriteLine("Goal            : diff controller Inventories blocks before/after Runeshape opens.");

    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    var before = CaptureRuneforgeInventoryBlockSnapshot(reader, areaInstance, playerGrid, componentWindow, entryLimit, objectWindow);
    PrintInventoryBlockSnapshotSummary("before", before);
    if (before.Count == 0)
    {
        Console.WriteLine("No RuneEncounterController inventory blocks found.");
        return 0;
    }

    Console.WriteLine();
    Console.WriteLine("Open Runeshape now. Waiting until visible reward rows appear...");
    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    while (DateTime.UtcNow < deadline)
    {
        Thread.Sleep(100);
        var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
        if (uiRoot == 0) continue;
        if (!TryResolveRuneforgeCatalogSlot(reader, uiRoot, 4000, 32, out _, out var catalog, out _, out _))
            continue;
        if (ReadRuneforgeCatalogRows(reader, catalog, 4000).Any(r => r.Visible))
            break;
    }

    var after = CaptureRuneforgeInventoryBlockSnapshot(reader, areaInstance, playerGrid, componentWindow, entryLimit, objectWindow);
    PrintInventoryBlockSnapshotSummary("after", after);
    Console.WriteLine();
    PrintInventoryBlockSnapshotDiff(reader, before, after, objectWindow);
    return 0;
}

static IReadOnlyList<RuneforgeInventoryBlockSnapshot> CaptureRuneforgeInventoryBlockSnapshot(
    MemoryReader reader,
    nint areaInstance,
    System.Numerics.Vector2? playerGrid,
    int componentWindow,
    int entryLimit,
    int objectWindow)
{
    var result = new List<RuneforgeInventoryBlockSnapshot>();
    foreach (var controller in EnumerateRuneforgeSourceEntities(reader, areaInstance, playerGrid)
                 .Where(e => e.Metadata.Contains("RuneEncounterController", StringComparison.OrdinalIgnoreCase)))
    {
        var inventories = ResolveComponentAddr(reader, controller.Entity, "Inventories");
        if (inventories == 0) continue;
        const int BlockStride = 0x150;
        var blockCount = Math.Min(componentWindow / BlockStride, 12);
        for (var block = 0; block < blockCount; block++)
        {
            var blockBase = inventories + block * BlockStride;
            if (!TryReadRuneforgeInventoryBlock(reader, blockBase, out var info)) continue;

            var smallValues = ReadVectorQwords(reader, info.SmallFirst, info.SmallCount, entryLimit);
            var largeValues = ReadVectorQwords(reader, info.LargeFirst, info.LargeCount, entryLimit);
            result.Add(new RuneforgeInventoryBlockSnapshot(
                controller.Source,
                controller.Id,
                controller.Entity,
                controller.Distance,
                block,
                inventories,
                info,
                HashMemory(reader, blockBase, Math.Min(BlockStride, componentWindow - block * BlockStride)),
                smallValues,
                smallValues.Select(v => CaptureMemoryBytes(reader, v, objectWindow)).ToArray(),
                largeValues,
                largeValues.Select(v => CaptureMemoryBytes(reader, v, objectWindow)).ToArray()));
        }
    }

    return result
        .OrderBy(x => x.Distance ?? float.MaxValue)
        .ThenBy(x => x.Source, StringComparer.Ordinal)
        .ThenBy(x => x.ControllerId)
        .ThenBy(x => x.Block)
        .ToArray();
}

static void PrintInventoryBlockSnapshotSummary(string label, IReadOnlyList<RuneforgeInventoryBlockSnapshot> rows)
{
    Console.WriteLine($"{label} snapshot: {rows.Count} block(s)");
    foreach (var row in rows.Take(20))
        Console.WriteLine(
            $"  {row.Source,-8} id={row.ControllerId,-8} dist={FormatDistance(row.Distance),6} block={row.Block} " +
            $"countField={row.Info.CountField} small={row.Info.SmallCount}/{row.Info.SmallCapacity} large={row.Info.LargeCount}/{row.Info.LargeCapacity} " +
            $"blockHash=0x{row.BlockHash:X8}");
}

static void PrintInventoryBlockSnapshotDiff(
    MemoryReader reader,
    IReadOnlyList<RuneforgeInventoryBlockSnapshot> before,
    IReadOnlyList<RuneforgeInventoryBlockSnapshot> after,
    int objectWindow)
{
    Console.WriteLine("Inventory block diff");
    Console.WriteLine("--------------------");
    var beforeMap = before.ToDictionary(x => (x.ControllerId, x.Block));
    var afterMap = after.ToDictionary(x => (x.ControllerId, x.Block));
    var keys = beforeMap.Keys.Union(afterMap.Keys).OrderBy(k => k.ControllerId).ThenBy(k => k.Block).ToArray();
    var changed = 0;
    foreach (var key in keys)
    {
        beforeMap.TryGetValue(key, out var b);
        afterMap.TryGetValue(key, out var a);
        if (b.ControllerId == 0)
        {
            Console.WriteLine($"  ADDED id={key.ControllerId} block={key.Block}");
            changed++;
            continue;
        }
        if (a.ControllerId == 0)
        {
            Console.WriteLine($"  REMOVED id={key.ControllerId} block={key.Block}");
            changed++;
            continue;
        }

        var diffs = new List<string>();
        if (b.BlockHash != a.BlockHash) diffs.Add($"blockHash 0x{b.BlockHash:X8}->0x{a.BlockHash:X8}");
        if (!b.Info.Equals(a.Info)) diffs.Add("blockInfo");
        if (!b.SmallValues.SequenceEqual(a.SmallValues)) diffs.Add("smallQwords");
        if (!MemorySamplesEqual(b.SmallTargetSamples, a.SmallTargetSamples)) diffs.Add("smallTarget");
        if (!b.LargeValues.SequenceEqual(a.LargeValues)) diffs.Add("largeQwords");
        if (!MemorySamplesEqual(b.LargeTargetSamples, a.LargeTargetSamples)) diffs.Add("largeTarget");
        if (diffs.Count == 0) continue;

        Console.WriteLine(
            $"  CHANGED {a.Source,-8} id={key.ControllerId,-8} dist={FormatDistance(a.Distance),6} block={key.Block}: {string.Join(", ", diffs)}");
        PrintQwordTargetDiff("small", b.SmallValues, b.SmallTargetSamples, a.SmallValues, a.SmallTargetSamples);
        PrintQwordTargetDiff("large", b.LargeValues, b.LargeTargetSamples, a.LargeValues, a.LargeTargetSamples);
        PrintChangedTargetDetails(reader, "small", b.SmallValues, b.SmallTargetSamples, a.SmallValues, a.SmallTargetSamples, objectWindow);
        PrintChangedTargetDetails(reader, "large", b.LargeValues, b.LargeTargetSamples, a.LargeValues, a.LargeTargetSamples, objectWindow);
        changed++;
    }

    if (changed == 0)
        Console.WriteLine("  no block/vector/target hash changes detected");
}

static void PrintQwordTargetDiff(string label, IReadOnlyList<nint> beforeValues, IReadOnlyList<byte[]> beforeSamples, IReadOnlyList<nint> afterValues, IReadOnlyList<byte[]> afterSamples)
{
    var max = Math.Max(beforeValues.Count, afterValues.Count);
    var printedGroups = new HashSet<(nint Before, nint After, uint BeforeHash, uint AfterHash)>();
    for (var i = 0; i < max; i++)
    {
        var bv = i < beforeValues.Count ? beforeValues[i] : 0;
        var av = i < afterValues.Count ? afterValues[i] : 0;
        var bb = i < beforeSamples.Count ? beforeSamples[i] : [];
        var ab = i < afterSamples.Count ? afterSamples[i] : [];
        var bh = HashBytes(bb);
        var ah = HashBytes(ab);
        if (bv == av && bh == ah) continue;
        if (!printedGroups.Add((bv, av, bh, ah)))
            continue;

        var sameChanges = new List<int>();
        for (var j = i; j < max; j++)
        {
            var gbv = j < beforeValues.Count ? beforeValues[j] : 0;
            var gav = j < afterValues.Count ? afterValues[j] : 0;
            var gbb = j < beforeSamples.Count ? beforeSamples[j] : [];
            var gab = j < afterSamples.Count ? afterSamples[j] : [];
            if (gbv == bv && gav == av && HashBytes(gbb) == bh && HashBytes(gab) == ah)
                sameChanges.Add(j);
        }

        Console.WriteLine($"    {label}[{FormatIndexList(sameChanges)}] 0x{bv:X16}/0x{bh:X8} -> 0x{av:X16}/0x{ah:X8}");
        if (bv == av && bv != 0)
            PrintMemorySampleDiff(bb, ab, "      ");
    }
}

static void PrintChangedTargetDetails(
    MemoryReader reader,
    string label,
    IReadOnlyList<nint> beforeValues,
    IReadOnlyList<byte[]> beforeSamples,
    IReadOnlyList<nint> afterValues,
    IReadOnlyList<byte[]> afterSamples,
    int objectWindow)
{
    var max = Math.Max(beforeValues.Count, afterValues.Count);
    var printedTargets = new HashSet<nint>();
    for (var i = 0; i < max; i++)
    {
        var bv = i < beforeValues.Count ? beforeValues[i] : 0;
        var av = i < afterValues.Count ? afterValues[i] : 0;
        var bb = i < beforeSamples.Count ? beforeSamples[i] : [];
        var ab = i < afterSamples.Count ? afterSamples[i] : [];
        if (bv == av && HashBytes(bb) == HashBytes(ab)) continue;

        var target = av != 0 ? av : bv;
        if (!IsPlausiblePointer(target) || !printedTargets.Add(target)) continue;
        Console.WriteLine($"    {label} target 0x{target:X16} after-open summary:");
        PrintPointedObjectSummary(reader, target, "      ", Math.Min(objectWindow, 0x800));
        PrintChangedPointerFieldDetails(reader, bb, ab, "      ");
        if (printedTargets.Count >= 8)
        {
            Console.WriteLine($"      ...additional changed {label} targets omitted");
            break;
        }
    }
}

static string FormatIndexList(IReadOnlyList<int> indexes)
{
    if (indexes.Count == 0) return "";
    if (indexes.Count == 1) return indexes[0].ToString();
    if (indexes.Count <= 8) return string.Join(",", indexes);
    return $"{indexes[0]}..{indexes[^1]} ({indexes.Count})";
}

static void PrintChangedPointerFieldDetails(MemoryReader reader, byte[] before, byte[] after, string indent)
{
    var min = Math.Min(before.Length, after.Length);
    var printed = 0;
    var seen = new HashSet<nint>();
    for (var offset = 0; offset + IntPtr.Size <= min && printed < 8; offset += 8)
    {
        var b = ReadPointerFromBytes(before, offset);
        var a = ReadPointerFromBytes(after, offset);
        if (b == a || !IsPlausiblePointer(a) || !seen.Add(a)) continue;
        Console.WriteLine($"{indent}changed ptr +0x{offset:X3}: 0x{b:X16} -> 0x{a:X16}");
        PrintPointedObjectSummary(reader, a, indent + "  ", 0x300);
        printed++;
    }
}

static nint ReadPointerFromBytes(byte[] bytes, int offset)
{
    if (offset < 0 || offset + IntPtr.Size > bytes.Length) return 0;
    return IntPtr.Size == 8
        ? (nint)BitConverter.ToInt64(bytes, offset)
        : (nint)BitConverter.ToInt32(bytes, offset);
}

static bool MemorySamplesEqual(IReadOnlyList<byte[]> left, IReadOnlyList<byte[]> right)
{
    if (left.Count != right.Count) return false;
    for (var i = 0; i < left.Count; i++)
        if (HashBytes(left[i]) != HashBytes(right[i])) return false;
    return true;
}

static void PrintMemorySampleDiff(byte[] before, byte[] after, string indent)
{
    var min = Math.Min(before.Length, after.Length);
    var printed = 0;
    for (var offset = 0; offset + 4 <= min && printed < 24; offset += 4)
    {
        var b = BitConverter.ToUInt32(before, offset);
        var a = BitConverter.ToUInt32(after, offset);
        if (b == a) continue;
        Console.WriteLine($"{indent}+0x{offset:X3}: u32 0x{b:X8} ({b}) -> 0x{a:X8} ({a})");
        printed++;
    }
    if (printed == 0)
    {
        for (var offset = 0; offset < min && printed < 24; offset++)
        {
            if (before[offset] == after[offset]) continue;
            Console.WriteLine($"{indent}+0x{offset:X3}: byte 0x{before[offset]:X2} -> 0x{after[offset]:X2}");
            printed++;
        }
    }
}

static IReadOnlyList<nint> ReadVectorQwords(MemoryReader reader, nint first, int count, int entryLimit)
{
    var result = new List<nint>();
    for (var i = 0; i < Math.Min(count, entryLimit); i++)
    {
        if (!reader.TryReadStruct<nint>(first + i * 8, out var value))
            break;
        result.Add(value);
    }
    return result;
}

static uint HashMemory(MemoryReader reader, nint address, int length)
{
    return HashBytes(CaptureMemoryBytes(reader, address, length));
}

static byte[] CaptureMemoryBytes(MemoryReader reader, nint address, int length)
{
    if (address == 0 || length <= 0) return [];
    var bytes = new byte[length];
    var read = reader.TryReadBytes(address, bytes);
    if (read <= 0) return [];
    if (read < bytes.Length) Array.Resize(ref bytes, read);
    return bytes;
}

static uint HashBytes(IReadOnlyList<byte> bytes)
{
    if (bytes.Count == 0) return 0;
    const uint offset = 2166136261;
    const uint prime = 16777619;
    var hash = offset;
    for (var i = 0; i < bytes.Count; i++)
    {
        hash ^= bytes[i];
        hash *= prime;
    }
    return hash;
}

static void PrintRuneforgeInventoryBlocks(MemoryReader reader, nint inventories, int componentWindow, int entryLimit, int objectWindow)
{
    const int BlockStride = 0x150;
    var blockCount = Math.Min(componentWindow / BlockStride, 12);
    for (var block = 0; block < blockCount; block++)
    {
        var blockBase = inventories + block * BlockStride;
        if (!TryReadRuneforgeInventoryBlock(reader, blockBase, out var info))
            continue;

        Console.WriteLine(
            $"  BLOCK[{block}] +0x{block * BlockStride:X3} " +
            $"smallVec={info.SmallCount}/{info.SmallCapacity} largeVec={info.LargeCount}/{info.LargeCapacity} " +
            $"countField@+0xF8={info.CountField} flags?=0x{info.Flags:X8}");
        Console.WriteLine(
            $"    small +0x20 0x{info.SmallFirst:X16}..0x{info.SmallLast:X16}..0x{info.SmallEnd:X16}");
        DumpQwordVectorSamples(reader, info.SmallFirst, info.SmallCount, entryLimit, objectWindow, "      small");
        Console.WriteLine(
            $"    large +0xE0 0x{info.LargeFirst:X16}..0x{info.LargeLast:X16}..0x{info.LargeEnd:X16}");
        DumpQwordVectorSamples(reader, info.LargeFirst, info.LargeCount, entryLimit, objectWindow, "      large");
    }
}

static bool TryReadRuneforgeInventoryBlock(MemoryReader reader, nint blockBase, out RuneforgeInventoryBlockInfo info)
{
    info = default;
    reader.TryReadStruct<uint>(blockBase, out var flags);
    reader.TryReadStruct<int>(blockBase + 0xF8, out var countField);

    if (!reader.TryReadStruct<StdVector>(blockBase + 0x20, out var small) ||
        !TryGetVectorByteSize(small, 8, 0x10000, out var smallBytes) ||
        !reader.TryReadStruct<StdVector>(blockBase + 0xE0, out var large) ||
        !TryGetVectorByteSize(large, 8, 0x20000, out var largeBytes))
        return false;

    var smallCapacity = ((long)small.End - (long)small.First) / 8;
    var largeCapacity = ((long)large.End - (long)large.First) / 8;
    if (smallCapacity is < 0 or > 8192 || largeCapacity is < 0 or > 8192)
        return false;

    info = new RuneforgeInventoryBlockInfo(
        flags,
        countField,
        small.First,
        small.Last,
        small.End,
        smallBytes / 8,
        (int)smallCapacity,
        large.First,
        large.Last,
        large.End,
        largeBytes / 8,
        (int)largeCapacity);
    return true;
}

static void DumpQwordVectorSamples(MemoryReader reader, nint first, int count, int entryLimit, int objectWindow, string label)
{
    for (var i = 0; i < Math.Min(count, entryLimit); i++)
    {
        if (!reader.TryReadStruct<nint>(first + i * 8, out var value))
            break;
        Console.WriteLine($"{label}[{i,2}] qword=0x{value:X16}");
        if (IsPlausiblePointer(value))
            PrintPointedObjectSummary(reader, value, $"{label}     ", objectWindow);
    }
}

static bool TryPrintInventoryContainer(MemoryReader reader, nint inventory, string label)
{
    if (!reader.TryReadStruct<int>(inventory + Poe2.Inventory.TotalBoxesX, out var boxesX) ||
        !reader.TryReadStruct<int>(inventory + Poe2.Inventory.TotalBoxesY, out var boxesY) ||
        boxesX is < 0 or > 80 ||
        boxesY is < 0 or > 80 ||
        !reader.TryReadStruct<StdVector>(inventory + Poe2.Inventory.ItemListVec, out var items) ||
        !TryGetVectorByteSize(items, Poe2.ServerData.InvArrayStride, Poe2.ServerData.InvArrayStride * 512, out var itemBytes))
        return false;

    var itemCount = itemBytes / Poe2.ServerData.InvArrayStride;
    if (itemCount == 0 && boxesX == 0 && boxesY == 0) return false;

    Console.WriteLine($"{label} inventory=0x{inventory:X16} size={boxesX}x{boxesY} items={itemCount}");
    for (var i = 0; i < Math.Min(itemCount, 32); i++)
    {
        var record = items.First + i * Poe2.ServerData.InvArrayStride;
        var item = SafePtr(reader, record + Poe2.InventoryItem.Item);
        if (item == 0) continue;
        reader.TryReadStruct<int>(record + Poe2.InventoryItem.SlotStartX, out var x0);
        reader.TryReadStruct<int>(record + Poe2.InventoryItem.SlotStartY, out var y0);
        reader.TryReadStruct<int>(record + Poe2.InventoryItem.SlotEndX, out var x1);
        reader.TryReadStruct<int>(record + Poe2.InventoryItem.SlotEndY, out var y1);
        TryPrintItemEntity(reader, item, $"      item[{i}] slot=({x0},{y0})-({x1},{y1})");
    }

    return true;
}

static bool TryPrintItemEntity(MemoryReader reader, nint item, string label)
{
    var metadata = ReadEntityMetadata(reader, item);
    var components = ReadComponentMap(reader, item);
    if (components.Count == 0 && string.IsNullOrWhiteSpace(metadata))
        return false;

    var name = ItemNameFromMetadata(metadata);
    var stack = ReadItemStackCountProbe(reader, item);
    var renderItem = components.FirstOrDefault(c => c.Name == "RenderItem").Address;
    var art = "";
    if (renderItem != 0)
    {
        var path = SafePtr(reader, renderItem + Poe2.RenderItemComponent.ResourcePath);
        if (path != 0) art = reader.ReadStringUtf16(path, 128);
    }

    Console.WriteLine($"{label} item=0x{item:X16} stack={stack} name='{name}' meta='{metadata}' art='{art}' comps=[{string.Join(",", components.Select(c => c.Name))}]");

    var mods = components.FirstOrDefault(c => c.Name == "Mods").Address;
    if (mods != 0)
        PrintItemModsProbe(reader, mods);
    return true;
}

static int ReadItemStackCountProbe(MemoryReader reader, nint item)
{
    var stack = ResolveComponentAddr(reader, item, "Stack");
    return stack != 0 &&
           reader.TryReadStruct<int>(stack + Poe2.StackComponent.Count, out var count) &&
           count is > 0 and < 100000
        ? count
        : 1;
}

static void PrintItemModsProbe(MemoryReader reader, nint mods)
{
    if (reader.TryReadStruct<int>(mods + Poe2.ModsComponent.Rarity, out var rarity))
        Console.WriteLine($"        rarity={rarity}");
    PrintItemModVectorProbe(reader, mods + Poe2.ModsComponent.ImplicitMods, "implicit");
    PrintItemModVectorProbe(reader, mods + Poe2.ModsComponent.ExplicitMods, "explicit");
    PrintItemModVectorProbe(reader, mods + Poe2.ModsComponent.EnchantMods, "enchant");
}

static void PrintItemModVectorProbe(MemoryReader reader, nint vectorAddress, string kind)
{
    if (!reader.TryReadStruct<StdVector>(vectorAddress, out var vector) ||
        !TryGetVectorByteSize(vector, Poe2.ModsComponent.ModArrayStride, Poe2.ModsComponent.ModArrayStride * 64, out var bytes))
        return;

    var count = bytes / Poe2.ModsComponent.ModArrayStride;
    for (var i = 0; i < Math.Min(count, 16); i++)
    {
        var modArray = vector.First + i * Poe2.ModsComponent.ModArrayStride;
        var row = SafePtr(reader, modArray + Poe2.ModsComponent.ModRecordPtr);
        var idPtr = row == 0 ? 0 : SafePtr(reader, row + Poe2.ModsComponent.ModRecordIdPtr);
        var id = idPtr == 0 ? "" : reader.ReadStringUtf16(idPtr, 96);
        if (string.IsNullOrWhiteSpace(id)) continue;

        var values = new List<int>();
        if (reader.TryReadStruct<StdVector>(modArray, out var valuesVec) &&
            TryGetVectorByteSize(valuesVec, sizeof(int), sizeof(int) * 16, out var valueBytes))
        {
            for (var v = 0; v < Math.Min(valueBytes / sizeof(int), 8); v++)
                if (reader.TryReadStruct<int>(valuesVec.First + v * sizeof(int), out var value))
                    values.Add(value);
        }
        if (values.Count == 0 && reader.TryReadStruct<int>(modArray + 0x18, out var fallback))
            values.Add(fallback);

        var rendered = string.Join("; ", ItemModTranslator.Shared.RenderMod(id, values));
        Console.WriteLine($"        {kind}[{i}] id='{id}' values=[{string.Join(",", values)}] text='{rendered}'");
    }
}

static int RunRuneforgeVectorProbe(ProcessHandle process, MemoryReader reader, int objectWindow, int entryLimit)
{
    objectWindow = Math.Clamp(objectWindow, 0x80, 0x2000);
    entryLimit = Math.Clamp(entryLimit, 1, 64);
    var (_, inGameState, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    Console.WriteLine();
    Console.WriteLine("Runeforge vector probe");
    Console.WriteLine("----------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Object window : 0x{objectWindow:X}");
    Console.WriteLine($"Entry limit   : {entryLimit}");
    Console.WriteLine("Goal          : walk Expedition2Encounter vector suspects that populate after opening Runeshape.");
    if (inGameState != 0)
    {
        var runeforge = new Poe2Runeforge(reader);
        var rewards = runeforge.ReadRewards(inGameState, 1920, 1080);
        var hiddenRewards = runeforge.ReadRewardsIncludingHidden(inGameState, 1920, 1080, out var hiddenResolved);
        Console.WriteLine($"Runeshape UI  : panelOpen={runeforge.PanelOpen} rewards={rewards.Count}");
        Console.WriteLine($"Hidden UI     : resolved={hiddenResolved} rewards={hiddenRewards.Count}");
        foreach (var reward in rewards.Take(16))
            Console.WriteLine($"  UI reward   : {reward.Count}x {reward.Name}");
        if (rewards.Count == 0)
            foreach (var reward in hiddenRewards.Take(16))
                Console.WriteLine($"  hidden row  : {reward.Count}x {reward.Name}");
    }

    var scanned = 0;
    var seen = new HashSet<uint>();
    foreach (var (source, mapOffset) in new[] { ("sleeping", Poe2.AreaInstance.SleepingEntities), ("awake", Poe2.AreaInstance.AwakeEntities) })
    {
        foreach (var (id, entity, metadata) in EnumerateEntityMap(reader, areaInstance, mapOffset))
        {
            if (!seen.Add(id) ||
                !metadata.Contains("Expedition2Encounter", StringComparison.OrdinalIgnoreCase))
                continue;

            scanned++;
            var grid = ReadEntityGrid(reader, entity);
            var dist = playerGrid.HasValue && grid.HasValue
                ? System.Numerics.Vector2.Distance(playerGrid.Value, grid.Value)
                : (float?)null;

            Console.WriteLine();
            Console.WriteLine(
                $"ENCOUNTER {source,-8} id={id} addr=0x{entity:X16} " +
                $"grid={FormatGrid(grid)} dist={FormatDistance(dist)} {metadata}");

            var stateMachine = ResolveComponentAddr(reader, entity, "StateMachine");
            if (stateMachine != 0)
            {
                Console.WriteLine($"  StateMachine @ 0x{stateMachine:X16}");
                PrintStateMachineCandidates(reader, stateMachine);
            }

            var stats = ResolveComponentAddr(reader, entity, "Stats");
            if (stats != 0)
            {
                Console.WriteLine($"  Stats @ 0x{stats:X16}");
                PrintStatsComponentClues(reader, stats);
                PrintSuspectObjectVectors(reader, stats, "Stats", [0x650, 0x860, 0xA70, 0xC80, 0xE90], entryLimit, objectWindow);
                PrintAllObjectVectors(reader, stats, "Stats", 0x1000, entryLimit, objectWindow);
            }
            else
            {
                Console.WriteLine("  Stats component not found.");
            }

            var preload = ResolveComponentAddr(reader, entity, "Preload");
            if (preload != 0)
            {
                Console.WriteLine($"  Preload @ 0x{preload:X16}");
                PrintPreloadRuneforgeWindow(reader, preload, 0x280, 0x420, entryLimit, objectWindow);
                PrintAllObjectVectors(reader, preload, "Preload", 0x800, entryLimit, objectWindow);
            }
            else
            {
                Console.WriteLine("  Preload component not found.");
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Scanned {scanned} Expedition2Encounter entity/entities.");
    Console.WriteLine("Run this before opening Runeshape and again after opening it; compare which vectors/text appear.");
    return 0;
}

static int RunRuneforgeVectorWatch(ProcessHandle process, MemoryReader reader, int timeoutSeconds, int objectWindow)
{
    timeoutSeconds = Math.Clamp(timeoutSeconds, 5, 600);
    objectWindow = Math.Clamp(objectWindow, 0x80, 0x2000);
    var (_, inGameState, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (inGameState == 0 || areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var encounter = FindNearestRuneforgeEncounter(reader, areaInstance, localPlayer);
    if (encounter.Entity == 0)
    {
        Console.Error.WriteLine("No Expedition2Encounter found in awake/sleeping maps.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Runeforge vector watch");
    Console.WriteLine("----------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Encounter     : {encounter.Source} id={encounter.Id} addr=0x{encounter.Entity:X16} grid={FormatGrid(encounter.Grid)} dist={FormatDistance(encounter.Distance)}");
    Console.WriteLine($"Object window : 0x{objectWindow:X}");
    Console.WriteLine($"Timeout       : {timeoutSeconds}s");
    Console.WriteLine("Step          : leave Runeshape closed until the baseline prints, then open the Runeshape panel.");

    var runeforge = new Poe2Runeforge(reader);
    var baselineRewards = runeforge.ReadRewards(inGameState, 1920, 1080);
    var baselineHiddenRewards = runeforge.ReadRewardsIncludingHidden(inGameState, 1920, 1080, out var baselineHiddenResolved);
    Console.WriteLine($"Baseline UI   : panelOpen={runeforge.PanelOpen} rewards={baselineRewards.Count}");
    Console.WriteLine($"Baseline hid  : resolved={baselineHiddenResolved} rewards={baselineHiddenRewards.Count}");
    foreach (var reward in baselineHiddenRewards.Take(16))
        Console.WriteLine($"  hidden row  : {reward.Count}x {reward.Name}");
    var before = CaptureRuneforgeEncounterSnapshot(reader, encounter.Entity);
    PrintRuneforgeSnapshotSummary("Baseline", before);

    if (runeforge.PanelOpen)
        Console.WriteLine("Panel is already open; close/reopen or run this on a fresh closed baseline for a cleaner delta.");

    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    List<Poe2Runeforge.RuneReward> rewards = baselineRewards;
    while (DateTime.UtcNow < deadline)
    {
        Thread.Sleep(250);
        rewards = runeforge.ReadRewards(inGameState, 1920, 1080);
        if (runeforge.PanelOpen && rewards.Count > 0)
            break;
    }

    if (!runeforge.PanelOpen || rewards.Count == 0)
    {
        Console.WriteLine("Timed out waiting for visible Runeshape rewards.");
        return 0;
    }

    Console.WriteLine();
    Console.WriteLine($"Opened UI     : rewards={rewards.Count}");
    foreach (var reward in rewards.Take(24))
        Console.WriteLine($"  UI reward   : {reward.Count}x {reward.Name}");
    var openedHiddenRewards = runeforge.ReadRewardsIncludingHidden(inGameState, 1920, 1080, out var openedHiddenResolved);
    Console.WriteLine($"Opened hidden : resolved={openedHiddenResolved} rewards={openedHiddenRewards.Count}");

    var after = CaptureRuneforgeEncounterSnapshot(reader, encounter.Entity);
    PrintRuneforgeSnapshotSummary("After open", after);
    PrintRuneforgeSnapshotDelta(reader, before, after, objectWindow);
    return 0;
}

static int RunRuneforgeUiGateProbe(ProcessHandle process, MemoryReader reader, int maxChildren, int maxBranches)
{
    maxChildren = Math.Clamp(maxChildren, 16, 20000);
    maxBranches = Math.Clamp(maxBranches, 1, 256);
    var (_, inGameState, areaInstance, _) = ResolveChain(process, reader);
    if (inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve InGameState (in game?).");
        return 1;
    }

    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    if (uiRoot == 0)
    {
        Console.Error.WriteLine("UiRoot is null.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Runeforge UI gate probe");
    Console.WriteLine("-----------------------");
    if (areaInstance != 0) PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"UiRoot       : 0x{uiRoot:X16}");
    Console.WriteLine($"Flags field  : UiElement+0x{Poe2.UiElement.Flags:X}, visible bit {Poe2.UiElement.FlagVisibleBit} mask=0x{1u << Poe2.UiElement.FlagVisibleBit:X}");
    Console.WriteLine($"Fingerprints : {string.Join(" -> ", Poe2.Runeforge.PanelFlagFingerprints.Select(x => $"0x{x:X8}"))}");
    Console.WriteLine($"Gate step    : {Poe2.Runeforge.GateStep}");
    Console.WriteLine($"Viewport step: {Poe2.Runeforge.ViewportStep}");

    var runeforge = new Poe2Runeforge(reader);
    var visibleRewards = runeforge.ReadRewards(inGameState, 1920, 1080);
    var hiddenRewards = runeforge.ReadRewardsIncludingHidden(inGameState, 1920, 1080, out var hiddenResolved);
    Console.WriteLine();
    Console.WriteLine($"Reader visible: panelOpen={runeforge.PanelOpen} rewards={visibleRewards.Count}");
    Console.WriteLine($"Reader hidden : resolved={hiddenResolved} rewards={hiddenRewards.Count}");
    foreach (var reward in (visibleRewards.Count > 0 ? visibleRewards : hiddenRewards).Take(16))
        Console.WriteLine($"  row         : {reward.Count}x {reward.Name}");

    Console.WriteLine();
    Console.WriteLine("Visible-gated fingerprint trace");
    Console.WriteLine("-------------------------------");
    var visiblePath = TraceRuneforgeUiPath(reader, uiRoot, requireVisibleGate: true, maxChildren, maxBranches);
    PrintRuneforgeUiTrace(reader, visiblePath);

    Console.WriteLine();
    Console.WriteLine("Hidden-gate fingerprint trace");
    Console.WriteLine("-----------------------------");
    var hiddenPath = TraceRuneforgeUiPath(reader, uiRoot, requireVisibleGate: false, maxChildren, maxBranches);
    PrintRuneforgeUiTrace(reader, hiddenPath);

    Console.WriteLine();
    Console.WriteLine("Visible-gated matching containers");
    Console.WriteLine("---------------------------------");
    var visiblePaths = EnumerateRuneforgeUiPaths(reader, uiRoot, requireVisibleGate: true, maxChildren, maxBranches);
    PrintRuneforgeUiPathSet(
        reader,
        visiblePaths,
        maxChildren);

    Console.WriteLine();
    Console.WriteLine("Hidden-gate matching containers");
    Console.WriteLine("-------------------------------");
    var hiddenPaths = EnumerateRuneforgeUiPaths(reader, uiRoot, requireVisibleGate: false, maxChildren, maxBranches);
    PrintRuneforgeUiPathSet(
        reader,
        hiddenPaths,
        maxChildren);

    Console.WriteLine();
    Console.WriteLine("Final-parent child dump");
    Console.WriteLine("-----------------------");
    PrintRuneforgeFinalParentChildren(reader, hiddenPaths.Count > 0 ? hiddenPaths : visiblePaths, maxChildren);

    Console.WriteLine();
    Console.WriteLine("Write-research notes:");
    Console.WriteLine("  - This probe is read-only; it does not modify client memory.");
    Console.WriteLine("  - A plausible write target requires a stable gate/path while closed plus a recipes container.");
    Console.WriteLine("  - If hidden trace fails before final recipes, flipping only the visible bit is unlikely to reveal rewards.");
    return 0;
}

static int RunRuneforgeUiChildWatch(ProcessHandle process, MemoryReader reader, int timeoutSeconds, int maxChildren, int maxBranches, int fieldWindow)
{
    timeoutSeconds = Math.Clamp(timeoutSeconds, 5, 600);
    maxChildren = Math.Clamp(maxChildren, 16, 20000);
    maxBranches = Math.Clamp(maxBranches, 1, 256);
    fieldWindow = Math.Clamp(fieldWindow, 0x200, 0x4000);

    var (_, inGameState, areaInstance, _) = ResolveChain(process, reader);
    if (inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve InGameState (in game?).");
        return 1;
    }

    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    if (uiRoot == 0)
    {
        Console.Error.WriteLine("UiRoot is null.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Runeforge UI child watch");
    Console.WriteLine("------------------------");
    if (areaInstance != 0) PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"UiRoot       : 0x{uiRoot:X16}");
    Console.WriteLine($"Timeout      : {timeoutSeconds}s");
    Console.WriteLine($"Field window : 0x{fieldWindow:X}");
    Console.WriteLine("Goal         : compare final-parent child[0] before/after Runeshape populates it.");

    if (!TryResolveRuneforgeCatalogSlot(reader, uiRoot, maxChildren, maxBranches, out var beforeParent, out var beforeCatalog, out var beforeBonus, out var beforePath))
    {
        Console.Error.WriteLine("Could not resolve the Runeshape final parent path. Open/close the panel once, then retry.");
        return 1;
    }

    var before = CaptureRuneforgeUiChildSnapshot(reader, "before", beforeCatalog, maxChildren, fieldWindow);
    Console.WriteLine();
    Console.WriteLine($"Resolved final parent: 0x{beforeParent:X16}, path=[{string.Join("/", beforePath.Select(p => p.ChildIndex))}]");
    Console.WriteLine($"Catalog child[0]    : 0x{beforeCatalog:X16}");
    Console.WriteLine($"Bonus child[1]      : 0x{beforeBonus:X16}");
    PrintRuneforgeUiChildSnapshot(before);

    if (before.ChildCount <= 0)
        Console.WriteLine();
    if (before.ChildCount <= 0)
        Console.WriteLine("Open the Runeshape panel now. Watching for catalog child[0] to populate...");
    else
        Console.WriteLine("Catalog child[0] is already populated; capturing comparison snapshot immediately.");

    RuneforgeUiChildSnapshot after = default;
    var gotAfter = before.ChildCount > 0;
    if (gotAfter)
    {
        after = CaptureRuneforgeUiChildSnapshot(reader, "after", beforeCatalog, maxChildren, fieldWindow);
    }
    else
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(250);
            if (!TryResolveRuneforgeCatalogSlot(reader, uiRoot, maxChildren, maxBranches, out _, out var catalog, out _, out _))
                continue;

            var snap = CaptureRuneforgeUiChildSnapshot(reader, "after", catalog, maxChildren, fieldWindow);
            if (snap.ChildCount <= 0 && snap.VisibleRows.Count == 0) continue;

            after = snap;
            gotAfter = true;
            break;
        }
    }

    if (!gotAfter)
    {
        Console.WriteLine("Timed out before catalog child[0] populated. Keep the game in map and run this again just before opening Runeshape.");
        return 2;
    }

    Console.WriteLine();
    PrintRuneforgeUiChildSnapshot(after);
    Console.WriteLine();
    PrintRuneforgeUiChildSnapshotDiff(before, after);
    Console.WriteLine();
    Console.WriteLine("Read-only conclusion aid:");
    Console.WriteLine("  - If only child vector pointers/count changed, the panel open action is materializing rows elsewhere.");
    Console.WriteLine("  - If stable scalar fields flip before child count changes, those fields are better write-research candidates than visibility flags.");
    return 0;
}

static int RunRuneforgeUiPopulateTimeline(ProcessHandle process, MemoryReader reader, int timeoutSeconds, int pollMs, int maxChildren, int maxBranches)
{
    timeoutSeconds = Math.Clamp(timeoutSeconds, 5, 600);
    pollMs = Math.Clamp(pollMs, 5, 1000);
    maxChildren = Math.Clamp(maxChildren, 16, 20000);
    maxBranches = Math.Clamp(maxBranches, 1, 256);

    var (_, inGameState, areaInstance, _) = ResolveChain(process, reader);
    if (inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve InGameState (in game?).");
        return 1;
    }

    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    if (uiRoot == 0)
    {
        Console.Error.WriteLine("UiRoot is null.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Runeforge UI populate timeline");
    Console.WriteLine("------------------------------");
    if (areaInstance != 0) PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"UiRoot       : 0x{uiRoot:X16}");
    Console.WriteLine($"Timeout      : {timeoutSeconds}s");
    Console.WriteLine($"Poll         : {pollMs}ms");
    Console.WriteLine("Goal         : determine whether +0x2E8/+0x2F0 lead or follow catalog row population.");

    if (!TryResolveRuneforgeCatalogSlot(reader, uiRoot, maxChildren, maxBranches, out var parent, out var catalog, out var bonus, out var path))
    {
        Console.Error.WriteLine("Could not resolve the Runeshape final parent path. Open/close the panel once, then retry.");
        return 1;
    }

    Console.WriteLine($"Resolved final parent: 0x{parent:X16}, path=[{string.Join("/", path.Select(p => p.ChildIndex))}]");
    Console.WriteLine($"Catalog child[0]    : 0x{catalog:X16}");
    Console.WriteLine($"Bonus child[1]      : 0x{bonus:X16}");
    Console.WriteLine();
    Console.WriteLine("Open the Runeshape panel now. Logging only when watched fields change...");
    Console.WriteLine("  ms       rows  cap   visibleRows  vecLast              mirrorRows  mirrorLast           tabs +2E8 +2F0 flags");

    var start = System.Diagnostics.Stopwatch.StartNew();
    var last = CaptureRuneforgePopulateState(reader, catalog, maxChildren);
    PrintRuneforgePopulateState(0, last);
    if (last.ChildCount > 0)
        Console.WriteLine("  note: catalog was already populated at start; use a fresh map/area or reopen the client for a clean pre-open run.");

    var sawRows = last.ChildCount > 0;
    var populatedAtStart = sawRows;
    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    while (DateTime.UtcNow < deadline)
    {
        Thread.Sleep(pollMs);
        if (!TryResolveRuneforgeCatalogSlot(reader, uiRoot, maxChildren, maxBranches, out _, out var currentCatalog, out _, out _))
            continue;

        var state = CaptureRuneforgePopulateState(reader, currentCatalog, maxChildren);
        if (!state.Equals(last))
        {
            PrintRuneforgePopulateState(start.ElapsedMilliseconds, state);
            last = state;
        }

        if (!populatedAtStart && state.ChildCount > 0)
        {
            sawRows = true;
            if (state.VisibleRows > 0)
                break;
        }
    }

    if (!sawRows)
    {
        Console.WriteLine("Timed out before catalog rows appeared.");
        return 2;
    }

    Console.WriteLine();
    Console.WriteLine("Interpretation:");
    Console.WriteLine("  - +2E8/+2F0 changing before rows suggests a possible population gate.");
    Console.WriteLine("  - rows changing first means those fields are likely bookkeeping after row creation.");
    Console.WriteLine("  - rows already present at t=0 means this was not a clean map-entry/pre-open sample.");
    Console.WriteLine("  - all changes in one poll means rerun with --poll-ms 5 for tighter ordering.");
    return 0;
}

static int RunRuneforgeUiLatchWriteTest(
    ProcessHandle process,
    MemoryReader reader,
    bool confirmWrite,
    int timeoutSeconds,
    int pollMs,
    int holdMs,
    int maxChildren,
    int maxBranches)
{
    timeoutSeconds = Math.Clamp(timeoutSeconds, 1, 120);
    pollMs = Math.Clamp(pollMs, 5, 1000);
    holdMs = Math.Clamp(holdMs, 0, 10000);
    maxChildren = Math.Clamp(maxChildren, 16, 20000);
    maxBranches = Math.Clamp(maxBranches, 1, 256);

    var (_, inGameState, areaInstance, _) = ResolveChain(process, reader);
    if (inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve InGameState (in game?).");
        return 1;
    }

    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    if (uiRoot == 0)
    {
        Console.Error.WriteLine("UiRoot is null.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Runeforge UI latch write test");
    Console.WriteLine("-----------------------------");
    if (areaInstance != 0) PrintAreaInfo(reader, areaInstance);
    Console.WriteLine("Scope        : writes catalog +0x2E8/+0x2F0 to 1, watches, restores originals.");
    Console.WriteLine("Safety       : refuses without --confirm-write and refuses if rows are already populated.");
    Console.WriteLine($"Timeout      : {timeoutSeconds}s");
    Console.WriteLine($"Poll         : {pollMs}ms");

    if (!confirmWrite)
    {
        Console.WriteLine("Refusing to write. Re-run with --confirm-write when Runeshape is closed and this is an intentional test.");
        return 2;
    }

    if (!TryResolveRuneforgeCatalogSlot(reader, uiRoot, maxChildren, maxBranches, out var parent, out var catalog, out var bonus, out var path))
    {
        Console.Error.WriteLine("Could not resolve the Runeshape final parent path. Open/close the panel once, then retry.");
        return 1;
    }

    Console.WriteLine($"Resolved final parent: 0x{parent:X16}, path=[{string.Join("/", path.Select(p => p.ChildIndex))}]");
    Console.WriteLine($"Catalog child[0]    : 0x{catalog:X16}");
    Console.WriteLine($"Bonus child[1]      : 0x{bonus:X16}");

    var before = CaptureRuneforgePopulateState(reader, catalog, maxChildren);
    PrintRuneforgePopulateStateHeader();
    PrintRuneforgePopulateState(0, before);
    if (before.ChildCount > 0)
    {
        Console.WriteLine("Refusing to write because catalog rows are already populated. Enter a fresh map or restart the client for a clean test.");
        return 3;
    }

    if (!reader.TryReadStruct<uint>(catalog + 0x2E8, out var original2E8) ||
        !reader.TryReadStruct<uint>(catalog + 0x2F0, out var original2F0))
    {
        Console.Error.WriteLine("Could not read original latch fields.");
        return 1;
    }

    var writeHandle = OpenWriteHandle(process.ProcessId);
    if (writeHandle == 0)
    {
        Console.Error.WriteLine($"OpenProcess for write failed: {Marshal.GetLastWin32Error()}");
        return 1;
    }

    try
    {
        Console.WriteLine($"Writing +0x2E8 0x{original2E8:X8}->1 and +0x2F0 0x{original2F0:X8}->1...");
        if (!WriteUInt32(writeHandle, catalog + 0x2E8, 1) ||
            !WriteUInt32(writeHandle, catalog + 0x2F0, 1))
        {
            Console.Error.WriteLine("Write failed; restoring originals.");
            return 1;
        }

        var start = System.Diagnostics.Stopwatch.StartNew();
        var last = CaptureRuneforgePopulateState(reader, catalog, maxChildren);
        PrintRuneforgePopulateState(start.ElapsedMilliseconds, last);
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(pollMs);
            var state = CaptureRuneforgePopulateState(reader, catalog, maxChildren);
            if (!state.Equals(last))
            {
                PrintRuneforgePopulateState(start.ElapsedMilliseconds, state);
                last = state;
            }

            if (state.ChildCount > 0 || state.VisibleRows > 0)
                break;
        }

        if (holdMs > 0)
            Thread.Sleep(holdMs);

        Console.WriteLine(last.ChildCount > 0
            ? "Result       : rows populated after latch write."
            : "Result       : rows did not populate from latch write alone.");
        return 0;
    }
    finally
    {
        WriteUInt32(writeHandle, catalog + 0x2E8, original2E8);
        WriteUInt32(writeHandle, catalog + 0x2F0, original2F0);
        CloseHandle(writeHandle);
        Console.WriteLine($"Restored     : +0x2E8=0x{original2E8:X8}, +0x2F0=0x{original2F0:X8}");
    }
}

static int RunRuneforgeSelectionSourceProbe(
    ProcessHandle process,
    MemoryReader reader,
    int maxChildren,
    int maxBranches,
    int fieldWindow,
    int maxEntities)
{
    maxChildren = Math.Clamp(maxChildren, 16, 20000);
    maxBranches = Math.Clamp(maxBranches, 1, 256);
    fieldWindow = Math.Clamp(fieldWindow, 0x200, 0x8000);
    maxEntities = Math.Clamp(maxEntities, 1, 200);

    var (_, inGameState, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (inGameState == 0 || areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    if (uiRoot == 0)
    {
        Console.Error.WriteLine("UiRoot is null.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Runeforge selection source probe");
    Console.WriteLine("--------------------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Field window : 0x{fieldWindow:X}");
    Console.WriteLine("Goal         : use visible reward row indexes as anchors to find backing selection data.");

    if (!TryResolveRuneforgeCatalogSlot(reader, uiRoot, maxChildren, maxBranches, out var parent, out var catalog, out var bonus, out var path))
    {
        Console.Error.WriteLine("Could not resolve the Runeshape final parent path. Open Runeshape, then retry.");
        return 1;
    }

    var rows = ReadRuneforgeCatalogRows(reader, catalog, maxChildren);
    var visibleRows = rows.Where(r => r.Visible).ToArray();
    Console.WriteLine($"Final parent : 0x{parent:X16}, path=[{string.Join("/", path.Select(p => p.ChildIndex))}]");
    Console.WriteLine($"Catalog      : 0x{catalog:X16}, rows={rows.Count}, visible={visibleRows.Length}");
    Console.WriteLine($"Bonus        : 0x{bonus:X16}");
    if (visibleRows.Length == 0)
    {
        Console.WriteLine("No visible reward rows. Open Runeshape first, then rerun this probe.");
        return 2;
    }

    Console.WriteLine();
    Console.WriteLine("Visible rewards");
    Console.WriteLine("---------------");
    foreach (var row in visibleRows)
        Console.WriteLine($"  idx={row.Index,4} row=0x{row.Row:X16} flags=0x{row.Flags:X8} text='{row.Text}'");

    Console.WriteLine();
    Console.WriteLine("Visible reward row object details");
    Console.WriteLine("---------------------------------");
    PrintRuneforgeVisibleRowDetails(reader, visibleRows, maxChildren, Math.Min(fieldWindow, 0x1200));

    var indexes = visibleRows.Select(r => r.Index).Distinct().OrderBy(x => x).ToArray();
    Console.WriteLine();
    Console.WriteLine("UI object index clues");
    Console.WriteLine("---------------------");
    PrintObjectIndexClues(reader, "catalog", catalog, fieldWindow, indexes, visibleRows.Length, rows.Count);
    PrintObjectIndexClues(reader, "final-parent", parent, fieldWindow, indexes, visibleRows.Length, rows.Count);
    PrintObjectIndexClues(reader, "bonus", bonus, fieldWindow, indexes, visibleRows.Length, rows.Count);
    foreach (var node in path)
        PrintObjectIndexClues(reader, $"path-step-{node.Step}", node.Address, Math.Min(fieldWindow, 0x1000), indexes, visibleRows.Length, rows.Count);

    Console.WriteLine();
    Console.WriteLine("Nearby Runeforge entities");
    Console.WriteLine("-------------------------");
    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    var printed = 0;
    foreach (var entity in EnumerateRuneforgeSourceEntities(reader, areaInstance, playerGrid).Take(maxEntities))
    {
        Console.WriteLine();
        Console.WriteLine(
            $"ENTITY {entity.Source,-8} id={entity.Id,-8} addr=0x{entity.Entity:X16} " +
            $"dist={FormatDistance(entity.Distance)} grid={FormatGrid(entity.Grid)} {entity.Metadata}");
        var components = ReadComponentMap(reader, entity.Entity);
        Console.WriteLine($"  comps=[{string.Join(", ", components.Select(c => c.Name))}]");
        foreach (var component in components)
        {
            var interestingComponent =
                component.Name.Equals("Stats", StringComparison.Ordinal) ||
                component.Name.Equals("StateMachine", StringComparison.Ordinal) ||
                component.Name.Equals("Preload", StringComparison.Ordinal) ||
                component.Name.Equals("Inventories", StringComparison.Ordinal) ||
                component.Name.Contains("Reward", StringComparison.OrdinalIgnoreCase) ||
                component.Name.Contains("Rune", StringComparison.OrdinalIgnoreCase);
            if (!interestingComponent) continue;

            Console.WriteLine($"  COMPONENT {component.Name,-18} idx={component.Index,3} addr=0x{component.Address:X16}");
            PrintObjectIndexClues(reader, $"  {component.Name}", component.Address, fieldWindow, indexes, visibleRows.Length, rows.Count);
            PrintComponentStringMatchClues(reader, component.Address, fieldWindow, visibleRows.Select(r => r.Text).ToArray());
        }

        printed++;
    }

    if (printed == 0)
        Console.WriteLine("  no Expedition2/RuneEncounter entities found");

    Console.WriteLine();
    Console.WriteLine("Next comparison:");
    Console.WriteLine("  - If a component/object has the visible indexes or visible-count vector after opening, run an entry snapshot before opening and check whether the same field exists.");
    Console.WriteLine("  - If only UI row objects contain the indexes, current rewards are probably constructed by UI code at open time.");
    return 0;
}

static void PrintRuneforgeVisibleRowDetails(
    MemoryReader reader,
    IReadOnlyList<RuneforgeCatalogRow> visibleRows,
    int maxChildren,
    int fieldWindow)
{
    var rowNumber = 0;
    foreach (var row in visibleRows.Take(12))
    {
        rowNumber++;
        var label = GetUiChildAt(reader, row.Row, 0, maxChildren);
        Console.WriteLine();
        Console.WriteLine($"ROW {rowNumber} catalogIdx={row.Index} row=0x{row.Row:X16} visible={row.Visible} flags=0x{row.Flags:X8} text='{row.Text}'");
        Console.WriteLine($"  row children={TryGetUiChildCount(reader, row.Row)} label=0x{label:X16}");
        PrintUiStringBlock(reader, row.Row, "  row text");
        PrintObjectStringSummary(reader, row.Row, "  row strings ", fieldWindow);
        PrintNestedVectorSummary(reader, row.Row, "  row vectors ", Math.Min(fieldWindow, 0x800));
        PrintRuneforgeKnownRowFields(reader, row.Row, row.Text, "  row known  ");
        PrintRuneforgeUiElementPointerFields(reader, row.Row, "  row ptr    ", fieldWindow);

        if (label != 0)
        {
            Console.WriteLine($"  LABEL 0x{label:X16} visible={ReadUiVisible(reader, label)} flags=0x{ReadUiFlags(reader, label):X8} children={TryGetUiChildCount(reader, label)}");
            PrintUiStringBlock(reader, label, "    label text");
            PrintObjectStringSummary(reader, label, "    label strings ", fieldWindow);
            PrintNestedVectorSummary(reader, label, "    label vectors ", Math.Min(fieldWindow, 0x800));
            PrintRuneforgeKnownRowFields(reader, label, row.Text, "    label known  ");
            PrintRuneforgeUiElementPointerFields(reader, label, "    label ptr    ", fieldWindow);
        }

        var children = ReadUiChildren(reader, row.Row, Math.Min(maxChildren, 24));
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == 0) continue;
            var strings = ReadUiStrings(reader, child).Take(8).ToArray();
            var childCount = TryGetUiChildCount(reader, child);
            if (strings.Length == 0 && childCount == 0 && i > 4) continue;
            Console.WriteLine($"    child[{i,2}] 0x{child:X16} visible={ReadUiVisible(reader, child)} flags=0x{ReadUiFlags(reader, child):X8} children={childCount}");
            foreach (var (offset, text) in strings)
                Console.WriteLine($"      +0x{offset:X3} {text}");
            PrintRuneforgeUiElementPointerFields(reader, child, "      ptr ", Math.Min(fieldWindow, 0x600), 4);
        }
    }
}

static void PrintRuneforgeKnownRowFields(MemoryReader reader, nint element, string rowText, string indent)
{
    foreach (var offset in new[] { 0x390, 0x3B0, 0x3B8, 0x400, 0x500, 0x510, 0x5C0, 0x618, 0x628, 0x630, 0x688, 0x6F0 })
    {
        if (offset == 0x390)
        {
            var inline = ReadStdWString(reader, element + offset);
            if (!string.IsNullOrWhiteSpace(inline))
                Console.WriteLine($"{indent}+0x{offset:X3} inline='{inline}'");
        }

        var ptr = SafePtr(reader, element + offset);
        if (!IsPlausiblePointer(ptr)) continue;

        var strings = ReadObjectTextClues(reader, ptr, 0x380)
            .Where(s => LooksLikeRuneforgeKnownFieldText(s.Text, rowText))
            .Take(14)
            .ToArray();
        if (strings.Length == 0 && offset is not (0x400 or 0x500 or 0x3B0 or 0x3B8))
            continue;

        Console.WriteLine($"{indent}+0x{offset:X3} ptr=0x{ptr:X16}");
        foreach (var (kind, subOffset, text) in strings)
            Console.WriteLine($"{indent}  {kind,-12} +0x{subOffset:X3} {text}");
        if (strings.Length == 0)
            PrintPointedObjectSummary(reader, ptr, indent + "  ", 0x300);
    }
}

static IEnumerable<(string Kind, int Offset, string Text)> ReadObjectTextClues(MemoryReader reader, nint address, int window)
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (var offset = 0; offset <= window - 8; offset += 8)
    {
        foreach (var clue in ReadStringCluesAt(reader, address, offset))
        {
            if (!seen.Add(clue.Text)) continue;
            yield return (clue.Kind, offset, clue.Text);
        }
    }
}

static bool LooksLikeRuneforgeKnownFieldText(string text, string rowText)
{
    if (string.IsNullOrWhiteSpace(text)) return false;
    if (LooksLikeRuneforgeRewardText(text)) return true;
    if (!string.IsNullOrWhiteSpace(rowText) && text.Contains(RewardKey(rowText), StringComparison.OrdinalIgnoreCase)) return true;
    if (text.Contains("Slot", StringComparison.OrdinalIgnoreCase) &&
        (text.Contains("Orb", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("Rune", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("Gem", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("Alloy", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("Flux", StringComparison.OrdinalIgnoreCase)))
        return true;
    if (text.Contains("Runeword", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("RecipeLabel", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("CraftingLabel", StringComparison.OrdinalIgnoreCase))
        return true;
    return false;
}

static void PrintRuneforgeUiElementPointerFields(
    MemoryReader reader,
    nint element,
    string indent,
    int fieldWindow,
    int limit = 8)
{
    var printed = 0;
    var seen = new HashSet<nint>();
    for (var offset = 0; offset <= fieldWindow - IntPtr.Size && printed < limit; offset += 8)
    {
        var ptr = SafePtr(reader, element + offset);
        if (!IsPlausiblePointer(ptr) || !seen.Add(ptr)) continue;

        var directText = ReadStdWString(reader, ptr);
        var nativeText = ReadNativeUtf8Text(reader, ptr);
        var utf16 = reader.ReadStringUtf16(ptr, 96);
        var utf8 = reader.ReadStringUtf8(ptr, 96);
        var usefulText =
            LooksLikeProbeText(directText) ? directText :
            LooksLikeProbeText(nativeText) ? nativeText :
            LooksLikeProbeText(utf16) ? utf16 :
            LooksLikeProbeText(utf8) ? utf8 :
            "";

        if (usefulText.Length == 0 && !LooksLikeRuneforgePointerTarget(reader, ptr))
            continue;

        Console.WriteLine($"{indent}+0x{offset:X3} -> 0x{ptr:X16}{(usefulText.Length == 0 ? "" : $" text='{usefulText}'")}");
        if (usefulText.Length == 0)
            PrintPointedObjectSummary(reader, ptr, indent + "  ", 0x300);
        printed++;
    }
}

static bool LooksLikeRuneforgePointerTarget(MemoryReader reader, nint ptr)
{
    if (ReadEntityMetadata(reader, ptr).Contains("Rune", StringComparison.OrdinalIgnoreCase))
        return true;
    if (ReadEntityMetadata(reader, ptr).Contains("Expedition", StringComparison.OrdinalIgnoreCase))
        return true;

    for (var offset = 0; offset <= 0x300 - 8; offset += 8)
    {
        foreach (var clue in ReadStringCluesAt(reader, ptr, offset))
        {
            if (LooksLikeRuneforgeRewardText(clue.Text) ||
                clue.Text.Contains("Rune", StringComparison.OrdinalIgnoreCase) ||
                clue.Text.Contains("Orb", StringComparison.OrdinalIgnoreCase))
                return true;
        }
    }

    return false;
}

static IReadOnlyList<RuneforgeCatalogRow> ReadRuneforgeCatalogRows(MemoryReader reader, nint catalog, int maxChildren)
{
    var rows = new List<RuneforgeCatalogRow>();
    if (!TryReadUiChildrenVector(reader, catalog, maxChildren, out var first, out var count)) return rows;
    for (long i = 0; i < count; i++)
    {
        var row = SafePtr(reader, first + (nint)(i * 8));
        if (row == 0) continue;
        var flags = ReadUiFlags(reader, row);
        var label = GetUiChildAt(reader, row, 0, maxChildren);
        var text = label == 0 ? "" : ReadStdWString(reader, label + Poe2.Runeforge.NameWString);
        if (string.IsNullOrWhiteSpace(text)) continue;
        rows.Add(new RuneforgeCatalogRow((int)i, row, flags, ReadUiVisible(reader, row), text));
    }
    return rows;
}

static void PrintObjectIndexClues(
    MemoryReader reader,
    string label,
    nint address,
    int fieldWindow,
    IReadOnlyList<int> visibleIndexes,
    int visibleCount,
    int totalRows)
{
    if (address == 0) return;
    var body = new byte[fieldWindow];
    var read = reader.TryReadBytes(address, body);
    if (read <= 0)
    {
        Console.WriteLine($"  {label,-14} 0x{address:X16}: unreadable");
        return;
    }
    if (read < body.Length) Array.Resize(ref body, read);

    var hits = new List<string>();
    for (var offset = 0; offset + 4 <= body.Length && hits.Count < 40; offset += 4)
    {
        var i32 = BitConverter.ToInt32(body, offset);
        if (visibleIndexes.Contains(i32))
            hits.Add($"+0x{offset:X3}=idx:{i32}");
        else if (i32 == visibleCount)
            hits.Add($"+0x{offset:X3}=visibleCount:{i32}");
        else if (i32 == totalRows)
            hits.Add($"+0x{offset:X3}=totalRows:{i32}");
    }

    var vectorHits = CaptureVectorFieldsFromBody(body, Math.Max(totalRows + 1024, 4000))
        .Where(v => v.Count == visibleCount || v.Count == totalRows || v.Count is > 0 and <= 16)
        .Take(12)
        .Select(v => $"+0x{v.Offset:X3}:count={v.Count}/cap={v.Capacity}")
        .ToArray();

    Console.WriteLine($"  {label,-14} 0x{address:X16}: intHits=[{string.Join(", ", hits.Take(20))}] vectors=[{string.Join("; ", vectorHits)}]");
}

static void PrintComponentStringMatchClues(MemoryReader reader, nint component, int fieldWindow, IReadOnlyList<string> rewardTexts)
{
    var tokens = rewardTexts
        .SelectMany(t => t.Split([' ', 'x', '[', ']', '|', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(t => t.Length >= 5 && !int.TryParse(t, out _))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(20)
        .ToArray();
    if (tokens.Length == 0) return;

    var printed = 0;
    for (var offset = 0; offset <= fieldWindow - 8 && printed < 10; offset += 8)
    {
        foreach (var clue in ReadStringCluesAt(reader, component, offset))
        {
            if (!tokens.Any(t => clue.Text.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;
            Console.WriteLine($"    TEXT-MATCH {clue.Kind,-12} +0x{offset:X3} -> {clue.Text}");
            printed++;
            if (printed >= 10) break;
        }
    }
}

static int RunRuneforgeEntryKeyScan(
    ProcessHandle process,
    MemoryReader reader,
    int maxEntities,
    int componentWindow,
    int objectWindow,
    int vectorEntries)
{
    maxEntities = Math.Clamp(maxEntities, 1, 200);
    componentWindow = Math.Clamp(componentWindow, 0x100, 0x8000);
    objectWindow = Math.Clamp(objectWindow, 0x80, 0x2000);
    vectorEntries = Math.Clamp(vectorEntries, 1, 256);

    var (_, _, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    Console.WriteLine();
    Console.WriteLine("Runeforge entry key scan");
    Console.WriteLine("------------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Entity cap      : {maxEntities}");
    Console.WriteLine($"Component window: 0x{componentWindow:X}");
    Console.WriteLine($"Object window   : 0x{objectWindow:X}");
    Console.WriteLine($"Vector entries  : {vectorEntries}");
    Console.WriteLine("Goal            : find Slot... reward/recipe keys before opening Runeshape.");

    var printedEntities = 0;
    var totalHits = 0;
    foreach (var entity in EnumerateRuneforgeSourceEntities(reader, areaInstance, playerGrid)
                 .OrderBy(e => e.Distance ?? float.MaxValue)
                 .Take(maxEntities))
    {
        var components = ReadComponentMap(reader, entity.Entity);
        var entityHits = new List<string>();
        foreach (var component in components)
        {
            var hits = FindRuneforgeRecipeKeyClues(reader, component.Address, componentWindow, objectWindow, vectorEntries)
                .Take(40)
                .ToArray();
            if (hits.Length == 0) continue;

            entityHits.Add($"  COMPONENT {component.Name,-18} idx={component.Index,3} addr=0x{component.Address:X16}");
            entityHits.AddRange(hits.Select(hit => $"    {hit}"));
            totalHits += hits.Length;
        }

        if (entityHits.Count == 0) continue;
        printedEntities++;
        Console.WriteLine();
        Console.WriteLine(
            $"ENTITY {entity.Source,-8} id={entity.Id,-8} addr=0x{entity.Entity:X16} " +
            $"dist={FormatDistance(entity.Distance)} grid={FormatGrid(entity.Grid)} {entity.Metadata}");
        foreach (var line in entityHits)
            Console.WriteLine(line);
    }

    Console.WriteLine();
    Console.WriteLine($"Entities with key clues: {printedEntities}");
    Console.WriteLine($"Total key clues        : {totalHits}");
    if (totalHits == 0)
    {
        Console.WriteLine("No recipe-key-looking strings found on loaded Runeforge entities.");
        Console.WriteLine("Run this before opening Runeshape and again after opening it; a post-open-only hit means the UI materializes the keys.");
    }
    return 0;
}

static IEnumerable<string> FindRuneforgeRecipeKeyClues(
    MemoryReader reader,
    nint component,
    int componentWindow,
    int objectWindow,
    int vectorEntries)
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var clue in ReadObjectTextClues(reader, component, componentWindow))
    {
        if (!LooksLikeRuneforgeRecipeKeyText(clue.Text) || !seen.Add($"direct:{clue.Offset}:{clue.Text}")) continue;
        yield return $"DIRECT {clue.Kind,-12} +0x{clue.Offset:X3} {clue.Text}";
    }

    var seenPointers = new HashSet<nint>();
    for (var offset = 0; offset <= componentWindow - IntPtr.Size; offset += 8)
    {
        var ptr = SafePtr(reader, component + offset);
        if (!IsPlausiblePointer(ptr) || !seenPointers.Add(ptr)) continue;
        foreach (var clue in ReadObjectTextClues(reader, ptr, objectWindow))
        {
            if (!LooksLikeRuneforgeRecipeKeyText(clue.Text) || !seen.Add($"ptr:{offset}:{clue.Offset}:{clue.Text}")) continue;
            yield return $"PTR    +0x{offset:X3}->0x{ptr:X16} {clue.Kind,-12} +0x{clue.Offset:X3} {clue.Text}";
        }
    }

    for (var offset = 0; offset <= componentWindow - 0x18; offset += 8)
    {
        if (!reader.TryReadStruct<StdVector>(component + offset, out var vector) ||
            !TryGetVectorByteSize(vector, 8, 0x40000, out var bytes))
            continue;

        var count = bytes / 8;
        if (count <= 0 || count > 8192) continue;
        for (var i = 0; i < Math.Min(count, vectorEntries); i++)
        {
            if (!reader.TryReadStruct<nint>(vector.First + i * 8, out var ptr) ||
                !IsPlausiblePointer(ptr) ||
                !seenPointers.Add(ptr))
                continue;

            foreach (var clue in ReadObjectTextClues(reader, ptr, objectWindow))
            {
                if (!LooksLikeRuneforgeRecipeKeyText(clue.Text) || !seen.Add($"vec:{offset}:{i}:{clue.Offset}:{clue.Text}")) continue;
                yield return $"VECTOR +0x{offset:X3}[{i}] ->0x{ptr:X16} {clue.Kind,-12} +0x{clue.Offset:X3} {clue.Text}";
            }
        }
    }
}

static bool LooksLikeRuneforgeRecipeKeyText(string text)
{
    if (string.IsNullOrWhiteSpace(text) || text.Length is < 6 or > 160) return false;
    if (text.Contains('/', StringComparison.Ordinal) || text.Contains('\\', StringComparison.Ordinal)) return false;

    var compact = text.Replace(" ", "", StringComparison.Ordinal);
    if (!compact.Contains("Slot", StringComparison.OrdinalIgnoreCase)) return false;
    return compact.Contains("Orb", StringComparison.OrdinalIgnoreCase) ||
           compact.Contains("Rune", StringComparison.OrdinalIgnoreCase) ||
           compact.Contains("Gem", StringComparison.OrdinalIgnoreCase) ||
           compact.Contains("Alloy", StringComparison.OrdinalIgnoreCase) ||
           compact.Contains("Flux", StringComparison.OrdinalIgnoreCase) ||
           compact.Contains("Jeweller", StringComparison.OrdinalIgnoreCase) ||
           compact.Contains("Annulment", StringComparison.OrdinalIgnoreCase);
}

static int RunRuneforgeSelectionIndexWatch(
    ProcessHandle process,
    MemoryReader reader,
    int timeoutSeconds,
    int maxEntities,
    int componentWindow,
    int maxChildren,
    int maxBranches)
{
    timeoutSeconds = Math.Clamp(timeoutSeconds, 5, 600);
    maxEntities = Math.Clamp(maxEntities, 1, 200);
    componentWindow = Math.Clamp(componentWindow, 0x100, 0x8000);
    maxChildren = Math.Clamp(maxChildren, 100, 60000);
    maxBranches = Math.Clamp(maxBranches, 4, 256);

    var (_, inGameState, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0 || inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    Console.WriteLine();
    Console.WriteLine("Runeforge selection index watch");
    Console.WriteLine("-------------------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Timeout         : {timeoutSeconds}s");
    Console.WriteLine($"Entity cap      : {maxEntities}");
    Console.WriteLine($"Component window: 0x{componentWindow:X}");
    Console.WriteLine("Goal            : see whether visible recipe row indexes exist before Runeshape opens.");

    var before = CaptureRuneforgeComponentMemorySnapshots(reader, areaInstance, playerGrid, maxEntities, componentWindow);
    PrintRuneforgeComponentSnapshotSummary("before", before);
    if (before.Count == 0)
    {
        Console.WriteLine("No Runeforge-related entity components found.");
        return 0;
    }

    Console.WriteLine();
    Console.WriteLine("Open Runeshape now. Waiting for visible reward rows...");
    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    IReadOnlyList<RuneforgeCatalogRow> visibleRows = [];
    nint catalog = 0;
    while (DateTime.UtcNow < deadline)
    {
        Thread.Sleep(100);
        var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
        if (uiRoot == 0) continue;
        if (!TryResolveRuneforgeCatalogSlot(reader, uiRoot, maxChildren, maxBranches, out _, out catalog, out _, out _, requireVisibleGate: true))
            continue;
        visibleRows = ReadRuneforgeCatalogRows(reader, catalog, maxChildren).Where(r => r.Visible).ToArray();
        if (visibleRows.Count is > 0 and <= 24) break;
    }

    if (visibleRows.Count == 0)
    {
        Console.WriteLine("No visible reward rows detected before timeout.");
        return 2;
    }
    if (visibleRows.Count > 24)
    {
        Console.WriteLine($"Visible row set looks like the master catalog ({visibleRows.Count} rows), not the selected map rewards.");
        Console.WriteLine("Open the Runeshape reward list from a clean map-entry state and rerun; index matching was skipped to avoid false positives.");
        return 3;
    }

    var after = CaptureRuneforgeComponentMemorySnapshots(reader, areaInstance, playerGrid, maxEntities, componentWindow);
    PrintRuneforgeComponentSnapshotSummary("after", after);

    Console.WriteLine();
    Console.WriteLine("Visible rows after open");
    Console.WriteLine("-----------------------");
    var rowAnchors = visibleRows
        .Select(r => new RuneforgeSelectionAnchor(r.Index, r.Text, ExtractRuneforgeRowRecipeKeys(reader, r.Row)))
        .ToArray();
    foreach (var anchor in rowAnchors)
        Console.WriteLine($"  idx={anchor.Index,4} text='{anchor.Text}' keys=[{string.Join(" | ", anchor.Keys)}]");
    if (rowAnchors.Any(a => a.Index is 0 or 1))
        Console.WriteLine("  note: idx 0/1 are printed for context but ignored during component-memory matching because they are too common.");

    Console.WriteLine();
    Console.WriteLine("Before-open index hits");
    Console.WriteLine("----------------------");
    PrintSelectionIndexHits(before, rowAnchors, "BEFORE");

    Console.WriteLine();
    Console.WriteLine("After-open index hits");
    Console.WriteLine("---------------------");
    PrintSelectionIndexHits(after, rowAnchors, "AFTER");

    Console.WriteLine();
    Console.WriteLine("Changed-to-index fields");
    Console.WriteLine("-----------------------");
    PrintSelectionIndexTransitions(before, after, rowAnchors);
    return 0;
}

static IReadOnlyList<RuneforgeComponentMemorySnapshot> CaptureRuneforgeComponentMemorySnapshots(
    MemoryReader reader,
    nint areaInstance,
    System.Numerics.Vector2? playerGrid,
    int maxEntities,
    int componentWindow)
{
    var result = new List<RuneforgeComponentMemorySnapshot>();
    foreach (var entity in EnumerateRuneforgeSourceEntities(reader, areaInstance, playerGrid)
                 .OrderBy(e => e.Distance ?? float.MaxValue)
                 .Take(maxEntities))
    {
        foreach (var component in ReadComponentMap(reader, entity.Entity))
        {
            if (component.Address == 0) continue;
            var body = CaptureMemoryBytes(reader, component.Address, componentWindow);
            if (body.Length == 0) continue;
            result.Add(new RuneforgeComponentMemorySnapshot(
                entity.Source,
                entity.Id,
                entity.Entity,
                entity.Metadata,
                entity.Distance,
                component.Name,
                component.Index,
                component.Address,
                body));
        }
    }

    return result;
}

static void PrintRuneforgeComponentSnapshotSummary(string label, IReadOnlyList<RuneforgeComponentMemorySnapshot> snapshots)
{
    Console.WriteLine($"{label} component snapshot: {snapshots.Count} component(s)");
    foreach (var group in snapshots.GroupBy(s => (s.Source, s.EntityId, s.Metadata, s.Distance)).Take(12))
    {
        Console.WriteLine(
            $"  {group.Key.Source,-8} id={group.Key.EntityId,-8} dist={FormatDistance(group.Key.Distance),6} " +
            $"components={group.Count(),2} {group.Key.Metadata}");
    }
}

static IReadOnlyList<string> ExtractRuneforgeRowRecipeKeys(MemoryReader reader, nint row)
{
    var keys = new List<string>();
    foreach (var offset in new[] { 0x500, 0x510, 0x5C0, 0x628, 0x630 })
    {
        var ptr = SafePtr(reader, row + offset);
        if (!IsPlausiblePointer(ptr)) continue;
        foreach (var clue in ReadObjectTextClues(reader, ptr, 0x500))
            if (LooksLikeRuneforgeRecipeKeyText(clue.Text))
                keys.Add($"+0x{offset:X3}/+0x{clue.Offset:X3}:{clue.Text}");
    }
    return keys.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray();
}

static void PrintSelectionIndexHits(
    IReadOnlyList<RuneforgeComponentMemorySnapshot> snapshots,
    IReadOnlyList<RuneforgeSelectionAnchor> anchors,
    string label)
{
    var indexes = anchors.Select(a => a.Index).Distinct().ToArray();
    var anchorByIndex = anchors
        .Where(a => a.Index > 1)
        .GroupBy(a => a.Index)
        .ToDictionary(g => g.Key, g => g.First().Text);
    var printed = 0;
    var suppressed = 0;
    foreach (var snapshot in snapshots)
    {
        var hits = FindIntHits(snapshot.Body, indexes).Take(64).ToArray();
        if (hits.Length == 0) continue;
        var uniqueIndexes = hits.Select(h => h.Index).Distinct().Count();
        var interactionCandidate = snapshot.ComponentName.Contains("Interaction", StringComparison.OrdinalIgnoreCase);
        var likelyNoise =
            (uniqueIndexes == 1 && IsLikelySelectionIndexNoise(snapshot.ComponentName, snapshot.Body, hits)) ||
            IsSequentialCatalogIndexNoise(snapshot.ComponentName, snapshot.Body, hits);
        if (likelyNoise && !interactionCandidate)
        {
            suppressed++;
            continue;
        }

        printed++;
        var strength = uniqueIndexes >= 2
            ? "signal"
            : interactionCandidate ? "weak-interaction" : "weak";
        Console.WriteLine(
            $"  {label} {strength,-16} {snapshot.Source,-8} id={snapshot.EntityId,-8} {snapshot.ComponentName,-22} " +
            $"idx={snapshot.ComponentIndex,3} addr=0x{snapshot.Component:X16} unique={uniqueIndexes} " +
            $"hits=[{string.Join(", ", hits.Take(24).Select(FormatSelectionIndexHit))}]");
        if (uniqueIndexes >= 2 || interactionCandidate)
            PrintSelectionIndexHitContext(snapshot.Body, hits, anchorByIndex);
        if (printed >= 40)
        {
            Console.WriteLine("  ...additional hits omitted");
            break;
        }
    }

    if (printed == 0)
        Console.WriteLine("  no strong visible row index hits found in captured component bodies");
    if (suppressed > 0)
        Console.WriteLine($"  suppressed {suppressed} likely-noise single-index component hit(s)");
}

static bool IsLikelySelectionIndexNoise(string componentName, byte[] body, IReadOnlyList<RuneforgeIndexHit> hits)
{
    if (hits.Count == 0) return false;
    if (componentName is "Life" or "Functions" or "DiesAfterTime") return true;
    if (hits.Count < 3) return false;

    var firstIndex = hits[0].Index;
    if (hits.Any(h => h.Index != firstIndex)) return false;

    var deltas = hits.Zip(hits.Skip(1), (a, b) => b.Offset - a.Offset).ToArray();
    return deltas.Length > 0 && deltas.Distinct().Count() <= 2;
}

static bool IsSequentialCatalogIndexNoise(string componentName, byte[] body, IReadOnlyList<RuneforgeIndexHit> hits)
{
    if (hits.Count == 0) return false;
    if (componentName is not "Functions" and not "Life" and not "DiesAfterTime") return false;

    var sequentialHits = 0;
    foreach (var hit in hits)
    {
        if (HasSequentialIndexNeighbor(body, hit.Offset, hit.Index, -8, -1) &&
            HasSequentialIndexNeighbor(body, hit.Offset, hit.Index, 8, 1))
            sequentialHits++;
    }

    return sequentialHits == hits.Count;
}

static bool HasSequentialIndexNeighbor(byte[] body, int offset, int value, int deltaOffset, int deltaValue)
{
    var neighborOffset = offset + deltaOffset;
    if (neighborOffset < 0 || neighborOffset + 4 > body.Length) return false;
    return BitConverter.ToInt32(body, neighborOffset) == value + deltaValue;
}

static string FormatSelectionIndexHit(RuneforgeIndexHit hit) => $"+0x{hit.Offset:X3}=idx:{hit.Index}";

static void PrintSelectionIndexHitContext(
    byte[] body,
    IReadOnlyList<RuneforgeIndexHit> hits,
    IReadOnlyDictionary<int, string> anchorByIndex)
{
    foreach (var hit in hits.Take(6))
    {
        var row = anchorByIndex.TryGetValue(hit.Index, out var text) ? text : "?";
        var start = Math.Max(0, hit.Offset - 0x10) & ~0x3;
        var end = Math.Min(body.Length - 4, hit.Offset + 0x10);
        var values = new List<string>();
        for (var offset = start; offset <= end; offset += 4)
        {
            var value = BitConverter.ToInt32(body, offset);
            var marker = offset == hit.Offset ? "*" : "";
            values.Add($"{marker}+0x{offset:X3}:{value}{marker}");
        }
        Console.WriteLine($"    ctx idx:{hit.Index} '{row}' -> {string.Join(" ", values)}");
    }
}

static IEnumerable<RuneforgeIndexHit> FindIntHits(byte[] body, IReadOnlyList<int> indexes)
{
    var usefulIndexes = indexes.Where(i => i > 1).Distinct().ToHashSet();
    if (usefulIndexes.Count == 0)
        yield break;

    for (var offset = 0; offset + 4 <= body.Length; offset += 4)
    {
        var value = BitConverter.ToInt32(body, offset);
        if (usefulIndexes.Contains(value))
            yield return new RuneforgeIndexHit(offset, value);
    }
}

static void PrintSelectionIndexTransitions(
    IReadOnlyList<RuneforgeComponentMemorySnapshot> before,
    IReadOnlyList<RuneforgeComponentMemorySnapshot> after,
    IReadOnlyList<RuneforgeSelectionAnchor> anchors)
{
    var indexes = anchors.Select(a => a.Index).Where(i => i > 1).Distinct().ToHashSet();
    if (indexes.Count == 0)
    {
        Console.WriteLine("  no useful visible row indexes to test (idx 0/1 are intentionally ignored)");
        return;
    }

    var beforeMap = before.ToDictionary(s => (s.EntityId, s.ComponentName, s.ComponentIndex));
    var printed = 0;
    foreach (var a in after)
    {
        if (!beforeMap.TryGetValue((a.EntityId, a.ComponentName, a.ComponentIndex), out var b))
            continue;
        var min = Math.Min(a.Body.Length, b.Body.Length);
        var hits = new List<string>();
        for (var offset = 0; offset + 4 <= min && hits.Count < 24; offset += 4)
        {
            var beforeValue = BitConverter.ToInt32(b.Body, offset);
            var afterValue = BitConverter.ToInt32(a.Body, offset);
            if (beforeValue == afterValue || !indexes.Contains(afterValue)) continue;
            hits.Add($"+0x{offset:X3}:{beforeValue}->idx:{afterValue}");
        }
        if (hits.Count == 0) continue;

        printed++;
        Console.WriteLine(
            $"  {a.Source,-8} id={a.EntityId,-8} {a.ComponentName,-22} idx={a.ComponentIndex,3} " +
            $"addr=0x{a.Component:X16} transitions=[{string.Join(", ", hits)}]");
        if (printed >= 40)
        {
            Console.WriteLine("  ...additional transitions omitted");
            break;
        }
    }

    if (printed == 0)
        Console.WriteLine("  no component fields changed to visible row indexes");
}

static int RunRuneforgeInteractionBlockProbe(
    ProcessHandle process,
    MemoryReader reader,
    int timeoutSeconds,
    int maxEntities,
    int componentWindow,
    int maxChildren,
    int maxBranches)
{
    timeoutSeconds = Math.Clamp(timeoutSeconds, 5, 600);
    maxEntities = Math.Clamp(maxEntities, 1, 200);
    componentWindow = Math.Clamp(componentWindow, 0x100, 0x8000);
    maxChildren = Math.Clamp(maxChildren, 100, 60000);
    maxBranches = Math.Clamp(maxBranches, 4, 256);

    var (_, inGameState, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0 || inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    Console.WriteLine();
    Console.WriteLine("Runeforge InteractionAction block probe");
    Console.WriteLine("---------------------------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Timeout         : {timeoutSeconds}s");
    Console.WriteLine($"Entity cap      : {maxEntities}");
    Console.WriteLine($"Component window: 0x{componentWindow:X}");
    Console.WriteLine("Goal            : inspect pre-open InteractionAction blocks that contain selected row indexes.");

    var before = CaptureRuneforgeComponentMemorySnapshots(reader, areaInstance, playerGrid, maxEntities, componentWindow)
        .Where(s => s.ComponentName.Contains("Interaction", StringComparison.OrdinalIgnoreCase))
        .ToArray();
    PrintRuneforgeComponentSnapshotSummary("before InteractionAction", before);
    if (before.Length == 0)
    {
        Console.WriteLine("No InteractionAction component found on loaded Runeforge entities.");
        return 0;
    }

    Console.WriteLine();
    Console.WriteLine("Open Runeshape now. Waiting for visible reward rows...");
    var visibleRows = WaitForRuneforgeVisibleRows(reader, inGameState, timeoutSeconds, maxChildren, maxBranches, out _);
    if (visibleRows.Count == 0)
    {
        Console.WriteLine("No visible reward rows detected before timeout.");
        return 2;
    }
    if (visibleRows.Count > 24)
    {
        Console.WriteLine($"Visible row set looks like the master catalog ({visibleRows.Count} rows), not the selected map rewards.");
        return 3;
    }

    var anchors = visibleRows
        .Select(r => new RuneforgeSelectionAnchor(r.Index, r.Text, ExtractRuneforgeRowRecipeKeys(reader, r.Row)))
        .ToArray();
    var anchorByIndex = anchors
        .Where(a => a.Index > 1)
        .GroupBy(a => a.Index)
        .ToDictionary(g => g.Key, g => g.First().Text);

    Console.WriteLine();
    Console.WriteLine("Visible selected rows");
    Console.WriteLine("---------------------");
    foreach (var anchor in anchors)
        Console.WriteLine($"  idx={anchor.Index,4} text='{anchor.Text}'");

    Console.WriteLine();
    Console.WriteLine("InteractionAction pre-open blocks");
    Console.WriteLine("---------------------------------");
    var printed = 0;
    foreach (var snapshot in before)
    {
        var hits = FindIntHits(snapshot.Body, anchors.Select(a => a.Index).Distinct().ToArray()).ToArray();
        if (hits.Length == 0) continue;

        printed++;
        Console.WriteLine(
            $"{snapshot.Source,-8} id={snapshot.EntityId,-8} component=0x{snapshot.Component:X16} " +
            $"idx={snapshot.ComponentIndex} hits={hits.Length}");

        foreach (var hit in hits.Take(16))
            PrintInteractionCandidateBlock(snapshot.Body, hit, anchorByIndex);
    }

    if (printed == 0)
        Console.WriteLine("  no selected row indexes found inside pre-open InteractionAction bodies");

    Console.WriteLine();
    Console.WriteLine("Read-only interpretation:");
    Console.WriteLine("  - repeated identical blocks at different offsets are likely action templates/caches.");
    Console.WriteLine("  - non-sequential selected indexes grouped near other nonzero fields are better reward-state candidates.");
    Console.WriteLine("  - if only one selected index appears, gather more samples before treating it as a real source.");
    return 0;
}

static IReadOnlyList<RuneforgeCatalogRow> WaitForRuneforgeVisibleRows(
    MemoryReader reader,
    nint inGameState,
    int timeoutSeconds,
    int maxChildren,
    int maxBranches,
    out nint catalog)
{
    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    catalog = 0;
    while (DateTime.UtcNow < deadline)
    {
        Thread.Sleep(100);
        var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
        if (uiRoot == 0) continue;
        if (!TryResolveRuneforgeCatalogSlot(reader, uiRoot, maxChildren, maxBranches, out _, out catalog, out _, out _, requireVisibleGate: true))
            continue;
        var rows = ReadRuneforgeCatalogRows(reader, catalog, maxChildren).Where(r => r.Visible).ToArray();
        if (rows.Length is > 0 and <= 24) return rows;
        if (rows.Length > 24) return rows;
    }

    return [];
}

static void PrintInteractionCandidateBlock(
    byte[] body,
    RuneforgeIndexHit hit,
    IReadOnlyDictionary<int, string> anchorByIndex)
{
    var row = anchorByIndex.TryGetValue(hit.Index, out var text) ? text : "?";
    var blockStart = Math.Max(0, hit.Offset - 0x20) & ~0xF;
    var blockEnd = Math.Min(body.Length - 4, hit.Offset + 0x40);
    Console.WriteLine($"  hit +0x{hit.Offset:X3}=idx:{hit.Index} '{row}' block=+0x{blockStart:X3}..+0x{blockEnd:X3}");

    for (var offset = blockStart; offset <= blockEnd; offset += 0x10)
    {
        var parts = new List<string>();
        for (var inner = 0; inner < 0x10 && offset + inner + 4 <= body.Length; inner += 4)
        {
            var fieldOffset = offset + inner;
            var value = BitConverter.ToInt32(body, fieldOffset);
            var marker = fieldOffset == hit.Offset ? "*" : " ";
            parts.Add($"{marker}+0x{fieldOffset:X3}:{value,11}{marker}");
        }
        Console.WriteLine("    " + string.Join(" ", parts));
    }
}

static int RunRuneforgeSelectionFingerprintWatch(
    ProcessHandle process,
    MemoryReader reader,
    int timeoutSeconds,
    int maxEntities,
    int componentWindow,
    int objectWindow,
    int pointerDepth,
    int maxChildren,
    int maxBranches)
{
    timeoutSeconds = Math.Clamp(timeoutSeconds, 5, 600);
    maxEntities = Math.Clamp(maxEntities, 1, 200);
    componentWindow = Math.Clamp(componentWindow, 0x100, 0x10000);
    objectWindow = Math.Clamp(objectWindow, 0x100, 0x8000);
    pointerDepth = Math.Clamp(pointerDepth, 0, 3);
    maxChildren = Math.Clamp(maxChildren, 100, 60000);
    maxBranches = Math.Clamp(maxBranches, 4, 256);

    var (_, inGameState, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0 || inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    Console.WriteLine();
    Console.WriteLine("Runeforge selection fingerprint watch");
    Console.WriteLine("-------------------------------------");
    PrintAreaInfo(reader, areaInstance);
    Console.WriteLine($"Timeout         : {timeoutSeconds}s");
    Console.WriteLine($"Entity cap      : {maxEntities}");
    Console.WriteLine($"Component window: 0x{componentWindow:X}");
    Console.WriteLine($"Object window   : 0x{objectWindow:X}");
    Console.WriteLine($"Pointer depth   : {pointerDepth}");
    Console.WriteLine("Goal            : correlate selected rewards against pre-open component/object memory.");

    var beforeComponents = CaptureRuneforgeComponentMemorySnapshots(reader, areaInstance, playerGrid, maxEntities, componentWindow);
    PrintRuneforgeComponentSnapshotSummary("before", beforeComponents);
    if (beforeComponents.Count == 0)
    {
        Console.WriteLine("No Runeforge-related entity components found.");
        return 0;
    }

    var beforeBlocks = BuildRuneforgeFingerprintBlocks(reader, beforeComponents, objectWindow, pointerDepth);
    Console.WriteLine($"Pre-open blocks : {beforeBlocks.Count} ({beforeComponents.Count} component bodies + reachable pointer objects)");

    Console.WriteLine();
    Console.WriteLine("Open Runeshape now. Waiting for visible reward rows...");
    var visibleRows = WaitForRuneforgeVisibleRows(reader, inGameState, timeoutSeconds, maxChildren, maxBranches, out _);
    if (visibleRows.Count == 0)
    {
        Console.WriteLine("No visible reward rows detected before timeout.");
        return 2;
    }
    if (visibleRows.Count > 24)
    {
        Console.WriteLine($"Visible row set looks like the master catalog ({visibleRows.Count} rows), not the selected map rewards.");
        return 3;
    }

    var anchors = visibleRows
        .Select(r => new RuneforgeSelectionAnchor(r.Index, r.Text, ExtractRuneforgeRowRecipeKeys(reader, r.Row)))
        .ToArray();
    var indexes = anchors.Select(a => a.Index).Where(i => i > 1).Distinct().OrderBy(i => i).ToArray();
    var anchorByIndex = anchors
        .Where(a => a.Index > 1)
        .GroupBy(a => a.Index)
        .ToDictionary(g => g.Key, g => g.First().Text);

    Console.WriteLine();
    Console.WriteLine("Visible selected rows");
    Console.WriteLine("---------------------");
    foreach (var anchor in anchors)
        Console.WriteLine($"  idx={anchor.Index,4} text='{anchor.Text}' keys=[{string.Join(" | ", anchor.Keys)}]");

    if (indexes.Length == 0)
    {
        Console.WriteLine("No useful row indexes to correlate (idx 0/1 ignored).");
        return 0;
    }

    var candidates = new List<RuneforgeFingerprintCandidate>();
    foreach (var block in beforeBlocks)
    {
        candidates.AddRange(FindRawIntFingerprintCandidates(block, indexes));
        candidates.AddRange(FindPackedUShortFingerprintCandidates(block, indexes));
        candidates.AddRange(FindPackedByteFingerprintCandidates(block, indexes));
        candidates.AddRange(FindBitsetFingerprintCandidates(block, indexes));
    }

    var ranked = candidates
        .Where(c => c.MatchedIndexes.Count > 0)
        .GroupBy(c => $"{c.Kind}|{c.Block.Source}|{c.Block.EntityId}|{c.Block.ComponentName}|{c.Block.ComponentIndex}|{c.Block.Path}|{c.Offset:X}")
        .Select(g => g.OrderByDescending(c => c.Score).First())
        .OrderByDescending(c => c.Score)
        .ThenByDescending(c => c.MatchedIndexes.Count)
        .ThenBy(c => c.Block.EntityId)
        .ThenBy(c => c.Block.ComponentName, StringComparer.Ordinal)
        .Take(40)
        .ToArray();

    Console.WriteLine();
    Console.WriteLine("Fingerprint candidates");
    Console.WriteLine("----------------------");
    if (ranked.Length == 0)
    {
        Console.WriteLine("  no pre-open fingerprint matches found");
    }
    else
    {
        foreach (var candidate in ranked)
            PrintFingerprintCandidate(candidate, anchorByIndex);
    }

    Console.WriteLine();
    Console.WriteLine("Read-only interpretation:");
    Console.WriteLine("  - int/ushort/byte hits show literal or packed row ids.");
    Console.WriteLine("  - bitset hits suggest selected rows may be stored as flags rather than ids.");
    Console.WriteLine("  - component-path hits are stronger than deep pointer hits unless repeated across maps.");
    Console.WriteLine("  - one-index matches are weak; multi-index matches across unrelated row numbers are the prize.");
    return 0;
}

static int RunMonolith(ProcessHandle process, MemoryReader reader)
{
    var slot = FindGameStateSlot(process, reader);
    if (slot == 0)
    {
        Console.Error.WriteLine("Could not lock GameState slot (in game?).");
        return 1;
    }

    var live = new Poe2Live(reader, slot);
    if (!live.TryResolve(out _, out var areaInstance, out var localPlayer))
    {
        Console.Error.WriteLine("Could not resolve area.");
        return 1;
    }

    var catalog = RuneMonolithCatalog.Instance;
    var areaLevel = live.AreaLevel(areaInstance);
    var playerGrid = ReadEntityGrid(reader, localPlayer);

    Console.WriteLine();
    Console.WriteLine("Runeshape monolith probe");
    Console.WriteLine("------------------------");
    Console.WriteLine($"AreaInfo     : code='{live.AreaCode(areaInstance)}' level={areaLevel} hash=0x{live.AreaHash(areaInstance):X8}");
    Console.WriteLine($"Catalog      : loaded={catalog.IsLoaded}");
    Console.WriteLine($"Player grid  : {FormatGrid(playerGrid)}");
    Console.WriteLine("Goal         : validate Expedition2Encounter -> StateMachine -> RuneStation reward state.\n");

    var devices = new List<(uint Id, nint Entity, string Metadata, System.Numerics.Vector2? Grid, float Distance)>();
    foreach (var mapOffset in new[] { Poe2.AreaInstance.AwakeEntities, Poe2.AreaInstance.SleepingEntities })
    foreach (var (id, entity, metadata) in EnumerateEntityMap(reader, areaInstance, mapOffset))
    {
        if (!metadata.Contains("Expedition2Encounter", StringComparison.OrdinalIgnoreCase)) continue;
        var grid = ReadEntityGrid(reader, entity);
        var distance = playerGrid.HasValue && grid.HasValue
            ? System.Numerics.Vector2.Distance(playerGrid.Value, grid.Value)
            : float.MaxValue;
        devices.Add((id, entity, metadata, grid, distance));
    }

    devices.Sort((a, b) => a.Distance.CompareTo(b.Distance));
    Console.WriteLine($"Devices      : {devices.Count}\n");
    foreach (var device in devices)
    {
        var monolith = live.ReadMonolith(device.Entity);
        var distText = device.Distance == float.MaxValue ? "-" : device.Distance.ToString("F1");
        Console.WriteLine($"ENTITY id={device.Id} addr=0x{device.Entity:X16} dist={distText} grid={FormatGrid(device.Grid)}");
        Console.WriteLine($"  meta={device.Metadata}");
        if (!monolith.Resolved)
        {
            Console.WriteLine($"  station: unresolved collected={monolith.Collected}");
            continue;
        }

        if (TryFindRuneStation(reader, device.Entity, out var station, out _, out _, out _))
            PrintRuneStationAnchorClues(reader, station);

        var anchor = monolith.IsUnique
            ? "unique/anchorless"
            : monolith.AnchorIdx >= 0
                ? $"{catalog.RuneName(monolith.AnchorIdx)} idx={monolith.AnchorIdx} pos={monolith.AnchorPos + 1}"
                : $"decode-failed pos={monolith.AnchorPos + 1}";
        Console.WriteLine($"  station: holes={monolith.HoleCount} anchor={anchor} collected={monolith.Collected}");

        var offers = catalog.Offers(monolith.AnchorIdx, monolith.AnchorPos, monolith.HoleCount, monolith.IsUnique, areaLevel);
        Console.WriteLine($"  offers={offers.Count}");
        foreach (var offer in offers.Take(50))
        {
            var name = string.IsNullOrWhiteSpace(offer.Name) ? $"({offer.Description})" : offer.Name;
            Console.WriteLine($"    size={offer.Size} count={offer.Count} {name} [{offer.Runes}]");
        }
    }

    return 0;
}

static bool TryFindRuneStation(
    MemoryReader reader,
    nint entity,
    out nint station,
    out int vecOff,
    out int listenerSub,
    out int ownerOff)
{
    station = 0;
    vecOff = 0;
    listenerSub = 0;
    ownerOff = 0;

    var stateMachine = ResolveComponentAddr(reader, entity, "StateMachine");
    if (stateMachine == 0) return false;

    var canonicalFirst = SafePtr(reader, stateMachine + Poe2.StateMachine.ListenerVec);
    if (canonicalFirst != 0 &&
        reader.TryReadStruct<nint>(stateMachine + Poe2.StateMachine.ListenerVec + 8, out var canonicalLast) &&
        canonicalLast > canonicalFirst)
    {
        var bytes = (long)canonicalLast - canonicalFirst;
        var count = bytes % 8 == 0 ? bytes / 8 : 0;
        if (count is > 0 and <= 64)
        {
            for (long i = 0; i < count; i++)
            {
                var node = SafePtr(reader, canonicalFirst + (nint)(i * 8));
                var listener = node == 0 ? 0 : SafePtr(reader, node);
                if (listener == 0) continue;

                var candidate = listener - Poe2.RuneStation.ListenerSub;
                if (SafePtr(reader, candidate + Poe2.RuneStation.Owner) != entity) continue;

                station = candidate;
                vecOff = Poe2.StateMachine.ListenerVec;
                listenerSub = Poe2.RuneStation.ListenerSub;
                ownerOff = Poe2.RuneStation.Owner;
                return true;
            }
        }
    }

    for (var vo = 0; vo <= 0x80; vo += 8)
    {
        var first = SafePtr(reader, stateMachine + vo);
        if (first == 0 ||
            !reader.TryReadStruct<nint>(stateMachine + vo + 8, out var last) ||
            last <= first)
            continue;

        var bytes = (long)last - first;
        if (bytes % 8 != 0) continue;
        var count = bytes / 8;
        if (count is <= 0 or > 64) continue;

        for (long i = 0; i < count; i++)
        {
            var node = SafePtr(reader, first + (nint)(i * 8));
            var listener = node == 0 ? 0 : SafePtr(reader, node);
            if (listener == 0) continue;

            for (var sub = 0x80; sub <= 0xC0; sub += 8)
            {
                var candidate = listener - sub;
                for (var oo = 0; oo <= 0x40; oo += 8)
                {
                    if (SafePtr(reader, candidate + oo) != entity) continue;

                    station = candidate;
                    vecOff = vo;
                    listenerSub = sub;
                    ownerOff = oo;
                    return true;
                }
            }
        }
    }

    return false;
}

static void PrintRuneStationAnchorClues(MemoryReader reader, nint station)
{
    Console.WriteLine($"  station ptr: 0x{station:X16}");
    var ints = new List<string>();
    for (var off = 0; off <= 0x90; off += 4)
    {
        if (!reader.TryReadStruct<int>(station + off, out var value)) continue;
        if (value is >= -1 and <= 64)
            ints.Add($"+0x{off:X}={value}");
    }
    Console.WriteLine($"  small ints : {(ints.Count == 0 ? "<none>" : string.Join(" ", ints))}");

    var ptrs = new List<string>();
    for (var off = 0; off <= 0x90; off += 8)
    {
        var ptr = SafePtr(reader, station + off);
        if (ptr == 0) continue;
        var nested = SafePtr(reader, ptr + 0x28);
        var nested2 = nested == 0 ? 0 : SafePtr(reader, nested);
        ptrs.Add($"+0x{off:X}=0x{ptr:X16} nested28=0x{nested:X16}/0x{nested2:X16}");
    }
    foreach (var line in ptrs.Take(12))
        Console.WriteLine($"  ptr clue   : {line}");
}

static int RunMonolithListenerScan(ProcessHandle process, MemoryReader reader, int maxEntities, int entryLimit, int scanWindow)
{
    var slot = FindGameStateSlot(process, reader);
    if (slot == 0)
    {
        Console.Error.WriteLine("Could not lock GameState slot (in game?).");
        return 1;
    }

    var live = new Poe2Live(reader, slot);
    if (!live.TryResolve(out _, out var areaInstance, out var localPlayer))
    {
        Console.Error.WriteLine("Could not resolve area.");
        return 1;
    }

    var playerGrid = ReadEntityGrid(reader, localPlayer);
    Console.WriteLine();
    Console.WriteLine("Runeshape monolith listener scan");
    Console.WriteLine("--------------------------------");
    Console.WriteLine($"AreaInfo     : code='{live.AreaCode(areaInstance)}' level={live.AreaLevel(areaInstance)} hash=0x{live.AreaHash(areaInstance):X8}");
    Console.WriteLine($"Player grid  : {FormatGrid(playerGrid)}");
    Console.WriteLine($"Entity cap   : {maxEntities}");
    Console.WriteLine($"Entry limit  : {entryLimit}");
    Console.WriteLine($"SM scan      : +0x0..+0x{scanWindow:X}");
    Console.WriteLine("Goal         : find shifted StateMachine listener vectors / RuneStation owner offsets.\n");

    var devices = new List<(uint Id, nint Entity, string Metadata, System.Numerics.Vector2? Grid, float Distance)>();
    foreach (var mapOffset in new[] { Poe2.AreaInstance.AwakeEntities, Poe2.AreaInstance.SleepingEntities })
    foreach (var (id, entity, metadata) in EnumerateEntityMap(reader, areaInstance, mapOffset))
    {
        if (!metadata.Contains("Expedition2Encounter", StringComparison.OrdinalIgnoreCase)) continue;
        var grid = ReadEntityGrid(reader, entity);
        var distance = playerGrid.HasValue && grid.HasValue
            ? System.Numerics.Vector2.Distance(playerGrid.Value, grid.Value)
            : float.MaxValue;
        devices.Add((id, entity, metadata, grid, distance));
    }

    devices.Sort((a, b) => a.Distance.CompareTo(b.Distance));
    var totalHits = 0;
    foreach (var device in devices.Take(Math.Max(1, maxEntities)))
    {
        var stateMachine = ResolveComponentAddr(reader, device.Entity, "StateMachine");
        var distText = device.Distance == float.MaxValue ? "-" : device.Distance.ToString("F1");
        Console.WriteLine($"ENTITY id={device.Id} addr=0x{device.Entity:X16} dist={distText} grid={FormatGrid(device.Grid)}");
        Console.WriteLine($"  StateMachine=0x{stateMachine:X16}");
        if (stateMachine == 0) continue;

        var entityHits = 0;
        for (var vecOff = 0; vecOff <= scanWindow; vecOff += 8)
        {
            var first = SafePtr(reader, stateMachine + vecOff);
            if (first == 0 ||
                !reader.TryReadStruct<nint>(stateMachine + vecOff + 8, out var last) ||
                last <= first)
                continue;

            var bytes = (long)last - first;
            if (bytes % 8 != 0) continue;
            var count = bytes / 8;
            if (count is <= 0 or > 512) continue;

            var tested = Math.Min(count, Math.Max(1, entryLimit));
            for (long i = 0; i < tested; i++)
            {
                var node = SafePtr(reader, first + (nint)(i * 8));
                if (node == 0) continue;

                var listeners = new[] { node, SafePtr(reader, node) };
                for (var li = 0; li < listeners.Length; li++)
                {
                    var listener = listeners[li];
                    if (listener == 0 || (li == 1 && listener == node)) continue;

                    for (var sub = 0; sub <= 0x240; sub += 8)
                    {
                        var candidate = listener - sub;
                        if (candidate == 0) continue;

                        for (var ownerOff = 0; ownerOff <= 0x100; ownerOff += 8)
                        {
                            if (SafePtr(reader, candidate + ownerOff) != device.Entity) continue;

                            entityHits++;
                            totalHits++;
                            Console.WriteLine(
                                $"  HIT vec+0x{vecOff:X3} count={count} entry={i} listener[{li}]=0x{listener:X16} " +
                                $"sub=0x{sub:X} station=0x{candidate:X16} owner+0x{ownerOff:X}");
                            Console.WriteLine($"      ints: {SummarizeCandidateInts(reader, candidate)}");

                            if (entityHits >= 24)
                            {
                                Console.WriteLine("  hit cap reached for this entity");
                                goto NextEntity;
                            }
                        }
                    }
                }
            }
        }

NextEntity:
        if (entityHits == 0)
            Console.WriteLine("  no listener->station owner hits found");
    }

    Console.WriteLine($"\nTotal hits: {totalHits}");
    Console.WriteLine("Interpretation:");
    Console.WriteLine("  - A hit with vec+0x020 sub=0x98 owner+0x10 matches current committed offsets.");
    Console.WriteLine("  - Repeated hits with a different vec/sub/owner point to the constants to update.");
    Console.WriteLine("  - Plausible hole/anchor fields should show as small ints (1..34) near the station candidate.");
    return 0;
}

static string SummarizeCandidateInts(MemoryReader reader, nint candidate)
{
    var parts = new List<string>();
    for (var off = 0; off <= 0x90; off += 4)
    {
        if (!reader.TryReadStruct<int>(candidate + off, out var value)) continue;
        if (value is >= -1 and <= 34)
            parts.Add($"+0x{off:X}={value}");
    }
    return parts.Count == 0 ? "<none>" : string.Join(" ", parts.Take(36));
}

static int RunRitualShop(ProcessHandle process, MemoryReader reader)
{
    var slot = FindGameStateSlot(process, reader);
    if (slot == 0)
    {
        Console.Error.WriteLine("Could not lock GameState slot (in game?).");
        return 1;
    }

    var live = new Poe2Live(reader, slot);
    if (!live.TryResolve(out var inGameState, out _, out _))
    {
        Console.Error.WriteLine("Could not resolve InGameState.");
        return 1;
    }

    var (winW, winH) = GetClientSize(process);
    var rewards = live.ReadRitualRewards(inGameState, winW, winH);
    Console.WriteLine();
    Console.WriteLine("Ritual shop reward probe");
    Console.WriteLine("------------------------");
    Console.WriteLine($"Window       : {winW:0}x{winH:0}");
    Console.WriteLine($"Rewards      : {rewards.Count}");
    Console.WriteLine("Goal         : validate Sikaka-style UiElement tile +0x4F8 reward item reads.\n");
    foreach (var reward in rewards)
    {
        Console.WriteLine($"  {reward.Rarity,-10} {(reward.Identified ? "id" : "unid"),4} art={reward.Art ?? "-",-24} name='{reward.Name ?? "-"}' rect=({reward.X:0},{reward.Y:0} {reward.W:0}x{reward.H:0})");
    }
    if (rewards.Count == 0)
        Console.WriteLine("  none: open the Ritual tribute shop before running this probe.");
    return 0;
}

static int RunAtlasMapName(ProcessHandle process, MemoryReader reader, int maxDistinct)
{
    var (_, inGameState, _, _) = ResolveChain(process, reader);
    if (inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    if (uiRoot == 0)
    {
        Console.Error.WriteLine("Could not resolve UiRoot.");
        return 1;
    }

    var root = SafePtr(reader, uiRoot + Poe2.UiElement.Parent);
    if (root == 0) root = uiRoot;

    Console.WriteLine();
    Console.WriteLine("Atlas map-name probe");
    Console.WriteLine("--------------------");
    Console.WriteLine("Goal: verify localized WorldAreas+0x08 map names from Atlas nodes. Open Atlas before running.\n");

    var queue = new Queue<nint>();
    var visited = new HashSet<nint>();
    var seenCodes = new HashSet<string>(StringComparer.Ordinal);
    queue.Enqueue(root);
    var shown = 0;

    while (queue.Count > 0 && visited.Count < 200000 && shown < maxDistinct)
    {
        var element = queue.Dequeue();
        if (element == 0 || !visited.Add(element)) continue;
        if (SafePtr(reader, element + Poe2.UiElement.Self) != element) continue;

        foreach (var child in ReadUiChildren(reader, element, 16384))
            queue.Enqueue(child);

        var row = SafePtr(reader, element + Poe2.AtlasNode.MapNodeId);
        if (row == 0) continue;

        var areaRow = SafePtr(reader, row);
        var codePtr = areaRow == 0 ? 0 : SafePtr(reader, areaRow);
        var namePtr = areaRow == 0 ? 0 : SafePtr(reader, areaRow + Poe2.AtlasMapRow.WorldAreaName);
        var code = codePtr == 0 ? "" : reader.ReadStringUtf16(codePtr, 80);
        if (!code.StartsWith("Map", StringComparison.Ordinal) || !seenCodes.Add(code)) continue;

        var displayName = namePtr == 0 ? "" : reader.ReadStringUtf16(namePtr, 80);
        shown++;
        Console.WriteLine($"  element=0x{element:X16} row=0x{row:X16} areaRow=0x{areaRow:X16}");
        Console.WriteLine($"    code='{code}' resolved='{displayName}' prettified='{PrettifyMapCode(code)}'");
    }

    Console.WriteLine($"\nDisplayed {shown} distinct map node(s).");
    return 0;
}

static int RunAtlasGraph(ProcessHandle process, MemoryReader reader)
{
    var (_, inGameState, _, _) = ResolveChain(process, reader);
    if (inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var atlas = new Poe2Atlas(reader);
    var nodes = atlas.ReadNodes(inGameState);
    var current = atlas.CurrentNodeGrid();
    var graphNodes = atlas.GraphNodeCount;

    Console.WriteLine();
    Console.WriteLine("Atlas graph probe");
    Console.WriteLine("-----------------");
    Console.WriteLine("Goal: validate Sikaka-style live Atlas graph/current-marker path data. Open Atlas before running.\n");
    Console.WriteLine($"Panel open    : {atlas.LastPanelOpen}");
    Console.WriteLine($"Load status   : {atlas.LoadStatus}");
    Console.WriteLine($"Load progress : {atlas.LoadProgress:P0}");
    Console.WriteLine($"Nodes read    : {nodes.Count}");
    Console.WriteLine($"Graph nodes   : {graphNodes}");
    Console.WriteLine($"Current grid  : {(current.HasValue ? $"{current.Value.X},{current.Value.Y}" : "-")}");

    if (nodes.Count == 0)
    {
        Console.WriteLine("\nNo nodes read. Open the Atlas map and rerun.");
        return 0;
    }

    var visible = nodes.Count(n => n.Visible);
    var unlocked = nodes.Count(n => n.Unlocked);
    var visited = nodes.Count(n => n.Visited);
    var content = nodes.Count(n => n.HasContent);
    Console.WriteLine($"Visible       : {visible}");
    Console.WriteLine($"Unlocked      : {unlocked}");
    Console.WriteLine($"Visited       : {visited}");
    Console.WriteLine($"Content       : {content}");

    var start = current ?? nodes.FirstOrDefault(n => atlas.GraphHas(n.Grid)).Grid;
    var target = nodes
        .Where(n => n.Grid != start && atlas.GraphHas(n.Grid))
        .OrderByDescending(n => n.HasContent)
        .ThenBy(n => n.Visited)
        .FirstOrDefault();

    if (!atlas.GraphHas(start))
    {
        Console.WriteLine("\nCurrent/start node is not in the graph. This usually means the graph vector was not detected.");
        return 0;
    }

    if (target.Element == 0)
    {
        Console.WriteLine("\nNo separate graph-backed target node found for path test.");
        return 0;
    }

    var path = atlas.FindPath(start, target.Grid);
    Console.WriteLine();
    Console.WriteLine("Sample route");
    Console.WriteLine("------------");
    Console.WriteLine($"Start : {start.X},{start.Y}");
    Console.WriteLine($"Target: {target.GridX},{target.GridY} '{target.MapName}' tags=[{string.Join(", ", target.Tags)}]");
    Console.WriteLine(path is null
        ? "Path   : none"
        : $"Path   : {path.Count} node(s) {string.Join(" -> ", path.Take(12).Select(p => $"{p.X},{p.Y}"))}{(path.Count > 12 ? " -> ..." : "")}");
    return 0;
}

static int RunAtlasCurrent(ProcessHandle process, MemoryReader reader)
{
    var (_, inGameState, areaInstance, _) = ResolveChain(process, reader);
    if (inGameState == 0)
    {
        Console.Error.WriteLine("Could not resolve chain (in game?).");
        return 1;
    }

    var live = new Poe2Live(reader, 0);
    var atlas = new Poe2Atlas(reader);
    var nodes = atlas.ReadNodes(inGameState);
    var current = atlas.CurrentNodeGrid();
    var currentNode = current.HasValue
        ? nodes.FirstOrDefault(n => n.Grid == current.Value)
        : default;

    Console.WriteLine();
    Console.WriteLine("Atlas current-marker probe");
    Console.WriteLine("--------------------------");
    Console.WriteLine("Goal: validate the structural current-location marker used for atlas routing.\n");
    Console.WriteLine($"Area code     : {live.AreaCode(areaInstance)}");
    Console.WriteLine($"Panel open    : {atlas.LastPanelOpen}");
    Console.WriteLine($"Nodes read    : {nodes.Count}");
    Console.WriteLine($"Graph nodes   : {atlas.GraphNodeCount}");
    Console.WriteLine($"Current grid  : {(current.HasValue ? $"{current.Value.X},{current.Value.Y}" : "-")}");

    if (currentNode.Element != 0)
    {
        Console.WriteLine($"Current node  : 0x{currentNode.Element:X16}");
        Console.WriteLine($"Map name      : {currentNode.MapName}");
        Console.WriteLine($"Map source    : {currentNode.MapSource}");
        Console.WriteLine($"State/flags   : state={currentNode.State} flags=0x{currentNode.Flags:X2} completion={currentNode.Completion}");
        Console.WriteLine($"Tags          : [{string.Join(", ", currentNode.Tags)}]");
    }
    else if (current.HasValue)
    {
        Console.WriteLine("Current marker resolved a grid, but that grid was not present in the current node snapshot.");
    }
    else
    {
        Console.WriteLine("Current marker not resolved. Open Atlas and make sure the player/current-map marker is visible.");
    }

    return 0;
}

static int RunAtlasMarker(ProcessHandle process, MemoryReader reader) => RunAtlasCurrent(process, reader);

static (float Width, float Height) GetClientSize(ProcessHandle process)
{
    _ = process;
    var hwnd = Win.GetForegroundWindow();
    if (hwnd != 0 && Win.GetClientRect(hwnd, out var rect) && rect.right > 0 && rect.bottom > 0)
        return (rect.right, rect.bottom);
    return (1920, 1080);
}

static string PrettifyMapCode(string code)
{
    if (string.IsNullOrWhiteSpace(code)) return "";
    var value = code.StartsWith("Map", StringComparison.Ordinal) ? code[3..] : code;
    if (value.Length == 0) return code;

    var output = new System.Text.StringBuilder(value.Length + 8);
    for (var i = 0; i < value.Length; i++)
    {
        var c = value[i];
        if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]))
            output.Append(' ');
        output.Append(c);
    }
    return output.ToString().Replace('_', ' ').Trim();
}

static IReadOnlyList<RuneforgeFingerprintBlock> BuildRuneforgeFingerprintBlocks(
    MemoryReader reader,
    IReadOnlyList<RuneforgeComponentMemorySnapshot> components,
    int objectWindow,
    int pointerDepth)
{
    var blocks = new List<RuneforgeFingerprintBlock>();
    var seen = new HashSet<nint>();
    foreach (var component in components)
    {
        blocks.Add(new RuneforgeFingerprintBlock(
            component.Source,
            component.EntityId,
            component.ComponentName,
            component.ComponentIndex,
            component.Component,
            "component",
            component.Body));

        if (pointerDepth <= 0) continue;
        foreach (var pointerBlock in CaptureReachablePointerBlocks(
                     reader,
                     component.Source,
                     component.EntityId,
                     component.ComponentName,
                     component.ComponentIndex,
                     component.Body,
                     component.Component,
                     objectWindow,
                     pointerDepth,
                     seen,
                     $"component[{component.ComponentName}]"))
            blocks.Add(pointerBlock);
    }

    return blocks;
}

static IEnumerable<RuneforgeFingerprintBlock> CaptureReachablePointerBlocks(
    MemoryReader reader,
    string source,
    uint entityId,
    string componentName,
    int componentIndex,
    byte[] ownerBody,
    nint ownerBase,
    int objectWindow,
    int depth,
    HashSet<nint> seen,
    string path)
{
    if (depth <= 0) yield break;

    var emitted = 0;
    for (var offset = 0; offset + IntPtr.Size <= ownerBody.Length && emitted < 64; offset += 8)
    {
        var pointer = IntPtr.Size == 8
            ? (nint)BitConverter.ToInt64(ownerBody, offset)
            : (nint)BitConverter.ToInt32(ownerBody, offset);
        if (!IsPlausiblePointer(pointer) || !seen.Add(pointer)) continue;

        var body = CaptureMemoryBytes(reader, pointer, objectWindow);
        if (body.Length < 0x20) continue;

        emitted++;
        var childPath = $"{path}+0x{offset:X3}->0x{pointer:X16}";
        yield return new RuneforgeFingerprintBlock(source, entityId, componentName, componentIndex, pointer, childPath, body);

        if (depth <= 1) continue;
        foreach (var child in CaptureReachablePointerBlocks(
                     reader,
                     source,
                     entityId,
                     componentName,
                     componentIndex,
                     body,
                     pointer,
                     objectWindow,
                     depth - 1,
                     seen,
                     childPath))
            yield return child;
    }
}

static IEnumerable<RuneforgeFingerprintCandidate> FindRawIntFingerprintCandidates(
    RuneforgeFingerprintBlock block,
    IReadOnlyList<int> indexes)
{
    var hits = FindIntHits(block.Body, indexes).ToArray();
    foreach (var group in hits.GroupBy(h => h.Offset & ~0x3F))
    {
        var matched = group.Select(h => h.Index).Distinct().OrderBy(i => i).ToArray();
        if (matched.Length == 0) continue;
        yield return new RuneforgeFingerprintCandidate(
            "int32-window",
            block,
            group.Key,
            matched,
            group.Select(FormatSelectionIndexHit).Take(12).ToArray(),
            ScoreFingerprint(block, "int32-window", matched.Length));
    }
}

static IEnumerable<RuneforgeFingerprintCandidate> FindPackedUShortFingerprintCandidates(
    RuneforgeFingerprintBlock block,
    IReadOnlyList<int> indexes)
{
    var set = indexes.Where(i => i is > 1 and <= ushort.MaxValue).ToHashSet();
    if (set.Count == 0) yield break;
    var hits = new List<RuneforgeIndexHit>();
    for (var offset = 0; offset + 2 <= block.Body.Length; offset += 2)
    {
        var value = BitConverter.ToUInt16(block.Body, offset);
        if (set.Contains(value))
            hits.Add(new RuneforgeIndexHit(offset, value));
    }

    foreach (var group in hits.GroupBy(h => h.Offset & ~0x3F))
    {
        var matched = group.Select(h => h.Index).Distinct().OrderBy(i => i).ToArray();
        if (matched.Length == 0) continue;
        yield return new RuneforgeFingerprintCandidate(
            "ushort-window",
            block,
            group.Key,
            matched,
            group.Take(12).Select(h => $"+0x{h.Offset:X3}=u16:{h.Index}").ToArray(),
            ScoreFingerprint(block, "ushort-window", matched.Length));
    }
}

static IEnumerable<RuneforgeFingerprintCandidate> FindPackedByteFingerprintCandidates(
    RuneforgeFingerprintBlock block,
    IReadOnlyList<int> indexes)
{
    var set = indexes.Where(i => i is > 1 and <= byte.MaxValue).ToHashSet();
    if (set.Count == 0) yield break;
    var hits = new List<RuneforgeIndexHit>();
    for (var offset = 0; offset < block.Body.Length; offset++)
    {
        var value = block.Body[offset];
        if (set.Contains(value))
            hits.Add(new RuneforgeIndexHit(offset, value));
    }

    foreach (var group in hits.GroupBy(h => h.Offset & ~0x3F))
    {
        var matched = group.Select(h => h.Index).Distinct().OrderBy(i => i).ToArray();
        if (matched.Length == 0) continue;
        yield return new RuneforgeFingerprintCandidate(
            "byte-window",
            block,
            group.Key,
            matched,
            group.Take(12).Select(h => $"+0x{h.Offset:X3}=u8:{h.Index}").ToArray(),
            ScoreFingerprint(block, "byte-window", matched.Length) - 8);
    }
}

static IEnumerable<RuneforgeFingerprintCandidate> FindBitsetFingerprintCandidates(
    RuneforgeFingerprintBlock block,
    IReadOnlyList<int> indexes)
{
    var useful = indexes.Where(i => i is > 1 and < 2048).Distinct().OrderBy(i => i).ToArray();
    if (useful.Length == 0) yield break;
    var maxWord = useful.Max() / 32;
    var bytesRequired = (maxWord + 1) * 4;
    if (block.Body.Length < bytesRequired) yield break;

    var minHits = Math.Min(2, useful.Length);
    for (var baseOffset = 0; baseOffset + bytesRequired <= block.Body.Length; baseOffset += 4)
    {
        var matched = new List<int>();
        foreach (var index in useful)
        {
            var wordOffset = baseOffset + (index / 32) * 4;
            var word = BitConverter.ToUInt32(block.Body, wordOffset);
            var mask = 1u << (index & 31);
            if ((word & mask) != 0)
                matched.Add(index);
        }

        if (matched.Count < minHits) continue;

        var selectedWords = useful.Select(i => i / 32).Distinct().ToArray();
        var setBits = 0;
        foreach (var wordIndex in selectedWords)
        {
            var word = BitConverter.ToUInt32(block.Body, baseOffset + wordIndex * 4);
            setBits += System.Numerics.BitOperations.PopCount(word);
        }

        if (setBits > Math.Max(16, matched.Count * 8)) continue;
        yield return new RuneforgeFingerprintCandidate(
            "bitset32",
            block,
            baseOffset,
            matched.OrderBy(i => i).ToArray(),
            selectedWords
                .Take(8)
                .Select(w => $"+0x{baseOffset + w * 4:X3}=0x{BitConverter.ToUInt32(block.Body, baseOffset + w * 4):X8}")
                .ToArray(),
            ScoreFingerprint(block, "bitset32", matched.Count) + 6);
    }
}

static int ScoreFingerprint(RuneforgeFingerprintBlock block, string kind, int matchedCount)
{
    var score = matchedCount * 20;
    if (block.Path == "component") score += 15;
    if (block.ComponentName.Contains("Interaction", StringComparison.OrdinalIgnoreCase)) score += 10;
    if (block.ComponentName.Contains("Stats", StringComparison.OrdinalIgnoreCase)) score += 4;
    if (kind.StartsWith("bitset", StringComparison.Ordinal)) score += 4;
    if (matchedCount == 1) score -= 16;
    if (block.Path.Length > 160) score -= 6;
    return score;
}

static void PrintFingerprintCandidate(
    RuneforgeFingerprintCandidate candidate,
    IReadOnlyDictionary<int, string> anchorByIndex)
{
    var names = candidate.MatchedIndexes
        .Take(8)
        .Select(i => anchorByIndex.TryGetValue(i, out var text) ? $"{i}:{text}" : i.ToString())
        .ToArray();
    Console.WriteLine(
        $"  score={candidate.Score,3} kind={candidate.Kind,-13} matched={candidate.MatchedIndexes.Count,2} " +
        $"{candidate.Block.Source,-8} id={candidate.Block.EntityId,-8} {candidate.Block.ComponentName,-18} " +
        $"idx={candidate.Block.ComponentIndex,2} off=+0x{candidate.Offset:X3}");
    Console.WriteLine($"    path={candidate.Block.Path}");
    Console.WriteLine($"    rows=[{string.Join(" | ", names)}]");
    Console.WriteLine($"    hits=[{string.Join(", ", candidate.Hits)}]");
}

static IEnumerable<(string Source, uint Id, nint Entity, string Metadata, System.Numerics.Vector2? Grid, float? Distance)> EnumerateRuneforgeSourceEntities(
    MemoryReader reader,
    nint areaInstance,
    System.Numerics.Vector2? playerGrid)
{
    foreach (var (source, mapOffset) in new[] { ("sleeping", Poe2.AreaInstance.SleepingEntities), ("awake", Poe2.AreaInstance.AwakeEntities) })
    {
        foreach (var (id, entity, metadata) in EnumerateEntityMap(reader, areaInstance, mapOffset))
        {
            if (!metadata.Contains("Expedition2", StringComparison.OrdinalIgnoreCase) &&
                !metadata.Contains("LeagueExpeditionNew", StringComparison.OrdinalIgnoreCase) &&
                !metadata.Contains("RuneEncounter", StringComparison.OrdinalIgnoreCase) &&
                !metadata.Contains("Runeforge", StringComparison.OrdinalIgnoreCase) &&
                !metadata.Contains("Runeshape", StringComparison.OrdinalIgnoreCase))
                continue;

            var grid = ReadEntityGrid(reader, entity);
            var distance = playerGrid.HasValue && grid.HasValue
                ? System.Numerics.Vector2.Distance(playerGrid.Value, grid.Value)
                : (float?)null;
            yield return (source, id, entity, metadata, grid, distance);
        }
    }
}

static RuneforgePopulateState CaptureRuneforgePopulateState(MemoryReader reader, nint catalog, int maxChildren)
{
    var flags = ReadUiFlags(reader, catalog);
    var childFirst = SafePtr(reader, catalog + Poe2.UiElement.Children);
    reader.TryReadStruct<nint>(catalog + Poe2.UiElement.ChildrenEnd, out var childLast);
    reader.TryReadStruct<nint>(catalog + Poe2.UiElement.ChildrenEnd + 8, out var childCapacity);
    reader.TryReadStruct<uint>(catalog + 0x2E8, out var field2E8);
    reader.TryReadStruct<uint>(catalog + 0x2F0, out var field2F0);
    reader.TryReadStruct<nint>(catalog + 0x48, out var mirrorFirst);
    reader.TryReadStruct<nint>(catalog + 0x50, out var mirrorLast);
    reader.TryReadStruct<nint>(catalog + 0x58, out var mirrorCapacity);
    reader.TryReadStruct<nint>(catalog + 0x318, out var tabsFirst);
    reader.TryReadStruct<nint>(catalog + 0x320, out var tabsLast);
    reader.TryReadStruct<nint>(catalog + 0x328, out var tabsCapacity);

    var childCount = CountPointerVector(childFirst, childLast, maxChildren * 8L);
    if (childCount < 0) childCount = 0;
    var capacity = CountPointerVector(childFirst, childCapacity, maxChildren * 16L);
    if (capacity < 0) capacity = 0;
    var mirrorCount = CountPointerVector(mirrorFirst, mirrorLast, maxChildren * 8L);
    if (mirrorCount < 0) mirrorCount = 0;
    var tabsCount = CountPointerVector(tabsFirst, tabsLast, 128 * 8L);
    if (tabsCount < 0) tabsCount = 0;

    var visibleRows = 0;
    if (TryReadUiChildrenVector(reader, catalog, maxChildren, out var first, out var count))
    {
        for (long i = 0; i < count; i++)
        {
            var row = SafePtr(reader, first + (nint)(i * 8));
            if (row == 0 || !ReadUiVisible(reader, row)) continue;
            visibleRows++;
        }
    }

    return new RuneforgePopulateState(flags, childLast, childCount, capacity, visibleRows, mirrorLast, mirrorCount, tabsCount, field2E8, field2F0);
}

static void PrintRuneforgePopulateState(long elapsedMs, RuneforgePopulateState state)
{
    Console.WriteLine(
        $"  {elapsedMs,6}  {state.ChildCount,4} {state.Capacity,5} {state.VisibleRows,11}  " +
        $"0x{state.ChildLast:X16}  {state.MirrorCount,10} 0x{state.MirrorLast:X16} {state.TabsCount,4} " +
        $"{state.Field2E8,4} {state.Field2F0,4} 0x{state.Flags:X8}");
}

static void PrintRuneforgePopulateStateHeader()
{
    Console.WriteLine("  ms       rows  cap   visibleRows  vecLast              mirrorRows  mirrorLast           tabs +2E8 +2F0 flags");
}

static bool TryResolveRuneforgeCatalogSlot(
    MemoryReader reader,
    nint uiRoot,
    int maxChildren,
    int maxBranches,
    out nint parent,
    out nint catalog,
    out nint bonus,
    out IReadOnlyList<RuneforgeUiTraceNode> parentPath,
    bool requireVisibleGate = false)
{
    parent = 0;
    catalog = 0;
    bonus = 0;
    parentPath = TraceRuneforgeUiParentPath(reader, uiRoot, requireVisibleGate, maxChildren, maxBranches);
    if (parentPath.Count < Poe2.Runeforge.PanelFlagFingerprints.Length - 1)
        return false;

    parent = parentPath[^1].Address;
    catalog = GetUiChildAt(reader, parent, 0, maxChildren);
    bonus = GetUiChildAt(reader, parent, 1, maxChildren);
    return catalog != 0;
}

static IReadOnlyList<RuneforgeUiTraceNode> TraceRuneforgeUiParentPath(
    MemoryReader reader,
    nint uiRoot,
    bool requireVisibleGate,
    int maxChildren,
    int maxBranches)
{
    var path = new List<RuneforgeUiTraceNode>();
    var visitedBranches = 0;
    Trace(uiRoot, 0);
    return path;

    bool Trace(nint parent, int step)
    {
        if (step == Poe2.Runeforge.PanelFlagFingerprints.Length - 1)
            return true;

        foreach (var candidate in EnumerateRuneforgeStepCandidates(reader, parent, step, requireVisibleGate, maxChildren))
        {
            if (++visitedBranches > maxBranches) return false;
            path.Add(candidate);
            if (Trace(candidate.Address, step + 1)) return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }
}

static RuneforgeUiChildSnapshot CaptureRuneforgeUiChildSnapshot(
    MemoryReader reader,
    string label,
    nint element,
    int maxChildren,
    int fieldWindow)
{
    var body = new byte[fieldWindow];
    var read = element == 0 ? 0 : reader.TryReadBytes(element, body);
    if (read < body.Length)
        Array.Resize(ref body, Math.Max(0, read));

    var flags = element == 0 ? 0 : ReadUiFlags(reader, element);
    var childFirst = element == 0 ? 0 : SafePtr(reader, element + Poe2.UiElement.Children);
    reader.TryReadStruct<nint>(element + Poe2.UiElement.ChildrenEnd, out var childLast);
    reader.TryReadStruct<nint>(element + Poe2.UiElement.ChildrenEnd + 8, out var childCapacity);
    var childCount = CountPointerVector(childFirst, childLast, maxChildren * 8L);
    var childCapacityCount = CountPointerVector(childFirst, childCapacity, maxChildren * 16L);

    var rows = new List<string>();
    if (TryReadUiChildrenVector(reader, element, maxChildren, out var first, out var count))
    {
        for (long i = 0; i < count; i++)
        {
            var row = SafePtr(reader, first + (nint)(i * 8));
            if (row == 0 || !ReadUiVisible(reader, row)) continue;
            var labelChild = GetUiChildAt(reader, row, 0, maxChildren);
            var text = labelChild == 0 ? "" : ReadStdWString(reader, labelChild + Poe2.Runeforge.NameWString);
            if (!string.IsNullOrWhiteSpace(text))
                rows.Add($"{i}:{text}");
        }
    }

    return new RuneforgeUiChildSnapshot(
        label,
        element,
        flags,
        (flags & (1u << Poe2.UiElement.FlagVisibleBit)) != 0,
        childFirst,
        childLast,
        childCapacity,
        childCount,
        childCapacityCount,
        body,
        CaptureVectorFieldsFromBody(body, maxChildren),
        rows);
}

static IReadOnlyList<RuneforgeVectorField> CaptureVectorFieldsFromBody(byte[] body, int maxChildren)
{
    var result = new List<RuneforgeVectorField>();
    for (var offset = 0; offset + 24 <= body.Length; offset += 8)
    {
        var first = (nint)BitConverter.ToInt64(body, offset);
        var last = (nint)BitConverter.ToInt64(body, offset + 8);
        var end = (nint)BitConverter.ToInt64(body, offset + 16);
        var count = CountPointerVector(first, last, maxChildren * 8L);
        var capacity = CountPointerVector(first, end, maxChildren * 16L);
        if (count <= 0 || capacity < count) continue;
        if (!LooksLikeUserPointer(first) || !LooksLikeUserPointer(last) || !LooksLikeUserPointer(end)) continue;
        result.Add(new RuneforgeVectorField(offset, first, last, end, count, capacity));
    }
    return result;
}

static void PrintRuneforgeUiChildSnapshot(RuneforgeUiChildSnapshot snapshot)
{
    Console.WriteLine(
        $"{snapshot.Label,-7} catalog=0x{snapshot.Address:X16} flags=0x{snapshot.Flags:X8} visible={snapshot.Visible} " +
        $"children={snapshot.ChildCount} capacity={snapshot.ChildCapacityCount} " +
        $"vec=[0x{snapshot.ChildFirst:X16}..0x{snapshot.ChildLast:X16}..0x{snapshot.ChildCapacity:X16}]");
    if (snapshot.VisibleRows.Count > 0)
        foreach (var row in snapshot.VisibleRows.Take(12))
            Console.WriteLine($"  visible row: {row}");
    else
        Console.WriteLine("  visible row: none");

    foreach (var vector in snapshot.VectorFields.Take(12))
        Console.WriteLine(
            $"  vector +0x{vector.Offset:X3}: count={vector.Count} capacity={vector.Capacity} " +
            $"0x{vector.First:X16}..0x{vector.Last:X16}..0x{vector.End:X16}");
}

static void PrintRuneforgeUiChildSnapshotDiff(RuneforgeUiChildSnapshot before, RuneforgeUiChildSnapshot after)
{
    Console.WriteLine("Catalog child[0] diff");
    Console.WriteLine("---------------------");
    Console.WriteLine($"address       : 0x{before.Address:X16} -> 0x{after.Address:X16}");
    Console.WriteLine($"flags         : 0x{before.Flags:X8} -> 0x{after.Flags:X8}");
    Console.WriteLine($"child count   : {before.ChildCount} -> {after.ChildCount}");
    Console.WriteLine($"child vector  : 0x{before.ChildFirst:X16}/0x{before.ChildLast:X16}/0x{before.ChildCapacity:X16}");
    Console.WriteLine($"             -> 0x{after.ChildFirst:X16}/0x{after.ChildLast:X16}/0x{after.ChildCapacity:X16}");

    Console.WriteLine();
    Console.WriteLine("Changed vector-like fields:");
    var beforeVectors = before.VectorFields.ToDictionary(v => v.Offset);
    var afterVectors = after.VectorFields.ToDictionary(v => v.Offset);
    foreach (var offset in beforeVectors.Keys.Union(afterVectors.Keys).OrderBy(x => x).Take(80))
    {
        beforeVectors.TryGetValue(offset, out var b);
        afterVectors.TryGetValue(offset, out var a);
        if (b.Equals(a)) continue;
        Console.WriteLine(
            $"  +0x{offset:X3}: {FormatVectorField(b)} -> {FormatVectorField(a)}");
    }

    Console.WriteLine();
    Console.WriteLine("Changed uint fields:");
    var uintChanges = 0;
    var min = Math.Min(before.Body.Length, after.Body.Length);
    for (var offset = 0; offset + 4 <= min && uintChanges < 80; offset += 4)
    {
        var b = BitConverter.ToUInt32(before.Body, offset);
        var a = BitConverter.ToUInt32(after.Body, offset);
        if (b == a) continue;
        Console.WriteLine($"  +0x{offset:X3}: 0x{b:X8} ({b}) -> 0x{a:X8} ({a})");
        uintChanges++;
    }

    Console.WriteLine();
    Console.WriteLine("Visible reward rows after populate:");
    foreach (var row in after.VisibleRows.Take(32))
        Console.WriteLine($"  {row}");
}

static string FormatVectorField(RuneforgeVectorField field)
{
    if (field.Offset == 0 && field.First == 0 && field.Last == 0 && field.End == 0)
        return "<none>";
    return $"count={field.Count} cap={field.Capacity} 0x{field.First:X16}/0x{field.Last:X16}/0x{field.End:X16}";
}

static long CountPointerVector(nint first, nint last, long maxBytes)
{
    var bytes = (long)last - (long)first;
    if (first == 0 || last == 0 || bytes < 0 || bytes > maxBytes || bytes % 8 != 0)
        return -1;
    return bytes / 8;
}

static bool LooksLikeUserPointer(nint value)
{
    var u = (ulong)value;
    return u >= 0x10000 && u <= 0x7FFFFFFFFFFF;
}

static nint OpenWriteHandle(int processId)
{
    const uint processVmWrite = 0x0020;
    const uint processVmOperation = 0x0008;
    const uint processQueryLimitedInformation = 0x1000;
    return OpenProcess(processVmWrite | processVmOperation | processQueryLimitedInformation, false, (uint)processId);
}

static bool WriteUInt32(nint processHandle, nint address, uint value)
{
    var bytes = BitConverter.GetBytes(value);
    if (!VirtualProtectEx(processHandle, address, (nuint)bytes.Length, 0x40, out var oldProtect))
        return false;

    var ok = WriteProcessMemory(processHandle, address, bytes, (nuint)bytes.Length, out var written) &&
             written == (nuint)bytes.Length;
    VirtualProtectEx(processHandle, address, (nuint)bytes.Length, oldProtect, out _);
    return ok;
}

[DllImport("kernel32.dll", SetLastError = true)]
static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool CloseHandle(nint hObject);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool VirtualProtectEx(nint hProcess, nint lpAddress, nuint dwSize, uint flNewProtect, out uint lpflOldProtect);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool WriteProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, nuint nSize, out nuint lpNumberOfBytesWritten);

static IReadOnlyList<RuneforgeUiTraceNode> TraceRuneforgeUiPath(
    MemoryReader reader,
    nint uiRoot,
    bool requireVisibleGate,
    int maxChildren,
    int maxBranches)
{
    var path = new List<RuneforgeUiTraceNode>();
    var visitedBranches = 0;
    Trace(uiRoot, 0);
    return path;

    bool Trace(nint parent, int step)
    {
        if (step == Poe2.Runeforge.PanelFlagFingerprints.Length)
            return IsRuneforgeRecipesContainer(reader, parent, maxChildren, out _);

        foreach (var candidate in EnumerateRuneforgeStepCandidates(reader, parent, step, requireVisibleGate, maxChildren))
        {
            if (++visitedBranches > maxBranches) return false;
            path.Add(candidate);
            if (Trace(candidate.Address, step + 1)) return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }
}

static IReadOnlyList<IReadOnlyList<RuneforgeUiTraceNode>> EnumerateRuneforgeUiPaths(
    MemoryReader reader,
    nint uiRoot,
    bool requireVisibleGate,
    int maxChildren,
    int maxPaths)
{
    var paths = new List<IReadOnlyList<RuneforgeUiTraceNode>>();
    var current = new List<RuneforgeUiTraceNode>();
    Walk(uiRoot, 0);
    return paths;

    void Walk(nint parent, int step)
    {
        if (paths.Count >= maxPaths) return;
        if (step == Poe2.Runeforge.PanelFlagFingerprints.Length)
        {
            if (IsRuneforgeRecipesContainer(reader, parent, maxChildren, out _))
                paths.Add(current.ToArray());
            return;
        }

        foreach (var candidate in EnumerateRuneforgeStepCandidates(reader, parent, step, requireVisibleGate, maxChildren))
        {
            current.Add(candidate);
            Walk(candidate.Address, step + 1);
            current.RemoveAt(current.Count - 1);
            if (paths.Count >= maxPaths) return;
        }
    }
}

static void PrintRuneforgeUiPathSet(
    MemoryReader reader,
    IReadOnlyList<IReadOnlyList<RuneforgeUiTraceNode>> paths,
    int maxChildren)
{
    if (paths.Count == 0)
    {
        Console.WriteLine("  none");
        return;
    }

    for (var i = 0; i < paths.Count; i++)
    {
        var path = paths[i];
        if (path.Count == 0) continue;
        var panel = path[^1].Address;
        TrySummarizeRuneforgeRows(reader, panel, maxChildren, out var total, out var visible, out var firstVisible, out var firstAny);
        Console.WriteLine(
            $"  path[{i}] final=0x{panel:X16} rows={total} visibleRows={visible} " +
            $"stepChildren=[{string.Join("/", path.Select(p => p.ChildIndex))}] " +
            $"firstVisible='{firstVisible}' firstAny='{firstAny}'");
        Console.WriteLine($"    finalFlags=0x{path[^1].Flags:X8} finalVisible={path[^1].Visible}");
        PrintRuneforgeRecipeRows(reader, panel, Math.Min(total, 12));
    }
}

static void PrintRuneforgeFinalParentChildren(
    MemoryReader reader,
    IReadOnlyList<IReadOnlyList<RuneforgeUiTraceNode>> paths,
    int maxChildren)
{
    var path = paths.FirstOrDefault(p => p.Count >= 5);
    if (path is null || path.Count < 5)
    {
        Console.WriteLine("  no full path available");
        return;
    }

    var parent = path[^2].Address;
    var finalTarget = Poe2.Runeforge.PanelFlagFingerprints[^1] & ~(1u << Poe2.UiElement.FlagVisibleBit);
    Console.WriteLine($"  parent step {path[^2].Step} addr=0x{parent:X16} childCount={TryGetUiChildCount(reader, parent)} finalTargetNoVisible=0x{finalTarget:X8}");
    if (!TryReadUiChildrenVector(reader, parent, maxChildren, out var first, out var count))
    {
        Console.WriteLine("  could not read final parent children");
        return;
    }

    for (long i = 0; i < count && i < 16; i++)
    {
        var child = SafePtr(reader, first + (nint)(i * 8));
        if (child == 0)
        {
            Console.WriteLine($"    child[{i}] null");
            continue;
        }

        var flags = ReadUiFlags(reader, child);
        var matchesFinal = (flags & ~(1u << Poe2.UiElement.FlagVisibleBit)) == finalTarget;
        IsRuneforgeRecipesContainer(reader, child, maxChildren, out var rowCount);
        TrySummarizeRuneforgeRows(reader, child, maxChildren, out var total, out var visible, out var firstVisible, out var firstAny);
        Console.WriteLine(
            $"    child[{i}] addr=0x{child:X16} flags=0x{flags:X8} visible={ReadUiVisible(reader, child)} " +
            $"matchFinal={matchesFinal} children={TryGetUiChildCount(reader, child)} rows={total} visibleRows={visible} " +
            $"recipeRows={rowCount} firstVisible='{firstVisible}' firstAny='{firstAny}'");
        if (total > 0)
            PrintRuneforgeRecipeRows(reader, child, 8);
    }
}

static void TrySummarizeRuneforgeRows(
    MemoryReader reader,
    nint panel,
    int maxChildren,
    out int total,
    out int visible,
    out string firstVisible,
    out string firstAny)
{
    total = 0;
    visible = 0;
    firstVisible = "";
    firstAny = "";
    if (!TryReadUiChildrenVector(reader, panel, maxChildren, out var first, out var count)) return;

    total = (int)count;
    for (long i = 0; i < count; i++)
    {
        var row = SafePtr(reader, first + (nint)(i * 8));
        if (row == 0) continue;
        var label = GetUiChildAt(reader, row, 0, maxChildren);
        var text = label == 0 ? "" : ReadStdWString(reader, label + Poe2.Runeforge.NameWString);
        if (string.IsNullOrWhiteSpace(text)) continue;
        firstAny = firstAny.Length == 0 ? text : firstAny;
        if (!ReadUiVisible(reader, row)) continue;
        visible++;
        firstVisible = firstVisible.Length == 0 ? text : firstVisible;
    }
}

static IEnumerable<RuneforgeUiTraceNode> EnumerateRuneforgeStepCandidates(
    MemoryReader reader,
    nint parent,
    int step,
    bool requireVisibleGate,
    int maxChildren)
{
    if (!TryReadUiChildrenVector(reader, parent, maxChildren, out var first, out var count))
        yield break;

    var visibleMask = 1u << Poe2.UiElement.FlagVisibleBit;
    var target = Poe2.Runeforge.PanelFlagFingerprints[step] & ~visibleMask;
    for (var pass = 0; pass < 2; pass++)
    {
        var wantVisible = pass == 0;
        for (long i = 0; i < count; i++)
        {
            var child = SafePtr(reader, first + (nint)(i * 8));
            if (child == 0 ||
                !reader.TryReadStruct<uint>(child + Poe2.UiElement.Flags, out var flags) ||
                (flags & ~visibleMask) != target)
                continue;

            var visible = (flags & visibleMask) != 0;
            if (visible != wantVisible) continue;
            if (requireVisibleGate && step == Poe2.Runeforge.GateStep && !visible) continue;
            IsRuneforgeRecipesContainer(reader, child, maxChildren, out var rowCount);
            yield return new RuneforgeUiTraceNode(
                step,
                (int)i,
                child,
                flags,
                visible,
                TryGetUiChildCount(reader, child),
                rowCount);
        }
    }
}

static void PrintRuneforgeUiTrace(MemoryReader reader, IReadOnlyList<RuneforgeUiTraceNode> path)
{
    if (path.Count == 0)
    {
        Console.WriteLine("  no complete path to recipes container");
        return;
    }

    for (var i = 0; i < path.Count; i++)
    {
        var node = path[i];
        var visibleBit = 1u << Poe2.UiElement.FlagVisibleBit;
        Console.WriteLine(
            $"  step {node.Step} child[{node.ChildIndex}] addr=0x{node.Address:X16} " +
            $"flags=0x{node.Flags:X8} visible={node.Visible} childCount={node.ChildCount} " +
            $"recipesRows={node.RecipeRowCount}");
        Console.WriteLine(
            $"       candidate write field: 0x{node.Address + Poe2.UiElement.Flags:X16} " +
            $"current=0x{node.Flags:X8} visibleOn=0x{node.Flags | visibleBit:X8} visibleOff=0x{node.Flags & ~visibleBit:X8}");
    }

    var panel = path[^1].Address;
    if (IsRuneforgeRecipesContainer(reader, panel, 4000, out var rows))
    {
        Console.WriteLine($"  final recipes container: 0x{panel:X16}, rows={rows}");
        PrintRuneforgeRecipeRows(reader, panel, Math.Min(rows, 20));
    }
}

static bool IsRuneforgeRecipesContainer(MemoryReader reader, nint element, int maxChildren, out int rowCount)
{
    rowCount = 0;
    if (!TryReadUiChildrenVector(reader, element, maxChildren, out var first, out var count))
        return false;

    var namedRows = 0;
    for (long i = 0; i < count; i++)
    {
        var row = SafePtr(reader, first + (nint)(i * 8));
        if (row == 0) continue;
        var label = GetUiChildAt(reader, row, 0, maxChildren);
        if (label == 0) continue;
        var text = ReadStdWString(reader, label + Poe2.Runeforge.NameWString);
        if (LooksLikeRuneforgeRewardText(text))
            namedRows++;
    }

    rowCount = (int)count;
    return namedRows > 0;
}

static void PrintRuneforgeRecipeRows(MemoryReader reader, nint panel, int limit)
{
    if (!TryReadUiChildrenVector(reader, panel, 4000, out var first, out var count)) return;
    var rows = new List<(long Index, nint Row, uint Flags, bool Visible, string Text)>();
    for (long i = 0; i < count; i++)
    {
        var row = SafePtr(reader, first + (nint)(i * 8));
        if (row == 0) continue;
        var flags = ReadUiFlags(reader, row);
        var label = GetUiChildAt(reader, row, 0, 4000);
        var text = label == 0 ? "" : ReadStdWString(reader, label + Poe2.Runeforge.NameWString);
        if (string.IsNullOrWhiteSpace(text)) continue;
        rows.Add((i, row, flags, ReadUiVisible(reader, row), text));
    }

    foreach (var row in rows
                 .OrderByDescending(r => r.Visible)
                 .ThenBy(r => r.Index)
                 .Take(limit))
        Console.WriteLine($"    row[{row.Index}] row=0x{row.Row:X16} flags=0x{row.Flags:X8} visible={row.Visible} text='{row.Text}'");
}

static bool TryReadUiChildrenVector(MemoryReader reader, nint element, int maxChildren, out nint first, out long count)
{
    first = SafePtr(reader, element + Poe2.UiElement.Children);
    count = 0;
    if (first == 0 ||
        !reader.TryReadStruct<nint>(element + Poe2.UiElement.ChildrenEnd, out var last))
        return false;
    count = ((long)last - (long)first) / 8;
    return count is > 0 && count <= maxChildren;
}

static nint GetUiChildAt(MemoryReader reader, nint element, int index, int maxChildren)
{
    return TryReadUiChildrenVector(reader, element, maxChildren, out var first, out var count) &&
           index >= 0 &&
           index < count
        ? SafePtr(reader, first + (nint)(index * 8))
        : 0;
}

static (string Source, uint Id, nint Entity, string Metadata, System.Numerics.Vector2? Grid, float? Distance)
    FindNearestRuneforgeEncounter(MemoryReader reader, nint areaInstance, nint localPlayer)
{
    var playerGrid = localPlayer == 0 ? null : ReadEntityGrid(reader, localPlayer);
    var matches = new List<(string Source, uint Id, nint Entity, string Metadata, System.Numerics.Vector2? Grid, float? Distance)>();
    foreach (var (source, mapOffset) in new[] { ("sleeping", Poe2.AreaInstance.SleepingEntities), ("awake", Poe2.AreaInstance.AwakeEntities) })
    {
        foreach (var (id, entity, metadata) in EnumerateEntityMap(reader, areaInstance, mapOffset))
        {
            if (!metadata.Contains("Expedition2Encounter", StringComparison.OrdinalIgnoreCase)) continue;
            var grid = ReadEntityGrid(reader, entity);
            var distance = playerGrid.HasValue && grid.HasValue
                ? System.Numerics.Vector2.Distance(playerGrid.Value, grid.Value)
                : (float?)null;
            matches.Add((source, id, entity, metadata, grid, distance));
        }
    }

    return matches
        .OrderBy(m => m.Distance ?? float.MaxValue)
        .FirstOrDefault();
}

static RuneforgeEncounterSnapshot CaptureRuneforgeEncounterSnapshot(MemoryReader reader, nint entity)
{
    var stateMachine = ResolveComponentAddr(reader, entity, "StateMachine");
    var stats = ResolveComponentAddr(reader, entity, "Stats");
    var preload = ResolveComponentAddr(reader, entity, "Preload");
    return new RuneforgeEncounterSnapshot(
        CaptureStateValues(reader, stateMachine),
        CaptureStatsVectors(reader, stats),
        CapturePreloadTexts(reader, preload, 0x280, 0x420));
}

static IReadOnlyList<long> CaptureStateValues(MemoryReader reader, nint stateMachine)
{
    if (stateMachine == 0 ||
        !reader.TryReadStruct<StdVector>(stateMachine + 0x160, out var values) ||
        !TryGetVectorCount(values, sizeof(long), 1, 100, out var count))
        return [];

    var result = new List<long>();
    for (var i = 0; i < Math.Min(count, 16); i++)
        if (reader.TryReadStruct<long>(values.First + i * sizeof(long), out var value))
            result.Add(value);
    return result;
}

static IReadOnlyDictionary<int, IReadOnlyList<nint>> CaptureStatsVectors(MemoryReader reader, nint stats)
{
    var result = new Dictionary<int, IReadOnlyList<nint>>();
    if (stats == 0) return result;
    for (var offset = 0; offset <= 0x1000 - 0x18; offset += 8)
    {
        if (!reader.TryReadStruct<StdVector>(stats + offset, out var vector) ||
            !TryGetVectorByteSize(vector, 8, 0x20000, out var bytes) ||
            bytes % 8 != 0)
            continue;

        var count = bytes / 8;
        if (count is <= 0 or > 8192) continue;
        var items = new List<nint>();
        for (var i = 0; i < Math.Min(count, 64); i++)
        {
            if (!reader.TryReadStruct<nint>(vector.First + i * 8, out var value)) break;
            items.Add(value);
        }
        result[offset] = items;
    }
    return result;
}

static IReadOnlyDictionary<int, string> CapturePreloadTexts(MemoryReader reader, nint preload, int startOffset, int endOffset)
{
    var result = new Dictionary<int, string>();
    if (preload == 0) return result;
    for (var offset = startOffset; offset <= endOffset - 8; offset += 8)
    {
        foreach (var clue in ReadStringCluesAt(reader, preload, offset))
        {
            if (!LooksLikeProbeText(clue.Text)) continue;
            result[offset] = clue.Text;
            break;
        }
    }
    return result;
}

static void PrintRuneforgeSnapshotSummary(string label, RuneforgeEncounterSnapshot snapshot)
{
    Console.WriteLine($"{label,-12}: state=[{string.Join(", ", snapshot.StateValues)}] vectors={snapshot.StatsVectors.Count} preloadTexts={snapshot.PreloadTexts.Count}");
}

static void PrintRuneforgeSnapshotDelta(
    MemoryReader reader,
    RuneforgeEncounterSnapshot before,
    RuneforgeEncounterSnapshot after,
    int objectWindow)
{
    Console.WriteLine();
    Console.WriteLine("Delta");
    Console.WriteLine("-----");
    if (!before.StateValues.SequenceEqual(after.StateValues))
        Console.WriteLine($"State values  : [{string.Join(", ", before.StateValues)}] -> [{string.Join(", ", after.StateValues)}]");
    else
        Console.WriteLine($"State values  : unchanged [{string.Join(", ", after.StateValues)}]");

    foreach (var offset in before.StatsVectors.Keys.Concat(after.StatsVectors.Keys).Distinct().OrderBy(x => x))
    {
        before.StatsVectors.TryGetValue(offset, out var b);
        after.StatsVectors.TryGetValue(offset, out var a);
        b ??= [];
        a ??= [];
        if (b.SequenceEqual(a)) continue;

        Console.WriteLine($"Stats+0x{offset:X3}: count {b.Count} -> {a.Count}");
        var changed = new List<(int Index, nint Before, nint After)>();
        var max = Math.Max(b.Count, a.Count);
        for (var i = 0; i < max && changed.Count < 12; i++)
        {
            var bv = i < b.Count ? b[i] : 0;
            var av = i < a.Count ? a[i] : 0;
            if (bv != av) changed.Add((i, bv, av));
        }

        foreach (var item in changed)
        {
            Console.WriteLine($"  [{item.Index,2}] 0x{item.Before:X16} -> 0x{item.After:X16}");
            if (IsPlausiblePointer(item.After))
                PrintPointedObjectSummary(reader, item.After, "       ", objectWindow);
        }
    }

    foreach (var offset in before.PreloadTexts.Keys.Concat(after.PreloadTexts.Keys).Distinct().OrderBy(x => x))
    {
        before.PreloadTexts.TryGetValue(offset, out var b);
        after.PreloadTexts.TryGetValue(offset, out var a);
        if (StringComparer.Ordinal.Equals(b, a)) continue;
        Console.WriteLine($"Preload+0x{offset:X3}: '{b ?? ""}' -> '{a ?? ""}'");
    }
}

static void PrintAllObjectVectors(
    MemoryReader reader,
    nint component,
    string componentName,
    int scanWindow,
    int entryLimit,
    int objectWindow)
{
    var printed = 0;
    Console.WriteLine($"    {componentName} broad vector scan 0x000..0x{scanWindow:X3}");
    for (var offset = 0; offset <= scanWindow - 0x18 && printed < 24; offset += 8)
    {
        if (!reader.TryReadStruct<StdVector>(component + offset, out var vector) ||
            !TryGetVectorByteSize(vector, 8, 0x20000, out var bytes))
            continue;

        var count8 = bytes / 8;
        if (!VectorHasInterestingEntry(reader, vector, count8))
            continue;

        Console.WriteLine($"      {componentName}+0x{offset:X3}: vector bytes=0x{bytes:X} count8={count8}");
        for (var i = 0; i < count8 && i < entryLimit; i++)
        {
            if (!reader.TryReadStruct<nint>(vector.First + i * 8, out var ptr))
                break;
            Console.WriteLine($"        [{i,2}] qword=0x{ptr:X16}");
            if (IsPlausiblePointer(ptr))
                PrintPointedObjectSummary(reader, ptr, "          ", objectWindow);
        }
        printed++;
    }
}

static bool VectorHasInterestingEntry(MemoryReader reader, StdVector vector, int count8)
{
    for (var i = 0; i < Math.Min(count8, 12); i++)
    {
        if (!reader.TryReadStruct<nint>(vector.First + i * 8, out var ptr)) continue;
        if (!IsPlausiblePointer(ptr)) continue;
        if (!string.IsNullOrWhiteSpace(ReadEntityMetadata(reader, ptr))) return true;
        for (var offset = 0; offset <= 0x180; offset += 8)
        {
            foreach (var clue in ReadStringCluesAt(reader, ptr, offset))
                if (LooksLikeRuneforgeRewardText(clue.Text) ||
                    LooksLikeRuneforgeModText(clue.Text) ||
                    LooksLikeProbeText(clue.Text))
                    return true;
        }
    }
    return false;
}

static void PrintSuspectObjectVectors(
    MemoryReader reader,
    nint component,
    string componentName,
    IReadOnlyList<int> offsets,
    int entryLimit,
    int objectWindow)
{
    foreach (var offset in offsets)
    {
        if (!reader.TryReadStruct<StdVector>(component + offset, out var vector) ||
            !TryGetVectorByteSize(vector, 8, 0x20000, out var bytes))
        {
            Console.WriteLine($"    {componentName}+0x{offset:X3}: no vector");
            continue;
        }

        var count8 = bytes / 8;
        Console.WriteLine($"    {componentName}+0x{offset:X3}: vector bytes=0x{bytes:X} count8={count8}");
        for (var i = 0; i < count8 && i < entryLimit; i++)
        {
            if (!reader.TryReadStruct<nint>(vector.First + i * 8, out var ptr))
                break;
            Console.WriteLine($"      [{i,2}] qword=0x{ptr:X16}");
            if (IsPlausiblePointer(ptr))
                PrintPointedObjectSummary(reader, ptr, "        ", objectWindow);
        }
    }
}

static void PrintPreloadRuneforgeWindow(
    MemoryReader reader,
    nint preload,
    int startOffset,
    int endOffset,
    int entryLimit,
    int objectWindow)
{
    var seenText = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    Console.WriteLine($"    Preload scan +0x{startOffset:X3}..+0x{endOffset:X3}");
    for (var offset = startOffset; offset <= endOffset - 8; offset += 8)
    {
        foreach (var clue in ReadStringCluesAt(reader, preload, offset))
        {
            if (!LooksLikeProbeText(clue.Text) || !seenText.Add(clue.Text)) continue;
            Console.WriteLine($"      TEXT {clue.Kind,-12} +0x{offset:X3} -> {clue.Text}");
        }

        if (!reader.TryReadStruct<StdVector>(preload + offset, out var vector) ||
            !TryGetVectorByteSize(vector, 8, 0x20000, out var bytes))
            continue;

        var count8 = bytes / 8;
        Console.WriteLine($"      VECTOR +0x{offset:X3} bytes=0x{bytes:X} count8={count8}");
        for (var i = 0; i < count8 && i < entryLimit; i++)
        {
            if (!reader.TryReadStruct<nint>(vector.First + i * 8, out var ptr))
                break;
            Console.WriteLine($"        [{i,2}] qword=0x{ptr:X16}");
            if (IsPlausiblePointer(ptr))
                PrintPointedObjectSummary(reader, ptr, "          ", objectWindow);
        }
    }
}

static void PrintPointedObjectSummary(MemoryReader reader, nint address, string indent, int objectWindow)
{
    var metadata = ReadEntityMetadata(reader, address);
    if (!string.IsNullOrWhiteSpace(metadata))
    {
        var components = ReadComponentMap(reader, address);
        Console.WriteLine($"{indent}entity-like metadata='{metadata}' comps=[{string.Join(",", components.Select(c => c.Name))}]");
    }

    PrintSmallIntSummary(reader, address, indent);
    PrintObjectStringSummary(reader, address, indent, objectWindow);
    PrintNestedVectorSummary(reader, address, indent, Math.Min(objectWindow, 0x500));
}

static void PrintSmallIntSummary(MemoryReader reader, nint address, string indent)
{
    var values = new List<string>();
    for (var offset = 0; offset < 0x80; offset += 4)
    {
        if (!reader.TryReadStruct<int>(address + offset, out var value)) continue;
        if (value == 0 || Math.Abs((long)value) > 250000) continue;
        values.Add($"+0x{offset:X2}={value}");
        if (values.Count >= 16) break;
    }

    if (values.Count > 0)
        Console.WriteLine($"{indent}ints [{string.Join(", ", values)}]");
}

static void PrintObjectStringSummary(MemoryReader reader, nint address, string indent, int objectWindow)
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var printed = 0;
    for (var offset = 0; offset <= objectWindow - 8 && printed < 16; offset += 8)
    {
        foreach (var clue in ReadStringCluesAt(reader, address, offset))
        {
            if (!LooksLikeProbeText(clue.Text) || !seen.Add(clue.Text)) continue;
            Console.WriteLine($"{indent}text {clue.Kind,-12} +0x{offset:X3} -> {clue.Text}");
            printed++;
            if (printed >= 16) break;
        }
    }
}

static void PrintNestedVectorSummary(MemoryReader reader, nint address, string indent, int objectWindow)
{
    var printed = 0;
    for (var offset = 0; offset <= objectWindow - 0x18 && printed < 8; offset += 8)
    {
        if (!reader.TryReadStruct<StdVector>(address + offset, out var vector) ||
            !TryGetVectorByteSize(vector, 8, 0x10000, out var bytes))
            continue;

        var count8 = bytes / 8;
        var samples = new List<string>();
        for (var i = 0; i < Math.Min(count8, 4); i++)
        {
            if (!reader.TryReadStruct<nint>(vector.First + i * 8, out var value)) break;
            samples.Add($"0x{value:X}");
        }

        Console.WriteLine($"{indent}nested-vector +0x{offset:X3} bytes=0x{bytes:X} count8={count8} sample=[{string.Join(", ", samples)}]");
        printed++;
    }
}

static string ItemNameFromMetadata(string metadata)
{
    if (string.IsNullOrWhiteSpace(metadata)) return "";
    if (!metadata.StartsWith("Metadata/", StringComparison.OrdinalIgnoreCase)) return metadata.Trim();
    var slash = metadata.LastIndexOf('/');
    var leaf = slash >= 0 ? metadata[(slash + 1)..] : metadata;
    return string.Join(' ', System.Text.RegularExpressions.Regex
        .Replace(leaf, "([a-z])([A-Z])", "$1 $2")
        .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

static void PrintComponentVectorClues(MemoryReader reader, nint component, int componentWindow)
{
    var printed = 0;
    for (var offset = 0; offset <= componentWindow - 0x18 && printed < 10; offset += 8)
    {
        if (!reader.TryReadStruct<StdVector>(component + offset, out var vector) ||
            !TryGetVectorByteSize(vector, 8, 0x8000, out var bytes))
            continue;

        var count8 = bytes / 8;
        var samples = new List<string>();
        for (var i = 0; i < Math.Min(count8, 4); i++)
        {
            if (!reader.TryReadStruct<nint>(vector.First + i * 8, out var value)) break;
            samples.Add($"0x{value:X}");
        }

        Console.WriteLine(
            $"    VECTOR candidate +0x{offset:X3} bytes=0x{bytes:X} count8={count8} sample=[{string.Join(", ", samples)}]");
        printed++;
    }
}

static void PrintComponentStringClues(MemoryReader reader, nint component, int componentWindow)
{
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var printed = 0;

    for (var offset = 0; offset <= componentWindow - 8 && printed < 16; offset += 8)
    {
        foreach (var clue in ReadStringCluesAt(reader, component, offset))
        {
            if (!LooksLikeProbeText(clue.Text) || !seen.Add(clue.Text)) continue;
            Console.WriteLine($"    TEXT {clue.Kind,-12} +0x{offset:X3} -> {clue.Text}");
            printed++;
            if (printed >= 16) break;
        }
    }
}

static IEnumerable<(string Kind, string Text)> ReadStringCluesAt(MemoryReader reader, nint component, int offset)
{
    var field = component + offset;

    var directStdW = ReadStdWString(reader, field);
    if (directStdW.Length > 0) yield return ("std::wstring", directStdW);

    var ptr = SafePtr(reader, field);
    if (ptr == 0) yield break;

    var pointedStdW = ReadStdWString(reader, ptr);
    if (pointedStdW.Length > 0) yield return ("ptr stdw", pointedStdW);

    var nativeUtf8 = ReadNativeUtf8Text(reader, ptr);
    if (nativeUtf8.Length > 0) yield return ("native utf8", nativeUtf8);

    var utf8 = reader.ReadStringUtf8(ptr, 128);
    if (utf8.Length > 0) yield return ("ptr utf8", utf8);

    var utf16 = reader.ReadStringUtf16(ptr, 128);
    if (utf16.Length > 0) yield return ("ptr utf16", utf16);
}

static bool LooksLikeProbeText(string value)
{
    if (value.Length is < 3 or > 160 || !value.Any(char.IsLetter)) return false;
    var printable = 0;
    foreach (var ch in value)
    {
        if (char.IsControl(ch) || ch == '\uFFFD') return false;
        if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || ch is '_' or '-' or '/' or '\\' or ':' or '\'' or '(' or ')' or '[' or ']' or '.' or ',')
            printable++;
    }
    return printable >= value.Length * 0.85;
}

static string FormatGrid(System.Numerics.Vector2? grid) =>
    grid.HasValue ? $"({grid.Value.X:F1},{grid.Value.Y:F1})" : "-";

static string FormatDistance(float? distance) =>
    distance.HasValue ? distance.Value.ToString("F1") : "-";

static IEnumerable<(uint Id, nint Address, string Metadata)> EnumerateEntityMap(
    MemoryReader reader,
    nint areaInstance,
    int mapOffset)
{
    var head = SafePtr(reader, areaInstance + mapOffset);
    if (head == 0 ||
        !reader.TryReadStruct<int>(areaInstance + mapOffset + 8, out var size) ||
        size is <= 0 or > 200000)
        yield break;

    var queue = new Queue<nint>();
    var visited = new HashSet<nint>();
    queue.Enqueue(SafePtr(reader, head + Poe2.StdMapNode.Parent));
    while (queue.Count > 0 && visited.Count < size + 16)
    {
        var node = queue.Dequeue();
        if (node == 0 || node == head || !visited.Add(node)) continue;
        if (!reader.TryReadStruct<byte>(node + Poe2.StdMapNode.IsNil, out var nil) || nil != 0) continue;

        queue.Enqueue(SafePtr(reader, node + Poe2.StdMapNode.Left));
        queue.Enqueue(SafePtr(reader, node + Poe2.StdMapNode.Right));

        reader.TryReadStruct<uint>(node + Poe2.StdMapNode.KeyId, out var id);
        var entity = SafePtr(reader, node + Poe2.StdMapNode.ValueEntityPtr);
        if (entity == 0 || id >= Poe2.EntityList.VisualIdThreshold) continue;

        var metadata = ReadEntityMetadata(reader, entity);
        if (metadata.Length > 0)
            yield return (id, entity, metadata);
    }
}

static int PrintStateMachineCandidates(MemoryReader reader, nint component)
{
    // The primary machine remains at +0x158/+0x160. Complex entities can expose four
    // additional machine-shaped layers at a stable +0x1B0 stride.
    const int PrimaryOffset = 0x158;
    const int LayerStride = 0x1B0;
    const int LayerCount = 5;
    var found = 0;
    for (var layer = 0; layer < LayerCount; layer++)
    {
        var offset = PrimaryOffset + layer * LayerStride;
        var statesPtr = SafePtr(reader, component + offset);
        if (statesPtr == 0 ||
            !reader.TryReadStruct<StdVector>(component + offset + 8, out var values) ||
            !TryGetVectorCount(values, sizeof(long), 1, 100, out var count))
            continue;

        var names = new List<string>();
        var namesBase = SafePtr(reader, statesPtr + 0x10);
        for (var stride = 0x20; namesBase != 0 && stride <= 0x180; stride += 8)
        {
            var strideNames = new List<string>();
            var sampleCount = Math.Min(count, 8);
            for (var i = 0; i < sampleCount; i++)
            {
                var name = ReadNativeUtf8Text(reader, namesBase + i * stride);
                if (!LooksLikeStateName(name)) break;
                strideNames.Add(name);
            }

            var required = sampleCount == 1 ? 1 : Math.Min(3, sampleCount);
            if (strideNames.Count >= required && strideNames.Count > names.Count)
                names = strideNames;
        }

        if (names.Count == 0)
            names.AddRange(FindStateTextLeaves(reader, statesPtr, 8));
        if (names.Count == 0 && CountLocalPointers(reader, statesPtr) < 4) continue;

        var valuesSample = new List<long>();
        for (var i = 0; i < Math.Min(count, 8); i++)
        {
            if (!reader.TryReadStruct<long>(values.First + i * sizeof(long), out var value)) break;
            valuesSample.Add(value);
        }

        var valuesText = string.Join(", ", valuesSample);
        var namesText = names.Count == 0 ? "none decoded" : string.Join(", ", names);
        var layerName = layer == 0 ? "primary" : $"embedded[{layer}]";
        Console.WriteLine(
            $"    STATE {layerName,-11} offset=+0x{offset:X3} values=+0x{offset + 8:X3} " +
            $"count={count} values=[{valuesText}] text=[{namesText}]");
        found++;
    }
    return found;
}

static IReadOnlyList<string> FindStateTextLeaves(MemoryReader reader, nint root, int maxResults)
{
    var results = new HashSet<string>(StringComparer.Ordinal);
    var visited = new HashSet<nint>();
    var queue = new Queue<nint>();
    var rootRegion = (ulong)root >> 32;
    Span<byte> block = stackalloc byte[0x80];
    queue.Enqueue(root);

    while (queue.Count > 0 && visited.Count < 512 && results.Count < maxResults)
    {
        var address = queue.Dequeue();
        if (!visited.Add(address)) continue;

        var direct = reader.ReadStringUtf8(address, 96);
        if (LooksLikeStateName(direct))
            results.Add(direct);

        var native = ReadNativeUtf8Text(reader, address);
        if (LooksLikeStateName(native))
            results.Add(native);

        if (reader.TryReadBytes(address, block) != block.Length) continue;
        for (var offset = 0; offset <= block.Length - sizeof(long); offset += sizeof(long))
        {
            var pointer = (nint)BitConverter.ToInt64(block[offset..(offset + sizeof(long))]);
            if (!IsPlausiblePointer(pointer) || ((ulong)pointer >> 32) != rootRegion) continue;
            if (!visited.Contains(pointer))
                queue.Enqueue(pointer);
        }
    }

    return results.OrderBy(x => x, StringComparer.Ordinal).Take(maxResults).ToArray();
}

static int CountLocalPointers(MemoryReader reader, nint root)
{
    Span<byte> block = stackalloc byte[0x80];
    if (reader.TryReadBytes(root, block) != block.Length) return 0;
    var rootRegion = (ulong)root >> 32;
    var count = 0;
    for (var offset = 0; offset <= block.Length - sizeof(long); offset += sizeof(long))
    {
        var pointer = (nint)BitConverter.ToInt64(block[offset..(offset + sizeof(long))]);
        if (IsPlausiblePointer(pointer) && ((ulong)pointer >> 32) == rootRegion)
            count++;
    }
    return count;
}

static int PrintMagicPropertyCandidates(MemoryReader reader, nint component)
{
    const int ScanSpan = 0x900;
    const int HeaderSize = 24;
    const int RecordSize = 64;
    const int KeyOffset = 16;
    var found = 0;

    for (var offset = 0; offset <= ScanSpan - 0x18; offset += 8)
    {
        if (!reader.TryReadStruct<StdVector>(component + offset, out var vector) ||
            !TryGetVectorByteSize(vector, HeaderSize + RecordSize, HeaderSize + RecordSize * 128, out var bytes) ||
            (bytes - HeaderSize) % RecordSize != 0)
            continue;

        var count = (bytes - HeaderSize) / RecordSize;
        var names = new List<string>();
        for (var i = 0; i < Math.Min(count, 8); i++)
        {
            var key = SafePtr(reader, vector.First + HeaderSize + i * RecordSize + KeyOffset);
            if (key == 0) break;
            var name = reader.ReadStringUtf16(key, 128);
            if (!LooksLikeModName(name)) break;
            names.Add(name);
        }

        if (names.Count == 0) continue;
        Console.WriteLine(
            $"    MODS candidate offset=+0x{offset:X3} count={count} " +
            $"records=0x{RecordSize:X} [{string.Join(", ", names)}]");
        found++;
    }
    return found;
}

static bool TryGetVectorCount(
    StdVector vector,
    int elementSize,
    int minCount,
    int maxCount,
    out int count)
{
    count = 0;
    if (!IsPlausiblePointer(vector.First) ||
        !IsPlausiblePointer(vector.Last) ||
        !IsPlausiblePointer(vector.End) ||
        vector.Last < vector.First ||
        vector.End < vector.Last)
        return false;

    var bytes = (long)vector.Last - (long)vector.First;
    if (bytes % elementSize != 0) return false;
    var elements = bytes / elementSize;
    if (elements < minCount || elements > maxCount) return false;
    count = (int)elements;
    return true;
}

static bool TryGetVectorByteSize(StdVector vector, int minBytes, int maxBytes, out int bytes)
{
    bytes = 0;
    if (!IsPlausiblePointer(vector.First) ||
        !IsPlausiblePointer(vector.Last) ||
        !IsPlausiblePointer(vector.End) ||
        vector.Last < vector.First ||
        vector.End < vector.Last)
        return false;

    var size = (long)vector.Last - (long)vector.First;
    if (size < minBytes || size > maxBytes) return false;
    bytes = (int)size;
    return true;
}

static string ReadNativeUtf8Text(MemoryReader reader, nint address)
{
    var buffer = SafePtr(reader, address);
    if (buffer == 0 ||
        !reader.TryReadStruct<int>(address + 0x10, out var length) ||
        length is <= 0 or > 128)
        return "";
    if (reader.TryReadStruct<int>(address + 0x18, out var withNull) &&
        withNull != 0 && withNull != length + 1)
        return "";
    return reader.ReadStringUtf8(buffer, length + 1);
}

static bool LooksLikeStateName(string value)
{
    if (value.Length is < 3 or > 96 || !value.Any(char.IsLetter)) return false;
    return value.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or ' ');
}

static bool LooksLikeModName(string value)
{
    if (value.Length is <= 2 or > 128 || !value.Any(char.IsLetter)) return false;
    return value.All(ch => !char.IsControl(ch) && ch != '\uFFFD');
}

static bool IsPlausiblePointer(nint pointer)
{
    var value = (ulong)pointer;
    return value is >= 0x10000 and <= 0x7FFFFFFFFFFF;
}

static nint FindGameStateSlot(ProcessHandle process, MemoryReader reader)
{
    foreach (var pattern in AobPatterns.GameStateRefs)
    foreach (var slot in AobScanner.ScanForResolvedAddresses(process, reader, pattern).Distinct())
        if (new Poe2Live(reader, slot).TryResolve(out _, out _, out _))
            return slot;
    return 0;
}

// Watch area changes using the same one-time GameState slot resolution.
static int RunWatch(ProcessHandle process, MemoryReader reader)
{
    var slot = FindGameStateSlot(process, reader);
    if (slot == 0) { Console.Error.WriteLine("Could not lock GameState slot (in game?)."); return 1; }
    var live = new Poe2Live(reader, slot);
    Console.WriteLine($"WATCH started, GameState slot 0x{slot:X16}. Logging on area change. Ctrl+C to stop.");

    nint prevArea = 0; var idx = 0;
    while (true)
    {
        if (live.TryResolve(out var igs, out var ai, out var lp) && ai != prevArea)
        {
            prevArea = ai;
            idx++;
            var meta = "";
            { var d = reader.TryReadStruct<nint>(lp + Poe2.Entity.EntityDetailsPtr, out var dp) ? dp : 0;
              if (d != 0) meta = ReadStdWString(reader, d + Poe2.EntityDetails.Name); }
            Console.WriteLine($"\n##### AREA #{idx}  AreaInstance=0x{ai:X16}  player={meta}  (t={Environment.TickCount64}) #####");
            // Candidate fields (GH2): level byte @0xBC, hash uint @0xFC — likely drifted.
            reader.TryReadStruct<byte>(ai + 0xBC, out var ghLvl);
            reader.TryReadStruct<uint>(ai + 0xFC, out var ghHash);
            Console.WriteLine($"  GH2 guesses: level@0xBC={ghLvl}  hash@0xFC=0x{ghHash:X8}");
            // Dump 0x00..0x200 so the changing uint (hash) + a 1..100 byte (level) can be found.
            var buf = new byte[0x200];
            if (reader.TryReadBytes(ai, buf) == buf.Length)
                for (var i = 0; i < buf.Length; i += 16)
                {
                    var hex = string.Join(' ', Enumerable.Range(0, 16).Select(j => buf[i + j].ToString("X2")));
                    Console.WriteLine($"  +0x{i:X3}  {hex}");
                }
        }
        Thread.Sleep(1500);
    }
}

// ── Discovery: large-map UI element + its visibility flag ───────────────────
// 1) Auto-detect UiRoot from InGameState (a pointer to a self-referential UiElement, which also
//    confirms the Self offset). 2) Auto-detect the children StdVector offset (a vector of
//    self-referential UiElements). 3) BFS the tree; identify the LargeMap by its DefaultShift
//    signature (0.0, -20.0). 4) Report its address, the visible-flag region, Zoom/Shift.
static int RunFindMap(ProcessHandle process, MemoryReader reader)
{
    var (_, inGameState, _, _) = ResolveChain(process, reader);
    if (inGameState == 0) { Console.Error.WriteLine("Could not resolve chain (in game?)."); return 1; }

    // 1+2) Find UiRoot: a self-referential UiElement whose children StdVector holds elements
    //      that are ALSO self-referential at the same offset. Try every self-ref candidate in
    //      InGameState and accept the first whose children validate (auto-detects Self + Children).
    int[] selfCandidates = { 0x30, 0x28, 0x38, 0x20, 0x18, 0x10, 0x08 };
    nint uiRoot = 0; var selfOff = -1; var childOff = -1; var rootField = -1;
    for (var o = 0; o < 0x1000 && uiRoot == 0; o += 8)
    {
        var p = SafePtr(reader, inGameState + o);
        if (p == 0) continue;
        foreach (var so in selfCandidates)
        {
            if (SafePtr(reader, p + so) != p) continue;
            // try to find a children vector under p whose first element self-refs at the same so
            for (var co = so + 8; co <= so + 0x60; co += 8)
            {
                var first = SafePtr(reader, p + co);
                if (first == 0) continue;
                if (!reader.TryReadStruct<nint>(p + co + 8, out var last)) continue;
                var n = ((long)last - (long)first) / 8;
                if (n < 1 || n > 8192) continue;
                var c0 = SafePtr(reader, first);
                if (c0 != 0 && SafePtr(reader, c0 + so) == c0)
                { uiRoot = p; selfOff = so; childOff = co; rootField = o; break; }
            }
            if (uiRoot != 0) break;
        }
    }
    if (uiRoot == 0) { Console.Error.WriteLine("No UiRoot (self-ref element with self-ref children) found in InGameState[0..0x1000]."); return 1; }
    Console.WriteLine($"UiRoot 0x{uiRoot:X16}  (InGameState+0x{rootField:X}, Self@+0x{selfOff:X}, Children@+0x{childOff:X})\n");

    // 3) BFS; collect elements carrying the DefaultShift (0,-20) signature, recording the
    //    offset it was found at and the element's child count. The large/mini map are outliers:
    //    a rare DefaultShift offset, with children (the map icons), and a real Zoom at +0x38.
    Console.WriteLine("Walking UI tree for map-element candidates (DefaultShift = (0,-20))...");
    var queue = new Queue<nint>(); queue.Enqueue(uiRoot);
    var visited = new HashSet<nint>();
    var parent = new Dictionary<nint, nint>();
    var hits = new List<(nint el, int dsOff, long children, float zoom)>();
    var body = new byte[0x400];
    while (queue.Count > 0 && visited.Count < 30000)
    {
        var el = queue.Dequeue();
        if (el == 0 || !visited.Add(el)) continue;

        var first0 = SafePtr(reader, el + childOff);
        long childCount = 0;
        if (first0 != 0 && reader.TryReadStruct<nint>(el + childOff + 8, out var last0))
        {
            childCount = ((long)last0 - (long)first0) / 8;
            if (childCount is > 0 and <= 8192)
                for (long k = 0; k < childCount; k++)
                {
                    var c = SafePtr(reader, first0 + (nint)(k * 8));
                    if (c != 0 && !parent.ContainsKey(c)) parent[c] = el;
                    queue.Enqueue(c);
                }
        }

        var n = reader.TryReadBytes(el, body);
        for (var i = 0x100; i + 8 <= n; i += 4)   // map fields are deep in the struct
        {
            if (BitConverter.ToSingle(body, i) != 0f || BitConverter.ToSingle(body, i + 4) != -20f) continue;
            var zoom = i + 0x3C <= n ? BitConverter.ToSingle(body, i + 0x38) : 0f; // GH2: Zoom = DefaultShift+0x38
            hits.Add((el, i, childCount, zoom));
            break;
        }
    }

    // The real map elements have a non-default zoom (0.5 live). Print their ancestry + a flag
    // fingerprint per ancestor, so the parent that toggles visibility can be diffed open/closed.
    foreach (var h in hits.Where(h => h.zoom is > 0.05f and < 4f && MathF.Abs(h.zoom - 1f) > 0.01f))
    {
        Console.WriteLine($"\nMAP element 0x{h.el:X16} (DefaultShift@+0x{h.dsOff:X}, Zoom={h.zoom:F3}) ancestry:");
        var cur = h.el; var depth = 0;
        while (cur != 0 && depth++ < 14)
        {
            reader.TryReadStruct<uint>(cur + 0x88, out var f88);
            reader.TryReadStruct<uint>(cur + 0xA8, out var fA8);
            reader.TryReadStruct<uint>(cur + 0x190, out var f190);
            reader.TryReadStruct<uint>(cur + 0x1B8, out var f1B8);
            Console.WriteLine($"  0x{cur:X16}  [+0x88]={f88:X8} [+0xA8]={fA8:X8} [+0x190]={f190:X8} [+0x1B8]={f1B8:X8}");
            if (!parent.TryGetValue(cur, out var par) || par == cur) break;
            cur = par;
        }
    }

    // Group by DefaultShift offset; rare offsets with children + plausible zoom are the map.
    Console.WriteLine($"\n{hits.Count} (0,-20) elements. Grouped by DefaultShift offset:");
    foreach (var g in hits.GroupBy(h => h.dsOff).OrderBy(g => g.Count()))
    {
        Console.WriteLine($"  DefaultShift@+0x{g.Key:X}: {g.Count()} element(s)");
        if (g.Count() <= 4) // likely the map (large+mini) — show details
            foreach (var h in g)
                Console.WriteLine($"      0x{h.el:X16}  children={h.children}  Zoom@+0x{h.dsOff + 0x38:X}={h.zoom:F3}");
    }
    Console.WriteLine("\nThe large map = a rare-offset element with children and a sensible Zoom.");
    Console.WriteLine("Confirm by toggling the map and re-running: the count/visibility of that group changes.");
    return 0;
}

// ── PoE2 top-level chain resolver ───────────────────────────────────────────
// AOB "Game States" → GameState → CurrentStatePtr StdVector @+0x08; its first element is the
// active InGameState. InGameState+0x290 → AreaInstance. AreaInstance+0x5A0 → LocalPlayer.
// Validated live (resolved LocalPlayer == the value-scanned player entity). Falls back to
// scanning the 12 States[] slots if the current-state vector doesn't validate.
static int RunAtlasSnapshot(ProcessHandle process, MemoryReader reader, int sampleCount)
{
    var (_, inGameState, _, _) = ResolveChain(process, reader);
    if (inGameState == 0) { Console.Error.WriteLine("Could not resolve chain (are you in game?)."); return 1; }

    var live = new Poe2Live(reader, 0);
    if (!live.TryReadAtlasSnapshot(inGameState, out var atlas))
    {
        Console.Error.WriteLine("Could not read Atlas snapshot.");
        return 1;
    }

    Console.WriteLine($"Atlas visible : {atlas.IsVisible}");
    Console.WriteLine($"Panel         : 0x{atlas.Panel:X16}");
    Console.WriteLine($"Node layer    : 0x{atlas.NodeLayer:X16}");
    Console.WriteLine($"Layer zoom    : {atlas.Zoom:F4}");
    Console.WriteLine($"Local rect    : ({atlas.LocalRect.L:F1},{atlas.LocalRect.T:F1})-({atlas.LocalRect.R:F1},{atlas.LocalRect.B:F1}) size=({atlas.LocalRect.Width:F1}x{atlas.LocalRect.Height:F1})");
    Console.WriteLine($"Client rect   : ({atlas.ClientRect.L:F1},{atlas.ClientRect.T:F1})-({atlas.ClientRect.R:F1},{atlas.ClientRect.B:F1}) size=({atlas.ClientRect.Width:F1}x{atlas.ClientRect.Height:F1})");
    Console.WriteLine($"Clip rect     : ({atlas.ClipRect.L:F1},{atlas.ClipRect.T:F1})-({atlas.ClipRect.R:F1},{atlas.ClipRect.B:F1}) size=({atlas.ClipRect.Width:F1}x{atlas.ClipRect.Height:F1})");
    Console.WriteLine($"Nodes         : {atlas.Nodes.Count} total, {atlas.Nodes.Count(n => n.InClip)} inside clip");

    Console.WriteLine("Sample nodes:");
    foreach (var n in atlas.Nodes
        .OrderByDescending(n => n.InClip)
        .ThenBy(n => n.Position.Y)
        .ThenBy(n => n.Position.X)
        .Take(sampleCount))
    {
        var center = n.Position + (n.Size * 0.5f);
        Console.WriteLine($"  0x{n.Element:X16} pos=({n.Position.X,10:F1},{n.Position.Y,10:F1}) center=({center.X,10:F1},{center.Y,10:F1}) size=({n.Size.X,5:F1}x{n.Size.Y,5:F1}) inClip={n.InClip,-5} children={n.ChildCount,4}");
    }

    return 0;
}

static int RunAtlasRectScan(ProcessHandle process, MemoryReader reader, int sampleCount)
{
    var (_, inGameState, _, _) = ResolveChain(process, reader);
    if (inGameState == 0) { Console.Error.WriteLine("Could not resolve chain (are you in game?)."); return 1; }

    TryGetClientSize(out var winW, out var winH);
    var live = new Poe2Live(reader, 0);
    if (!live.TryReadAtlasSnapshot(inGameState, out var atlas) || !atlas.IsVisible)
    {
        Console.Error.WriteLine("Atlas snapshot is not visible. Open the Atlas before running this probe.");
        return 1;
    }

    var nodes = atlas.Nodes
        .Where(n => n.UiVisible && n.InClip)
        .OrderBy(n => n.Position.Y)
        .ThenBy(n => n.Position.X)
        .Take(sampleCount)
        .ToList();

    Console.WriteLine($"Window/client : {winW}x{winH}");
    Console.WriteLine($"Atlas panel   : 0x{atlas.Panel:X16}");
    Console.WriteLine($"Node layer    : 0x{atlas.NodeLayer:X16}");
    Console.WriteLine($"Layer zoom    : {atlas.Zoom:F4}");
    Console.WriteLine($"Nodes scanned : {nodes.Count} visible/in-clip of {atlas.Nodes.Count} total");
    Console.WriteLine("Goal          : find the ExileMaps-style final node GetClientRect equivalent.");
    Console.WriteLine();

    var groups = new Dictionary<int, List<(float L, float T, float R, float B)>>();
    var body = new byte[0x600];
    foreach (var node in nodes)
    {
        var read = reader.TryReadBytes(node.Element, body);
        if (read < 0x80) continue;
        for (var off = 0x20; off + 16 <= read; off += 4)
        {
            var l = BitConverter.ToSingle(body, off);
            var t = BitConverter.ToSingle(body, off + 4);
            var r = BitConverter.ToSingle(body, off + 8);
            var b = BitConverter.ToSingle(body, off + 12);
            if (!LooksLikeFinalAtlasNodeRect(l, t, r, b, winW, winH)) continue;
            if (!groups.TryGetValue(off, out var values))
                groups[off] = values = new List<(float L, float T, float R, float B)>();
            values.Add((l, t, r, b));
        }
    }

    Console.WriteLine("Common final-screen rect candidates:");
    foreach (var g in groups
        .Select(g =>
        {
            var rects = g.Value;
            var xs = rects.Select(r => (r.L + r.R) * 0.5f).ToArray();
            var ys = rects.Select(r => (r.T + r.B) * 0.5f).ToArray();
            var ws = rects.Select(r => r.R - r.L).ToArray();
            var hs = rects.Select(r => r.B - r.T).ToArray();
            return new
            {
                Offset = g.Key,
                Count = rects.Count,
                Spread = (xs.Max() - xs.Min()) + (ys.Max() - ys.Min()),
                MinX = xs.Min(),
                MaxX = xs.Max(),
                MinY = ys.Min(),
                MaxY = ys.Max(),
                AvgW = ws.Average(),
                AvgH = hs.Average()
            };
        })
        .Where(x => x.Count >= Math.Max(5, nodes.Count / 3) && x.Spread > 120f)
        .OrderByDescending(x => x.Count)
        .ThenByDescending(x => x.Spread)
        .Take(20))
    {
        Console.WriteLine($"  +0x{g.Offset:X3}: hits={g.Count,4} spread={g.Spread,8:F1} centerX={g.MinX,8:F1}..{g.MaxX,8:F1} centerY={g.MinY,8:F1}..{g.MaxY,8:F1} avgSize=({g.AvgW:F1}x{g.AvgH:F1})");
    }

    Console.WriteLine();
    PrintAtlasTransformScan(reader, atlas.Panel, "panel", winW, winH);
    PrintAtlasTransformScan(reader, atlas.NodeLayer, "node-layer", winW, winH);
    foreach (var node in nodes.Take(3))
        PrintAtlasTransformScan(reader, node.Element, $"node 0x{node.Element:X16}", winW, winH);

    Console.WriteLine();
    Console.WriteLine("Run this at medium zoom, max zoom-in, and max zoom-out. A true client rect offset should keep centers on-screen and avgSize should change with Atlas zoom.");
    Console.WriteLine("For ExileCore2-style transform fields, compare panel/node-layer float candidates; the Atlas zoom field should change across runs.");
    return 0;
}

// Atlas/World Map discovery. This intentionally lives in Research until the
// live panel/node offsets have been validated across closed/open/panned states.
static int RunAtlasProbe(ProcessHandle process, MemoryReader reader, int atlasChildIndex, int maxElements, int sampleCount, nint dumpNode)
{
    var (_, inGameState, _, _) = ResolveChain(process, reader);
    if (inGameState == 0) { Console.Error.WriteLine("Could not resolve chain (are you in game?)."); return 1; }

    var uiRoot = SafePtr(reader, inGameState + Poe2.InGameState.UiRoot);
    if (uiRoot == 0) { Console.Error.WriteLine("UiRoot is null. Open a character in game first."); return 1; }

    TryGetClientSize(out var winW, out var winH);
    Console.WriteLine($"UiRoot        : 0x{uiRoot:X16}");
    Console.WriteLine($"Window/client : {winW}x{winH} (foreground window; rect plausibility only)");
    Console.WriteLine($"Atlas child   : index {atlasChildIndex} (override with --atlas-child N)");
    Console.WriteLine();

    var rootChildren = ReadUiChildren(reader, uiRoot, maxChildren: 256);
    Console.WriteLine($"UiRoot children: {rootChildren.Count}");
    Console.WriteLine("Top-level child summary:");
    for (var i = 0; i < Math.Min(rootChildren.Count, 64); i++)
    {
        var child = rootChildren[i];
        Console.WriteLine($"  [{i,2}] 0x{child:X16}  visible={ReadUiVisible(reader, child),-5}  children={TryGetUiChildCount(reader, child),5}  flags=0x{ReadUiFlags(reader, child):X8}");
    }

    if (atlasChildIndex < 0 || atlasChildIndex >= rootChildren.Count)
    {
        Console.Error.WriteLine($"\nAtlas child index {atlasChildIndex} is outside the root child list.");
        return 1;
    }

    var atlasPanel = rootChildren[atlasChildIndex];
    var atlasVisible = ReadUiVisible(reader, atlasPanel);
    Console.WriteLine($"\nAtlas panel candidate: 0x{atlasPanel:X16}");
    Console.WriteLine($"  Visible bit @ UiElement+0x{Poe2.UiElement.Flags:X} bit {Poe2.UiElement.FlagVisibleBit}: {atlasVisible}");
    Console.WriteLine($"  Child count: {TryGetUiChildCount(reader, atlasPanel)}");

    if (!atlasVisible)
    {
        Console.WriteLine("\nAtlas appears closed. This is the desired cheap gate: do not scan node UI while closed.");
        Console.WriteLine("Open the Atlas/World Map and run the same command again to discover node candidates.");
        return 0;
    }

    var uiElements = WalkUiSubtree(reader, atlasPanel, maxElements, out var parents, out var childIndexes);
    Console.WriteLine($"\nWalked Atlas subtree: {uiElements.Count} UI elements (limit {maxElements})");

    var rectHits = new List<(nint Element, int Offset, float L, float T, float R, float B, long Children, bool Visible)>();
    var body = new byte[0x360];
    foreach (var el in uiElements)
    {
        var read = reader.TryReadBytes(el, body);
        if (read < 0x80) continue;
        for (var off = 0x20; off + 16 <= read; off += 4)
        {
            var l = BitConverter.ToSingle(body, off);
            var t = BitConverter.ToSingle(body, off + 4);
            var r = BitConverter.ToSingle(body, off + 8);
            var b = BitConverter.ToSingle(body, off + 12);
            if (!LooksLikeRect(l, t, r, b, winW, winH)) continue;
            rectHits.Add((el, off, l, t, r, b, TryGetUiChildCount(reader, el), ReadUiVisible(reader, el)));
        }
    }

    Console.WriteLine("\nRect-like fields grouped by offset:");
    foreach (var g in rectHits.GroupBy(x => x.Offset).OrderByDescending(g => g.Count()).Take(20))
    {
        var nodeish = g.Count(x => IsNodeSized(x.L, x.T, x.R, x.B));
        var sample = g.First();
        Console.WriteLine($"  +0x{g.Key:X3}: hits={g.Count(),5}  node-sized={nodeish,5}  sample=({sample.L:F0},{sample.T:F0})-({sample.R:F0},{sample.B:F0})");
    }

    var best = rectHits
        .GroupBy(x => x.Offset)
        .Select(g => new { Offset = g.Key, NodeSized = g.Count(x => IsNodeSized(x.L, x.T, x.R, x.B)), Total = g.Count() })
        .OrderByDescending(x => x.NodeSized)
        .ThenByDescending(x => x.Total)
        .FirstOrDefault();
    if (best == null || best.Total == 0)
    {
        Console.WriteLine("\nNo plausible rect fields found. Try opening the Atlas, panning it into view, or increasing --atlas-max.");
        return 0;
    }

    var nodeCandidates = rectHits
        .Where(x => x.Offset == best.Offset && IsNodeSized(x.L, x.T, x.R, x.B))
        .OrderBy(x => x.T)
        .ThenBy(x => x.L)
        .ToList();

    Console.WriteLine($"\nBest node-rect offset candidate: +0x{best.Offset:X3} ({best.NodeSized} node-sized / {best.Total} total)");
    Console.WriteLine("Sample node-like elements:");
    foreach (var h in nodeCandidates.Take(sampleCount))
    {
        var cx = (h.L + h.R) * 0.5f;
        var cy = (h.T + h.B) * 0.5f;
        Console.WriteLine($"  0x{h.Element:X16} vis={h.Visible,-5} children={h.Children,3} rect=({h.L,7:F1},{h.T,7:F1})-({h.R,7:F1},{h.B,7:F1}) center=({cx,7:F1},{cy,7:F1}) size=({h.R - h.L,5:F1}x{h.B - h.T,5:F1})");
    }

    PrintAtlasCoordinateCandidates(reader, atlasPanel, nodeCandidates, parents, sampleCount);
    PrintAtlasPointCandidateSummary(reader, nodeCandidates.Select(x => x.Element).Distinct().Take(Math.Max(sampleCount, 80)), winW, winH);

    if (dumpNode != 0)
        DumpAtlasNodeAncestry(reader, dumpNode, parents, childIndexes, winW, winH);

    Console.WriteLine("\nValidation flow:");
    Console.WriteLine("  closed: --atlas-probe");
    Console.WriteLine("  open  : --atlas-probe");
    Console.WriteLine("  panned: --atlas-probe --atlas-dump-node 0xADDR");
    Console.WriteLine("Stable child index + rect offset are the first two offsets needed for Atlas Assist rendering.");
    return 0;
}

static (nint gameState, nint inGameState, nint areaInstance, nint localPlayer) ResolveChain(
    ProcessHandle process, MemoryReader reader)
{
    foreach (var pattern in AobPatterns.GameStateRefs)
    foreach (var slot in AobScanner.ScanForResolvedAddresses(process, reader, pattern).Distinct())
    {
        var gameState = SafePtr(reader, slot);
        if (gameState == 0) continue;

        var candidates = new List<nint>();
        var vecFirst = SafePtr(reader, gameState + Poe2.GameState.CurrentStatePtr);
        if (vecFirst != 0) candidates.Add(SafePtr(reader, vecFirst));
        for (var i = 0; i < Poe2.GameState.StateSlotCount; i++)
            candidates.Add(SafePtr(reader, gameState + Poe2.GameState.States + (nint)(i * Poe2.GameState.StateSlotStride)));

        foreach (var inGameState in candidates)
        {
            if (inGameState == 0) continue;
            var areaInstance = SafePtr(reader, inGameState + Poe2.InGameState.AreaInstanceData);
            if (areaInstance == 0) continue;
            var localPlayer = SafePtr(reader, areaInstance + Poe2.AreaInstance.LocalPlayer);
            if (localPlayer == 0) continue;
            if (!ReadEntityMetadata(reader, localPlayer).StartsWith("Metadata/", StringComparison.Ordinal)) continue;
            return (gameState, inGameState, areaInstance, localPlayer);
        }
    }
    return (0, 0, 0, 0);
}

static int RunChainProbe(ProcessHandle process, MemoryReader reader)
{
    var (gameState, inGameState, areaInstance, localPlayer) = ResolveChain(process, reader);
    if (areaInstance == 0) { Console.Error.WriteLine("Could not resolve in-game chain (are you in game?)."); return 1; }
    Console.WriteLine($"GameState    : 0x{gameState:X16}");
    Console.WriteLine($"InGameState  : 0x{inGameState:X16}");
    Console.WriteLine($"AreaInstance : 0x{areaInstance:X16}");
    Console.WriteLine($"LocalPlayer  : 0x{localPlayer:X16}  ({ReadEntityMetadata(reader, localPlayer)})");
    return 0;
}

// ── Discovery: entity-list StdMap offset within AreaInstance ────────────────
// Scans [AreaInstance, +scan) for {ptr Head, int Size} pairs that validate as a std::map of
// entities: Head is a heap ptr whose Parent (root) leads to a node whose value is an Entity
// (metadata starts with "Metadata/"). Reports the offset(s) — these are AwakeEntities/Sleeping.
static int RunCheatScanProbe(ProcessHandle process, MemoryReader reader)
{
    Console.WriteLine();
    Console.WriteLine("Cheat AOB diagnostic");
    Console.WriteLine("--------------------");
    Console.WriteLine("Read-only scan for startup byte-patch signatures. No memory is written.");

    var sections = AobScanner.ReadExecutableSections(process, reader);
    Console.WriteLine($"Executable section groups: {sections.Count}");

    var missing = 0;
    foreach (var def in CheatDefinition.All())
    {
        var hits = new List<(nint SectionBase, byte[] Bytes, int Offset)>();
        foreach (var (sectionBase, bytes) in sections)
        {
            foreach (var match in AobScanner.FindPattern(bytes, def.Pattern))
                hits.Add((sectionBase, bytes, match));
        }

        if (hits.Count == 0)
        {
            missing++;
            Console.WriteLine($"[FAIL] {def.ShortName,-5} {def.Name,-18} pattern not found");
            continue;
        }

        var first = hits[0];
        var matchAddress = first.SectionBase + first.Offset;
        var patchAddress = matchAddress + def.TargetOffset;
        var readLen = def.Type == CheatType.PatchConstant ? 4 : Math.Max(1, def.PatchBytes.Length);
        var currentBytes = ReadCheatBytes(reader, patchAddress, readLen);
        Console.WriteLine($"[OK  ] {def.ShortName,-5} {def.Name,-18} matches={hits.Count,-2} match=0x{matchAddress:X16} patch=0x{patchAddress:X16} bytes={FormatBytes(currentBytes)}");

        if (def.Type == CheatType.PatchConstant)
            PrintCheatConstantProbe(reader, def, first.SectionBase, first.Bytes, first.Offset);
        else
            Console.WriteLine($"       patch bytes: {FormatBytes(def.PatchBytes)}");

        foreach (var extra in hits.Skip(1).Take(4))
            Console.WriteLine($"       extra match: 0x{(extra.SectionBase + extra.Offset):X16}");
        if (hits.Count > 5)
            Console.WriteLine($"       ... {hits.Count - 5} more match(es)");
    }

    Console.WriteLine();
    if (missing == 0)
    {
        Console.WriteLine("All committed cheat signatures resolved.");
        return 0;
    }

    Console.Error.WriteLine($"{missing} cheat signature(s) did not resolve. Those toggles should stay disabled until re-discovered.");
    return 1;
}

static void PrintCheatConstantProbe(MemoryReader reader, CheatDefinition def, nint sectionBase, byte[] sectionBytes, int matchOffset)
{
    var instrAddr = sectionBase + matchOffset + def.TargetOffset;
    var dispPos = matchOffset + def.TargetOffset + def.RipDispOffset;
    if (dispPos + 4 > sectionBytes.Length)
    {
        Console.WriteLine("       constant: RIP displacement outside section");
        return;
    }

    var ripOffset = BitConverter.ToInt32(sectionBytes, dispPos);
    var candidate = instrAddr + def.RipInstrLen + ripOffset;
    var currentBytes = ReadCheatBytes(reader, candidate, 4);
    if (currentBytes == null)
    {
        Console.WriteLine($"       constant: 0x{candidate:X16} unreadable (RIP disp 0x{ripOffset:X})");
        return;
    }

    var value = BitConverter.ToSingle(currentBytes, 0);
    Console.WriteLine($"       constant: 0x{candidate:X16} value={value:G9} bytes={FormatBytes(currentBytes)} ripDisp=0x{ripOffset:X}");
}

static byte[]? ReadCheatBytes(MemoryReader reader, nint address, int count)
{
    if (address == 0 || count <= 0) return null;
    var bytes = new byte[count];
    return reader.TryReadBytes(address, bytes) == count ? bytes : null;
}

static string FormatBytes(byte[]? bytes) =>
    bytes == null ? "<unreadable>" : string.Join(" ", bytes.Select(b => b.ToString("X2")));

static int RunGameStateAobProbe(ProcessHandle process, MemoryReader reader)
{
    Console.WriteLine();
    Console.WriteLine("GameState AOB diagnostic");
    Console.WriteLine("------------------------");
    Console.WriteLine("Goal: verify the root GameState signature and locate where the updated client chain breaks.");
    Console.WriteLine($"Known offsets: GameState.CurrentStatePtr=0x{Poe2.GameState.CurrentStatePtr:X}, " +
        $"GameState.States=0x{Poe2.GameState.States:X}, InGameState.AreaInstance=0x{Poe2.InGameState.AreaInstanceData:X}, " +
        $"AreaInstance.LocalPlayer=0x{Poe2.AreaInstance.LocalPlayer:X}");

    if (AobPatterns.GameStateRefs.Length == 0)
    {
        Console.Error.WriteLine("No GameState AOB patterns committed.");
        return 1;
    }

    var totalSlots = 0;
    var resolved = 0;
    foreach (var pattern in AobPatterns.GameStateRefs)
    {
        Console.WriteLine();
        Console.WriteLine($"Pattern: {pattern.Description}");
        var slots = AobScanner.ScanForResolvedAddresses(process, reader, pattern).Distinct().Take(32).ToList();
        Console.WriteLine($"Slots matched: {slots.Count}");
        totalSlots += slots.Count;

        foreach (var slot in slots)
        {
            var gameState = SafePtr(reader, slot);
            Console.WriteLine();
            Console.WriteLine($"slot=0x{slot:X16} -> gameState=0x{gameState:X16}");
            if (gameState == 0) continue;

            var currentVector = SafePtr(reader, gameState + Poe2.GameState.CurrentStatePtr);
            var currentState = currentVector == 0 ? 0 : SafePtr(reader, currentVector);
            Console.WriteLine($"  current vector field +0x{Poe2.GameState.CurrentStatePtr:X}: 0x{currentVector:X16} first=0x{currentState:X16}");
            if (PrintInGameStateCandidate(reader, "current[0]", currentState, scanNearby: true))
                resolved++;

            Console.WriteLine($"  inline states @ +0x{Poe2.GameState.States:X}:");
            for (var i = 0; i < Poe2.GameState.StateSlotCount; i++)
            {
                var offset = Poe2.GameState.States + i * Poe2.GameState.StateSlotStride;
                var state = SafePtr(reader, gameState + offset);
                if (state == 0) continue;
                if (PrintInGameStateCandidate(reader, $"state[{i}] +0x{offset:X}", state, scanNearby: false))
                    resolved++;
            }
        }
    }

    Console.WriteLine();
    if (totalSlots == 0)
    {
        Console.Error.WriteLine("No GameState AOB matches. The root signature likely changed.");
        return 2;
    }
    if (resolved == 0)
    {
        Console.Error.WriteLine("GameState AOB matched, but no candidate resolved to AreaInstance + LocalPlayer.");
        Console.Error.WriteLine("Likely drift: GameState state slots, InGameState.AreaInstance, or AreaInstance.LocalPlayer.");
        return 1;
    }

    Console.WriteLine($"Resolved {resolved} plausible in-game candidate(s).");
    return 0;
}

static bool PrintInGameStateCandidate(MemoryReader reader, string label, nint inGameState, bool scanNearby)
{
    if (inGameState == 0) return false;

    var areaInstance = SafePtr(reader, inGameState + Poe2.InGameState.AreaInstanceData);
    var localPlayer = areaInstance == 0 ? 0 : SafePtr(reader, areaInstance + Poe2.AreaInstance.LocalPlayer);
    var meta = localPlayer == 0 ? "" : ReadEntityMetadata(reader, localPlayer);
    var ok = meta.StartsWith("Metadata/", StringComparison.Ordinal);
    Console.WriteLine($"    {label,-18} igs=0x{inGameState:X16} area(+0x{Poe2.InGameState.AreaInstanceData:X})=0x{areaInstance:X16} " +
        $"local(+0x{Poe2.AreaInstance.LocalPlayer:X})=0x{localPlayer:X16} {(ok ? meta : "")}");

    if (ok) return true;
    if (areaInstance != 0)
        PrintAreaLocalPlayerOffsetHits(reader, areaInstance);
    if (!scanNearby) return false;

    var hits = 0;
    for (var o = 0; o <= 0x600; o += 8)
    {
        if (o == Poe2.InGameState.AreaInstanceData) continue;
        var candidateArea = SafePtr(reader, inGameState + o);
        if (candidateArea == 0) continue;
        var candidateLocal = SafePtr(reader, candidateArea + Poe2.AreaInstance.LocalPlayer);
        if (candidateLocal == 0) continue;
        var candidateMeta = ReadEntityMetadata(reader, candidateLocal);
        if (!candidateMeta.StartsWith("Metadata/", StringComparison.Ordinal)) continue;
        Console.WriteLine($"      nearby area hit: InGameState+0x{o:X3} -> 0x{candidateArea:X16}, " +
            $"local=0x{candidateLocal:X16} {candidateMeta}");
        hits++;
        if (hits >= 8) break;
    }

    return false;
}

static void PrintAreaLocalPlayerOffsetHits(MemoryReader reader, nint areaInstance)
{
    var hits = 0;
    for (var o = 0; o <= 0x1000; o += 8)
    {
        if (o == Poe2.AreaInstance.LocalPlayer) continue;
        var candidateLocal = SafePtr(reader, areaInstance + o);
        if (candidateLocal == 0) continue;
        var candidateMeta = ReadEntityMetadata(reader, candidateLocal);
        if (!candidateMeta.StartsWith("Metadata/", StringComparison.Ordinal)) continue;
        Console.WriteLine($"      nearby local hit: AreaInstance+0x{o:X3} -> 0x{candidateLocal:X16} {candidateMeta}");
        hits++;
        if (hits >= 8) break;
    }
}

static int RunFindEntities(ProcessHandle process, MemoryReader reader, int scan)
{
    var (_, _, areaInstance, _) = ResolveChain(process, reader);
    if (areaInstance == 0) { Console.Error.WriteLine("Could not resolve chain (in game?)."); return 1; }
    Console.WriteLine($"AreaInstance 0x{areaInstance:X16} — scanning +0x0..+0x{scan:X} for entity std::maps...");

    var found = 0;
    for (var o = 0; o + 0x10 <= scan; o += 8)
    {
        var head = SafePtr(reader, areaInstance + o);
        if (head == 0) continue;
        if (!reader.TryReadStruct<int>(areaInstance + o + 8, out var size)) continue;
        if (size <= 0 || size > 100000) continue;

        var root = SafePtr(reader, head + Poe2.StdMapNode.Parent);
        if (root == 0) continue;
        // root node should be non-nil; its value should be an entity.
        if (!reader.TryReadStruct<byte>(root + Poe2.StdMapNode.IsNil, out var nil) || nil != 0) continue;
        var entityPtr = SafePtr(reader, root + Poe2.StdMapNode.ValueEntityPtr);
        var meta = ReadEntityMetadata(reader, entityPtr);
        if (!meta.StartsWith("Metadata/", StringComparison.Ordinal)) continue;

        found++;
        Console.WriteLine($"\n  +0x{o:X}: std::map size={size} head=0x{head:X16}  (root entity: {meta})");
        WalkEntityMap(reader, head, size);
    }
    if (found == 0) Console.WriteLine("  no entity std::map found in range — widen --window.");
    return 0;
}

// ── Discovery: terrain StdVectors within AreaInstance ───────────────────────
// Lists StdVector-looking triples {First,Last,End} with First≤Last≤End (heap), reporting byte
// count + a guess. The walkable grid is a big byte vector (≈ rows × bytesPerRow); an int right
// after a big vector is a BytesPerRow candidate. Helps locate the TerrainStruct.
static int RunFindTerrain(ProcessHandle process, MemoryReader reader, int scan)
{
    var (_, _, areaInstance, _) = ResolveChain(process, reader);
    if (areaInstance == 0) { Console.Error.WriteLine("Could not resolve chain (in game?)."); return 1; }
    Console.WriteLine($"AreaInstance 0x{areaInstance:X16} — scanning +0x0..+0x{scan:X} for StdVectors...");

    for (var o = 0; o + 24 <= scan; o += 8)
    {
        var first = SafePtr(reader, areaInstance + o);
        if (first == 0) continue;
        if (!reader.TryReadStruct<nint>(areaInstance + o + 8, out var last)) continue;
        if (!reader.TryReadStruct<nint>(areaInstance + o + 16, out var end)) continue;
        var u = (ulong)last;
        if (u < 0x10000 || u > 0x7FFFFFFFFFFF) continue;
        if ((long)last < (long)first || (long)end < (long)last) continue;
        var bytes = (long)last - (long)first;
        if (bytes < 0x200 || bytes > 0x4000000) continue;       // big-ish allocations only
        reader.TryReadStruct<int>(areaInstance + o + 24, out var trailingInt); // BytesPerRow candidate
        Console.WriteLine($"  +0x{o:X4}: vec first=0x{first:X12} bytes={bytes} (0x{bytes:X})  nextInt={trailingInt}");
    }
    Console.WriteLine("Look for a large byte vector whose size ≈ gridRows × bytesPerRow (nextInt≈row stride).");
    return 0;
}

// BFS over the MSVC std::map red-black tree. Node: Left@0, Parent@8, Right@0x10, IsNil@0x19;
// Data@0x20 = key{uint id}, value{IntPtr EntityPtr}@0x28. Leaf children point at the nil sentinel.
static void WalkEntityMap(MemoryReader reader, nint head, int size)
{
    if (head == 0 || size <= 0 || size > 200000) return;
    var root = SafePtr(reader, head + Poe2.StdMapNode.Parent);
    var queue = new Queue<nint>();
    queue.Enqueue(root);
    var seen = 0; var printed = 0; var visited = new HashSet<nint>();
    while (queue.Count > 0 && seen < size + 8 && visited.Count < 300000)
    {
        var node = queue.Dequeue();
        if (node == 0 || node == head || !visited.Add(node)) continue;
        if (!reader.TryReadStruct<byte>(node + Poe2.StdMapNode.IsNil, out var isNil) || isNil != 0) continue;
        seen++;

        reader.TryReadStruct<uint>(node + Poe2.StdMapNode.KeyId, out var id);
        var entityPtr = SafePtr(reader, node + Poe2.StdMapNode.ValueEntityPtr);
        if (printed < 14 && entityPtr != 0 && id < Poe2.EntityList.VisualIdThreshold)
        {
            var meta = ReadEntityMetadata(reader, entityPtr);
            if (meta.Length > 0)
            {
                Console.WriteLine($"      id {id,-10} 0x{entityPtr:X16}  {meta}");
                printed++;
            }
        }
        queue.Enqueue(SafePtr(reader, node + Poe2.StdMapNode.Left));
        queue.Enqueue(SafePtr(reader, node + Poe2.StdMapNode.Right));
    }
    Console.WriteLine($"      … walked {seen} non-nil nodes (printed first {printed} real entities).");
}

// Safe pointer read — returns 0 on any failure (never throws). Also rejects obviously-bad
// pointers (non-canonical / low addresses) so garbage from a wrong chain branch can't propagate.
static List<nint> ReadUiChildren(MemoryReader reader, nint element, int maxChildren)
{
    var result = new List<nint>();
    var first = SafePtr(reader, element + Poe2.UiElement.Children);
    if (first == 0) return result;
    if (!reader.TryReadStruct<nint>(element + Poe2.UiElement.Children + 8, out var last)) return result;
    var count = ((long)last - (long)first) / 8;
    if (count <= 0 || count > maxChildren) return result;
    for (long i = 0; i < count; i++)
    {
        var child = SafePtr(reader, first + (nint)(i * 8));
        if (child != 0) result.Add(child);
    }
    return result;
}

static long TryGetUiChildCount(MemoryReader reader, nint element)
{
    var first = SafePtr(reader, element + Poe2.UiElement.Children);
    if (first == 0) return 0;
    if (!reader.TryReadStruct<nint>(element + Poe2.UiElement.Children + 8, out var last)) return 0;
    var count = ((long)last - (long)first) / 8;
    return count is >= 0 and <= 100000 ? count : 0;
}

static uint ReadUiFlags(MemoryReader reader, nint element)
    => reader.TryReadStruct<uint>(element + Poe2.UiElement.Flags, out var flags) ? flags : 0;

static bool ReadUiVisible(MemoryReader reader, nint element)
    => (ReadUiFlags(reader, element) & (1u << Poe2.UiElement.FlagVisibleBit)) != 0;

static List<nint> WalkUiSubtree(
    MemoryReader reader,
    nint root,
    int maxElements,
    out Dictionary<nint, nint> parents,
    out Dictionary<nint, int> childIndexes)
{
    var result = new List<nint>();
    parents = new Dictionary<nint, nint> { [root] = 0 };
    childIndexes = new Dictionary<nint, int> { [root] = -1 };
    var queue = new Queue<nint>();
    var visited = new HashSet<nint>();
    queue.Enqueue(root);
    while (queue.Count > 0 && result.Count < maxElements)
    {
        var el = queue.Dequeue();
        if (el == 0 || !visited.Add(el)) continue;
        result.Add(el);
        var children = ReadUiChildren(reader, el, maxChildren: 8192);
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == 0) continue;
            parents.TryAdd(child, el);
            childIndexes.TryAdd(child, i);
            queue.Enqueue(child);
        }
    }
    return result;
}

static void PrintAtlasPointCandidateSummary(MemoryReader reader, IEnumerable<nint> elements, int winW, int winH)
{
    var groups = new Dictionary<int, List<(float X, float Y)>>();
    var body = new byte[0x360];
    var seen = 0;

    foreach (var el in elements)
    {
        var read = reader.TryReadBytes(el, body);
        if (read < 0x80) continue;
        seen++;
        for (var off = 0x20; off + 8 <= read; off += 4)
        {
            var x = BitConverter.ToSingle(body, off);
            var y = BitConverter.ToSingle(body, off + 4);
            if (!LooksLikePoint(x, y, winW, winH)) continue;
            if (!groups.TryGetValue(off, out var values))
                groups[off] = values = new List<(float X, float Y)>();
            values.Add((x, y));
        }
    }

    Console.WriteLine("\nPosition-like Vector2 fields across sampled node candidates:");
    Console.WriteLine("  Re-run after panning the Atlas; real node-position/transform offsets should shift together.");
    foreach (var g in groups
        .Select(g =>
        {
            var xs = g.Value.Select(v => v.X).ToArray();
            var ys = g.Value.Select(v => v.Y).ToArray();
            return new
            {
                Offset = g.Key,
                Count = g.Value.Count,
                MinX = xs.Min(),
                MaxX = xs.Max(),
                MinY = ys.Min(),
                MaxY = ys.Max()
            };
        })
        .Where(x => x.Count >= Math.Max(4, seen / 6))
        .OrderByDescending(x => (x.MaxX - x.MinX) + (x.MaxY - x.MinY))
        .ThenByDescending(x => x.Count)
        .Take(20))
    {
        Console.WriteLine($"  +0x{g.Offset:X3}: hits={g.Count,4}  x={g.MinX,8:F1}..{g.MaxX,8:F1}  y={g.MinY,8:F1}..{g.MaxY,8:F1}  spread={(g.MaxX - g.MinX) + (g.MaxY - g.MinY),8:F1}");
    }
}

static void PrintAtlasCoordinateCandidates(
    MemoryReader reader,
    nint atlasPanel,
    IReadOnlyList<(nint Element, int Offset, float L, float T, float R, float B, long Children, bool Visible)> nodeCandidates,
    Dictionary<nint, nint> parents,
    int sampleCount)
{
    Console.WriteLine("\nAtlas coordinate candidate snapshot:");
    Console.WriteLine("  Finding: node raw position is the pan-adjusted float2 at +0x118/+0x11C; +0x280 is local icon bounds.");
    if (TryReadRectAt(reader, atlasPanel, 0x280, out var panelLocal))
        Console.WriteLine($"  panel local +0x280: ({panelLocal.L:F1},{panelLocal.T:F1})-({panelLocal.R:F1},{panelLocal.B:F1}) size=({panelLocal.R - panelLocal.L:F1}x{panelLocal.B - panelLocal.T:F1})");
    if (TryReadRectAt(reader, atlasPanel, 0x330, out var panelClient))
        Console.WriteLine($"  panel client +0x330: ({panelClient.L:F1},{panelClient.T:F1})-({panelClient.R:F1},{panelClient.B:F1}) size=({panelClient.R - panelClient.L:F1}x{panelClient.B - panelClient.T:F1})");
    if (TryReadRectAt(reader, atlasPanel, 0x340, out var panelClip))
        Console.WriteLine($"  panel clip  +0x340: ({panelClip.L:F1},{panelClip.T:F1})-({panelClip.R:F1},{panelClip.B:F1}) size=({panelClip.R - panelClip.L:F1}x{panelClip.B - panelClip.T:F1})");

    var parentGroups = nodeCandidates
        .Where(x => x.Visible && parents.TryGetValue(x.Element, out _))
        .GroupBy(x => parents[x.Element])
        .Select(g => new
        {
            Parent = g.Key,
            Count = g.Count(),
            ParentChildren = TryGetUiChildCount(reader, g.Key),
            LayerSize = TryReadRectAt(reader, g.Key, 0x280, out var r) ? (W: r.R - r.L, H: r.B - r.T) : (W: 0f, H: 0f)
        })
        .OrderByDescending(x => x.Count)
        .ThenByDescending(x => x.ParentChildren)
        .Take(8)
        .ToList();

    Console.WriteLine("  top node parent containers:");
    foreach (var g in parentGroups)
        Console.WriteLine($"    parent=0x{g.Parent:X16} visibleNodes={g.Count,5} parentChildren={g.ParentChildren,5} localSize=({g.LayerSize.W:F1}x{g.LayerSize.H:F1})");

    var dominantParent = parentGroups.FirstOrDefault()?.Parent ?? 0;
    var focused = dominantParent == 0
        ? nodeCandidates.Where(x => x.Visible)
        : nodeCandidates.Where(x => x.Visible && parents.TryGetValue(x.Element, out var p) && p == dominantParent);

    Console.WriteLine(dominantParent == 0
        ? "  visible node samples:"
        : $"  visible samples under dominant parent 0x{dominantParent:X16}:");
    foreach (var h in focused.Take(Math.Max(sampleCount, 24)))
    {
        if (!TryReadFloat(reader, h.Element + 0x118, out var rawX) ||
            !TryReadFloat(reader, h.Element + 0x11C, out var rawY))
            continue;

        var w = h.R - h.L;
        var height = h.B - h.T;
        var cx = rawX + (w * 0.5f);
        var cy = rawY + (height * 0.5f);
        var inClip = TryReadRectAt(reader, atlasPanel, 0x340, out var clip) &&
            cx >= clip.L && cx <= clip.R &&
            cy >= clip.T && cy <= clip.B;
        Console.WriteLine($"    0x{h.Element:X16} raw118=({rawX,10:F1},{rawY,10:F1}) center=({cx,10:F1},{cy,10:F1}) inClip={inClip,-5} localSize=({w,5:F1}x{height,5:F1}) children={h.Children,4}");
    }
}

static bool TryReadRectAt(MemoryReader reader, nint element, int offset, out (float L, float T, float R, float B) rect)
{
    rect = default;
    if (!reader.TryReadStruct<float>(element + offset, out var l) ||
        !reader.TryReadStruct<float>(element + offset + 4, out var t) ||
        !reader.TryReadStruct<float>(element + offset + 8, out var r) ||
        !reader.TryReadStruct<float>(element + offset + 12, out var b))
        return false;
    if (!float.IsFinite(l) || !float.IsFinite(t) || !float.IsFinite(r) || !float.IsFinite(b))
        return false;
    rect = (l, t, r, b);
    return true;
}

static bool TryReadFloat(MemoryReader reader, nint addr, out float value)
{
    value = 0;
    if (!reader.TryReadStruct<float>(addr, out var v) || !float.IsFinite(v))
        return false;
    value = v;
    return true;
}

static void PrintAtlasTransformScan(MemoryReader reader, nint element, string label, int winW, int winH)
{
    if (element == 0) return;
    var body = new byte[0x700];
    var read = reader.TryReadBytes(element, body);
    if (read < 0x80) return;

    Console.WriteLine($"Transform scan: {label} @ 0x{element:X16}");

    var floats = new List<(int Off, float Value)>();
    for (var off = 0x20; off + 4 <= read; off += 4)
    {
        var v = BitConverter.ToSingle(body, off);
        if (!float.IsFinite(v)) continue;
        if (Math.Abs(v) is < 0.0001f or > 10000f) continue;
        if (v is >= 0.01f and <= 20f || Math.Abs(v) is >= 20f and <= 5000f)
            floats.Add((off, v));
    }

    Console.WriteLine("  scale-like floats:");
    foreach (var f in floats
        .Where(x => x.Value is >= 0.05f and <= 8f)
        .OrderBy(x => x.Off)
        .Take(48))
        Console.WriteLine($"    +0x{f.Off:X3}: {f.Value,10:F5}");

    Console.WriteLine("  point-like pairs:");
    var printedPoints = 0;
    for (var off = 0x20; off + 8 <= read && printedPoints < 32; off += 4)
    {
        var x = BitConverter.ToSingle(body, off);
        var y = BitConverter.ToSingle(body, off + 4);
        if (!LooksLikePoint(x, y, winW, winH)) continue;
        Console.WriteLine($"    +0x{off:X3}: ({x,10:F2},{y,10:F2})");
        printedPoints++;
    }

    Console.WriteLine("  rect-like fields:");
    var printedRects = 0;
    for (var off = 0x20; off + 16 <= read && printedRects < 24; off += 4)
    {
        var l = BitConverter.ToSingle(body, off);
        var t = BitConverter.ToSingle(body, off + 4);
        var r = BitConverter.ToSingle(body, off + 8);
        var b = BitConverter.ToSingle(body, off + 12);
        if (!LooksLikeRect(l, t, r, b, winW, winH)) continue;
        Console.WriteLine($"    +0x{off:X3}: ({l,9:F1},{t,9:F1})-({r,9:F1},{b,9:F1}) size=({r - l,8:F1}x{b - t,8:F1})");
        printedRects++;
    }
}

static void DumpAtlasNodeAncestry(
    MemoryReader reader,
    nint node,
    Dictionary<nint, nint> parents,
    Dictionary<nint, int> childIndexes,
    int winW,
    int winH)
{
    Console.WriteLine($"\nAtlas node ancestry dump for 0x{node:X16}:");
    if (!parents.ContainsKey(node))
    {
        Console.WriteLine("  Node is not inside the current Atlas subtree. Use an address from this run's sample list.");
        return;
    }

    var depth = 0;
    for (var cur = node; cur != 0 && depth < 16; cur = parents.TryGetValue(cur, out var parent) ? parent : 0, depth++)
    {
        childIndexes.TryGetValue(cur, out var childIndex);
        Console.WriteLine($"  depth={depth,2} idx={childIndex,4} el=0x{cur:X16} visible={ReadUiVisible(reader, cur),-5} children={TryGetUiChildCount(reader, cur),5} flags=0x{ReadUiFlags(reader, cur):X8}");
        DumpAtlasTransformWindow(reader, cur);
        DumpAtlasKnownRects(reader, cur, winW, winH);
        DumpAtlasTopPoints(reader, cur, winW, winH);
    }
}

static void DumpAtlasTransformWindow(MemoryReader reader, nint element)
{
    var body = new byte[0x360];
    var read = reader.TryReadBytes(element, body);
    if (read < 0x140) return;

    Console.WriteLine("      floats +0x100..+0x140:");
    for (var off = 0x100; off <= 0x140; off += 0x10)
    {
        var a = BitConverter.ToSingle(body, off);
        var b = BitConverter.ToSingle(body, off + 4);
        var c = BitConverter.ToSingle(body, off + 8);
        var d = BitConverter.ToSingle(body, off + 12);
        Console.WriteLine($"        +0x{off:X3}: {a,10:F3} {b,10:F3} {c,10:F3} {d,10:F3}");
    }

    PrintRectAt(body, read, 0x110, "rect? +0x110");
    PrintRectAt(body, read, 0x114, "pan?  +0x114");
    PrintRectAt(body, read, 0x118, "pan?  +0x118");
    PrintRectAt(body, read, 0x280, "local +0x280");
    PrintRectAt(body, read, 0x330, "panel +0x330");
    PrintRectAt(body, read, 0x340, "panel +0x340");
}

static void PrintRectAt(byte[] body, int read, int off, string label)
{
    if (off + 16 > read) return;
    var l = BitConverter.ToSingle(body, off);
    var t = BitConverter.ToSingle(body, off + 4);
    var r = BitConverter.ToSingle(body, off + 8);
    var b = BitConverter.ToSingle(body, off + 12);
    if (!float.IsFinite(l) || !float.IsFinite(t) || !float.IsFinite(r) || !float.IsFinite(b)) return;
    Console.WriteLine($"      {label}: ({l,10:F3},{t,10:F3})-({r,10:F3},{b,10:F3}) size=({r - l,10:F3}x{b - t,10:F3})");
}

static void DumpAtlasKnownRects(MemoryReader reader, nint element, int winW, int winH)
{
    var body = new byte[0x360];
    var read = reader.TryReadBytes(element, body);
    if (read < 0x80) return;

    var printed = 0;
    for (var off = 0x20; off + 16 <= read; off += 4)
    {
        var l = BitConverter.ToSingle(body, off);
        var t = BitConverter.ToSingle(body, off + 4);
        var r = BitConverter.ToSingle(body, off + 8);
        var b = BitConverter.ToSingle(body, off + 12);
        if (!LooksLikeRect(l, t, r, b, winW, winH)) continue;
        if (printed++ >= 8) break;
        Console.WriteLine($"      rect +0x{off:X3}: ({l,8:F1},{t,8:F1})-({r,8:F1},{b,8:F1}) size=({r - l,6:F1}x{b - t,6:F1})");
    }
}

static void DumpAtlasTopPoints(MemoryReader reader, nint element, int winW, int winH)
{
    var body = new byte[0x360];
    var read = reader.TryReadBytes(element, body);
    if (read < 0x80) return;

    var printed = 0;
    for (var off = 0x20; off + 8 <= read; off += 4)
    {
        var x = BitConverter.ToSingle(body, off);
        var y = BitConverter.ToSingle(body, off + 4);
        if (!LooksLikePoint(x, y, winW, winH)) continue;
        if (printed++ >= 10) break;
        Console.WriteLine($"      vec2 +0x{off:X3}: ({x,8:F1},{y,8:F1})");
    }
}

static bool LooksLikeRect(float l, float t, float r, float b, int winW, int winH)
{
    if (!float.IsFinite(l) || !float.IsFinite(t) || !float.IsFinite(r) || !float.IsFinite(b)) return false;
    var w = r - l;
    var h = b - t;
    if (w <= 2f || h <= 2f || w > Math.Max(5000, winW * 4) || h > Math.Max(5000, winH * 4)) return false;
    var marginX = Math.Max(4000, winW * 2);
    var marginY = Math.Max(4000, winH * 2);
    return l > -marginX && r < winW + marginX && t > -marginY && b < winH + marginY;
}

static bool LooksLikePoint(float x, float y, int winW, int winH)
{
    if (!float.IsFinite(x) || !float.IsFinite(y)) return false;
    if (Math.Abs(x) < 0.001f && Math.Abs(y) < 0.001f) return false;
    var marginX = Math.Max(8000, winW * 8);
    var marginY = Math.Max(8000, winH * 8);
    return x > -marginX && x < winW + marginX && y > -marginY && y < winH + marginY;
}

static bool IsNodeSized(float l, float t, float r, float b)
{
    var w = r - l;
    var h = b - t;
    return w is >= 6f and <= 180f && h is >= 6f and <= 180f;
}

static bool LooksLikeFinalAtlasNodeRect(float l, float t, float r, float b, int winW, int winH)
{
    if (!float.IsFinite(l) || !float.IsFinite(t) || !float.IsFinite(r) || !float.IsFinite(b)) return false;
    var w = r - l;
    var h = b - t;
    if (w is < 6f or > 220f || h is < 6f or > 220f) return false;
    if (Math.Abs(w / h) is < 0.35f or > 2.85f) return false;
    var cx = (l + r) * 0.5f;
    var cy = (t + b) * 0.5f;
    return cx > -100f && cx < winW + 100f && cy > -100f && cy < winH + 100f;
}

static void TryGetClientSize(out int width, out int height)
{
    width = 1920;
    height = 1080;
    var hwnd = Win.GetForegroundWindow();
    if (hwnd != 0 && Win.GetClientRect(hwnd, out var rc))
    {
        var w = rc.right - rc.left;
        var h = rc.bottom - rc.top;
        if (w > 0 && h > 0)
        {
            width = w;
            height = h;
        }
    }
}

static nint SafePtr(MemoryReader reader, nint addr)
{
    if (!reader.TryReadStruct<nint>(addr, out var p)) return 0;
    var u = (ulong)p;
    if (u < 0x10000 || u > 0x7FFFFFFFFFFF) return 0; // user-mode heap range sanity
    return p;
}

// Resolve an entity's metadata path via EntityDetails (ptr @ +0x08) → name StdWString @ +0x08.
static string ReadEntityMetadata(MemoryReader reader, nint entity)
{
    if (entity == 0) return "";
    var detailsPtr = SafePtr(reader, entity + Poe2.Entity.EntityDetailsPtr);
    if (detailsPtr == 0) return "";
    return ReadStdWString(reader, detailsPtr + Poe2.EntityDetails.Name);
}

// Read a PoE/MSVC std::wstring (SSO): Length (chars) at +0x10; inline UTF-16 at base when
// Length < 8, otherwise Buffer (at +0x00) is a pointer to the chars.
static string ReadStdWString(MemoryReader reader, nint addr)
{
    if (!reader.TryReadStruct<int>(addr + 0x10, out var len) || len <= 0 || len > 1024) return "";
    if (len < 8) return reader.ReadStringUtf16(addr, len);
    var ptr = SafePtr(reader, addr);
    return ptr == 0 ? "" : reader.ReadStringUtf16(ptr, len);
}

static int RunValueScan(MemoryReader reader, int hp, int? mana)
{
    Console.WriteLine($"Value-scanning for LifeComponent (hp={hp}{(mana.HasValue ? $", mana={mana}" : "")})...");
    var matches = LifeValidator.FindCandidates(reader, hp, mana,
        onProgress: p =>
        {
            if (p.RegionsScanned % 20 == 0 || p.RegionsScanned == p.TotalRegions)
                Console.Write($"\r  {p.RegionsScanned}/{p.TotalRegions} regions  {p.BytesScanned / 1024 / 1024} MB  {p.CandidatesFound} hit(s)   ");
        });
    Console.WriteLine();

    if (matches.Count == 0)
    {
        Console.Error.WriteLine("No match. HP must equal the current value at scan time; stand still in town.");
        return 1;
    }

    Console.WriteLine($"{matches.Count} candidate Life component(s):");
    foreach (var m in matches)
        Console.WriteLine($"  Life @ 0x{m.LifeComponentAddress:X16}  owner(entity) @ 0x{m.OwnerAddress:X16}");
    Console.WriteLine("Use --entity <owner> to walk the entity, or --chain to resolve roots via AOB.");
    return 0;
}

static int RunDump(MemoryReader reader, nint addr, int len)
{
    Console.WriteLine($"Dumping 0x{len:X} bytes @ 0x{addr:X16}:");
    var buf = new byte[len];
    if (reader.TryReadBytes(addr, buf) != len)
    {
        Console.Error.WriteLine("Read failed (or partial).");
        return 1;
    }
    for (var i = 0; i < len; i += 16)
    {
        var n = Math.Min(16, len - i);
        var hex = string.Join(' ', Enumerable.Range(0, n).Select(j => buf[i + j].ToString("X2")));
        Console.WriteLine($"  +0x{i:X3}  {hex}");
    }
    return 0;
}

static int RunAobScan(ProcessHandle process, MemoryReader reader)
{
    if (AobPatterns.IngameStateRefs.Length == 0)
    {
        Console.Error.WriteLine("No AOB patterns committed yet (AobPatterns.IngameStateRefs is empty).");
        Console.Error.WriteLine("Discover a PoE2 IngameState pattern first, then add it to AobPatterns.cs.");
        return 1;
    }
    foreach (var pattern in AobPatterns.IngameStateRefs)
    {
        Console.WriteLine($"Scanning pattern: {pattern}");
        var slots = AobScanner.ScanForResolvedAddresses(process, reader, pattern);
        foreach (var slot in slots)
            Console.WriteLine($"  slot @ 0x{slot:X16}  -> 0x{(reader.TryReadStruct<nint>(slot, out var v) ? v : 0):X16}");
    }
    return 0;
}

static bool HasFlag(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;

static int? TryGetIntArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    if (idx < 0 || idx + 1 >= args.Length) return null;
    var s = args[idx + 1];
    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        return int.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex) ? hex : null;
    return int.TryParse(s, out var v) ? v : null;
}

static string? TryGetStringArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    return idx < 0 || idx + 1 >= args.Length ? null : args[idx + 1];
}

static nint? TryGetHexArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    if (idx < 0 || idx + 1 >= args.Length) return null;
    var s = args[idx + 1];
    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
    return long.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? (nint)v : null;
}

readonly record struct MechanicProbeEntity(
    uint Id,
    nint Address,
    string Source,
    string Metadata,
    System.Numerics.Vector2? Grid,
    float? Distance,
    string Components,
    int? IconComplete,
    string LifeState,
    int? HpCur,
    int? HpMax,
    string StateValues);

sealed record RuneforgeEncounterSnapshot(
    IReadOnlyList<long> StateValues,
    IReadOnlyDictionary<int, IReadOnlyList<nint>> StatsVectors,
    IReadOnlyDictionary<int, string> PreloadTexts);

readonly record struct RuneforgeUiTraceNode(
    int Step,
    int ChildIndex,
    nint Address,
    uint Flags,
    bool Visible,
    long ChildCount,
    int RecipeRowCount);

readonly record struct RuneforgeUiChildSnapshot(
    string Label,
    nint Address,
    uint Flags,
    bool Visible,
    nint ChildFirst,
    nint ChildLast,
    nint ChildCapacity,
    long ChildCount,
    long ChildCapacityCount,
    byte[] Body,
    IReadOnlyList<RuneforgeVectorField> VectorFields,
    IReadOnlyList<string> VisibleRows);

readonly record struct RuneforgeVectorField(
    int Offset,
    nint First,
    nint Last,
    nint End,
    long Count,
    long Capacity);

readonly record struct RuneforgePopulateState(
    uint Flags,
    nint ChildLast,
    long ChildCount,
    long Capacity,
    int VisibleRows,
    nint MirrorLast,
    long MirrorCount,
    long TabsCount,
    uint Field2E8,
    uint Field2F0);

readonly record struct RuneforgeCatalogRow(
    int Index,
    nint Row,
    uint Flags,
    bool Visible,
    string Text);

readonly record struct RuneforgeInventoryBlockInfo(
    uint Flags,
    int CountField,
    nint SmallFirst,
    nint SmallLast,
    nint SmallEnd,
    int SmallCount,
    int SmallCapacity,
    nint LargeFirst,
    nint LargeLast,
    nint LargeEnd,
    int LargeCount,
    int LargeCapacity);

readonly record struct RuneforgeInventoryBlockSnapshot(
    string Source,
    uint ControllerId,
    nint Controller,
    float? Distance,
    int Block,
    nint Inventories,
    RuneforgeInventoryBlockInfo Info,
    uint BlockHash,
    IReadOnlyList<nint> SmallValues,
    IReadOnlyList<byte[]> SmallTargetSamples,
    IReadOnlyList<nint> LargeValues,
    IReadOnlyList<byte[]> LargeTargetSamples);

readonly record struct RuneforgeComponentMemorySnapshot(
    string Source,
    uint EntityId,
    nint Entity,
    string Metadata,
    float? Distance,
    string ComponentName,
    int ComponentIndex,
    nint Component,
    byte[] Body);

readonly record struct RuneforgeSelectionAnchor(
    int Index,
    string Text,
    IReadOnlyList<string> Keys);

readonly record struct RuneforgeIndexHit(
    int Offset,
    int Index);

readonly record struct RuneforgeFingerprintBlock(
    string Source,
    uint EntityId,
    string ComponentName,
    int ComponentIndex,
    nint Base,
    string Path,
    byte[] Body);

readonly record struct RuneforgeFingerprintCandidate(
    string Kind,
    RuneforgeFingerprintBlock Block,
    int Offset,
    IReadOnlyList<int> MatchedIndexes,
    IReadOnlyList<string> Hits,
    int Score);

readonly record struct EntrySnapshotRow(
    string Source,
    string Kind,
    uint Id,
    nint Address,
    string Metadata,
    System.Numerics.Vector2? Grid,
    float? Distance,
    string Components,
    int? IconComplete,
    string StateValues);

static class Win
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool GetClientRect(nint h, out RECT r);
}
