#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";

const API = "http://localhost:7777";

async function api(path, method = "GET", body = null) {
  const opts = { method, headers: {} };
  if (body) {
    opts.headers["Content-Type"] = "application/json";
    opts.body = JSON.stringify(body);
  }
  const r = await fetch(`${API}${path}`, opts);
  return await r.json();
}

const server = new McpServer({
  name: "poe2radar",
  version: "1.0.0",
});

// ── Game State ──

server.tool("game_state", "Get current game state: area, HP, mana, entity counts", {}, async () => {
  const s = await api("/state");
  return { content: [{ type: "text", text: JSON.stringify(s, null, 2) }] };
});

// ── Entities ──

server.tool("get_entities",
  "List entities in the current zone. Filter by category, alive status, search text, radius from player",
  {
    category: z.string().optional().describe("Filter: Monster, Npc, Chest, Transition, Player, Other"),
    alive: z.boolean().optional().describe("Only alive entities"),
    search: z.string().optional().describe("Search metadata path"),
    radius: z.number().optional().describe("Max grid distance from player"),
    limit: z.number().optional().describe("Max results (default 100)"),
  },
  async ({ category, alive, search, radius, limit }) => {
    let q = `?limit=${limit || 100}`;
    if (category) q += `&category=${category}`;
    if (alive) q += `&alive=true`;
    if (radius) q += `&radius=${radius}`;
    let entities = await api(`/entities${q}`);
    if (search) entities = entities.filter(e => e.metadata.toLowerCase().includes(search.toLowerCase()));
    return { content: [{ type: "text", text: JSON.stringify(entities, null, 2) }] };
  }
);

// ── Landmarks ──

server.tool("get_landmarks", "List terrain landmarks in the current zone", {}, async () => {
  const lm = await api("/landmarks");
  return { content: [{ type: "text", text: JSON.stringify(lm, null, 2) }] };
});

// ── Watched Entities ──

server.tool("get_watched", "List all watched entity patterns with nicknames", {}, async () => {
  const w = await api("/api/watched");
  return { content: [{ type: "text", text: JSON.stringify(w, null, 2) }] };
});

server.tool("watch_entity",
  "Add an entity pattern to the watchlist with a custom nickname shown on the radar",
  {
    pattern: z.string().describe("Metadata path or substring to match"),
    label: z.string().describe("Nickname to display on the radar overlay"),
    color: z.string().optional().describe("Hex color like #ff5555"),
    size: z.number().optional().describe("Dot size on radar (default 7)"),
  },
  async ({ pattern, label, color, size }) => {
    await api("/api/watched", "POST", { pattern, label, color: color || "#ff5555", enabled: true, size: size || 7 });
    return { content: [{ type: "text", text: `Watching: ${pattern} as "${label}"` }] };
  }
);

server.tool("unwatch_entity",
  "Remove an entity pattern from the watchlist",
  { pattern: z.string().describe("The pattern to remove") },
  async ({ pattern }) => {
    await api(`/api/watched?pattern=${encodeURIComponent(pattern)}`, "DELETE");
    return { content: [{ type: "text", text: `Removed: ${pattern}` }] };
  }
);

// ── Settings ──

server.tool("get_settings", "Get all radar settings (visibility, sizes, colors, calibration, etc.)", {}, async () => {
  const s = await api("/api/settings");
  return { content: [{ type: "text", text: JSON.stringify(s, null, 2) }] };
});

server.tool("update_settings",
  "Update radar settings. Pass any subset of settings to change.",
  { settings: z.record(z.any()).describe("Key-value pairs to update, e.g. {showMinimap: true, minimapSize: 300}") },
  async ({ settings }) => {
    const result = await api("/api/settings", "POST", settings);
    return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
  }
);

// ── Pathing ──

server.tool("get_pathing", "Get pathing targets and which one is currently active", {}, async () => {
  const p = await api("/api/pathing");
  return { content: [{ type: "text", text: JSON.stringify(p, null, 2) }] };
});

server.tool("set_path_target",
  "Set what entity type to path toward on the radar",
  {
    pattern: z.string().describe("Entity metadata pattern to path to"),
    label: z.string().optional().describe("Display label"),
  },
  async ({ pattern, label }) => {
    await api("/api/pathing", "POST", { pattern, label: label || pattern, enabled: true });
    return { content: [{ type: "text", text: `Pathing to: ${pattern}` }] };
  }
);

// ── Auto-Skill Rules ──

server.tool("get_rules", "Get auto-skill rules configuration", {}, async () => {
  const r = await api("/api/rules");
  return { content: [{ type: "text", text: JSON.stringify(r, null, 2) }] };
});

server.tool("add_rule",
  "Add an auto-skill rule: if conditions met, press key",
  {
    name: z.string().describe("Rule name"),
    key: z.string().describe("Key to press: Q, W, E, R, 1, 2, 3, etc."),
    cooldown: z.number().optional().describe("Seconds between presses (default 2)"),
    hpBelow: z.number().optional().describe("Trigger when HP below this %"),
    manaBelow: z.number().optional().describe("Trigger when mana below this %"),
    enemiesNearby: z.number().optional().describe("Trigger when this many enemies nearby"),
  },
  async ({ name, key, cooldown, hpBelow, manaBelow, enemiesNearby }) => {
    const vk = key.length === 1 ? key.toUpperCase().charCodeAt(0) : 0x51;
    await api("/api/rules", "POST", {
      name, key: vk, enabled: true,
      cooldownSec: cooldown || 2,
      hpBelow: hpBelow || null,
      manaBelow: manaBelow || null,
      enemiesNearby: enemiesNearby || null,
    });
    return { content: [{ type: "text", text: `Rule added: ${name} (key ${key})` }] };
  }
);

// ── Entity Database ──

server.tool("search_database",
  "Search the GGPK entity database (6692 entities) by keyword",
  { query: z.string().describe("Search term, e.g. 'Waypoint', 'Zombie', 'Chest'") },
  async ({ query }) => {
    const db = await api("/api/database");
    const q = query.toLowerCase();
    const results = db.filter(p => p.toLowerCase().includes(q)).slice(0, 50);
    return { content: [{ type: "text", text: `${results.length} matches:\n${results.join("\n")}` }] };
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);
