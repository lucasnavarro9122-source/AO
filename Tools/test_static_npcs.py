"""Regression: stationary NPCs remain anchored, including old displaced saves."""
import copy
import gzip
import json
import pathlib
import secrets
import socket
import subprocess
import tempfile
import time

from test_coop_server import DLL, ROOT, Peer, fixture


def run():
    catalog = fixture()
    template = catalog['maps'][0]['npcs'][0]
    catalog['maps'][0]['npcs'] = [
        dict(template, npcIndex=1331, x=55, y=55, movement=0, attackable=False),
        dict(template, npcIndex=301, x=55, y=48, movement=1, hostile=True),
        dict(template, npcIndex=303, x=48, y=55, movement=3, hostile=True),
        dict(template, npcIndex=320, x=65, y=65, movement=20,
             walkRoute=[dict(offsetX=2, offsetY=0, waitMs=0),
                        dict(offsetX=0, offsetY=0, waitMs=0)])]
    expected = {1: (55, 55, 3), 2: (55, 48, 3), 3: (48, 55, 3)}
    with tempfile.TemporaryDirectory(prefix='ao-static-test-') as temp:
        root = pathlib.Path(temp)
        key = secrets.token_hex(18)
        (root/'key').write_text(key, encoding='utf-8')
        (root/'catalog.json').write_text(json.dumps(catalog), encoding='utf-8')
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        args = ['dotnet', str(DLL), '--port', str(port), '--key-file', str(root/'key'),
                '--catalog', str(root/'catalog.json'), '--data', str(root/'saves')]
        process = peer = None
        with (root/'server.log').open('w', encoding='utf-8') as log:
            def launch():
                child = subprocess.Popen(args, stdout=log, stderr=log)
                try:
                    for _ in range(60):
                        try:
                            with socket.create_connection(('127.0.0.1', port), timeout=.1):
                                return child
                        except OSError:
                            time.sleep(.1)
                    raise AssertionError('server did not start')
                except BaseException:
                    child.terminate()
                    child.wait(5)
                    raise

            try:
                process = launch()
                peer = Peer(port, key, 'StaticProbe')
                identity = peer.identity
                peer.action('sync')
                # Check several real AI ticks, including pursuit. Route NPCs must still move.
                route_positions = set()
                deadline = time.monotonic() + 4
                while time.monotonic() < deadline:
                    state = peer.state()
                    for npc in state['npcs']:
                        position = (npc['x'], npc['y'], npc['heading'])
                        if npc['id'] in expected:
                            assert position == expected[npc['id']], ('stationary NPC moved', npc)
                        else:
                            route_positions.add(position[:2])
                assert len(route_positions) > 1, 'scripted NPC movement stopped'
                peer.close()
                peer = None
                time.sleep(.3)
                process.terminate()
                process.wait(5)
                # Reproduce a save written by the buggy server, preserving everything else.
                path = root/'saves/world.json'
                saved = json.loads(path.read_text(encoding='utf-8'))
                characters = copy.deepcopy(saved['characters'])
                for npc in saved['maps']['1']['npcs'][:3]:
                    npc['state'].update(x=80, y=80, heading=1)
                path.write_text(json.dumps(saved), encoding='utf-8')
                process = launch()
                peer = Peer(port, key, 'StaticProbe', identity)
                state = peer.state()
                for npc in state['npcs'][:3]:
                    assert (npc['x'], npc['y'], npc['heading']) == expected[npc['id']]
                peer.close()
                peer = None
                time.sleep(.3)
                repaired = json.loads(path.read_text(encoding='utf-8'))
                assert repaired['characters'] == characters, 'character data changed during repair'
                for npc in repaired['maps']['1']['npcs'][:3]:
                    value = npc['state']
                    assert (value['x'], value['y'], value['heading']) == expected[value['id']]
            finally:
                if peer is not None:
                    peer.close()
                if process is not None and process.poll() is None:
                    process.terminate()
                    process.wait(5)
    real = json.loads(gzip.decompress((ROOT/'OnlineServer/Data/catalog.json.gz').read_bytes()))
    boards = [n for m in real['maps'] for n in m['npcs'] if n['npcIndex'] == 1331]
    assert len(boards) == 7 and all(n['movement'] == 0 for n in boards)
    print('PASS: modes 0/1/3 stay fixed; scripted movement works; old positions repaired; character data unchanged; all 7 quest boards covered.')


if __name__ == '__main__':
    run()
