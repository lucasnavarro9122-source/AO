"""Pérdida al morir (decisión 18, AODeathDropRules) en línea: solo con --demo, en mapas con dropOnDeath.
El servidor la aplica al recibir el guardado del jugador muerto, una vez por muerte. Puerto, clave y guardados temporales."""
import json
import pathlib
import secrets
import socket
import subprocess
import tempfile
import time

from test_coop_server import DLL, Peer, fixture, snapshot

NORMAL, NO_DROP, NEWBIE, KEY, CANT_THROW, UNTRANSFERABLE, DESTROY = 500, 501, 502, 503, 504, 505, 506
EVERYTHING = [(NORMAL, 3), (NO_DROP, 1), (NEWBIE, 2), (KEY, 1), (CANT_THROW, 1), (UNTRANSFERABLE, 1), (DESTROY, 1)]


def catalog():
    c = fixture()
    base = dict(value=30, objType=1)
    c['items'] = [dict(base, index=12, name='Oro'), dict(base, index=NORMAL, name='Espada'),
                  dict(base, index=NO_DROP, name='Anillo', noSeCae=True), dict(base, index=NEWBIE, name='Daga newbie', newbie=True),
                  dict(base, index=KEY, name='Llave', objType=9), dict(base, index=CANT_THROW, name='Runa', intirable=True),
                  dict(base, index=UNTRANSFERABLE, name='Medalla', untransferable=True), dict(base, index=DESTROY, name='Pase', destroyOnSell=True)]
    c['maps'][0]['dropOnDeath'] = True
    c['maps'][0]['triggers'] = [dict(x=70, y=70, trigger=6)]  # zona de pelea: no se cae nada
    return c


def character(port, key, name, level, gold, inventory, x, y):
    """The wallet opens from the first save, so gold and items are set before connecting."""
    save = snapshot(name)
    save['rpg']['level'] = level; save['combat']['gold'] = gold; save['world'].update(x=x, y=y)
    for i, (item, amount) in enumerate(inventory):
        save['inventory']['itemIndices'][i], save['inventory']['amounts'][i] = item, amount
    return Peer(port, key, name, save=save)


def die(peer, dead=True):
    peer.save['combat'].update(dead=dead, hp=0 if dead else 100)
    time.sleep(.05)
    result = peer.action('sync'); assert result['ok'], result
    fresh = [e for e in result['events'] if e['seq'] > peer.ack]
    peer.events(result['events'])
    return next((e for e in fresh if e['type'] == 'deathDrop'), None), [e for e in fresh if e['type'] == 'remove']


def launch(root, key, port, demo):
    log = (root/('server-%s.log' % demo)).open('w')
    args = ['dotnet', str(DLL), '--port', str(port), '--key-file', str(root/'key'), '--catalog', str(root/'catalog.json'),
            '--data', str(root/('saves-%s' % demo)), '--test'] + (['--demo'] if demo else [])
    process = subprocess.Popen(args, stdout=log, stderr=log)
    for _ in range(60):
        try: socket.create_connection(('127.0.0.1', port), timeout=.1).close(); break
        except OSError: time.sleep(.1)
    return process, log


def run():
    with tempfile.TemporaryDirectory(prefix='ao-death-test-') as temp:
        root = pathlib.Path(temp); key = secrets.token_hex(18)
        (root/'key').write_text(key); (root/'catalog.json').write_text(json.dumps(catalog()))
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        peers, running = [], []
        try:
            process, log = launch(root, key, port, True); running.append((process, log))
            # Level 5 with 10,000 gold: 5,000 stays protected; the newbie item is kept (level <= 12).
            a = character(port, key, 'Alpha', 5, 10000, EVERYTHING, 50, 51); peers.append(a)
            drop, removed = die(a)
            assert drop and drop['gold'] == 5000 and [(i['item'], i['amount']) for i in drop['items']] == [(NORMAL, 3)], drop
            assert [(e['item'], e['amount']) for e in removed] == [(NORMAL, 3)] and a.wallet == 5000, (removed, a.wallet)
            # Tilelibre: ring 1 from the top-left; (49,50) merchant and (50,50) NPC are taken, one object per tile.
            state = a.until(lambda m: m['type'] == 'state' and len(m['loot']) >= 2)
            floor = {(l['x'], l['y']): (l['item'], l['amount']) for l in state['loot']}
            assert floor == {(51, 50): (12, 5000), (49, 51): (NORMAL, 3)}, floor
            # Still the same death: nothing falls twice.
            assert die(a) == (None, []) and a.wallet == 5000
            # Level 13: the newbie item falls now; gold exactly at the protected amount: no gold falls.
            b = character(port, key, 'Beta', 13, 13000, EVERYTHING, 30, 30); peers.append(b)
            drop, _ = die(b)
            assert drop['gold'] == 0 and sorted((i['item'], i['amount']) for i in drop['items']) == [(NORMAL, 3), (NEWBIE, 2)], drop
            # Fight zone (trigger 6): nothing falls.
            c = character(port, key, 'Gamma', 20, 50000, EVERYTHING, 70, 70); peers.append(c)
            assert die(c) == (None, []) and c.wallet == 50000
            # Another player picks up what fell.
            d = character(port, key, 'Delta', 1, 0, [], 48, 52); peers.append(d)
            loot = next(l for l in d.until(lambda m: m['type'] == 'state' and m['loot'])['loot'] if (l['x'], l['y']) == (49, 51))
            time.sleep(.05); got = d.action('pickup', target=loot['id']); assert got['ok'], got
            for peer in peers: peer.close()
            peers = []
            # The normal room (no --demo) never takes anything from a dead player.
            with socket.socket() as reservation:
                reservation.bind(('127.0.0.1', 0)); normal_port = reservation.getsockname()[1]
            process, log = launch(root, key, normal_port, False); running.append((process, log))
            e = character(normal_port, key, 'Epsilon', 30, 90000, EVERYTHING, 50, 51); peers.append(e)
            assert die(e) == (None, []) and e.wallet == 90000
            print('PASS: pérdida al morir en la demo (oro protegido por nivel, objetos protegidos, newbie hasta nivel 12, '
                  'zona de pelea, una casilla libre por objeto, sin duplicados, otro jugador levanta); la sala normal no tira nada.')
        finally:
            for peer in peers:
                try: peer.close()
                except OSError: pass
            for process, log in running:
                if process.poll() is None: process.terminate(); process.wait(5)
                log.close()


if __name__ == '__main__':
    run()
