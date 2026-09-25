"""Export gameplay data used by the private cooperative room (no textures)."""
import json, gzip, base64, struct, hashlib
from pathlib import Path

project = Path(__file__).resolve().parents[1]
resources = project / "Assets/Resources/AOMigrator"
def read(path):
    return json.loads((resources / path).read_text(encoding="utf-8-sig"))

maps = []
door_defs = {d['objIndex']: d for d in read('WorldV07/door_catalog.json')['doors']}
for path in sorted((resources / 'WorldV07/Maps').glob('map_*.json')):
    source = json.loads(path.read_text(encoding='utf-8-sig'))
    data = {k: source[k] for k in ('mapNumber', 'xmin', 'xmax', 'ymin', 'ymax', 'blocks', 'triggers', 'exits')}
    # Demo maps (demo_map_builder.py): death drops (dungeon floors) and duel rings (arena zone).
    for key in ('dropOnDeath', 'arenaRings'):
        if source.get(key): data[key] = source[key]
    flags, triggers = [0]*10201, [0]*10201
    for b in data.pop('blocks'): flags[b['y']*101+b['x']] = b['flags']
    for b in data.pop('triggers'): triggers[b['y']*101+b['x']] = b['trigger']
    data['grid'] = base64.b64encode(gzip.compress(struct.pack('<20402H', *(flags+triggers)), mtime=0)).decode('ascii')
    data['npcs'] = [{k:v for k,v in npc.items() if k != 'directions'} for npc in source.get('npcs', [])]
    # NPC ids are array positions: if the list changes, the server must reset that map's NPCs.
    layout = json.dumps([[n.get('npcIndex'), n.get('x'), n.get('y')] for n in data['npcs']], separators=(',',':'))
    data['npcLayoutVersion'] = source.get('npcLayoutVersion') or hashlib.sha1(layout.encode()).hexdigest()[:16]
    data['doors'] = []
    for obj in source.get('objects', []):
        door = door_defs.get(obj['objIndex'])
        if door and door.get('openFrame'):
            width = min(2, max(1, (obj.get('frames', [{}])[0].get('width',32)+31)//32))
            data['doors'].append(dict(x=obj['x'], y=obj['y'], width=width, locked=door.get('locked',False)))
    maps.append(data)

items = [{k:v for k,v in item.items() if k not in ('icon','visualDirections','description')}
         for item in read('ItemsV10/items.json')['items']]
spells = [{k:v for k,v in s.items() if k not in ('icon','fxFrames','description')}
          for s in read('MagicV129/spells.json')['spells']]
effects = {e['id']: e for e in read('MagicV129/effects.json')['effects']}
for spell in spells:
    effect = effects.get(spell.get('eotId'), {})
    spell['hostileEffect'] = int(effect.get('buffType') in (2,4) or (effect.get('type') == 1 and effect.get('tickPowerMax', 0) < 0))
catalog = dict(maps=maps, loot=read('LootV180/npc_loot.json'), items=items, spells=spells,
               summons=read('MagicV129/summons.json'), npcMagic=read('MagicV129/npc_magic.json'),
               shops=read('CityV130/city_npcs.json'),
               # Protocol 3: the server pays quest gold from here, never from the client.
               quests=[dict(id=q['id'], rewardGold=q.get('rewardGold',0), repeatable=bool(q.get('repeatable')))
                       for q in read('QuestsV150/quests.json')['quests']],
               # Retos of the demo (Retos.dat + docs/claude/demo/decisiones.md): only used with --demo.
               retos=dict(minBet=1000, maxTeam=5, taxPercent=10, maxSeconds=600, countdownSeconds=15,
                          inviteSeconds=60, graceSeconds=30, maps=[1000, 1001], arenas=[]))
# Rings come from the maps (arenaRings: id, x, y, w, h, theme); until map 1001 exists, the 4 interiors of 324
# in Retos.dat. The generator (AOArenaGen) only makes 23x19 rings; theme = AOArenaGen.Theme, fixed per ring.
THEMES = ['Bosque', 'Desierto', 'Nieve', 'Mazmorra', 'Pantano', 'Ciudad']   # order of AOArenaGen.Theme
for m in maps:
    for ring in m.get('arenaRings', []):
        if (ring.get('w', 23), ring.get('h', 19)) != (23, 19): raise SystemExit(f"Ring {ring} del mapa {m['mapNumber']}: tiene que ser 23x19.")
        theme = ring.get('theme', 0)
        if isinstance(theme, str):
            if theme not in THEMES: raise SystemExit(f"Ring {ring} del mapa {m['mapNumber']}: tema desconocido.")
            theme = THEMES.index(theme)
        catalog['retos']['arenas'].append(dict(sala=ring['id'], map=m['mapNumber'], x=ring['x'], y=ring['y'], theme=theme))
if not catalog['retos']['arenas']:
    catalog['retos']['arenas'] = [dict(sala=1, map=1001, x=13, y=11, theme=0), dict(sala=2, map=1001, x=65, y=11, theme=1),
                                  dict(sala=3, map=1001, x=13, y=73, theme=2), dict(sala=4, map=1001, x=66, y=73, theme=3)]
for summon in catalog['summons'].get('summons',[]):
    summon.pop('directions', None)
target = project / 'OnlineServer/Data/catalog.json.gz'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_bytes(gzip.compress(json.dumps(catalog, ensure_ascii=False, separators=(',',':')).encode('utf-8'), compresslevel=6, mtime=0))
print(f'Catálogo: {len(maps)} mapas, {sum(len(m["npcs"]) for m in maps)} NPC; {target.stat().st_size:,} bytes.')
