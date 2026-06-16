namespace POE2Radar.Overlay.Web;

public static class DashboardHtml
{
    public const string Page = """
<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>POE2Radar Console</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{background:#1a1a24;color:#e0e0e0;font-family:'Segoe UI',sans-serif;padding:16px;max-width:900px;margin:0 auto}
h1{font-size:18px;color:#78b4ff;margin-bottom:8px}
h2{font-size:14px;color:#78b4ff;margin:12px 0 6px}
.status{background:#252530;padding:8px 12px;border-radius:6px;margin-bottom:12px;font-size:13px;color:#aaa}
.status span{color:#fff}
.tabs{display:flex;gap:4px;margin-bottom:8px;flex-wrap:wrap}
.tab{padding:6px 16px;background:#252530;border:none;color:#aaa;cursor:pointer;border-radius:6px 6px 0 0;font-size:13px}
.tab.active{background:#2a2a3a;color:#78b4ff}
.panel{display:none;background:#2a2a3a;border-radius:0 6px 6px 6px;padding:12px}
.panel.active{display:block}
input[type=text]{background:#1e1e28;border:1px solid #444;color:#fff;padding:4px 8px;border-radius:4px;font-size:13px}
input[type=color]{width:36px;height:24px;border:1px solid #555;background:#1e1e28;cursor:pointer;vertical-align:middle;border-radius:3px}
input[type=number]{background:#1e1e28;border:1px solid #444;color:#fff;padding:4px 8px;border-radius:4px;width:80px;font-size:13px}
select{background:#1e1e28;border:1px solid #444;color:#fff;padding:4px 6px;border-radius:4px;font-size:12px}
input[type=range]{width:150px;vertical-align:middle}
input.num-val{width:64px;background:#1e1e28;border:1px solid #444;color:#78b4ff;border-radius:3px;padding:2px 4px;font-size:12px;text-align:right}
.search{margin-bottom:8px;display:flex;gap:8px;align-items:center}
.search input[type=text]{width:250px}
.filter-btns{display:flex;gap:4px;flex-wrap:wrap;margin-bottom:8px}
.filter-btn{padding:3px 10px;background:#333;border:1px solid #555;color:#ccc;cursor:pointer;border-radius:4px;font-size:12px}
.filter-btn.active{background:#3a5080;border-color:#78b4ff;color:#fff}
table{width:100%;border-collapse:collapse;font-size:12px}
th{text-align:left;padding:4px 8px;background:#1e1e28;color:#78b4ff;position:sticky;top:0}
td{padding:4px 8px;border-bottom:1px solid #333}
tr:hover{background:#333}
tr.watched{background:#2a3a2a}
.scrollbox{max-height:450px;overflow-y:auto}
.btn{padding:3px 10px;border:none;border-radius:4px;cursor:pointer;font-size:12px}
.btn.on{background:#314d32;color:#9f9}
.btn-add{background:#2a5a2a;color:#8f8}.btn-add:hover{background:#3a7a3a}
.btn-rm{background:#5a2a2a;color:#f88}.btn-rm:hover{background:#7a3a3a}
.btn-save{background:#3a5080;color:#9cf;padding:6px 20px;font-size:13px}.btn-save:hover{background:#4a60a0}
.watched-item{display:flex;align-items:center;gap:8px;padding:6px 8px;background:#252530;border-radius:4px;margin-bottom:4px}
.watched-item .pattern{flex:1;font-family:monospace;font-size:11px;color:#999;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.watched-item .label{color:#afc;font-size:13px;font-weight:bold;min-width:100px}
.watched-item .swatch{width:20px;height:20px;border-radius:3px;border:1px solid #555;flex-shrink:0}
.add-form{display:flex;gap:6px;align-items:center;padding:8px;background:#252530;border-radius:6px;margin-bottom:8px;flex-wrap:wrap}
.meta-short{max-width:300px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-family:monospace;font-size:11px}
.cat{padding:2px 6px;border-radius:3px;font-size:11px;font-weight:bold}
.cat-Monster{background:#4a2020;color:#f55}.cat-Player{background:#1a3a4a;color:#5cf}
.cat-Npc{background:#4a4a10;color:#fd5}.cat-Chest{background:#4a3010;color:#f90}
.cat-Transition{background:#1a4a2a;color:#6f9}.cat-Other{background:#333;color:#aaa}
.rarity-Magic{color:#79a8ff}.rarity-Rare{color:#ffd926}.rarity-Unique{color:#ff7300}
.source-state{display:inline-block;padding:2px 6px;border-radius:3px;font-size:10px;font-weight:bold;white-space:nowrap}
.source-awake{background:#173d2a;color:#7fffb2}
.source-sleeping{background:#3b3152;color:#d6b8ff}
.auto-refresh{font-size:12px;color:#666;margin-left:auto}
.db-path{font-family:monospace;font-size:11px;color:#bbb}
.db-cat{color:#78b4ff;font-size:11px}
.count{background:#333;padding:2px 6px;border-radius:10px;font-size:11px;color:#aaa;margin-left:4px}
.setting-row{display:flex;align-items:center;gap:10px;margin-bottom:6px;min-height:28px}
.setting-row label{width:160px;font-size:13px;color:#ccc;flex-shrink:0}
.setting-row .val{font-size:12px;color:#78b4ff;width:50px;text-align:right}
.hp-rarity-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.hp-rarity-controls label{width:auto;display:flex;align-items:center;gap:6px;color:var(--ink-dim);font-size:12px}
.hp-rarity-controls input[type=color]{width:42px;height:26px}
.section{background:#252530;border-radius:6px;padding:10px 14px;margin-bottom:10px}
.section h3{font-size:13px;color:#78b4ff;margin-bottom:8px}
.settings-subtabs{position:sticky;top:0;z-index:3;display:flex;gap:4px;flex-wrap:wrap;background:#2a2a3a;padding:2px 0 10px;margin-bottom:2px}
.settings-subtab{padding:5px 12px;background:#20202b;border:1px solid #3b3b49;color:#aaa;cursor:pointer;border-radius:4px;font-size:12px}
.settings-subtab.active{background:#35435e;border-color:#6082bd;color:#fff}
.saved{color:#5f5;font-size:12px;opacity:0;transition:opacity 0.3s}
.saved.show{opacity:1}
.panel.panel-with-rail.active{display:grid;grid-template-columns:minmax(0,1fr) 116px;gap:12px;align-items:start}
.panel-main{min-width:0}
.panel-title{display:flex;justify-content:space-between;align-items:center;margin-bottom:10px}
.action-rail{position:sticky;top:12px;display:flex;flex-direction:column;gap:6px;align-items:stretch;background:#202838;border:1px solid #38445f;border-radius:6px;padding:8px;z-index:2}
.action-rail .btn{width:100%;padding-left:8px;padding-right:8px}
.action-rail .saved{text-align:center;min-height:16px}
.mechanic-row{display:grid;grid-template-columns:90px 64px 94px 52px 110px 72px 1fr;gap:8px;align-items:center;margin-bottom:6px}
.mechanic-row input[type=text],.mechanic-row select{background:#1e1e28;border:1px solid #444;color:#fff;padding:4px 6px;border-radius:4px;font-size:12px;min-width:0}
.mechanic-match{font-family:monospace;font-size:11px;color:#888;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.atlas-toolbar{display:flex;gap:8px;align-items:center;margin-bottom:10px;flex-wrap:wrap}
.atlas-toolbar input{background:#1e1e28;border:1px solid #444;color:#fff;padding:5px 8px;border-radius:4px;font-size:13px;min-width:260px}
.atlas-chip{display:inline-block;padding:1px 6px;border-radius:10px;background:#1e1e28;border:1px solid #444;color:#aaa;font-size:11px;margin:1px 3px 1px 0}
.atlas-pin{background:#4a3a12;color:#ffd76d}
.atlas-muted{color:#777;font-size:12px}
.atlas-rule-row{display:grid;grid-template-columns:44px 44px 42px 30px minmax(160px,1fr) 56px 90px;gap:8px;align-items:center;padding:5px 8px;border-bottom:1px solid #333;font-size:12px}
.atlas-rule-row button{padding:2px 7px;border:1px solid #555;border-radius:4px;background:#252530;color:#999;cursor:pointer}
.atlas-rule-row button.on{background:#314d32;color:#9f9;border-color:#5a8}
.atlas-rule-row button.arrow.on{background:#4a3a12;color:#ffd76d;border-color:#b88}
.atlas-rule-row button.rename{padding:2px 5px;color:#78b4ff}
.display-rule{background:#252530;border:1px solid #393947;border-radius:6px;margin-bottom:6px;overflow:hidden}
.display-rule.off{opacity:.55}
.display-rule-head{display:grid;grid-template-columns:24px 24px minmax(100px,180px) minmax(120px,1fr) auto auto;gap:7px;align-items:center;padding:7px 9px;cursor:pointer}
.display-rule-summary{font-size:11px;color:#888;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.display-rule-body{border-top:1px solid #393947;padding:9px;display:grid;gap:8px}
.display-rule-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(145px,1fr));gap:7px}
.display-rule-grid label,.display-rule-flags label{font-size:11px;color:#aaa;display:flex;gap:5px;align-items:center}
.display-rule-grid input[type=text],.display-rule-grid select{width:100%;min-width:0}
.display-rule-flags{display:flex;gap:12px;align-items:center;flex-wrap:wrap}
.display-rule-cats{display:flex;gap:5px;flex-wrap:wrap}
.display-rule-cats label{font-size:11px;background:#1e1e28;border:1px solid #444;border-radius:4px;padding:3px 6px}
.display-rule-actions{display:flex;gap:6px;align-items:center;flex-wrap:wrap}
.display-rule-actions input[type=text]{min-width:130px;flex:1}
.display-rule-order{display:flex;gap:3px}
.display-rule-order .btn{padding:2px 6px;background:#30303d;color:#aaa}
.display-rule-note{font-size:11px;color:#888;line-height:1.5}
.price-status{font-size:11px;color:#aaa;background:#1e1e28;border-radius:4px;padding:7px 9px;margin-bottom:8px}
@media(max-width:760px){
  .panel.panel-with-rail.active{display:block}
  .action-rail{position:sticky;bottom:8px;top:auto;flex-direction:row;flex-wrap:wrap;margin-top:10px}
  .action-rail .btn{width:auto}
}

/* Unified console shell, adapted from Sikaka's dashboard while preserving this fork's tools. */
:root{
  --bg:#0a0907;--bg2:#100d09;--panel:#15110b;--panel2:#1b1610;
  --line:#3a2f1d;--line-soft:#271f14;--ink:#e8dcc2;--ink-dim:#9c8e72;
  --ink-faint:#6b5f49;--gold:#c8a049;--gold-bright:#ecca7e;--gold-deep:#8a6d34;
  --blood:#9c342a;--blood-bright:#d6584a;--good:#79b06a;--magic:#7f93ff;
  --rare:#f1e36b;--unique:#d2641e;--poi:#4bb3c4;--shadow:0 18px 40px -20px rgba(0,0,0,.9)
}
html,body{height:100%}
body{
  max-width:none;margin:0;padding:0;overflow:hidden;color:var(--ink);
  background:radial-gradient(120% 90% at 50% -10%,#1a150d 0%,var(--bg) 58%) fixed;
  font-family:Consolas,"Cascadia Mono",ui-monospace,monospace;font-size:13px;line-height:1.45
}
body:before{content:"";position:fixed;inset:0;pointer-events:none;z-index:999;background:radial-gradient(120% 120% at 50% 40%,transparent 58%,rgba(0,0,0,.5) 100%);mix-blend-mode:multiply}
.dashboard-shell{display:grid;grid-template-rows:auto minmax(0,1fr);height:100vh}
.dashboard-header{
  display:flex;align-items:center;gap:18px;padding:13px 24px;border-bottom:1px solid var(--line);
  background:linear-gradient(180deg,rgba(30,24,14,.78),rgba(10,9,7,.82));min-width:0
}
.brand{display:flex;align-items:baseline;gap:12px;white-space:nowrap}
.brand h1{font-family:Georgia,serif;font-size:21px;letter-spacing:.14em;color:var(--gold-bright);margin:0;text-shadow:0 1px #000,0 0 22px rgba(200,160,73,.22)}
.brand small{font-size:9px;letter-spacing:.3em;text-transform:uppercase;color:var(--ink-faint)}
.header-spacer{flex:1}
.area-chip{font-family:Georgia,serif;letter-spacing:.06em;color:var(--ink);border:1px solid var(--line);padding:5px 13px;border-radius:2px;background:var(--panel);font-size:12px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:42vw}
.connection{display:flex;align-items:center;gap:8px;color:var(--ink-dim);font-size:10px;letter-spacing:.12em;text-transform:uppercase;white-space:nowrap}
.connection-dot{width:9px;height:9px;border-radius:50%;background:var(--blood)}
.connection.live .connection-dot{background:var(--good);box-shadow:0 0 9px rgba(121,176,106,.55)}
.dashboard-body{display:grid;grid-template-columns:260px minmax(0,1fr);min-height:0}
.dashboard-sidebar{overflow:auto;border-right:1px solid var(--line);padding:20px;background:linear-gradient(180deg,rgba(20,16,10,.64),transparent 240px)}
.sidebar-section{font-family:Georgia,serif;font-size:11px;letter-spacing:.2em;text-transform:uppercase;color:var(--gold);margin:22px 0 10px;display:flex;align-items:center;gap:9px}
.sidebar-section:after{content:"";flex:1;height:1px;background:linear-gradient(90deg,var(--line),transparent)}
.vital{margin-bottom:14px}.vital-label{display:flex;justify-content:space-between;color:var(--ink-dim);font-size:10px;letter-spacing:.14em;text-transform:uppercase;margin-bottom:5px}
.vital-label b{color:var(--ink);font-weight:600}.vital-bar{height:8px;border:1px solid var(--line);background:#0c0a07;overflow:hidden}
.vital-bar i{display:block;height:100%;transition:width .25s ease}.vital-bar.hp i{background:linear-gradient(90deg,#6e1f18,var(--blood-bright))}.vital-bar.mana i{background:linear-gradient(90deg,#23306e,var(--magic))}
.side-kv{display:flex;justify-content:space-between;gap:10px;padding:5px 0;border-bottom:1px dotted var(--line-soft);font-size:11px}
.side-kv span:first-child{color:var(--ink-faint)}.side-kv span:last-child{color:var(--ink);text-align:right;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.census{display:grid;grid-template-columns:1fr 1fr;gap:7px}.census-item{border:1px solid var(--line-soft);background:var(--panel);padding:8px 9px}
.census-item b{display:block;font-family:Georgia,serif;color:var(--gold-bright);font-size:19px;line-height:1}.census-item span{display:block;color:var(--ink-faint);font-size:8px;letter-spacing:.12em;text-transform:uppercase;margin-top:4px}
#status{background:transparent;padding:0;margin:0;color:var(--ink-dim);font-size:11px;line-height:1.55}
.dashboard-main{display:flex;flex-direction:column;min-width:0;min-height:0}
.dashboard-main>.tabs{flex:none;overflow-x:auto;flex-wrap:nowrap;gap:2px;margin:0;padding:13px 22px 0;border-bottom:1px solid var(--line);background:rgba(10,9,7,.45)}
.dashboard-main>.tabs::-webkit-scrollbar{height:5px}
.dashboard-main>.tabs .tab{flex:none;font-family:Georgia,serif;font-size:10px;letter-spacing:.11em;text-transform:uppercase;color:var(--ink-faint);background:transparent;border:1px solid transparent;border-bottom:none;border-radius:3px 3px 0 0;padding:8px 13px;position:relative;top:1px}
.dashboard-main>.tabs .tab:hover{color:var(--ink-dim)}
.dashboard-main>.tabs .tab.active{color:var(--gold-bright);background:var(--panel);border-color:var(--line)}
.panel{background:transparent;border-radius:0;padding:22px;min-height:0}
.panel.active{display:block;overflow:auto;flex:1}
.panel.panel-with-rail.active{overflow:auto;grid-template-columns:minmax(0,1fr) 116px}
h2{font-family:Georgia,serif;font-size:13px;letter-spacing:.08em;color:var(--gold-bright)}
.section{background:var(--panel);border:1px solid var(--line);border-radius:4px;padding:14px 16px;margin-bottom:12px;box-shadow:var(--shadow)}
.section h3{font-family:Georgia,serif;font-size:11px;letter-spacing:.14em;text-transform:uppercase;color:var(--gold);margin-bottom:9px}
.scrollbox{max-height:calc(100vh - 245px);border:1px solid var(--line-soft);background:rgba(12,10,7,.36)}
table{color:var(--ink)}th{background:#0c0a07;color:var(--gold);font-size:9px;letter-spacing:.11em;text-transform:uppercase;padding:7px 9px;border-bottom:1px solid var(--line)}
td{padding:6px 9px;border-bottom:1px solid var(--line-soft)}tr:hover{background:rgba(200,160,73,.05)}tr.watched{background:rgba(121,176,106,.08)}
input[type=text],input[type=number],input[type=search],select,input.num-val{font-family:inherit;background:#0c0a07;border:1px solid var(--line);color:var(--ink);border-radius:2px}
input:focus,select:focus{outline:none;border-color:var(--gold-deep)}
input[type=range]{accent-color:var(--gold)}
.btn,.filter-btn,.settings-subtab{font-family:inherit;border:1px solid var(--line);border-radius:2px;background:var(--panel2);color:var(--ink-dim)}
.btn:hover,.filter-btn:hover,.settings-subtab:hover{border-color:var(--gold-deep);color:var(--ink)}
.btn-save,.filter-btn.active,.settings-subtab.active{background:var(--gold-deep)!important;border-color:var(--gold)!important;color:#160f06!important}
.btn-add{background:#263821;color:#9dcc8e;border-color:#47653d}.btn-rm{background:#3e1713;color:#ef8378;border-color:#6c2a24}
.action-rail{top:0;background:var(--panel);border-color:var(--line);border-radius:4px}
.saved{color:var(--good)}
.settings-subtabs{top:-22px;background:var(--bg);padding:4px 0 10px}
.display-rule,.watched-item,.add-form,.price-status{background:var(--panel2);border-color:var(--line);border-radius:3px}
.display-rule-head{padding:9px 10px}.display-rule-body{border-color:var(--line-soft);padding:12px}.display-rule-summary,.display-rule-note,.atlas-muted,.mechanic-match,.auto-refresh{color:var(--ink-faint)}
.display-rule-grid label{flex-direction:column;align-items:stretch;gap:4px}
.display-rule-grid label:has(input[type=checkbox]){flex-direction:row;align-items:center}
.display-rule-cats label{background:#0c0a07;border-color:var(--line)}
.source-awake{background:#173d2a;color:#7fffb2}.source-sleeping{background:#332744;color:#d6b8ff}
.cat{border:1px solid currentColor;background:transparent!important}.cat-Monster{color:#e26559}.cat-Player{color:#5ec4dd}.cat-Npc{color:#dbc55c}.cat-Chest{color:#e69a45}.cat-Transition{color:#7bcf8b}.cat-Other{color:#aaa}
.rarity-Magic{color:var(--magic)}.rarity-Rare{color:var(--rare)}.rarity-Unique{color:var(--unique)}
.atlas-rule-row{border-color:var(--line-soft)}.atlas-rule-row button{background:#0c0a07;border-color:var(--line);color:var(--ink-dim)}
.atlas-rule-row[style]{background:#0c0a07!important;color:var(--gold)!important}
#inspEntitySummary{background:var(--panel2)!important;border-color:var(--line)!important}
#inspComponents span[style*="background"]{background:#0c0a07!important;border:1px solid var(--line);border-radius:2px!important;color:var(--ink-dim)!important}
#inspResults h3{color:var(--gold-bright)!important}
#inspResults table{background:rgba(12,10,7,.42)}
#inspEntity+button{font-family:inherit;padding:5px 12px!important;cursor:pointer;background:var(--panel2);color:var(--ink);border:1px solid var(--line);border-radius:2px}
code{color:var(--gold-bright)}
::-webkit-scrollbar{width:9px;height:9px}::-webkit-scrollbar-thumb{background:var(--line);border-radius:5px;border:2px solid var(--bg)}::-webkit-scrollbar-track{background:transparent}
@media(max-width:900px){
  .dashboard-body{grid-template-columns:1fr}.dashboard-sidebar{display:none}.dashboard-header{padding:11px 14px}.brand small{display:none}.area-chip{max-width:48vw}
  .panel{padding:14px}.dashboard-main>.tabs{padding-left:12px}.panel.panel-with-rail.active{display:block}.action-rail{position:sticky;bottom:0;top:auto;flex-direction:row;flex-wrap:wrap;margin-top:10px}
}
@media(max-width:560px){.area-chip{display:none}.dashboard-header{gap:10px}.brand h1{font-size:17px}.display-rule-head{grid-template-columns:22px 18px minmax(90px,1fr) auto auto}.display-rule-summary{display:none}}
</style></head><body>
<div class="dashboard-shell">
<header class="dashboard-header">
  <div class="brand"><h1>POE2RADAR</h1><small>Field Console</small></div>
  <div class="header-spacer"></div>
  <div class="area-chip" id="areaChip">Waiting for game</div>
  <div class="connection" id="connection"><span class="connection-dot"></span><span id="connectionText">offline</span></div>
</header>
<div class="dashboard-body">
<aside class="dashboard-sidebar">
  <div class="vital"><div class="vital-label"><span>Life</span><b id="sideHp">--</b></div><div class="vital-bar hp"><i id="sideHpBar" style="width:0"></i></div></div>
  <div class="vital"><div class="vital-label"><span>Mana</span><b id="sideMana">--</b></div><div class="vital-bar mana"><i id="sideManaBar" style="width:0"></i></div></div>
  <div class="sidebar-section">Zone</div>
  <div class="side-kv"><span>Area</span><span id="sideArea">--</span></div>
  <div class="side-kv"><span>Code</span><span id="sideCode">--</span></div>
  <div class="side-kv"><span>Act / Level</span><span id="sideLevel">--</span></div>
  <div class="side-kv"><span>Map</span><span id="sideMap">--</span></div>
  <div class="side-kv"><span>Auto-flask</span><span id="sideFlask">--</span></div>
  <div class="sidebar-section">Census</div>
  <div class="census">
    <div class="census-item"><b id="sideEntities">0</b><span>Entities</span></div>
    <div class="census-item"><b id="sideMonsters">0</b><span>Monsters</span></div>
    <div class="census-item"><b id="sideChests">0</b><span>Chests</span></div>
    <div class="census-item"><b id="sideNpcs">0</b><span>NPCs</span></div>
  </div>
  <div class="sidebar-section">Connection</div>
  <div class="status" id="status">Connecting...</div>
</aside>
<main class="dashboard-main">
<div class="tabs">
  <button class="tab active" onclick="showTab('entities')">Live Entities</button>
  <button class="tab" onclick="showTab('watched')">Watched</button>
  <button class="tab" onclick="showTab('database')">Database</button>
  <button class="tab" onclick="showTab('display')">Display Rules</button>
  <button class="tab" onclick="showTab('settings')">Radar Settings</button>
  <button class="tab" onclick="showTab('rules')">Auto-Skills</button>
  <button class="tab" onclick="showTab('pathing')">Pathing</button>
  <button class="tab" onclick="showTab('minimap')">Minimap</button>
  <button class="tab" onclick="showTab('landmarks')">Landmarks</button>
  <button class="tab" onclick="showTab('atlas')">Atlas</button>
  <button class="tab" onclick="showTab('gamedata')">Game Data</button>
  <button class="tab" onclick="showTab('hidden')">Hidden</button>
  <button class="tab" onclick="showTab('keybinds')">Keybinds</button>
  <button class="tab" onclick="showTab('devtest')" style="color:#f88">DevTest</button>
  <button class="tab" onclick="showTab('inspector')">Inspector</button>
</div>

<!-- LIVE ENTITIES -->
<div class="panel active" id="tab-entities">
  <div class="search">
    <input type="text" id="search" placeholder="Search metadata..." oninput="filterEntities()">
    <label><input type="checkbox" id="aliveOnly" onchange="refresh()"> Alive only</label>
    <label><input type="checkbox" id="sleepingOnly" onchange="toggleSleepingOnly()"> Sleeping only</label>
    <span class="auto-refresh">Auto-refresh 2s</span>
  </div>
  <div class="filter-btns" id="catFilters"></div>
  <div class="scrollbox"><table><thead>
    <tr><th>State</th><th>Cat</th><th>Rarity</th><th>Metadata</th><th>HP</th><th>Dist</th><th></th></tr>
  </thead><tbody id="entityBody"></tbody></table></div>
</div>

<!-- WATCHED -->
<div class="panel" id="tab-watched">
  <h2>Add Custom Watch</h2>
  <div class="add-form">
    <input type="text" id="addPattern" placeholder="Metadata pattern" style="width:220px">
    <input type="text" id="addLabel" placeholder="Radar nickname" style="width:130px">
    <input type="color" id="addColor" value="#ff5555">
    <button class="btn btn-add" onclick="addWatched()">Add</button>
  </div>
  <p style="font-size:11px;color:#666;margin-bottom:8px">The nickname is displayed on the radar overlay next to the entity dot.</p>
  <div style="display:flex;gap:8px;margin-bottom:10px;align-items:center">
    <h2 style="margin:0">Current Watched</h2>
    <button class="btn btn-save" onclick="exportWatched()" style="margin-left:auto">Export JSON</button>
    <button class="btn btn-add" onclick="$('importFile').click()">Import JSON</button>
    <input type="file" id="importFile" accept=".json" style="display:none" onchange="importWatched(this)">
    <span class="saved" id="importMsg">Imported!</span>
  </div>
  <div id="watchedList"></div>
</div>

<!-- DATABASE -->
<div class="panel" id="tab-database">
  <div class="search">
    <input type="text" id="dbSearch" placeholder="Search all game entities..." style="width:350px" oninput="filterDb()">
    <label style="font-size:12px;color:#aaa"><input type="checkbox" id="dbHideJunk" checked onchange="filterDb()"> Hide junk</label>
    <span id="dbCount" class="count"></span>
  </div>
  <div class="filter-btns" id="dbCatFilters"></div>
  <div class="scrollbox"><table><thead>
    <tr><th>Category</th><th>Path</th><th></th></tr>
  </thead><tbody id="dbBody"></tbody></table></div>
</div>

<!-- DISPLAY RULES -->
<div class="panel panel-with-rail" id="tab-display">
  <div class="panel-main">
    <div class="panel-title">
      <h2 style="margin:0">Display Rules</h2>
      <span class="display-rule-note">Ordered top-to-bottom; first enabled match wins.</span>
    </div>
    <div class="section">
      <h3>Entity And Tile Rules</h3>
      <p class="display-rule-note" style="margin-bottom:8px">Blank matcher fields mean any. Metadata and mod fields accept comma-separated terms. Tile rules match terrain paths.</p>
      <datalist id="knownMods"></datalist>
      <div id="displayRuleList"></div>
      <div style="display:flex;gap:6px;flex-wrap:wrap;margin-top:8px">
        <button class="btn btn-add" onclick="addDisplayRule()">Add Blank Rule</button>
        <button class="btn" style="background:#2a4a5a;color:#5cf" onclick="addDisplayRuleFromEntity()">Add From Live Entity</button>
      </div>
    </div>
    <div class="section">
      <h3>Ground Item Pricing</h3>
      <div class="price-status" id="priceStatus">Price source not loaded.</div>
      <div class="display-rule-grid">
        <label><input type="checkbox" id="priceEnabled"> Show priced ground items</label>
        <label><input type="checkbox" id="priceRuneforge"> Show Runeforge values</label>
        <label>Highlight minimum (ex)<input type="number" id="priceMin" min="0" step="0.5"></label>
        <label>Unique minimum (ex)<input type="number" id="priceUniqueMin" min="0" step="0.5"></label>
        <label>Minimum stack quantity<input type="number" id="priceQty" min="1" step="1"></label>
        <label>League / realm<select id="priceLeague"><option value="">Auto current Softcore</option></select></label>
      </div>
      <div class="display-rule-flags" style="margin-top:8px">
        <label><input type="checkbox" class="price-cat" value="Uniques"> Uniques</label>
        <label><input type="checkbox" class="price-cat" value="Runes"> Runes</label>
        <label><input type="checkbox" class="price-cat" value="Essences"> Essences</label>
        <label><input type="checkbox" class="price-cat" value="Currency"> Currency</label>
      </div>
    </div>
  </div>
  <div class="action-rail">
    <span class="saved" id="displaySavedMsg">Saved!</span>
    <button class="btn btn-save" onclick="saveDisplayPage()">Save</button>
    <button class="btn" style="background:#2a4a5a;color:#5cf" onclick="loadDisplayPage()">Reload</button>
  </div>
</div>

<!-- RADAR SETTINGS -->
<div class="panel panel-with-rail" id="tab-settings">
  <div class="panel-main">
    <div class="panel-title">
      <h2 style="margin:0">Radar Settings</h2>
    </div>
    <div class="settings-subtabs" id="settingsSubtabs"></div>
    <div id="settingsBody"></div>
  </div>
  <div class="action-rail">
    <span class="saved" id="savedMsg">Saved!</span>
    <button class="btn btn-save" onclick="saveSettings()">Save</button>
    <button class="btn" style="background:#2a3a5a;color:#8cf" onclick="exportSettings()">Export</button>
    <button class="btn" style="background:#2a5a3a;color:#8f8" onclick="$('settingsImportFile').click()">Import</button>
    <input type="file" id="settingsImportFile" accept=".json" style="display:none" onchange="importSettings(event)">
    <button class="btn" style="background:#5a2a2a;color:#f88" onclick="resetSettings()">Reset</button>
  </div>
</div>

<!-- AUTO-SKILLS -->
<div class="panel" id="tab-rules">
  <div style="display:flex;align-items:center;gap:10px;margin-bottom:10px">
    <h2 style="margin:0">Auto-Skill Rules</h2>
    <button class="btn btn-save" id="rulesToggle" onclick="toggleRules()">Loading...</button>
    <span style="font-size:11px;color:#888">F8 also toggles in-game</span>
  </div>
  <p style="font-size:12px;color:#aaa;margin-bottom:10px">
    Each rule: if ALL conditions are true → press the key. Rules checked every tick. Cooldown prevents spam.
  </p>
  <div id="rulesList"></div>
  <div class="section" style="margin-top:10px">
    <h3>Add Rule</h3>
    <div style="display:flex;gap:6px;flex-wrap:wrap;align-items:center">
      <input type="text" id="ruleAddName" placeholder="Name" style="width:100px">
      <label style="font-size:12px;color:#ccc">Key:</label>
      <input type="text" id="ruleAddKey" placeholder="e.g. Q, 1, R" style="width:50px">
      <label style="font-size:12px;color:#ccc">CD:</label>
      <input type="number" id="ruleAddCd" value="2" min="0.1" max="30" step="0.5" style="width:50px">
      <label style="font-size:12px;color:#ccc">HP&lt;</label>
      <input type="number" id="ruleAddHp" placeholder="-" min="0" max="100" style="width:50px">
      <label style="font-size:12px;color:#ccc">Mana&lt;</label>
      <input type="number" id="ruleAddMana" placeholder="-" min="0" max="100" style="width:50px">
      <label style="font-size:12px;color:#ccc">ES&lt;</label>
      <input type="number" id="ruleAddEs" placeholder="-" min="0" max="100" style="width:50px">
      <label style="font-size:12px;color:#ccc">Enemies&ge;</label>
      <input type="number" id="ruleAddEnemies" placeholder="-" min="0" max="50" style="width:50px">
      <label style="font-size:12px;color:#ccc">Boss:</label>
      <select id="ruleAddBoss" style="width:60px"><option value="">-</option><option value="true">Yes</option><option value="false">No</option></select>
      <label style="font-size:12px;color:#ccc">Wait(s):</label>
      <input type="number" id="ruleAddWait" placeholder="-" min="0" max="10" step="0.1" style="width:50px">
      <label style="font-size:12px;color:#ccc">Moving:</label>
      <select id="ruleAddMoving" style="width:60px"><option value="">-</option><option value="true">Yes</option><option value="false">No</option></select>
      <label style="font-size:12px;color:#ccc">Hold Key:</label>
      <input type="text" id="ruleAddHoldKey" placeholder="-" style="width:40px">
      <button class="btn btn-add" onclick="addRule()">Add</button>
    </div>
  </div>
</div>

<!-- PATHING -->
<div class="panel" id="tab-pathing">
  <p style="font-size:12px;color:#aaa;margin-bottom:10px">
    The radar auto-paths to the <b>nearest</b> entity matching any enabled target below.<br>
    Press <b>F7</b> in-game to cycle through targets manually.
  </p>
  <div class="add-form">
    <input type="text" id="pathAddPattern" placeholder="Metadata pattern" style="width:200px">
    <input type="text" id="pathAddLabel" placeholder="Label" style="width:120px">
    <button class="btn btn-add" onclick="addPathTarget()">Add</button>
    <button class="btn" style="background:#2a4a5a;color:#5cf;margin-left:auto" onclick="cyclePathTarget()">Cycle Next (F7)</button>
  </div>
  <div id="pathingList" style="margin-top:8px"></div>
</div>

<!-- MINIMAP -->
<div class="panel panel-with-rail" id="tab-minimap">
  <div class="panel-main">
    <div class="panel-title">
      <h2 style="margin:0">Minimap Settings</h2>
    </div>
    <div id="minimapSettingsBody"></div>
  </div>
  <div class="action-rail">
    <span class="saved" id="mmSavedMsg">Saved!</span>
    <button class="btn btn-save" onclick="saveSettings();$('mmSavedMsg').classList.add('show');setTimeout(()=>$('mmSavedMsg').classList.remove('show'),1500)">Save</button>
  </div>
</div>

<!-- LANDMARKS -->
<div class="panel" id="tab-landmarks">
  <div class="search"><input type="text" id="lmSearch" placeholder="Search landmarks..." style="width:300px" oninput="filterLandmarks()"></div>
  <div class="scrollbox"><table><thead>
    <tr><th>Name</th><th>Path</th><th>Tiles</th><th>Dist</th><th></th></tr>
  </thead><tbody id="lmBody"></tbody></table></div>
</div>

<!-- ATLAS -->
<div class="panel" id="tab-atlas">
  <div class="section">
    <h3>Atlas Assist</h3>
    <div id="atlasSettingsBody"></div>
    <div style="display:flex;gap:8px;align-items:center;margin-top:8px">
      <button class="btn btn-save" onclick="saveAtlasSettings()">Save Atlas Settings</button>
      <span class="saved" id="atlasSavedMsg">Saved!</span>
    </div>
  </div>
  <div class="atlas-toolbar">
    <input type="search" id="atlasSearch" placeholder="Search map names or content..." oninput="renderAtlas()">
    <button class="btn btn-save" onclick="loadAtlas()">Refresh</button>
    <button class="btn" style="background:#4a3a1a;color:#ffd76d" onclick="clearAtlasPins()">Clear Pins</button>
    <span id="atlasStatus" class="atlas-muted">Open the Atlas in-game, then refresh.</span>
  </div>
  <div class="section">
    <h3>Ring Rules</h3>
    <div class="atlas-toolbar">
      <input type="search" id="atlasRuleSearch" placeholder="Search content/map rule names..." oninput="renderAtlasRules()">
      <button class="btn" id="atlasRuleTrackFilter" onclick="toggleAtlasRuleFilter('track')">Tracked</button>
      <button class="btn" id="atlasRuleArrowFilter" onclick="toggleAtlasRuleFilter('arrow')">Arrows</button>
      <button class="btn" style="background:#5a2a2a;color:#f88" onclick="clearAtlasRules()">Clear Rules</button>
      <span class="atlas-muted" id="atlasRuleStatus">Rules color rings by map/content type.</span>
    </div>
    <div class="scrollbox" style="max-height:260px">
      <div class="atlas-rule-row" style="position:sticky;top:0;background:#1e1e28;color:#78b4ff;font-weight:bold">
        <span>Track</span><span>Arrow</span><span>Color</span><span></span><span>Name</span><span>Count</span><span>Type</span>
      </div>
      <div id="atlasRuleBody"></div>
    </div>
  </div>
  <div class="scrollbox"><table><thead>
    <tr><th>Map</th><th>Content</th><th>State</th><th>Pos</th><th></th></tr>
  </thead><tbody id="atlasBody"></tbody></table></div>
</div>

<!-- DEVTEST -->
<div class="panel panel-with-rail" id="tab-devtest">
  <div class="panel-main">
  <div style="background:#3a1a1a;border:1px solid #f44;border-radius:6px;padding:10px;margin-bottom:12px">
    <h2 style="margin:0;color:#f88">⚠ DevTest — Game Memory Writes</h2>
    <p style="color:#faa;font-size:12px;margin:6px 0 0">These write directly to game entity memory. <b>WILL LIKELY CRASH</b> — component byte field offsets need re-validation for the current game patch. The header delta between ComponentList pointers and setter function bases has not been resolved yet. Use at your own risk.</p>
  </div>
  <div id="devtestBody"></div>
  </div>
  <div class="action-rail">
    <button class="btn btn-save" onclick="saveSettings()">Save</button>
  </div>
</div>

<!-- GAME DATA -->
<div class="panel" id="tab-gamedata">
  <div style="display:flex;gap:8px;margin-bottom:10px">
    <button class="btn btn-save" onclick="loadGdAreas()">World Areas</button>
    <button class="btn btn-save" onclick="loadGdBuffs()">Buffs</button>
    <button class="btn btn-save" onclick="loadGdPins()">Map Pins (current zone)</button>
  </div>
  <div class="search"><input type="text" id="gdSearch" placeholder="Search..." style="width:300px" oninput="searchGameData()"></div>
  <div class="scrollbox" id="gdResults" style="font-size:12px"></div>
</div>

<!-- HIDDEN ENTITIES -->
<div class="panel" id="tab-hidden">
  <h2>Hidden Entities / Landmarks</h2>
  <p style="font-size:12px;color:#aaa;margin-bottom:10px">
    Patterns listed here are hidden from the radar overlay. <b>Ctrl+Click</b> on any entity or landmark in-game to add it here.
    You can also use the <b>Hide</b> buttons in the Live Entities and Landmarks tabs.<br>
    <b>Wildcards:</b> Use <code>*</code> (any chars) and <code>?</code> (single char). Example: <code>*StrongBox</code> hides everything ending in StrongBox.
    Without wildcards, patterns match as substring (current behavior).
  </p>
  <div class="add-form">
    <input type="text" id="hiddenAddPattern" placeholder="Pattern (e.g. AbyssCrack, *StrongBox, Breach*)" style="width:280px">
    <button class="btn btn-add" onclick="addHidden()">Add</button>
  </div>
  <div style="margin-top:8px" id="hiddenList"></div>
</div>

<!-- KEYBINDS -->
<div class="panel panel-with-rail" id="tab-keybinds">
  <div class="panel-main">
  <h2>Hotkey Bindings</h2>
  <p style="font-size:12px;color:#aaa;margin-bottom:10px">
    Click a key field and press any key to rebind. Click <b>Save</b> to persist. Changes take effect immediately after save.
  </p>
  <div id="keybindsBody"></div>
  </div>
  <div class="action-rail">
    <button class="btn btn-save" onclick="saveKeybinds()">Save</button>
    <span class="saved" id="kbSavedMsg">Saved!</span>
  </div>
</div>

<!-- INSPECTOR -->
<div class="panel" id="tab-inspector">
  <div style="display:flex;gap:10px;align-items:center;margin-bottom:10px;flex-wrap:wrap">
    <select id="inspEntity" onchange="inspectEntity()" style="flex:1;min-width:200px;padding:4px;background:#1e1e1e;color:#eee;border:1px solid #555"></select>
    <button onclick="loadInspectorEntities()" style="padding:4px 12px;cursor:pointer">Refresh</button>
    <label style="font-size:12px"><input type="checkbox" id="inspAutoRefresh" checked> Auto-refresh</label>
    <span style="font-size:12px;color:#888" id="inspStatus"></span>
  </div>
  <div id="inspEntitySummary" style="display:none;background:#20202b;border:1px solid #3b3b49;padding:8px 10px;margin-bottom:10px;font-size:12px"></div>
  <div id="inspComponents" style="display:flex;flex-wrap:wrap;gap:4px;margin-bottom:10px"></div>
  <div class="scrollbox" id="inspResults" style="font-size:12px"></div>
</div>

</main>
</div>
</div>

<script>
let entities=[],watched=[],landmarks=[],db=[],settings={},displayRules=[],knownMods=[],catFilter='',dbCatFilter='',atlasData=null,atlasPins=new Set();
let atlasRuleFilterTrack=false, atlasRuleFilterArrow=false;
const $=id=>document.getElementById(id);
const esc=s=>String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/'/g,"\\'").replace(/"/g,'&quot;');

function showTab(name){
  const panel=$('tab-'+name);
  if(!panel)return;
  document.querySelectorAll('.tab').forEach(t=>t.classList.remove('active'));
  document.querySelectorAll('.panel').forEach(p=>p.classList.remove('active'));
  document.querySelector(`.tab[onclick="showTab('${name}')"]`)?.classList.add('active');
  panel.classList.add('active');
  try{localStorage.setItem('radarActiveTab',name)}catch{}
  if(name==='watched')refreshWatched();
  if(name==='display')loadDisplayPage();
  if(name==='rules')refreshRules();
  if(name==='pathing')refreshPathing();
  if(name==='landmarks')refreshLandmarks();
  if(name==='atlas'){loadAtlasSettings();loadAtlas();}
  if(name==='database'&&db.length===0)loadDb();
  if(name==='settings')loadSettings();
  if(name==='devtest')loadDevTest();
  if(name==='gamedata')loadGdAreas();
  if(name==='minimap')loadMinimapSettings();
  if(name==='hidden')refreshHidden();
  if(name==='keybinds'){if(!settings||!Object.keys(settings).length)loadSettings().then(loadKeybinds);else loadKeybinds();}
  if(name==='inspector'){if(!Object.keys(inspSchema).length)loadInspectorSchema();loadInspectorEntities();}
}

// ── LIVE ENTITIES ──
async function refresh(){
  try{
    const s=await(await fetch('/state')).json();
    updateConsoleState(s);
    $('status').innerHTML=s.inGame
      ?`<span style="color:#fff">${s.areaName||s.areaCode}</span> <span style="color:#888">(${s.areaCode} · Act ${s.act||'?'} · lvl ${s.areaLevel}${s.isTown?' · Town':''}${s.hasWaypoint?' · WP':''})</span> | ${s.player?.name||''} Lv${s.player?.level||'?'} | HP ${s.hpPct.toFixed(0)}% | Entities: ${s.entityCount}`
      :'Waiting for in-game...';
    const alive=$('aliveOnly').checked?'&alive=true':'';
    entities=await(await fetch('/entities?limit=1000'+alive)).json();
    renderEntities();
  }catch(e){
    $('status').textContent='Connection lost. Retrying automatically.';
    $('connection').classList.remove('live');
    $('connectionText').textContent='offline';
    $('areaChip').textContent='Waiting for API';
  }
}
function updateConsoleState(s){
  const hp=Math.max(0,Math.min(100,Number(s.hpPct)||0));
  const mana=Math.max(0,Math.min(100,Number(s.manaPct)||0));
  $('connection').classList.add('live');
  $('connectionText').textContent=s.inGame?'live':'attached';
  $('areaChip').textContent=s.inGame?`${s.areaName||s.areaCode||'Unknown area'} | ${s.player?.name||'Player'}`:'Waiting for in-game';
  $('sideHp').textContent=s.inGame?`${hp.toFixed(0)}%`:'--';
  $('sideMana').textContent=s.inGame?`${mana.toFixed(0)}%`:'--';
  $('sideHpBar').style.width=(s.inGame?hp:0)+'%';
  $('sideManaBar').style.width=(s.inGame?mana:0)+'%';
  $('sideArea').textContent=s.areaName||'--';
  $('sideCode').textContent=s.areaCode||'--';
  $('sideLevel').textContent=s.inGame?`${s.act||'?'} / ${s.areaLevel||'?'}`:'--';
  $('sideMap').textContent=s.mapVisible?'Open':'Closed';
  $('sideFlask').textContent=s.autoFlask?(s.flask||'Armed'):'Off';
  $('sideEntities').textContent=s.entityCount||0;
  $('sideMonsters').textContent=s.counts?.Monster||0;
  $('sideChests').textContent=s.counts?.Chest||0;
  $('sideNpcs').textContent=s.counts?.Npc||0;
}
function renderEntities(){
  const search=$('search').value.toLowerCase();
  const sleepingOnly=$('sleepingOnly').checked;
  const cats=[...new Set(entities.map(e=>e.category))];
  $('catFilters').innerHTML=['All',...cats].map(c=>
    `<button class="filter-btn ${catFilter===(c==='All'?'':c)?'active':''}" onclick="setCat('${c==='All'?'':c}')">${c}</button>`).join('');
  const f=entities.filter(e=>
    (!catFilter||e.category===catFilter)&&
    (!sleepingOnly||e.sleeping)&&
    (!search||(e.name||'').toLowerCase().includes(search)||e.metadata.toLowerCase().includes(search)));
  $('entityBody').innerHTML=f.map(e=>`<tr class="${e.watched?'watched':''}">
    <td><span class="source-state ${e.sleeping?'source-sleeping':'source-awake'}">${e.sleeping?'SLEEPING':'AWAKE'}</span></td>
    <td><span class="cat cat-${e.category}">${e.category}</span>${e.boss?'<span style="color:#f44;font-weight:bold" title="Boss"> ★</span>':''}${e.league&&e.league!=='None'?`<span style="color:#0af;font-size:10px" title="League Mechanic"> ${e.league}</span>`:''}</td>
    <td><span class="rarity-${e.rarity}">${e.rarity}</span></td>
    <td class="meta-short" title="${e.metadata}">${e.name||e.metadata}${e.locked?'<span style="color:#fa0" title="Locked"> 🔒</span>':''}${e.large?'<span style="color:#0af" title="Large"> L</span>':''}</td>
    <td>${e.hpMax>0?e.hpCur+'/'+e.hpMax:'-'}</td><td>${e.dist}</td>
    <td style="white-space:nowrap">
      ${e.watched?`<button class="btn btn-rm" onclick="rmByMeta('${esc(e.metadata)}')">-</button>`
                 :`<button class="btn btn-add" onclick="quickWatch('${esc(e.metadata)}')">Watch</button>`}
      <button class="btn" style="background:#2a4a5a;color:#5cf" onclick="navigateTo('${esc(e.metadata)}')">Nav</button>
      ${e.addr?`<button class="btn" style="background:#3a2a4a;color:#c8f" onclick="inspectFromList('${e.addr}')">Inspect</button>`:''}
      <button class="btn" style="background:#4a3a1a;color:#fa0" onclick="hideFromEntity('${esc(e.metadata)}')">Hide</button>
    </td>
  </tr>`).join('');
}
function setCat(c){catFilter=c;renderEntities();}
function filterEntities(){renderEntities();}
function toggleSleepingOnly(){
  if($('sleepingOnly').checked && $('aliveOnly').checked){
    $('aliveOnly').checked=false;
    refresh();
    return;
  }
  renderEntities();
}

async function navigateTo(meta){
  const short=meta.split('/').pop().replace(/@\d+$/,'');
  await fetch('/api/settings',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({showPath:true,pathTarget:short})});
}

// ── WATCHED ──
// -- DISPLAY RULES --
const displayCategories=['Monster','Chest','Npc','Object','Other','Transition','Player','Tile'];
const displayConditions=[
  ['rarity','Rarity',['Normal','Magic','Rare','Unique']],
  ['reaction','Reaction',['Hostile','Friendly']],
  ['life','Life',['Alive','Dead']],
  ['chest','Chest',['Opened','Unopened']],
  ['poi','POI',['Yes','No']],
  ['encounter','Encounter',['Active','Complete']]
];
function displaySummary(r){
  const parts=[(r.categories||[]).length?(r.categories||[]).join('/'):'any type'];
  if((r.match||[]).length)parts.push((r.match||[]).join(', '));
  if((r.mods||[]).length)parts.push('mods: '+(r.mods||[]).join(', '));
  displayConditions.forEach(([key])=>{if(r[key])parts.push(r[key]);});
  return parts.join(' | ');
}
function displaySelect(index,key,label,options,current){
  return `<label>${label}<select onchange="setDisplayRule(${index},'${key}',this.value||null)"><option value="">Any</option>${
    options.map(x=>`<option value="${x}" ${current===x?'selected':''}>${x}</option>`).join('')
  }</select></label>`;
}
function renderDisplayRules(){
  const host=$('displayRuleList');
  if(!host)return;
  if(!displayRules.length){
    host.innerHTML='<div class="display-rule-note">No rules configured. Entities will not draw until a rule matches them.</div>';
    return;
  }
  host.innerHTML=displayRules.map((r,i)=>{
    const open=!!r._open;
    const body=open?`<div class="display-rule-body">
      <div class="display-rule-grid">
        <label>Name<input type="text" value="${esc(r.name||'')}" onchange="setDisplayRule(${i},'name',this.value)"></label>
        <label>Metadata terms<input type="text" value="${esc((r.match||[]).join(', '))}" onchange="setDisplayList(${i},'match',this.value)"></label>
        <label>Monster mods<input type="text" list="knownMods" value="${esc((r.mods||[]).join(', '))}" onchange="setDisplayList(${i},'mods',this.value)"></label>
        ${displayConditions.map(([k,l,o])=>displaySelect(i,k,l,o,r[k])).join('')}
      </div>
      <div class="display-rule-cats">${displayCategories.map(c=>`<label><input type="checkbox" ${r.categories?.includes(c)?'checked':''} onchange="toggleDisplayCategory(${i},'${c}',this.checked)"> ${c}</label>`).join('')}</div>
      <div class="display-rule-actions">
        <label><input type="checkbox" ${r.hide?'checked':''} onchange="setDisplayRule(${i},'hide',this.checked)"> Hide</label>
        <label>Shape <select onchange="setDisplayRule(${i},'shape',this.value)">${['Circle','Diamond','Square','Triangle','Star','Plus'].map(x=>`<option ${r.shape===x?'selected':''}>${x}</option>`).join('')}</select></label>
        <input type="color" value="${r.color||'#ffffff'}" title="Color" onchange="setDisplayRule(${i},'color',this.value)">
        <label>Opacity <input type="number" min="0" max="1" step="0.05" value="${r.opacity??1}" onchange="setDisplayRule(${i},'opacity',parseFloat(this.value))"></label>
        <label>Size <input type="number" min="0.5" max="100" step="0.5" value="${r.size??3}" onchange="setDisplayRule(${i},'size',parseFloat(this.value))"></label>
        <input type="text" value="${esc(r.label||'')}" placeholder="Optional label" onchange="setDisplayRule(${i},'label',this.value||null)">
        <label><input type="checkbox" ${r.navigable?'checked':''} onchange="setDisplayRule(${i},'navigable',this.checked)"> Auto-path</label>
      </div>
    </div>`:'';
    return `<div class="display-rule ${r.enabled===false?'off':''}">
      <div class="display-rule-head" onclick="toggleDisplayOpen(${i},event)">
        <input type="checkbox" ${r.enabled!==false?'checked':''} title="Enabled" onclick="event.stopPropagation()" onchange="setDisplayRule(${i},'enabled',this.checked,true)">
        <span style="color:${r.color||'#fff'}">${r.hide?'X':'O'}</span>
        <strong>${esc(r.name||'(unnamed)')}</strong>
        <span class="display-rule-summary">${esc(displaySummary(r))}</span>
        <div class="display-rule-order" onclick="event.stopPropagation()"><button class="btn" onclick="moveDisplayRule(${i},-1)">Up</button><button class="btn" onclick="moveDisplayRule(${i},1)">Down</button></div>
        <button class="btn btn-rm" onclick="event.stopPropagation();removeDisplayRule(${i})">X</button>
      </div>${body}</div>`;
  }).join('');
}
function toggleDisplayOpen(i,e){if(e?.target?.closest('input,button,select,label'))return;displayRules[i]._open=!displayRules[i]._open;renderDisplayRules();}
let displaySaveTimer=null;
function queueDisplaySave(){
  clearTimeout(displaySaveTimer);
  $('displaySavedMsg').textContent='Saving...';
  $('displaySavedMsg').classList.add('show');
  displaySaveTimer=setTimeout(saveDisplayRulesOnly,350);
}
async function saveDisplayRulesOnly(){
  const clean=displayRules.map(({_open,...r})=>r);
  try{
    await fetch('/api/display-rules',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(clean)});
    $('displaySavedMsg').textContent='Saved';
    setTimeout(()=>$('displaySavedMsg').classList.remove('show'),900);
  }catch(e){
    $('displaySavedMsg').textContent='Save failed';
  }
}
function setDisplayRule(i,key,value,rerender=false){displayRules[i][key]=value;if(rerender)renderDisplayRules();queueDisplaySave();}
function setDisplayList(i,key,value){displayRules[i][key]=value.split(',').map(x=>x.trim()).filter(Boolean);queueDisplaySave();}
function toggleDisplayCategory(i,category,on){
  const cats=new Set(displayRules[i].categories||[]);
  if(on)cats.add(category);else cats.delete(category);
  displayRules[i].categories=[...cats];
  queueDisplaySave();
}
function moveDisplayRule(i,delta){
  const to=i+delta;if(to<0||to>=displayRules.length)return;
  [displayRules[i],displayRules[to]]=[displayRules[to],displayRules[i]];
  renderDisplayRules();
  queueDisplaySave();
}
function removeDisplayRule(i){displayRules.splice(i,1);renderDisplayRules();queueDisplaySave();}
function addDisplayRule(){
  displayRules.push({enabled:true,name:'New rule',categories:[],match:[],mods:[],hide:false,shape:'Circle',color:'#ffd926',opacity:1,size:4,navigable:false,_open:true});
  renderDisplayRules();
  queueDisplaySave();
}
function addDisplayRuleFromEntity(){
  const query=prompt('Search live entity name or metadata:','');
  if(query===null)return;
  const q=query.trim().toLowerCase();
  const matches=entities.filter(e=>!q||(e.name||'').toLowerCase().includes(q)||(e.metadata||'').toLowerCase().includes(q)).slice(0,20);
  if(!matches.length){alert('No matching live entity. Refresh Live Entities and try again.');return;}
  let choice=0;
  if(matches.length>1){
    const selected=prompt(matches.map((e,i)=>`${i+1}. ${e.name||e.metadata} [${e.category}]`).join('\n')+'\n\nEnter number:','1');
    if(selected===null)return;
    choice=Math.max(0,Math.min(matches.length-1,(parseInt(selected)||1)-1));
  }
  const e=matches[choice],term=(e.metadata||'').split('/').pop().replace(/@\d+$/,'');
  displayRules.unshift({enabled:true,name:e.name||term,categories:[e.category],match:[term],mods:[],hide:false,shape:'Star',color:'#ffd926',opacity:1,size:6,navigable:false,_open:true});
  renderDisplayRules();
  queueDisplaySave();
}
async function loadDisplayPage(){
  try{
    const [rules,mods,radar,prices,priceLeagues]=await Promise.all([
      fetch('/api/display-rules').then(r=>r.json()),
      fetch('/api/mods').then(r=>r.json()),
      fetch('/api/settings').then(r=>r.json()),
      fetch('/api/prices').then(r=>r.json()),
      fetch('/api/price-leagues').then(r=>r.ok?r.json():[]).catch(()=>[])
    ]);
    displayRules=Array.isArray(rules)?rules:[];
    knownMods=mods.mods||[];
    $('knownMods').innerHTML=knownMods.map(m=>`<option value="${esc(m)}">`).join('');
    settings=radar;
    const g=settings.groundItems||{};
    $('priceEnabled').checked=!!g.enabled;
    $('priceRuneforge').checked=!!g.showRuneforgePrices;
    $('priceMin').value=g.highlightMinEx??10;
    $('priceUniqueMin').value=g.uniqueMinEx??5;
    $('priceQty').value=g.minQuantity??2;
    renderPriceLeagues(priceLeagues,g.league||'',prices.league||'');
    document.querySelectorAll('.price-cat').forEach(c=>c.checked=(g.categories||[]).includes(c.value));
    $('priceStatus').textContent=formatPriceStatus(prices);
    renderDisplayRules();
  }catch(e){$('priceStatus').textContent='Unable to load display controls: '+e;}
}
function renderPriceLeagues(leagues,selected,active){
  const host=$('priceLeague'); if(!host)return;
  const opts=['<option value="">Auto current Softcore</option>'];
  const seen=new Set(['']);
  const list=Array.isArray(leagues)?leagues:[];
  const add=(value,label)=>{
    if(!value||seen.has(value))return;
    seen.add(value);
    opts.push(`<option value="${esc(value)}">${esc(label)}</option>`);
  };
  list.forEach(l=>{
    const value=l.value||'';
    const base=value.replace(/^HC\s+/i,'');
    const realm=l.hardcore?'Hardcore':'Softcore';
    add(value,`${realm} - ${base}${l.isCurrent?' (current)':''}`);
  });
  if(selected&&!seen.has(selected))add(selected,`Custom - ${selected}`);
  if(active&&!seen.has(active))add(active,`Active - ${active}`);
  host.innerHTML=opts.join('');
  host.value=selected||'';
}
function formatPriceStatus(prices){
  const loaded=prices.loaded?'Loaded':'Waiting';
  const league=prices.league||'auto current softcore';
  const realm=prices.hardcore?'Hardcore':'Softcore';
  const rate=Number(prices.exPerDivine)||0;
  const inv=Number(prices.divPerExalted)||(rate>0?1/rate:0);
  const rateText=rate>0?` | 1 div = ${rate.toFixed(2)} ex | 1 ex = ${inv.toFixed(5)} div`:'';
  return `${loaded} | ${realm} | ${league} | ${prices.count||0} prices${rateText} | ${prices.status||''}`;
}
async function saveDisplayPage(){
  const clean=displayRules.map(({_open,...r})=>r);
  const groundItems={
    enabled:$('priceEnabled').checked,
    showRuneforgePrices:$('priceRuneforge').checked,
    highlightMinEx:parseFloat($('priceMin').value)||0,
    uniqueMinEx:parseFloat($('priceUniqueMin').value)||0,
    minQuantity:parseInt($('priceQty').value)||1,
    league:$('priceLeague').value.trim(),
    categories:[...document.querySelectorAll('.price-cat:checked')].map(c=>c.value)
  };
  await Promise.all([
    fetch('/api/display-rules',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(clean)}),
    fetch('/api/settings',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({groundItems})})
  ]);
  displayRules=clean;
  $('displaySavedMsg').classList.add('show');
  setTimeout(()=>$('displaySavedMsg').classList.remove('show'),1500);
  await loadDisplayPage();
}

async function quickWatch(meta){
  const parts=meta.split('/');const def=parts[parts.length-1].replace(/@\d+$/,'');
  const nick=prompt('Nickname for radar:',def);if(nick===null)return;
  await doAdd(meta,nick||def,$('addColor').value);
}
async function addWatched(){
  const p=$('addPattern').value.trim(),l=$('addLabel').value.trim(),c=$('addColor').value;
  if(!p)return;await doAdd(p,l||p.split('/').pop(),c);$('addPattern').value='';$('addLabel').value='';
}
async function doAdd(pattern,label,color,size=7){
  await fetch('/api/watched',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({pattern,label,color,enabled:true,size})});
  refresh();refreshWatched();
}
async function rmByMeta(meta){const w=watched.find(w=>meta.includes(w.pattern));if(w)await rmWatched(w.pattern);}
async function rmWatched(pattern){await fetch('/api/watched?pattern='+encodeURIComponent(pattern),{method:'DELETE'});refreshWatched();refresh();}
async function refreshWatched(){
  watched=await(await fetch('/api/watched')).json();
  $('watchedList').innerHTML=watched.map(w=>
    `<div class="watched-item">
      <input type="color" value="${w.color}" onchange="editWatched('${esc(w.pattern)}',{color:this.value})" title="Color">
      <input type="text" value="${w.label}" style="width:120px;background:#1e1e28;border:1px solid #444;color:#afc;border-radius:3px;padding:2px 6px;font-size:13px;font-weight:bold"
        onchange="editWatched('${esc(w.pattern)}',{label:this.value})" title="Nickname">
      <input type="number" value="${w.size??7}" min="1" max="30" step="0.5" style="width:55px;background:#1e1e28;border:1px solid #444;color:#78b4ff;border-radius:3px;padding:2px 4px;font-size:12px"
        onchange="editWatched('${esc(w.pattern)}',{size:parseFloat(this.value)})" title="Dot size">
      <div class="pattern" title="${w.pattern}">${w.pattern}</div>
      <label style="font-size:11px;color:#888;white-space:nowrap"><input type="checkbox" ${w.enabled?'checked':''} onchange="editWatched('${esc(w.pattern)}',{enabled:this.checked})"> on</label>
      <button class="btn btn-rm" onclick="rmWatched('${esc(w.pattern)}')">X</button>
    </div>`
  ).join('')||'<div style="color:#666;padding:8px">No watched entities yet.</div>';
}
async function editWatched(pattern,changes){
  const w=watched.find(w=>w.pattern===pattern);if(!w)return;
  const updated={...w,...changes};
  await fetch('/api/watched',{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(updated)});
  refreshWatched();
}
function exportWatched(){
  const blob=new Blob([JSON.stringify(watched,null,2)],{type:'application/json'});
  const a=document.createElement('a');a.href=URL.createObjectURL(blob);
  a.download='watched_entities.json';a.click();URL.revokeObjectURL(a.href);
}
async function importWatched(input){
  if(!input.files[0])return;
  const text=await input.files[0].text();
  try{
    const r=await fetch('/api/watched/import',{method:'POST',headers:{'Content-Type':'application/json'},body:text});
    const res=await r.json();
    if(res.ok){$('importMsg').textContent=`Imported ${res.imported} entries!`;$('importMsg').classList.add('show');setTimeout(()=>$('importMsg').classList.remove('show'),2000);}
    else alert('Import error: '+res.error);
    refreshWatched();refresh();
  }catch(e){alert('Invalid JSON file');}
  input.value='';
}

// ── DATABASE ──
const JUNK_PATTERNS=['/attachments','monstermods','microtransactions','/timelines/','stashskins','/fx/','/mat/','/ao/','/epk/','/graph/','/audio/','/pet/','/clone/','playersummoned','essencemoddaemons','tormentedspirits','/daemon/','bossroomminimapicon','/environment/','hairstyles','/outfits/','/runemarked'];
function isJunk(p){const l=p.toLowerCase();return JUNK_PATTERNS.some(j=>l.includes(j));}

async function loadDb(){$('dbCount').textContent='Loading...';db=await(await fetch('/api/database')).json();$('dbCount').textContent=db.length+' entities';filterDb();}
function filterDb(){
  const s=($('dbSearch')?.value||'').toLowerCase();const cats=new Set();const hj=$('dbHideJunk')?.checked;
  const f=db.filter(p=>{if(hj&&isJunk(p))return false;if(s&&!p.toLowerCase().includes(s))return false;const c=getCat(p);cats.add(c);return!dbCatFilter||c===dbCatFilter;});
  $('dbCatFilters').innerHTML=['All',...[...cats].sort()].map(c=>
    `<button class="filter-btn ${dbCatFilter===(c==='All'?'':c)?'active':''}" onclick="setDbCat('${c==='All'?'':c}')">${c}</button>`).join('');
  const show=f.slice(0,200);
  $('dbBody').innerHTML=show.map(p=>{const isW=watched.some(w=>p.includes(w.pattern));
    return`<tr class="${isW?'watched':''}"><td><span class="db-cat">${getCat(p)}</span></td>
    <td class="db-path" title="${p}">${p}</td>
    <td>${isW?`<button class="btn btn-rm" onclick="rmByMeta('${esc(p)}')">-</button>`
             :`<button class="btn btn-add" onclick="dbWatch('${esc(p)}')">Watch</button>`}</td></tr>`;
  }).join('')+(f.length>200?`<tr><td colspan=3 style="color:#666">Showing 200/${f.length}. Narrow search.</td></tr>`:'');
  $('dbCount').textContent=f.length+' matches';
}
function setDbCat(c){dbCatFilter=c;filterDb();}
function getCat(p){const parts=p.split('/');return parts.length>=2?parts[1]:'?';}
async function dbWatch(path){const def=path.split('/').pop();const nick=prompt('Nickname for radar:',def);if(nick===null)return;await doAdd(path,nick||def,'#ff5555');filterDb();}

// ── SETTINGS ──
const settingsDef = [
  {section:'Visibility',items:[
    {key:'showMonsters',label:'Monsters (all)',type:'bool'},
    {key:'showRareMonsters',label:'Rare Monsters',type:'bool'},
    {key:'showUniqueMonsters',label:'Unique Monsters',type:'bool'},
    {key:'showNpcs',label:'NPCs',type:'bool'},
    {key:'showChests',label:'Chests',type:'bool'},
    {key:'showTransitions',label:'Transitions / Exits',type:'bool'},
    {key:'showPlayers',label:'Other Players',type:'bool'},
    {key:'showLandmarks',label:'Landmarks',type:'bool'},
    {key:'showTerrain',label:'Terrain',type:'bool'},
    {key:'showStatusBar',label:'Status HUD (top-left)',type:'bool'},
    {key:'showWatchedLabels',label:'Watched Entity Labels',type:'bool'},
    {key:'persistEntities',label:'Remember Entities Beyond Bubble',type:'bool'},
  ]},
  {section:'Label Toggles (dot still shown, only text hidden)',items:[
    {key:'showTransitionLabels',label:'Transition / Exit Labels',type:'bool'},
    {key:'showNpcLabels',label:'NPC Labels',type:'bool'},
    {key:'showLandmarkLabels',label:'Landmark Labels',type:'bool'},
    {key:'showPoiLabels',label:'POI Labels (non-NPC)',type:'bool'},
    {key:'showMonsterLabels',label:'Monster Names (Rare/Unique)',type:'bool'},
    {key:'showChestLabels',label:'Chest Type Labels',type:'bool'},
    {key:'showMechanicLabels',label:'Encounter / Mechanic Labels',type:'bool'},
  ]},
  {section:'Visual Clutter Reduction',items:[
    {key:'hideJunkEntities',label:'Hide Junk (attachments, effects, cosmetics)',type:'bool'},
    {key:'hideUntargetable',label:'Hide Untargetable Entities',type:'bool'},
    {key:'showDeadMonsters',label:'Show Dead Corpses (off = cleaner map)',type:'bool'},
    {key:'showMechanicIcons',label:'Show Content/Mechanic Icons',type:'bool'},
    {key:'showMechanicMonsters',label:'Show Encounter Monsters',type:'bool'},
    {key:'showPreloadedMechanicLocations',label:'Show Preloaded Mechanic Locations',type:'bool'},
    {key:'hideDeadMechanicMonsters',label:'Hide Dead Content Monsters',type:'bool'},
    {key:'mechanicDeadFadeSeconds',label:'Dead Content Fade (seconds, 0 = immediate)',type:'num',min:0,max:10,step:0.1},
    {key:'showMechanicNonMonsterIcons',label:'Show Content Non-Monster/Effect Icons',type:'bool'},
    {key:'showNormalMonsters',label:'Show Normal (white) Monsters',type:'bool'},
    {key:'showNormalChests',label:'Show Normal Chests (not just Rare/Unique)',type:'bool'},
    {key:'showFriendlyEntities',label:'Show Friendly Monsters (minions, allies)',type:'bool'},
    {key:'showImmobileEntities',label:'Show Immobile Entities (speed=0, decorative)',type:'bool'},
    {key:'entityDrawRange',label:'Max Draw Range (0 = unlimited, grid units)',type:'num',min:0,max:300,step:5},
    {key:'minEntityHpPct',label:'Min HP% to Show (0 = show all, hides near-dead)',type:'num',min:0,max:50,step:5},
    {key:'showDistanceRing',label:'Show Distance Ring on Map',type:'bool'},
    {key:'distanceRingRadius',label:'Ring Radius (grid units)',type:'num',min:10,max:200,step:5},
  ]},
  {section:'Dot Sizes',items:[
    {key:'monsterDotSize',label:'Normal Monster',type:'num',min:1,max:30,step:0.5},
    {key:'magicDotSize',label:'Magic Monster',type:'num',min:1,max:30,step:0.5},
    {key:'rareDotSize',label:'Rare Monster',type:'num',min:1,max:30,step:0.5},
    {key:'uniqueDotSize',label:'Unique Monster',type:'num',min:1,max:30,step:0.5},
    {key:'npcDotSize',label:'NPC',type:'num',min:1,max:30,step:0.5},
    {key:'chestDotSize',label:'Chest',type:'num',min:1,max:30,step:0.5},
    {key:'transitionDotSize',label:'Transition',type:'num',min:1,max:30,step:0.5},
    {key:'playerDotSize',label:'Player',type:'num',min:1,max:30,step:0.5},
    {key:'watchedDotSize',label:'Watched Entity',type:'num',min:1,max:30,step:0.5},
  ]},
  {section:'Outline',items:[
    {key:'dotOutlineWidth',label:'Dot Outline Width',type:'num',min:0,max:10,step:0.5},
    {key:'dotOutlineColor',label:'Dot Outline Color',type:'color'},
    {key:'landmarkOutlineWidth',label:'Landmark Outline Width',type:'num',min:0,max:10,step:0.5},
  ]},
  {section:'Font Sizes (no limit — scale up for 4K)',items:[
    {key:'fontFamily',label:'Font Family',type:'text'},
    {key:'statusFontSize',label:'Status Bar',type:'num',min:6,max:72,step:1},
    {key:'landmarkFontSize',label:'Landmarks',type:'num',min:6,max:72,step:1},
    {key:'transitionFontSize',label:'Transitions',type:'num',min:6,max:72,step:1},
    {key:'chestFontSize',label:'Chests',type:'num',min:6,max:72,step:1},
    {key:'watchedFontSize',label:'Watched Labels',type:'num',min:6,max:72,step:1},
    {key:'nameplateFontSize',label:'HP Nameplates',type:'num',min:6,max:72,step:1},
  ]},
  {section:'Colors',items:[
    {key:'monsterColor',label:'Normal Monster',type:'color'},
    {key:'magicColor',label:'Magic Monster',type:'color'},
    {key:'rareColor',label:'Rare Monster',type:'color'},
    {key:'uniqueColor',label:'Unique Monster',type:'color'},
    {key:'npcColor',label:'NPC',type:'color'},
    {key:'chestColor',label:'Chest',type:'color'},
    {key:'transitionColor',label:'Transition',type:'color'},
    {key:'playerColor',label:'Player',type:'color'},
    {key:'landmarkColor',label:'Landmark',type:'color'},
    {key:'watchedColor',label:'Watched Entity',type:'color'},
  ]},
  {section:'Terrain / Map Outline',items:[
    {key:'terrainOpacity',label:'Overlay Opacity',type:'num',min:0,max:1,step:0.05},
    {key:'terrainEdgeColor',label:'Edge Color',type:'color'},
    {key:'terrainEdgeAlpha',label:'Edge Opacity',type:'num',min:0.1,max:1,step:0.05},
    {key:'terrainInteriorAlpha',label:'Interior Opacity',type:'num',min:0,max:0.5,step:0.02},
  ]},
  {section:'Performance',items:[
    {key:'fpsCap',label:'FPS Cap (15-60, software compositor)',type:'num',min:15,max:60,step:5},
    {key:'showPerformanceDiagnostics',label:'Log performance diagnostics',type:'bool'},
    {key:'fastTerrainSampling',label:'Fast terrain sampling',type:'bool'},
  ]},
  {section:'Calibration',items:[
    {key:'resetCalibrationOnZoneChange',label:'Auto-reset on zone change',type:'bool'},
    {key:'offsetX',label:'Offset X',type:'num',min:-50,max:50,step:0.5},
    {key:'offsetY',label:'Offset Y',type:'num',min:-50,max:50,step:0.5},
    {key:'scaleMul',label:'Scale',type:'num',min:0.3,max:3,step:0.02},
  ]},
  {section:'Exploration Fog',items:[
    {key:'showExplorationFog',label:'Show Unexplored Fog',type:'bool'},
    {key:'fogOpacity',label:'Fog Darkness',type:'num',min:0.1,max:0.9,step:0.05},
    {key:'fogGridStep',label:'Fog Resolution (lower = sharper, more CPU)',type:'num',min:1,max:8,step:1},
    {key:'fogCellScale',label:'Fog Cell Size',type:'num',min:0.05,max:0.3,step:0.01},
  ]},
  {section:'Map Drawing',items:[
    {key:'mapCenterOnPlayerScreen',label:'Compensate inventory/stash viewport shift',type:'bool'},
    {key:'mapCenterYShift',label:'Map Center Y Shift',type:'num',min:-100,max:100,step:1},
    {key:'playerBlipSize',label:'Player Blip Size (map)',type:'num',min:1,max:15,step:0.5},

    {key:'landmarkIconSize',label:'Landmark Icon Size',type:'num',min:1,max:15,step:0.5},
    {key:'pathEndMarkerSize',label:'Path End Marker Size',type:'num',min:1,max:15,step:0.5},
    {key:'clickInspectDistance',label:'Click Inspect Distance (px)',type:'num',min:10,max:100,step:5},
  ]},
  {section:'Boss Highlight',items:[
    {key:'showBossHighlight',label:'Highlight Bosses (large star icon)',type:'bool'},
    {key:'bossDotSize',label:'Boss Dot Size',type:'num',min:3,max:15,step:0.5},
  ]},
  {section:'Nameplate HP Bars',items:[
    {key:'showNameplates',label:'Show HP Bars',type:'bool'},
    {type:'hpRarity',label:'Normal Monsters',toggle:'showNormalNameplates',color:'hpBars.normalColor'},
    {type:'hpRarity',label:'Magic Monsters',toggle:'showMagicNameplates',color:'hpBars.magicColor'},
    {type:'hpRarity',label:'Rare Monsters',toggle:'showRareNameplates',color:'hpBars.rareColor'},
    {type:'hpRarity',label:'Unique Monsters',toggle:'showUniqueNameplates',color:'hpBars.uniqueColor'},
    {key:'hpBars.lowHealthColor',label:'Low HP Color',type:'color'},
    {key:'hpBars.backgroundColor',label:'Background Color',type:'color'},
    {key:'hpBars.borderColor',label:'Border Color',type:'color'},
    {key:'hpBars.opacity',label:'Bar Opacity',type:'num',min:0,max:1,step:0.05},
    {key:'hpBars.backgroundOpacity',label:'Background Opacity',type:'num',min:0,max:1,step:0.05},
    {key:'hpBars.borderOpacity',label:'Border Opacity',type:'num',min:0,max:1,step:0.05},
    {key:'hpBars.lowHealthThreshold',label:'Low HP Threshold',type:'num',min:0.05,max:0.95,step:0.05},
    {key:'nameplateBarWidth',label:'Bar Width Scale',type:'num',min:0.3,max:3,step:0.1},
    {key:'nameplateBarHeight',label:'Bar Height (px)',type:'num',min:1,max:20,step:1},
    {key:'nameplateOffsetY',label:'Y Offset (negative = above)',type:'num',min:-100,max:50,step:1},
    {key:'nameplateFontSize',label:'Name Font Size',type:'num',min:6,max:30,step:1},
  ]},
  {section:'Pathfinding',items:[
    {key:'showPath',label:'Enable Pathfinding',type:'bool'},
    {key:'showGroundWaypoints',label:'Show Ground Waypoints (when map closed)',type:'bool'},
    {key:'pathTarget',label:'Target Pattern',type:'text'},
    {key:'pathColor',label:'Path Color',type:'color'},
    {key:'pathWidth',label:'Path Width',type:'num',min:0.5,max:8,step:0.5},
    {key:'pathMaxNodes',label:'Max Nodes (higher = longer paths, more CPU)',type:'num',min:50000,max:2000000,step:50000},
  ]},
  {section:'Auto-Flask',items:[
    {key:'hpThreshold',label:'HP Threshold %',type:'num',min:5,max:95,step:5},
    {key:'manaThreshold',label:'Mana Threshold %',type:'num',min:5,max:95,step:5},
    {key:'flaskLifeKey',label:'Life Flask Key (VK code, 0x31=1)',type:'num',min:0,max:255,step:1},
    {key:'flaskManaKey',label:'Mana Flask Key (VK code, 0x32=2)',type:'num',min:0,max:255,step:1},
    {key:'flaskLifeCooldownMs',label:'Life Flask Cooldown (ms)',type:'num',min:500,max:10000,step:100},
    {key:'flaskManaCooldownMs',label:'Mana Flask Cooldown (ms)',type:'num',min:500,max:10000,step:100},
  ]},
  {section:'Auto-Logout (kill game on low HP)',items:[
    {key:'autoLogoutEnabled',label:'Enable Auto-Logout',type:'bool'},
    {key:'autoLogoutHpThreshold',label:'HP Threshold % (quit at or below)',type:'num',min:5,max:80,step:5},
  ]},
];

const atlasSettingsDef = [
  {key:'showAtlasNodes',label:'Show Atlas Nodes',type:'bool'},
  {key:'atlasShowLabels',label:'Show Node Labels',type:'bool'},
  {key:'atlasDrawAll',label:'Draw All Nodes (debug)',type:'bool'},
  {key:'atlasShowWaypointArrows',label:'Show Off-Screen Waypoint Arrows',type:'bool'},
  {key:'atlasNodeColor',label:'Debug Node Color',type:'color'},
  {key:'atlasWaypointColor',label:'Waypoint Color',type:'color'},
  {key:'atlasNodeDotSize',label:'Debug Node Dot Size',type:'num',min:1,max:12,step:0.5},
  {key:'atlasLabelFontSize',label:'Node Label Font',type:'num',min:8,max:24,step:1},
  {key:'atlasLabelOffsetY',label:'Label Offset Y',type:'num',min:-60,max:20,step:1},
  {key:'atlasScale',label:'Atlas Scale Trim',type:'num',min:0.75,max:1.25,step:0.005},
  {key:'atlasOffsetX',label:'Atlas Offset X',type:'num',min:-400,max:400,step:1},
  {key:'atlasOffsetY',label:'Atlas Offset Y',type:'num',min:-400,max:400,step:1},
];

function renderSettingItem(item){
  if(item.type==='hpRarity'){
    const enabled=getSettingValue(item.toggle);
    const color=getSettingValue(item.color);
    return `<div class="setting-row"><label>${item.label}</label>
      <div class="hp-rarity-controls">
        <label><input type="checkbox" ${enabled?'checked':''} onchange="setSetting('${item.toggle}',this.checked)"> Enabled</label>
        <input type="color" value="${color||'#ffffff'}" title="${item.label} HP bar color" onchange="setSetting('${item.color}',this.value)">
      </div>
    </div>`;
  }
  const v=getSettingValue(item.key);
  let html=`<div class="setting-row"><label>${item.label}</label>`;
  if(item.type==='bool')
    html+=`<input type="checkbox" ${v?'checked':''} onchange="setSetting('${item.key}',this.checked)">`;
  else if(item.type==='color')
    html+=`<input type="color" value="${v}" onchange="setSetting('${item.key}',this.value)">`;
  else if(item.type==='text')
    html+=`<input type="text" value="${v||''}" style="width:250px" onchange="setSetting('${item.key}',this.value)" placeholder="e.g. AreaTransition, Waypoint">`;
  else if(item.type==='num')
    html+=`<input type="range" min="${item.min}" max="${item.max}" step="${item.step}" value="${v}"
      oninput="syncNumberSetting('${item.key}',this.value,this.nextElementSibling)">
      <input class="val num-val" type="number" min="${item.min}" max="${item.max}" step="${item.step}" value="${v}"
        ondblclick="this.select()" onchange="syncRangeSetting('${item.key}',this.value,this.previousElementSibling)">`;
  html+=`</div>`;
  return html;
}

function syncNumberSetting(key,value,box){
  const n=parseFloat(value);
  if(Number.isNaN(n))return;
  setSettingValue(key,n);
  if(box)box.value=value;
}
function syncRangeSetting(key,value,range){
  const n=parseFloat(value);
  if(Number.isNaN(n))return;
  setSettingValue(key,n);
  if(range)range.value=value;
}

async function loadAtlasSettings(){
  if(!settings||!Object.keys(settings).length)settings=await(await fetch('/api/settings')).json();
  const body=$('atlasSettingsBody'); if(!body)return;
  body.innerHTML=atlasSettingsDef.map(renderSettingItem).join('');
}

async function saveAtlasSettings(){
  await fetch('/api/settings',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(settings)});
  $('atlasSavedMsg').classList.add('show');setTimeout(()=>$('atlasSavedMsg').classList.remove('show'),1500);
}

async function loadSettings(){
  settings=await(await fetch('/api/settings')).json();
  let html='';
  for(const sec of settingsDef){
    const collapsed=sec.section.startsWith('Game Tweaks')||sec.section==='DevTest';
    const sid='sec_'+sec.section.replace(/[^a-zA-Z]/g,'');
    html+=`<div class="section"><h3 style="cursor:pointer;user-select:none" onclick="document.getElementById('${sid}').style.display=document.getElementById('${sid}').style.display==='none'?'':'none'">${collapsed?'▶':'▼'} ${sec.section}</h3><div id="${sid}" style="${collapsed?'display:none':''}">`;
    for(const item of sec.items){
      const v=item.type==='hpRarity'?'':getSettingValue(item.key);
      html+=`<div class="setting-row"><label>${item.label}</label>`;
      if(item.type==='hpRarity'){
        const enabled=getSettingValue(item.toggle);
        const color=getSettingValue(item.color);
        html+=`<div class="hp-rarity-controls">
          <label><input type="checkbox" ${enabled?'checked':''} onchange="setSetting('${item.toggle}',this.checked)"> Enabled</label>
          <input type="color" value="${color||'#ffffff'}" title="${item.label} HP bar color" onchange="setSetting('${item.color}',this.value)">
        </div>`;
      }
      else if(item.type==='bool')
        html+=`<input type="checkbox" ${v?'checked':''} onchange="setSetting('${item.key}',this.checked)">`;
      else if(item.type==='color')
        html+=`<input type="color" value="${v}" onchange="setSetting('${item.key}',this.value)">`;
      else if(item.type==='text')
        html+=`<input type="text" value="${v||''}" style="width:250px" onchange="setSetting('${item.key}',this.value)" placeholder="e.g. AreaTransition, Waypoint">`;
      else if(item.type==='num')
        html+=`<input type="range" min="${item.min}" max="${item.max}" step="${item.step}" value="${v}"
          oninput="syncNumberSetting('${item.key}',this.value,this.nextElementSibling)">
          <input class="val num-val" type="number" min="${item.min}" max="${item.max}" step="${item.step}" value="${v}"
            ondblclick="this.select()" onchange="syncRangeSetting('${item.key}',this.value,this.previousElementSibling)">`;
      html+=`</div>`;
    }
    html+=`</div></div>`;
  }
  html+=renderMechanicStyles();
  $('settingsBody').innerHTML=html;
  renderSettingsSubtabs();
  showSettingsGroup(activeSettingsGroup);
}
let activeSettingsGroup='entities';
const settingsGroups=[
  {id:'entities',label:'Entities'},
  {id:'appearance',label:'Appearance'},
  {id:'map',label:'Map'},
  {id:'automation',label:'Automation'},
  {id:'performance',label:'Performance'},
];
function settingsGroupFor(section){
  if(section.includes('Dot Sizes')||section.includes('Outline')||section.includes('Font Sizes')||section.includes('Colors')||section.includes('Nameplate'))return 'appearance';
  if(section.includes('Terrain / Map Outline')||section.includes('Calibration')||section.includes('Exploration Fog')||section.includes('Map Drawing'))return 'map';
  if(section.includes('Pathfinding')||section.includes('Auto-Flask')||section.includes('Auto-Logout'))return 'automation';
  if(section.includes('Performance'))return 'performance';
  return 'entities';
}
function renderSettingsSubtabs(){
  $('settingsSubtabs').innerHTML=settingsGroups.map(g=>
    `<button class="settings-subtab" data-settings-group="${g.id}" onclick="showSettingsGroup('${g.id}')">${g.label}</button>`
  ).join('');
}
function showSettingsGroup(group){
  activeSettingsGroup=group;
  document.querySelectorAll('#settingsBody > .section').forEach(section=>{
    const title=section.querySelector('h3')?.textContent?.trim()||'';
    section.style.display=settingsGroupFor(title)===group?'':'none';
  });
  document.querySelectorAll('.settings-subtab').forEach(button=>
    button.classList.toggle('active',button.dataset.settingsGroup===group)
  );
}
function getSettingValue(key){
  if(!key.includes('.'))return settings[key]??'';
  return key.split('.').reduce((obj,part)=>obj&&obj[part]!==undefined?obj[part]:undefined,settings)??'';
}
function setSettingValue(key,val){
  const parts=key.split('.');
  let obj=settings;
  for(let i=0;i<parts.length-1;i++){
    const part=parts[i];
    if(!obj[part]||typeof obj[part]!=='object')obj[part]={};
    obj=obj[part];
  }
  obj[parts[parts.length-1]]=val;
}
function setSetting(key,val){setSettingValue(key,val);}
const iconShapeOptions=['Circle','Square','Diamond','Triangle','TriangleDown','Star','Plus','Cross','Hexagon','Pentagon','Exclamation','Ring','Shield','Gem','Droplet','Heart','ArrowUp'];
function renderMechanicStyles(){
  const styles=settings.styles||(settings.styles={});
  const mechanics=styles.mechanics||(styles.mechanics=[]);
  if(!mechanics.length)return '';
  const rows=mechanics.map((m,i)=>`
    <div class="mechanic-row">
      <label style="font-size:13px;color:#ccc"><input type="checkbox" ${m.enabled!==false?'checked':''} onchange="setMechanic(${i},'enabled',this.checked)"> ${m.name||('Rule '+(i+1))}</label>
      <input type="color" value="${m.color||'#ffffff'}" onchange="setMechanic(${i},'color',this.value)" title="Color">
      <select onchange="setMechanic(${i},'shape',this.value)" title="Shape">
        ${iconShapeOptions.map(s=>`<option value="${s}" ${(m.shape||'Star')===s?'selected':''}>${s}</option>`).join('')}
      </select>
      <input type="number" min="1" max="30" step="0.5" value="${m.size??6}" onchange="setMechanic(${i},'size',parseFloat(this.value))" title="Size">
      <label style="font-size:12px;color:#aaa">Opacity
        <input type="range" min="0" max="1" step="0.05" value="${m.opacity??1}" oninput="setMechanic(${i},'opacity',parseFloat(this.value));this.nextElementSibling.textContent=this.value">
        <span class="val">${m.opacity??1}</span>
      </label>
      <span style="font-size:11px;color:#666">matches</span>
      <div class="mechanic-match" title="${(m.match||[]).join(', ')}">${(m.match||[]).join(', ')}</div>
    </div>`).join('');
  return `<div class="section"><h3>Mechanic Icon Styles</h3>
    <p style="font-size:12px;color:#aaa;margin-bottom:8px">These rules style content icons such as Breach, Expedition, Ritual, Strongbox, Essence, and Shrine.</p>
    ${rows}
  </div>`;
}
function setMechanic(index,key,val){
  settings.styles=settings.styles||{};
  settings.styles.mechanics=settings.styles.mechanics||[];
  settings.styles.mechanics[index]=settings.styles.mechanics[index]||{};
  settings.styles.mechanics[index][key]=val;
}
async function saveSettings(){
  await fetch('/api/settings',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(settings)});
  $('savedMsg').classList.add('show');setTimeout(()=>$('savedMsg').classList.remove('show'),1500);
}
function exportSettings(){
  const blob=new Blob([JSON.stringify(settings,null,2)],{type:'application/json'});
  const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download='radar_settings.json';a.click();
}
async function importSettings(e){
  const file=e.target.files[0];if(!file)return;
  const text=await file.text();
  try{
    const imported=JSON.parse(text);
    await fetch('/api/settings',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(imported)});
    await loadSettings();
    $('savedMsg').textContent='Imported!';$('savedMsg').classList.add('show');setTimeout(()=>{$('savedMsg').classList.remove('show');$('savedMsg').textContent='Saved!';},2000);
  }catch(ex){alert('Invalid settings file: '+ex.message)}
  e.target.value='';
}
async function resetSettings(){
  if(!confirm('Reset ALL settings to defaults? This cannot be undone.'))return;
  await fetch('/api/settings/reset',{method:'POST'});
  await loadSettings();
  $('savedMsg').textContent='Reset!';$('savedMsg').classList.add('show');setTimeout(()=>{$('savedMsg').classList.remove('show');$('savedMsg').textContent='Saved!';},2000);
}

// ── AUTO-SKILLS ──
const VK_NAMES={0x31:'1',0x32:'2',0x33:'3',0x34:'4',0x35:'5',0x51:'Q',0x57:'W',0x45:'E',0x52:'R',0x54:'T'};
function vkName(k){return VK_NAMES[k]||`0x${k.toString(16).toUpperCase()}`;}
function parseKey(s){
  s=s.trim().toUpperCase();
  for(const[k,v]of Object.entries(VK_NAMES))if(v===s)return parseInt(k);
  if(s.length===1)return s.charCodeAt(0);
  if(s.startsWith('0X'))return parseInt(s,16);
  return 0x51;
}

async function refreshRules(){
  const data=await(await fetch('/api/rules')).json();
  $('rulesToggle').textContent=data.enabled?'ON':'OFF';
  $('rulesToggle').style.background=data.enabled?'#2a5a2a':'#5a2a2a';
  $('rulesList').innerHTML=data.rules.map((r,i)=>
    `<div class="watched-item" style="flex-wrap:wrap">
      <label><input type="checkbox" ${r.enabled?'checked':''} onchange="updateRule(${i},{enabled:this.checked})"></label>
      <input type="text" value="${r.name}" style="width:90px;background:#1e1e28;border:1px solid #444;color:#afc;border-radius:3px;padding:2px 6px;font-size:13px;font-weight:bold"
        onchange="updateRule(${i},{name:this.value})">
      <span style="color:#78b4ff;font-size:12px">Key: ${vkName(r.key)}</span>
      <span style="color:#aaa;font-size:11px">CD: ${r.cooldownSec}s</span>
      ${r.hpBelow?`<span style="color:#f88;font-size:11px">HP&lt;${r.hpBelow}%</span>`:''}
      ${r.hpAbove?`<span style="color:#f88;font-size:11px">HP&gt;${r.hpAbove}%</span>`:''}
      ${r.manaBelow?`<span style="color:#88f;font-size:11px">Mana&lt;${r.manaBelow}%</span>`:''}
      ${r.esBelow?`<span style="color:#8ff;font-size:11px">ES&lt;${r.esBelow}%</span>`:''}
      ${r.enemiesNearby?`<span style="color:#ff8;font-size:11px">Enemies&ge;${r.enemiesNearby}</span>`:''}
      ${r.bossNearby===true?'<span style="color:#f80;font-size:11px">Boss nearby</span>':''}
      ${r.bossNearby===false?'<span style="color:#888;font-size:11px">No boss</span>':''}
      ${r.waitSec?`<span style="color:#8f8;font-size:11px">Wait ${r.waitSec}s</span>`:''}
      ${r.requireKeyHeld?`<span style="color:#c8f;font-size:11px">Hold:${vkName(r.requireKeyHeld)}</span>`:''}
      ${r.playerMoving===true?'<span style="color:#aaf;font-size:11px">Moving</span>':''}
      ${r.playerMoving===false?'<span style="color:#aaf;font-size:11px">Standing</span>':''}
      <button class="btn btn-rm" style="margin-left:auto" onclick="deleteRule(${i})">X</button>
    </div>`
  ).join('')||'<div style="color:#666;padding:8px">No rules. Add one below.</div>';
}
async function toggleRules(){await fetch('/api/rules/toggle');refreshRules();}
async function updateRule(i,changes){
  const data=await(await fetch('/api/rules')).json();
  const rule={...data.rules[i],...changes};
  await fetch('/api/rules',{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify({index:i,rule})});
  refreshRules();
}
async function deleteRule(i){await fetch('/api/rules?index='+i,{method:'DELETE'});refreshRules();}
async function addRule(){
  const name=$('ruleAddName').value||'Skill';
  const key=parseKey($('ruleAddKey').value||'Q');
  const cd=parseFloat($('ruleAddCd').value)||2;
  const hp=$('ruleAddHp').value?parseFloat($('ruleAddHp').value):null;
  const mana=$('ruleAddMana').value?parseFloat($('ruleAddMana').value):null;
  const es=$('ruleAddEs').value?parseFloat($('ruleAddEs').value):null;
  const enemies=$('ruleAddEnemies').value?parseInt($('ruleAddEnemies').value):null;
  const bossVal=$('ruleAddBoss').value;
  const boss=bossVal==='true'?true:bossVal==='false'?false:null;
  const wait=$('ruleAddWait').value?parseFloat($('ruleAddWait').value):null;
  const holdKeyStr=$('ruleAddHoldKey').value;
  const holdKey=holdKeyStr?parseKey(holdKeyStr):null;
  const movingVal=$('ruleAddMoving').value;
  const moving=movingVal==='true'?true:movingVal==='false'?false:null;
  await fetch('/api/rules',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({name,key,enabled:true,cooldownSec:cd,hpBelow:hp,manaBelow:mana,esBelow:es,enemiesNearby:enemies,bossNearby:boss,waitSec:wait,requireKeyHeld:holdKey,playerMoving:moving})});
  $('ruleAddName').value='';$('ruleAddKey').value='';$('ruleAddHoldKey').value='';refreshRules();
}

// ── PATHING ──
async function refreshPathing(){
  const data=await(await fetch('/api/pathing')).json();
  const targets=data.targets, cur=data.current;
  $('pathingList').innerHTML=targets.map((t,i)=>
    `<div class="watched-item" style="${i===cur?'border-left:3px solid #5cf':'border-left:3px solid transparent'}">
      <label style="font-size:11px;color:#888;white-space:nowrap"><input type="checkbox" ${t.enabled?'checked':''} onchange="editPathTarget('${esc(t.pattern)}',{enabled:this.checked})"> </label>
      <input type="text" value="${t.label}" style="width:120px;background:#1e1e28;border:1px solid #444;color:#afc;border-radius:3px;padding:2px 6px;font-size:13px;font-weight:bold"
        onchange="editPathTarget('${esc(t.pattern)}',{label:this.value})">
      <div class="pattern" title="${t.pattern}">${t.pattern}</div>
      ${i===cur?'<span style="color:#5cf;font-size:11px;font-weight:bold">ACTIVE</span>':''}
      <button class="btn btn-rm" onclick="rmPathTarget('${esc(t.pattern)}')">X</button>
    </div>`
  ).join('')||'<div style="color:#666;padding:8px">No pathing targets. Add patterns above.</div>';
}
async function addPathTarget(){
  const p=$('pathAddPattern').value.trim(),l=$('pathAddLabel').value.trim();
  if(!p)return;
  await fetch('/api/pathing',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({pattern:p,label:l||p,enabled:true})});
  $('pathAddPattern').value='';$('pathAddLabel').value='';refreshPathing();
}
async function editPathTarget(pattern,changes){
  await fetch('/api/pathing',{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify({pattern,...changes})});
  refreshPathing();
}
async function rmPathTarget(pattern){
  await fetch('/api/pathing?pattern='+encodeURIComponent(pattern),{method:'DELETE'});refreshPathing();
}
async function cyclePathTarget(){
  await fetch('/api/pathing/cycle');refreshPathing();
}

// ── LANDMARKS ──
async function refreshLandmarks(){landmarks=await(await fetch('/landmarks')).json();filterLandmarks();}
function filterLandmarks(){
  const s=($('lmSearch')?.value||'').toLowerCase();
  $('lmBody').innerHTML=landmarks.filter(l=>!s||l.name.toLowerCase().includes(s)||l.path.toLowerCase().includes(s))
    .map(l=>`<tr><td>${l.name}</td><td class="meta-short" title="${l.path}">${l.path}</td><td>${l.tiles}</td><td>${l.dist}</td>
      <td><button class="btn" style="background:#4a3a1a;color:#fa0" onclick="hideFromLandmark('${esc(l.name)}','${esc(l.path)}')">Hide</button></td></tr>`).join('');
}

// ── DevTest ──
const devtestDef = [
  {section:'Rendering (game memory writes)',items:[
    {key:'tweakHideNormalLifeBars',label:'Hide Normal Monster HP Bars',type:'bool'},
    {key:'tweakHideMagicLifeBars',label:'Hide Normal+Magic HP Bars',type:'bool'},
    {key:'tweakHideAllLifeBars',label:'Hide ALL Monster HP Bars',type:'bool'},
    {key:'tweakHideBuffVisuals',label:'Hide Buff/Debuff VFX',type:'bool'},
    {key:'tweakHideNormalRendering',label:'Make Normal Monsters Invisible',type:'bool'},
    {key:'tweakForceShowHover',label:'Force Hover Highlight',type:'bool'},
    {key:'tweakDisableSelectionBoxes',label:'Disable Selection Boxes',type:'bool'},
    {key:'tweakHideInfoDisplay',label:'Hide Info Tooltips',type:'bool'},
    {key:'tweakHideTalismanIcons',label:'Hide Overhead Icons',type:'bool'},
    {key:'tweakForceOutline',label:'Force Outline Glow',type:'bool'},
  ]},
  {section:'Physics & Collision',items:[
    {key:'tweakDisableMonsterBlocking',label:'Disable Monster Collision',type:'bool'},
    {key:'tweakDisableMonsterPush',label:'Disable Push',type:'bool'},
    {key:'tweakEnablePhaseThrough',label:'Phase Through Terrain',type:'bool'},
  ]},
  {section:'Targeting',items:[
    {key:'tweakForceAllTargetable',label:'Force All Targetable',type:'bool'},
    {key:'tweakForceAllAttackable',label:'Force All Attackable',type:'bool'},
  ]},
  {section:'Behavior',items:[
    {key:'tweakFreezeNormalMonsters',label:'Freeze Normals (speed=0)',type:'bool'},
    {key:'tweakPreventCorpseSinking',label:'Prevent Corpse Sinking',type:'bool'},
  ]},
  {section:'World Interaction',items:[
    {key:'tweakInstantTransitions',label:'Instant Zone Transitions',type:'bool'},
    {key:'tweakUnblockDoors',label:'Unblock Doors',type:'bool'},
    {key:'tweakUnlockChests',label:'Unlock Chests',type:'bool'},
    {key:'tweakOpenChestsOnDamage',label:'Open Chests on Damage',type:'bool'},
  ]},
  {section:'Entity Manipulation',items:[
    {key:'tweakSwapTeamToFriendly',label:'Make Monsters Friendly',type:'bool'},
    {key:'tweakMakeAllBoss',label:'Flag All as Boss',type:'bool'},
    {key:'tweakRemoveBossFlag',label:'Remove Boss Flag',type:'bool'},
    {key:'tweakEntityScale',label:'Scale Override (0=off)',type:'num',min:0,max:5,step:0.1},
    {key:'tweakEntityColorR',label:'Tint R (-1=off)',type:'num',min:-1,max:2,step:0.1},
    {key:'tweakEntityColorG',label:'Tint G (-1=off)',type:'num',min:-1,max:2,step:0.1},
    {key:'tweakEntityColorB',label:'Tint B (-1=off)',type:'num',min:-1,max:2,step:0.1},
    {key:'tweakLabelViewDistance',label:'Label Distance (0=off)',type:'num',min:0,max:500,step:10},
  ]},
  {section:'Scope',items:[
    {key:'tweakApplyToNpcs',label:'Apply to NPCs',type:'bool'},
    {key:'tweakApplyToChests',label:'Apply to Chests',type:'bool'},
  ]},
  {section:'Raw Field Writes (very experimental)',items:[
    {key:'tweakDevHideHover',label:'Hide Hover',type:'bool'},
    {key:'tweakDevFadeArrows',label:'Fade Arrows',type:'bool'},
    {key:'tweakDevDisableLight',label:'Disable Lights',type:'bool'},
    {key:'tweakDevFixedSelectionSize',label:'Fixed Selection Size',type:'bool'},
    {key:'tweakDevBBoxIgnoreGround',label:'BBox Ignore Ground',type:'bool'},
    {key:'tweakDevFaceWindDirection',label:'Face Wind',type:'bool'},
    {key:'tweakDevDampenHeight',label:'Dampen Height',type:'bool'},
    {key:'tweakDevHeightOffset',label:'Height Offset',type:'num',min:-200,max:200,step:5},
    {key:'tweakDevSelectionHeightOverride',label:'Selection Height',type:'num',min:0,max:500,step:10},
    {key:'tweakDevLockOrientation',label:'Lock Orientation',type:'bool'},
    {key:'tweakDevMakeFlying',label:'Make Flying',type:'bool'},
    {key:'tweakDevMakeStatic',label:'Make Static',type:'bool'},
    {key:'tweakDevFaceMovementDir',label:'Face Movement Dir',type:'bool'},
    {key:'tweakDevAvoidOthers',label:'Disable Avoidance',type:'bool'},
    {key:'tweakDevLockAnimation',label:'Lock Animation',type:'bool'},
    {key:'tweakDevCorpseUsable',label:'Corpse Usable',type:'bool'},
    {key:'tweakDevNoCorpseMarker',label:'No Corpse Marker',type:'bool'},
  ]},
];
function loadDevTest(){
  let html='';
  for(const sec of devtestDef){
    html+=`<div class="section"><h3 style="color:#f88">${sec.section}</h3>`;
    for(const item of sec.items){
      const v=settings[item.key]??'';
      html+=`<div class="setting-row"><label>${item.label}</label>`;
      if(item.type==='bool')
        html+=`<input type="checkbox" ${v?'checked':''} onchange="setSetting('${item.key}',this.checked)">`;
      else if(item.type==='num')
        html+=`<input type="range" min="${item.min}" max="${item.max}" step="${item.step}" value="${v}"
          oninput="setSetting('${item.key}',parseFloat(this.value));this.nextElementSibling.textContent=this.value">
          <span class="val">${v}</span>`;
      html+=`</div>`;
    }
    html+=`</div>`;
  }
  $('devtestBody').innerHTML=html;
}

// ── Game Data ──
let gdData=[], gdMode='areas';
async function loadGdAreas(){
  gdMode='areas';
  const s=$('gdSearch')?.value||'';
  gdData=await(await fetch(`/api/gamedata/areas?search=${encodeURIComponent(s)}`)).json();
  renderGd();
}
async function loadGdBuffs(){
  gdMode='buffs';
  const s=$('gdSearch')?.value||'';
  gdData=await(await fetch(`/api/gamedata/buffs?search=${encodeURIComponent(s)}&limit=500`)).json();
  renderGd();
}
async function loadGdPins(){
  gdMode='pins';
  const data=await(await fetch('/api/gamedata/pins')).json();
  gdData=data.pins||[];
  $('gdResults').innerHTML=`<h3 style="color:#0af">${data.area} — ${gdData.length} pins</h3>`+
    (gdData.length?`<table style="width:100%"><thead><tr><th>Name</th><th>ID</th><th>Type</th></tr></thead><tbody>`+
    gdData.map(p=>`<tr><td>${p.name}</td><td style="color:#888">${p.id}</td><td>${p.type}</td></tr>`).join('')+
    '</tbody></table>':'<p style="color:#888">No map pins for this zone</p>');
}
function searchGameData(){
  if(gdMode==='areas')loadGdAreas();
  else if(gdMode==='buffs')loadGdBuffs();
}
function renderGd(){
  if(gdMode==='areas'){
    $('gdResults').innerHTML=`<p style="color:#888">${gdData.length} areas</p><table style="width:100%"><thead><tr><th>Code</th><th>Name</th><th>Act</th><th>Lvl</th><th>Town</th><th>WP</th></tr></thead><tbody>`+
      gdData.map(a=>`<tr><td style="color:#0af">${a.code}</td><td>${a.name}</td><td>${a.act}</td><td>${a.level}</td><td>${a.town?'Yes':''}</td><td>${a.waypoint?'Yes':''}</td></tr>`).join('')+
      '</tbody></table>';
  } else if(gdMode==='buffs'){
    $('gdResults').innerHTML=`<p style="color:#888">${gdData.length} buffs</p><table style="width:100%"><thead><tr><th>ID</th><th>Name</th><th>Description</th></tr></thead><tbody>`+
      gdData.map(b=>`<tr><td style="color:#888;font-size:10px">${b.id}</td><td style="color:#0af">${b.name}</td><td style="font-size:11px">${b.description||''}</td></tr>`).join('')+
      '</tbody></table>';
  }
}

// ── MINIMAP ──
const minimapDef = [
  {section:'General',items:[
    {key:'showMinimap',label:'Show Minimap (when big map closed)',type:'bool'},
    {key:'minimapSize',label:'Size (px)',type:'num',min:100,max:500,step:10},
    {key:'minimapScale',label:'Zoom',type:'num',min:0.1,max:2,step:0.05},
    {key:'minimapOpacity',label:'Opacity',type:'num',min:0.2,max:1,step:0.05},
    {key:'minimapAutoAlignToGame',label:'Auto-align to game minimap',type:'bool'},
    {key:'minimapPosition',label:'Corner (topleft, topright, bottomleft, bottomright)',type:'text'},
    {key:'minimapOffsetX',label:'Offset X (px)',type:'num',min:-2000,max:2000,step:5},
    {key:'minimapOffsetY',label:'Offset Y (px)',type:'num',min:-2000,max:2000,step:5},
    {key:'minimapPlayerBlipSize',label:'Player Blip Size',type:'num',min:1,max:10,step:0.5},
    {key:'minimapDotScale',label:'Dot Scale (all dots)',type:'num',min:0.3,max:3,step:0.1},
  ]},
  {section:'Entity Dots — Show/Hide',items:[
    {key:'minimapShowMonsters',label:'Show Monsters',type:'bool'},
    {key:'minimapShowBosses',label:'Show Bosses (large dot)',type:'bool'},
    {key:'minimapShowNpcs',label:'Show NPCs',type:'bool'},
    {key:'minimapShowChests',label:'Show Chests',type:'bool'},
    {key:'minimapShowTransitions',label:'Show Transitions',type:'bool'},
    {key:'minimapShowTerrain',label:'Show Terrain',type:'bool'},
    {key:'minimapShowPath',label:'Show Path Line',type:'bool'},
  ]},
  {section:'Labels — Show/Hide',items:[
    {key:'minimapLabelBoss',label:'Label: Bosses',type:'bool'},
    {key:'minimapLabelUnique',label:'Label: Unique Monsters',type:'bool'},
    {key:'minimapLabelTransition',label:'Label: Transitions / Exits',type:'bool'},
    {key:'minimapLabelNpc',label:'Label: POI NPCs',type:'bool'},
    {key:'minimapLabelWatched',label:'Label: Watched Entities',type:'bool'},
    {key:'minimapLabelFontSize',label:'Label Font Size',type:'num',min:6,max:36,step:1},
  ]},
];
async function loadMinimapSettings(){
  if(!settings||!Object.keys(settings).length) settings=await(await fetch('/api/settings')).json();
  let html='';
  for(const sec of minimapDef){
    html+=`<div class="section"><h3>${sec.section}</h3>`;
    for(const item of sec.items){
      const v=settings[item.key]??'';
      html+=`<div class="setting-row"><label>${item.label}</label>`;
      if(item.type==='bool')
        html+=`<input type="checkbox" ${v?'checked':''} onchange="setSetting('${item.key}',this.checked)">`;
      else if(item.type==='color')
        html+=`<input type="color" value="${v}" onchange="setSetting('${item.key}',this.value)">`;
      else if(item.type==='text')
        html+=`<input type="text" value="${v||''}" style="width:250px" onchange="setSetting('${item.key}',this.value)">`;
      else if(item.type==='num')
        html+=`<input type="range" min="${item.min}" max="${item.max}" step="${item.step}" value="${v}"
          oninput="setSetting('${item.key}',parseFloat(this.value));this.nextElementSibling.textContent=this.value">
          <span class="val">${v}</span>`;
      html+=`</div>`;
    }
    html+=`</div>`;
  }
  $('minimapSettingsBody').innerHTML=html;
}

// ── HIDDEN ENTITIES ──
let hiddenPatterns=[];
async function refreshHidden(){
  hiddenPatterns=await(await fetch('/api/hidden')).json();
  $('hiddenList').innerHTML=hiddenPatterns.map(p=>
    `<div class="watched-item">
      <span style="font-family:monospace;font-size:13px;color:#fa0;flex:1">${p}</span>
      <button class="btn btn-rm" onclick="removeHidden('${esc(p)}')">X</button>
    </div>`
  ).join('')||'<div style="color:#666;padding:8px">No hidden patterns. Everything is visible on the radar.</div>';
}
async function addHidden(){
  const p=$('hiddenAddPattern').value.trim();
  if(!p)return;
  await fetch('/api/hidden',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({pattern:p})});
  $('hiddenAddPattern').value='';
  refreshHidden();refresh();
}
async function removeHidden(pattern){
  await fetch('/api/hidden?pattern='+encodeURIComponent(pattern),{method:'DELETE'});
  refreshHidden();refresh();
}
function hideFromEntity(meta){
  const parts=meta.split('/');
  const short=parts[parts.length-1].replace(/@\d+$/,'');
  const pattern=prompt('Hide pattern (entities matching this will be hidden from the radar):',short);
  if(pattern===null||!pattern.trim())return;
  fetch('/api/hidden',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({pattern:pattern.trim()})})
    .then(()=>{refresh();});
}
function hideFromLandmark(name,path){
  const pattern=prompt('Hide pattern (landmarks matching this will be hidden from the radar):',name);
  if(pattern===null||!pattern.trim())return;
  fetch('/api/hidden',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({pattern:pattern.trim()})})
    .then(()=>{refreshLandmarks();});
}

// ---- ATLAS WAYPOINTS ----
async function loadAtlas(){
  try{
    atlasData=await(await fetch('/api/atlas')).json();
    atlasPins=new Set((atlasData.pinned||[]).map(String));
    syncAtlasRulesFromData();
    renderAtlas();
    renderAtlasRules();
  }catch(ex){
    $('atlasStatus').textContent='Atlas API error: '+ex.message;
  }
}
function renderAtlas(){
  const box=$('atlasBody'); if(!box)return;
  const q=($('atlasSearch')?.value||'').toLowerCase();
  const nodes=(atlasData&&atlasData.nodeList)||[];
  const filtered=nodes.filter(n=>{
    const hay=[n.label,n.map,n.mapSource,(n.tags||[]).join(' '),(n.mapCandidates||[]).join(' ')].join(' ').toLowerCase();
    return !q||hay.includes(q);
  }).slice(0,600);
  $('atlasStatus').textContent=atlasData
    ? `${atlasData.total||0} nodes · ${atlasPins.size} pinned${atlasData.open?'':' · atlas closed/no live nodes'}`
    : 'Open the Atlas in-game, then refresh.';
  box.innerHTML=filtered.map(n=>{
    const pinned=atlasPins.has(String(n.el));
    const tags=(n.tags||[]).slice(0,4).map(t=>`<span class="atlas-chip">${esc(t)}</span>`).join('');
    const state=[n.visible?'visible':'hidden',n.visited?'visited':'unvisited',n.unlocked?'unlocked':'locked'].join(' · ');
    const map=n.map||n.label||('Node '+n.id);
    return `<tr class="${pinned?'watched':''}">
      <td><b style="color:${pinned?'#ffd76d':'#fff'}">${esc(map)}</b></td>
      <td>${tags||'<span class="atlas-muted">none</span>'}</td>
      <td class="atlas-muted">${state}</td>
      <td class="atlas-muted">${n.x}, ${n.y}</td>
      <td><button class="btn ${pinned?'btn-rm':'atlas-pin'}" onclick="toggleAtlasPin('${n.el}')">${pinned?'Unpin':'Pin'}</button></td>
    </tr>`;
  }).join('')||'<tr><td colspan="5" class="atlas-muted">No matching Atlas nodes. Open the Atlas in-game and refresh.</td></tr>';
}
async function toggleAtlasPin(el){
  el=String(el);
  if(atlasPins.has(el))atlasPins.delete(el);else atlasPins.add(el);
  await postAtlasPins();
  renderAtlas();
}
async function clearAtlasPins(){
  atlasPins.clear();
  await postAtlasPins();
  renderAtlas();
}
async function postAtlasPins(){
  await fetch('/api/atlas-pins',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({pins:[...atlasPins]})});
}
function syncAtlasRulesFromData(){
  settings.atlasHighlightTags=(atlasData.highlightTags||settings.atlasHighlightTags||[]).slice();
  settings.atlasArrowTags=(atlasData.arrowTags||settings.atlasArrowTags||[]).slice();
  settings.atlasHighlightColors=atlasData.highlightColors||settings.atlasHighlightColors||{};
  settings.atlasRuleLabels=atlasData.ruleLabels||settings.atlasRuleLabels||{};
}
function atlasRuleRows(){
  const rows=[];
  (atlasData?.allMaps||[]).forEach(x=>rows.push({title:x.tag,count:x.count,type:'Map'}));
  (atlasData?.allTags||[]).forEach(x=>rows.push({title:x.tag,count:x.count,type:'Content'}));
  return rows;
}
function defaultAtlasRuleColor(title,type){
  const s=String(title||'').toLowerCase();
  if(s.includes('citadel'))return'#e0b341';
  if(s.includes('boss'))return'#ff4040';
  if(s.includes('breach'))return'#b05cff';
  if(s.includes('ritual'))return'#ff4d6d';
  if(s.includes('delirium'))return'#c8c8c8';
  if(s.includes('expedition'))return'#26e6d9';
  if(s.includes('corrupt'))return'#ff66ff';
  if(s.includes('tower'))return'#66aaff';
  return type==='Map'?'#6ee888':'#ff9e42';
}
function renderAtlasRules(){
  const box=$('atlasRuleBody'); if(!box)return;
  const q=($('atlasRuleSearch')?.value||'').toLowerCase();
  const track=new Set((settings.atlasHighlightTags||[]).map(x=>x.toLowerCase()));
  const arrows=new Set((settings.atlasArrowTags||[]).map(x=>x.toLowerCase()));
  const colors=settings.atlasHighlightColors||(settings.atlasHighlightColors={});
  const labels=settings.atlasRuleLabels||(settings.atlasRuleLabels={});
  const activeFilter=atlasRuleFilterTrack||atlasRuleFilterArrow;
  $('atlasRuleTrackFilter')?.classList.toggle('on',atlasRuleFilterTrack);
  $('atlasRuleArrowFilter')?.classList.toggle('on',atlasRuleFilterArrow);
  const rows=atlasRuleRows().filter(r=>{
    const key=r.title.toLowerCase();
    const alias=(labels[r.title]||'').toLowerCase();
    const tr=track.has(key), ar=arrows.has(key);
    if(activeFilter&&!((atlasRuleFilterTrack&&tr)||(atlasRuleFilterArrow&&ar)))return false;
    return !q||key.includes(q)||alias.includes(q);
  }).slice(0,500);
  $('atlasRuleStatus').textContent=`${settings.atlasHighlightTags?.length||0} tracked · ${settings.atlasArrowTags?.length||0} arrows`;
  box.innerHTML=rows.map(r=>{
    const key=r.title.toLowerCase();
    const tr=track.has(key), ar=arrows.has(key);
    const col=colors[r.title]||defaultAtlasRuleColor(r.title,r.type);
    const alias=labels[r.title]||'';
    const display=alias||r.title;
    const title=alias?`${alias} (${r.title})`:r.title;
    return `<div class="atlas-rule-row">
      <button class="${tr?'on':''}" onclick="toggleAtlasRule('${esc(r.title)}','track')">${tr?'On':'Off'}</button>
      <button class="arrow ${ar?'on':''}" onclick="toggleAtlasRule('${esc(r.title)}','arrow')">${ar?'On':'Off'}</button>
      <input type="color" value="${col}" onchange="setAtlasRuleColor('${esc(r.title)}',this.value)">
      <button class="rename" title="Rename rule" onclick="renameAtlasRule('${esc(r.title)}')">&#9998;</button>
      <span title="${esc(title)}">${esc(display)}</span>
      <span class="atlas-muted">${r.count}</span>
      <span class="atlas-muted">${r.type}</span>
    </div>`;
  }).join('')||'<div class="atlas-muted" style="padding:8px">No live Atlas rule candidates yet.</div>';
}
function toggleAtlasRuleFilter(kind){
  if(kind==='track')atlasRuleFilterTrack=!atlasRuleFilterTrack;
  if(kind==='arrow')atlasRuleFilterArrow=!atlasRuleFilterArrow;
  renderAtlasRules();
}
async function toggleAtlasRule(title,kind){
  settings.atlasHighlightTags=settings.atlasHighlightTags||[];
  settings.atlasArrowTags=settings.atlasArrowTags||[];
  settings.atlasHighlightColors=settings.atlasHighlightColors||{};
  const arr=kind==='arrow'?settings.atlasArrowTags:settings.atlasHighlightTags;
  const i=arr.findIndex(x=>x.toLowerCase()===title.toLowerCase());
  if(i>=0)arr.splice(i,1);else arr.push(title);
  if(!settings.atlasHighlightColors[title])settings.atlasHighlightColors[title]=defaultAtlasRuleColor(title,'Content');
  settings.atlasRulesInitialized=true;
  await saveAtlasRuleSettings();
  renderAtlasRules();
}
async function setAtlasRuleColor(title,color){
  settings.atlasHighlightColors=settings.atlasHighlightColors||{};
  settings.atlasHighlightColors[title]=color;
  settings.atlasRulesInitialized=true;
  await saveAtlasRuleSettings();
}
async function renameAtlasRule(title){
  settings.atlasRuleLabels=settings.atlasRuleLabels||{};
  const current=settings.atlasRuleLabels[title]||title;
  const name=prompt('Rename Atlas ring rule:',current);
  if(name===null)return;
  const trimmed=name.trim();
  if(!trimmed||trimmed===title)delete settings.atlasRuleLabels[title];
  else settings.atlasRuleLabels[title]=trimmed;
  settings.atlasRulesInitialized=true;
  await saveAtlasRuleSettings();
  renderAtlasRules();
}
async function clearAtlasRules(){
  settings.atlasHighlightTags=[];
  settings.atlasArrowTags=[];
  settings.atlasHighlightColors={};
  settings.atlasRuleLabels={};
  settings.atlasRulesInitialized=true;
  await saveAtlasRuleSettings();
  renderAtlasRules();
}
async function saveAtlasRuleSettings(){
  await fetch('/api/settings',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({
    atlasHighlightTags:settings.atlasHighlightTags||[],
    atlasArrowTags:settings.atlasArrowTags||[],
    atlasHighlightColors:settings.atlasHighlightColors||{},
    atlasRuleLabels:settings.atlasRuleLabels||{},
    atlasRulesInitialized:settings.atlasRulesInitialized||false
  })});
}

function inspectFromList(addr){
  showTab('inspector');
  setTimeout(()=>{
    $('inspEntity').value=addr;
    inspSelectedAddr=addr;
    inspectEntity();
  },100);
}

// ── Inspector ──
let inspEntities=[], inspTimer=null, inspSelectedAddr='';
async function loadInspectorEntities(){
  try{
    const data=await(await fetch('/entities?limit=999')).json();
    inspEntities=data;
    const sel=$('inspEntity');
    sel.innerHTML='<option value="">-- Select entity --</option>'+
      data.map(e=>`<option value="${e.addr}">[${e.sleeping?'SLEEP':'AWAKE'}] [${e.category}] ${e.name||e.metadata.split('/').pop()} (${e.addr})${e.boss?' ★':''}${e.locked?' 🔒':''}</option>`).join('');
    $('inspStatus').textContent=`${data.length} entities`;
  }catch(ex){$('inspStatus').textContent='Error: '+ex.message}
}
let inspSchema={};
async function loadInspectorSchema(){
  try{
    const comps=await(await fetch('/api/inspect/components')).json();
    if(Array.isArray(comps)){
      for(const c of comps){
        const s=await(await fetch(`/api/inspect/schema?component=${c.name}`)).json();
        if(s.fields) inspSchema[c.name]={};
        for(const f of (s.fields||[])) inspSchema[c.name][f.name]={offset:f.offset,type:f.type,verified:f.verified,notes:f.notes};
      }
    }
  }catch{}
}
async function inspectEntity(){
  const addr=$('inspEntity').value;
  if(!addr){
    $('inspResults').innerHTML='';
    $('inspComponents').innerHTML='';
    $('inspEntitySummary').style.display='none';
    return;
  }
  inspSelectedAddr=addr;
  const entity=inspEntities.find(e=>e.addr===addr);
  if(entity){
    const summary=$('inspEntitySummary');
    summary.style.display='block';
    summary.innerHTML=`
      <span class="source-state ${entity.sleeping?'source-sleeping':'source-awake'}">${entity.sleeping?'SLEEPING':'AWAKE'}</span>
      <b style="margin-left:8px;color:#fff">${entity.name||entity.metadata.split('/').pop()}</b>
      <span style="color:#888;margin-left:8px">Dist ${entity.dist} · ${entity.lifeState||'No Life'}${entity.mechanicAnchor?' · Mechanic anchor':''}${entity.league&&entity.league!=='None'?' · '+entity.league:''}</span>
      <div style="font-family:monospace;color:#999;margin-top:5px;overflow-wrap:anywhere">${entity.metadata}</div>`;
  }
  try{
    const data=await(await fetch(`/api/inspect?entity=${addr}`)).json();
    if(data.error){$('inspResults').innerHTML=`<p style="color:#f66">${data.error}</p>`;return}
    const comps=Object.keys(data.components||{});
    const compCount=comps.length;
    const fieldCount=comps.reduce((s,c)=>s+Object.keys(data.components[c]||{}).length,0);
    $('inspComponents').innerHTML=`<span style="color:#888;font-size:11px">${compCount} components, ${fieldCount} fields</span> `+
      comps.map(c=>`<span style="background:#333;padding:2px 8px;border-radius:4px;cursor:pointer;font-size:11px" onclick="scrollToComp('${c}')">${c}</span>`).join('');
    let html='';
    for(const[name,fields]of Object.entries(data.components||{})){
      const fc=Object.keys(fields||{}).length;
      html+=`<h3 id="insp-${name}" style="margin:12px 0 4px;color:#0af">${name} <span style="color:#666;font-size:11px">(${fc} fields)</span></h3>`;
      const schema=inspSchema[name]||{};
      html+='<table style="width:100%"><thead><tr><th style="text-align:left">Field</th><th>Offset</th><th>Type</th><th>Value</th></tr></thead><tbody>';
      for(const[fn,fv]of Object.entries(fields||{})){
        const s=schema[fn]||{};
        const nonZero=fv!==null&&fv!==0&&fv!==false;
        const style=nonZero?'color:#eee':'color:#555';
        const val=fv===null?'<span style="color:#444">null</span>':typeof fv==='object'?`<span style="color:#8af">${JSON.stringify(fv)}</span>`:`<b style="${style}">${fv}</b>`;
        const verified=s.verified?'<span style="color:#4f4" title="Verified">&#10003;</span>':'';
        const notes=s.notes?` <span style="color:#666;font-size:10px" title="${s.notes}">[?]</span>`:'';
        html+=`<tr><td style="color:#aaa">${fn}${verified}${notes}</td><td style="color:#666;font-size:10px">${s.offset||''}</td><td style="color:#666;font-size:10px">${s.type||''}</td><td>${val}</td></tr>`;
      }
      html+='</tbody></table>';
    }
    $('inspResults').innerHTML=html||'<p style="color:#888">No components resolved (entity may be out of range)</p>';
  }catch(ex){$('inspResults').innerHTML=`<p style="color:#f66">${ex.message}</p>`}
}
function scrollToComp(name){const el=document.getElementById('insp-'+name);if(el)el.scrollIntoView({behavior:'smooth'})}
function inspAutoTick(){
  if($('inspAutoRefresh')?.checked && inspSelectedAddr && document.getElementById('tab-inspector')?.classList.contains('active'))
    inspectEntity();
}

// ── KEYBINDS ──
const keybindsDef=[
  {key:'keyCheat1',label:'Cheat: No Atlas Fog',def:0x70},
  {key:'keyCheat2',label:'Cheat: Reveal Map',def:0x71},
  {key:'keyCheat3',label:'Cheat: Infinite Zoom',def:0x72},
  {key:'keyCheat4',label:'Cheat: Enemy HP Bars',def:0x73},
  {key:'keyCheat5',label:'Cheat: Light Radius',def:0x74},
  {key:'keyCycleLandmarks',label:'Cycle Landmarks (path-to)',def:0x75},
  {key:'keyCycleEntities',label:'Cycle Entities (path-to)',def:0x76},
  {key:'keyAutoFlask',label:'Toggle Auto-Flask',def:0x77},
  {key:'keySettings',label:'Open Settings Panel',def:0x78},
  {key:'keyToggleOverlay',label:'Toggle Overlay Visibility',def:0x79},
  {key:'keyDashboard',label:'Open Dashboard',def:0x7A},
];
const VK_DISPLAY={0x70:'F1',0x71:'F2',0x72:'F3',0x73:'F4',0x74:'F5',0x75:'F6',0x76:'F7',0x77:'F8',0x78:'F9',0x79:'F10',0x7A:'F11',0x7B:'F12',
  0x31:'1',0x32:'2',0x33:'3',0x34:'4',0x35:'5',0x36:'6',0x37:'7',0x38:'8',0x39:'9',0x30:'0',
  0x41:'A',0x42:'B',0x43:'C',0x44:'D',0x45:'E',0x46:'F',0x47:'G',0x48:'H',0x49:'I',0x4A:'J',
  0x4B:'K',0x4C:'L',0x4D:'M',0x4E:'N',0x4F:'O',0x50:'P',0x51:'Q',0x52:'R',0x53:'S',0x54:'T',
  0x55:'U',0x56:'V',0x57:'W',0x58:'X',0x59:'Y',0x5A:'Z',
  0x60:'Num0',0x61:'Num1',0x62:'Num2',0x63:'Num3',0x64:'Num4',0x65:'Num5',0x66:'Num6',0x67:'Num7',0x68:'Num8',0x69:'Num9',
  0x6A:'Num*',0x6B:'Num+',0x6D:'Num-',0x6E:'Num.',0x6F:'Num/',
  0xBE:'.',0xBC:',',0xBA:';',0xBF:'/',0xC0:'`',0xDB:'[',0xDD:']',0xDC:'\\\\',0xDE:"'",0xBD:'-',0xBB:'='};
function vkDisplay(code){return VK_DISPLAY[code]||('0x'+code.toString(16).toUpperCase());}
function loadKeybinds(){
  if(!settings||!Object.keys(settings).length)return;
  let html='';
  const seen={};
  for(const kb of keybindsDef){
    const vk=settings[kb.key]??kb.def;
    if(seen[vk])seen[vk].push(kb.key);else seen[vk]=[kb.key];
  }
  for(const kb of keybindsDef){
    const vk=settings[kb.key]??kb.def;
    const dup=seen[vk]&&seen[vk].length>1;
    html+=`<div class="setting-row">
      <label>${kb.label}</label>
      <input type="text" readonly value="${vkDisplay(vk)}" id="kb_${kb.key}"
        style="width:80px;text-align:center;cursor:pointer;background:#1e1e28;border:1px solid ${dup?'#f55':'#555'};color:#78b4ff;font-weight:bold"
        onfocus="this.value='... press key ...';this.style.borderColor='#78b4ff'"
        onkeydown="captureKey(event,'${kb.key}');return false"
        onblur="this.value=vkDisplay(settings['${kb.key}']??${kb.def});this.style.borderColor='#555'">
      <span style="color:#666;font-size:11px;margin-left:8px">VK: 0x${vk.toString(16).toUpperCase()}</span>
      ${dup?'<span style="color:#f55;font-size:11px;margin-left:4px">duplicate!</span>':''}
    </div>`;
  }
  $('keybindsBody').innerHTML=html;
}
function captureKey(e,settingKey){
  e.preventDefault();e.stopPropagation();
  const vk=e.keyCode;
  settings[settingKey]=vk;
  const el=$('kb_'+settingKey);
  el.value=vkDisplay(vk);
  el.style.borderColor='#5f5';
  setTimeout(()=>{el.style.borderColor='#555';el.blur();loadKeybinds();},300);
}
async function saveKeybinds(){
  await fetch('/api/settings',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(settings)});
  $('kbSavedMsg').classList.add('show');setTimeout(()=>$('kbSavedMsg').classList.remove('show'),1500);
}

try{
  const savedTab=localStorage.getItem('radarActiveTab');
  if(savedTab&&$('tab-'+savedTab))showTab(savedTab);
}catch{}
refresh();refreshWatched();setInterval(refresh,2000);setInterval(inspAutoTick,2000);
</script></body></html>
""";
}
