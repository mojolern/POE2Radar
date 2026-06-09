using POE2Radar.Core;
using POE2Radar.Core.Game;

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

using var process = ProcessHandle.AttachToPoE();
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
Console.WriteLine("  --atlas-probe [--atlas-child N] [--atlas-dump-node 0xADDR]  discover Atlas panel/node UI candidates");
Console.WriteLine("  --atlas-snapshot [--atlas-samples N]  validate the Core Atlas snapshot reader");
Console.WriteLine("  --atlas-rect-scan [--atlas-samples N]  scan Atlas nodes for final screen/client rect offsets");
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
    var terrain = ai + 0x8A0;
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

// ── Watch: poll as the player plays, logging an AreaInstance snapshot on every area
// change so the area-hash/level offsets can be diffed out across zones. Resolves the
// GameState slot once (AOB), then cheap chain derefs each poll. Run in the background and
// inspect the log. Each area block also reads a few candidate fields so drift is obvious.
static int RunWatch(ProcessHandle process, MemoryReader reader)
{
    nint slot = 0;
    foreach (var pat in AobPatterns.GameStateRefs)
        foreach (var s in AobScanner.ScanForResolvedAddresses(process, reader, pat).Distinct())
        {
            if (new Poe2Live(reader, s).TryResolve(out _, out _, out _)) { slot = s; break; }
            if (slot != 0) break;
        }
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
    return int.TryParse(args[idx + 1], out var v) ? v : null;
}

static nint? TryGetHexArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    if (idx < 0 || idx + 1 >= args.Length) return null;
    var s = args[idx + 1];
    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
    return long.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? (nint)v : null;
}

static class Win
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool GetClientRect(nint h, out RECT r);
}
