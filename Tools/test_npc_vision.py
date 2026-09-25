"""Visión de NPC como el original: 15 x 13 por eje; sin visionRangeX/Y, radio cuadrado visionRange. Puerto, clave y guardados temporales."""
import json
import pathlib
import secrets
import socket
import subprocess
import tempfile
import time

from test_coop_server import DLL, Peer, fixture


def run():
    catalog = fixture()
    base = dict(catalog['maps'][0]['npcs'][0], hostile=True, moveIntervalMs=4000, attackRange=1,
                minHit=0, maxHit=0, attackPower=0)
    axes = dict(visionRange=15, visionRangeX=15, visionRangeY=13)
    # El jugador de prueba entra en (50, 51).
    npcs = {301: dict(base, npcIndex=301, x=35, y=51, **axes),   # dx 15: ve
            302: dict(base, npcIndex=302, x=50, y=37, **axes),   # dy 14: no ve (13 en vertical)
            303: dict(base, npcIndex=303, x=50, y=38, **axes),   # dy 13: ve
            304: dict(base, npcIndex=304, x=41, y=51, visionRange=8),   # catálogo viejo, dx 9: no ve
            305: dict(base, npcIndex=305, x=42, y=51, visionRange=8)}   # catálogo viejo, dx 8: ve
    catalog['maps'][0]['npcs'] = list(npcs.values())
    expected = {301: True, 302: False, 303: True, 304: False, 305: True}
    with tempfile.TemporaryDirectory(prefix='ao-vision-test-') as temp:
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
            peer = Peer(port, key, 'Vigia')
            state = peer.until(lambda m: m['type'] == 'state' and any(n.get('target') for n in m['npcs']))
            seen = {n['npc']: n.get('target') == peer.id for n in state['npcs']}
            assert seen == expected, f'visión distinta: {seen} (esperado {expected})'
            print('PASS: 15 en horizontal y 13 en vertical; los catálogos viejos siguen usando visionRange.')
        finally:
            if peer is not None: peer.close()
            if process.poll() is None:
                process.terminate(); process.wait(5)
            log.close()  # Windows cannot delete the temp folder while the log is open


if __name__ == '__main__':
    run()
