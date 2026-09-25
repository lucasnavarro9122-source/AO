"""IA mágica de NPC en la sala (CoopRoom.NpcMagic.cs, reglas de AONpcSpellRulesV902): rango 11 x 9, daño con la
resistencia mágica del jugador (AntiRm la ignora), área de AreaRadio alrededor del objetivo, intervalo de hechizo,
melee solo si no lanzó (DontHitVisiblePlayers), IA de apoyo (Movement 11: RangoSpell, sin melee), invocaciones
(tope CantidadInvocaciones, mueren con el invocador) y el aviso npcCast para todos. Puerto, clave y guardados temporales."""
import json
import pathlib
import secrets
import socket
import subprocess
import tempfile
import time

from test_coop_server import DLL, Peer, fixture, snapshot
from test_duel_server import DuelPeer   # keeps the pushes (npcCast) that arrive while waiting

ARMOR = 600   # armadura con 50 % de resistencia mágica


def run():
    catalog = fixture()
    catalog['items'].append(dict(index=ARMOR, name='Túnica', value=1, objType=3, magicResistance=50))
    catalog['spells'] += [dict(id=3, target=4, areaRadius=22, raiseHp=2, minHp=7, maxHp=7, antiRm=1),
                          dict(id=4, target=3, paralyze=1, duration=10),
                          dict(id=5, target=3, raiseHp=2, minHp=10, maxHp=10),
                          dict(id=7, target=4, areaRadius=1, summonNpc=1226, summonCount=2)]
    base = dict(catalog['maps'][0]['npcs'][0], hostile=True, movement=3, moveIntervalMs=250, visionRange=15,
                visionRangeX=15, visionRangeY=13, attackRange=1, minHit=0, maxHit=0, attackPower=0)
    # El jugador de prueba entra en (50, 51). ids del servidor = posición en la lista (1..4).
    catalog['maps'][0]['npcs'] = [
        dict(base, npcIndex=901, name='Brujo', x=42, y=51),                                  # dx 8: lanza 5
        dict(base, npcIndex=902, name='Lejano', x=38, y=51),                                 # dx 12: fuera de 11
        dict(base, npcIndex=903, name='Medusa', x=50, y=52, minHit=50, maxHit=50, attackPower=100000),   # pegado
        dict(base, npcIndex=904, name='Dragón', x=60, y=51),                                 # área con AntiRm
        dict(base, npcIndex=905, name='Devorador', x=58, y=48, maxHp=2),                     # invoca (tope 3)
        dict(base, npcIndex=906, name='Espíritu', x=36, y=51)]                               # IA de apoyo, dx 14
    catalog['npcSpells'] = dict(npcs=[
        dict(npcIndex=901, spells=[5], castIntervalMs=400),
        dict(npcIndex=902, spells=[5], castIntervalMs=400),
        dict(npcIndex=903, spells=[4], castIntervalMs=100000, dontHitVisiblePlayers=True),
        dict(npcIndex=904, spells=[3], castIntervalMs=600),
        dict(npcIndex=905, spells=[7], castIntervalMs=300, summonLimit=3),
        dict(npcIndex=906, spells=[5], castIntervalMs=500, movement=11, rangeSpell=18, cooldowns=[0])],
        summoned=[dict(base, npcIndex=1226, name='Elemental', hostile=False, maxHp=150)])
    with tempfile.TemporaryDirectory(prefix='ao-npc-magic-test-') as temp:
        root = pathlib.Path(temp); key = secrets.token_hex(18)
        (root/'key').write_text(key); (root/'catalog.json').write_text(json.dumps(catalog))
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        log = (root/'server.log').open('w')
        process = subprocess.Popen(['dotnet', str(DLL), '--port', str(port), '--key-file', str(root/'key'),
                                    '--catalog', str(root/'catalog.json'), '--data', str(root/'saves')],
                                   stdout=log, stderr=log)
        peer = None
        try:
            for _ in range(60):
                try:
                    socket.create_connection(('127.0.0.1', port), timeout=.1).close(); break
                except OSError: time.sleep(.1)
            save = snapshot('Blanco')   # la sala lee el equipo del inventario del snapshot
            save['inventory']['itemIndices'][0], save['inventory']['amounts'][0], save['inventory']['armor'] = ARMOR, 1, ARMOR
            save['rpg']['maxHp'] = save['combat']['hp'] = 1000   # survives every caster of the test at once
            peer = DuelPeer(port, key, 'Blanco', save=save)
            hurts, end = [], time.monotonic() + 3.2
            while time.monotonic() < end:
                time.sleep(.25)
                result = peer.action('sync')
                fresh = [e for e in result['events'] if e['seq'] > peer.ack]
                peer.events(result['events'])
                hurts += [e for e in fresh if e['type'] == 'hurt']
            by = lambda npc: [e for e in hurts if e.get('npc') == npc]   # noqa: E731
            brujo, lejano, medusa, dragon, espiritu = by(1), by(2), by(3), by(4), by(6)
            assert len(brujo) >= 4 and all(e['spell'] == 5 and e['damage'] == 5 and e['text'] == 'Brujo' for e in brujo), brujo
            assert not lejano, f'lanzó fuera del rango 11 x 9: {lejano}'
            assert len(medusa) == 1 and medusa[0]['spell'] == 4 and medusa[0]['damage'] == 0, medusa
            assert not [e for e in hurts if not e.get('spell') and e['text'] in ('Medusa', 'Espíritu')], 'melee de un lanzador que no debe pegar'
            # Support AI: beyond the classic 11 x 9 but inside RangoSpell 18 (Euclidean); spells only.
            assert len(espiritu) >= 3 and all(e['spell'] == 5 and e['damage'] == 5 for e in espiritu), espiritu
            # Everybody on the map sees each cast (npcCast), with the caster and the spell.
            casts = [m for m in peer.log if m['type'] == 'npcCast']
            assert any(m['id'] == 1 and m['spell'] == 5 and m['target'] == peer.id for m in casts), casts[:5]
            assert any(m['id'] == 5 and m['spell'] == 7 for m in casts), 'no se vio la invocación'
            # Summons: up to CantidadInvocaciones (3) alive, far ids, the summoned creature's index.
            state = peer.until(lambda m: m['type'] == 'state')
            summons = [n for n in state['npcs'] if n['id'] > 100000 and not n['dead']]
            assert len(summons) == 3 and all(n['npc'] == 1226 for n in summons), summons
            # The summoner dies (one spell, 2 HP): its creatures go with it.
            time.sleep(1.2)   # the player's cast cooldown
            result = peer.action('cast', spell=2, target=5, x=58, y=48); assert result['ok'], result
            gone = peer.until(lambda m: m['type'] == 'state' and not any(n['id'] > 100000 for n in m['npcs']))
            assert any(n['id'] == 5 and n['dead'] for n in gone['npcs']), 'el invocador no murió'
            assert len(dragon) >= 3 and all(e['spell'] == 3 and e['damage'] == 7 for e in dragon), dragon
            print(f'PASS: NPC lanzan hechizos ({len(brujo)} del Brujo con la resistencia al 50 %, {len(dragon)} de área con AntiRm, '
                  f'parálisis de la Medusa sin melee, {len(espiritu)} de la IA de apoyo a 14 casillas); nada fuera de 11 x 9; '
                  'todos ven npcCast; 3 invocadas como tope, mueren con el invocador.')
        finally:
            if peer is not None: peer.close()
            if process.poll() is None:
                process.terminate(); process.wait(5)
            log.close()  # Windows cannot delete the temp folder while the log is open


if __name__ == '__main__':
    run()
