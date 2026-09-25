"""IA mágica de NPC en la sala (CoopRoom.NpcMagic.cs, reglas de AONpcSpellRulesV902): rango 11 x 9, daño con la
resistencia mágica del jugador (AntiRm la ignora), área de AreaRadio alrededor del objetivo, intervalo de hechizo y
melee solo si no lanzó (DontHitVisiblePlayers). Puerto, clave y guardados temporales."""
import json
import pathlib
import secrets
import socket
import subprocess
import tempfile
import time

from test_coop_server import DLL, Peer, fixture, snapshot

ARMOR = 600   # armadura con 50 % de resistencia mágica


def run():
    catalog = fixture()
    catalog['items'].append(dict(index=ARMOR, name='Túnica', value=1, objType=3, magicResistance=50))
    catalog['spells'] += [dict(id=3, target=4, areaRadius=22, raiseHp=2, minHp=7, maxHp=7, antiRm=1),
                          dict(id=4, target=3, paralyze=1, duration=10),
                          dict(id=5, target=3, raiseHp=2, minHp=10, maxHp=10)]
    base = dict(catalog['maps'][0]['npcs'][0], hostile=True, movement=3, moveIntervalMs=250, visionRange=15,
                visionRangeX=15, visionRangeY=13, attackRange=1, minHit=0, maxHit=0, attackPower=0)
    # El jugador de prueba entra en (50, 51). ids del servidor = posición en la lista (1..4).
    catalog['maps'][0]['npcs'] = [
        dict(base, npcIndex=901, name='Brujo', x=42, y=51),                                  # dx 8: lanza 5
        dict(base, npcIndex=902, name='Lejano', x=38, y=51),                                 # dx 12: fuera de 11
        dict(base, npcIndex=903, name='Medusa', x=50, y=52, minHit=50, maxHit=50, attackPower=100000),   # pegado
        dict(base, npcIndex=904, name='Dragón', x=60, y=51)]                                 # área con AntiRm
    catalog['npcSpells'] = dict(npcs=[
        dict(npcIndex=901, spells=[5], castIntervalMs=400),
        dict(npcIndex=902, spells=[5], castIntervalMs=400),
        dict(npcIndex=903, spells=[4], castIntervalMs=100000, dontHitVisiblePlayers=True),
        dict(npcIndex=904, spells=[3], castIntervalMs=600)])
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
            peer = Peer(port, key, 'Blanco', save=save)
            hurts, end = [], time.monotonic() + 3.2
            while time.monotonic() < end:
                time.sleep(.25)
                result = peer.action('sync')
                fresh = [e for e in result['events'] if e['seq'] > peer.ack]
                peer.events(result['events'])
                hurts += [e for e in fresh if e['type'] == 'hurt']
            by = lambda npc: [e for e in hurts if e.get('npc') == npc]   # noqa: E731
            brujo, lejano, medusa, dragon = by(1), by(2), by(3), by(4)
            assert len(brujo) >= 4 and all(e['spell'] == 5 and e['damage'] == 5 and e['text'] == 'Brujo' for e in brujo), brujo
            assert not lejano, f'lanzó fuera del rango 11 x 9: {lejano}'
            assert len(medusa) == 1 and medusa[0]['spell'] == 4 and medusa[0]['damage'] == 0, medusa
            assert not [e for e in hurts if not e.get('spell')], 'pegó cuerpo a cuerpo teniendo DontHitVisiblePlayers'
            assert len(dragon) >= 3 and all(e['spell'] == 3 and e['damage'] == 7 for e in dragon), dragon
            print(f'PASS: NPC lanzan hechizos ({len(brujo)} del Brujo con la resistencia al 50 %, {len(dragon)} de área con AntiRm, '
                  'parálisis de la Medusa sin melee); nada fuera de 11 x 9.')
        finally:
            if peer is not None: peer.close()
            if process.poll() is None:
                process.terminate(); process.wait(5)
            log.close()  # Windows cannot delete the temp folder while the log is open


if __name__ == '__main__':
    run()
