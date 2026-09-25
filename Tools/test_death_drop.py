"""Pérdida al morir como el original (acción "death" del servidor). Puerto, clave y guardados temporales."""
import json
import pathlib
import secrets
import socket
import subprocess
import tempfile
import time

from test_coop_server import DLL, Peer, fixture

NORMAL, NO_DROP, NEWBIE, KEY, CANT_THROW, UNTRANSFERABLE, DESTROY = 500, 501, 502, 503, 504, 505, 506


def catalog():
    c = fixture()
    base = dict(value=30, objType=1)
    c['items'] = [dict(base, index=12, name='Oro'), dict(base, index=NORMAL, name='Espada'),
                  dict(base, index=NO_DROP, name='Anillo', noDrop=True), dict(base, index=NEWBIE, name='Daga newbie', newbie=True),
                  dict(base, index=KEY, name='Llave', objType=9), dict(base, index=CANT_THROW, name='Runa', cantThrow=True),
                  dict(base, index=UNTRANSFERABLE, name='Medalla', untransferable=True), dict(base, index=DESTROY, name='Pase', destroyOnSell=True)]
    c['maps'][0]['triggers'] = [dict(x=70, y=70, trigger=6)]  # arena: no se cae nada
    return c


def equip(peer, level, gold, inventory, x, y):
    s = peer.save
    s['rpg']['level'] = level; s['combat']['gold'] = gold; s['world'].update(x=x, y=y)
    ids, amounts = s['inventory']['itemIndices'], s['inventory']['amounts']
    for i, (item, amount) in enumerate(inventory):
        ids[i], amounts[i] = item, amount
    assert peer.action('sync')['ok']; time.sleep(.05)


def die(peer):
    peer.save['combat'].update(dead=True, hp=0)
    time.sleep(.05)
    return peer.action('death')


def death_event(result):
    return next((e for e in result['events'] if e['type'] == 'death'), None)


def run():
    with tempfile.TemporaryDirectory(prefix='ao-death-test-') as temp:
        root = pathlib.Path(temp); key = secrets.token_hex(18)
        (root/'key').write_text(key); (root/'catalog.json').write_text(json.dumps(catalog()))
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        log = (root/'server.log').open('w')
        process = subprocess.Popen(['dotnet', str(DLL), '--port', str(port), '--key-file', str(root/'key'),
                                    '--catalog', str(root/'catalog.json'), '--data', str(root/'saves')], stdout=log, stderr=log)
        peers = []
        try:
            for _ in range(60):
                try:
                    socket.create_connection(('127.0.0.1', port), timeout=.1).close(); break
                except OSError: time.sleep(.1)
            everything = [(NORMAL, 3), (NO_DROP, 1), (NEWBIE, 2), (KEY, 1), (CANT_THROW, 1), (UNTRANSFERABLE, 1), (DESTROY, 1)]

            # Nivel 5 con 10.000 de oro: billetera 5.000; el objeto newbie queda protegido (nivel <= 12).
            a = Peer(port, key, 'Alpha'); peers.append(a)
            equip(a, 5, 10000, everything, 50, 51)
            time.sleep(.05); assert not a.action('death')['ok'], 'un vivo no puede pedir la pérdida'
            result = die(a); assert result['ok'], result
            e = death_event(result); assert e, result
            assert [(i['item'], i['amount']) for i in e['items']] == [(NORMAL, 3)] and e['gold'] == -5000, e
            time.sleep(.05); again = a.action('death'); assert not again['ok'] and 'confirmar' in again['text'], again
            # Tilelibre: radio 1 desde arriba a la izquierda; (49,50) comerciante y (50,50) NPC ocupados.
            state = a.until(lambda m: m['type'] == 'state' and len(m['loot']) >= 2)
            floor = {(l['x'], l['y']): (l['item'], l['amount']) for l in state['loot']}
            assert floor == {(51, 50): (12, 5000), (49, 51): (NORMAL, 3)}, floor
            # Aplicar el evento y confirmarlo: pedirla de nuevo ya no tira nada (no hay duplicados).
            a.save['combat']['gold'] += e['gold']
            a.save['inventory']['itemIndices'][0] = 0; a.save['inventory']['amounts'][0] = 0
            a.ack = e['seq']; time.sleep(.05)
            assert death_event(a.action('death')) is None

            # Nivel 13: el objeto newbie ya se cae; oro justo en la billetera: no cae oro.
            b = Peer(port, key, 'Beta'); peers.append(b)
            equip(b, 13, 13000, everything, 30, 30)
            e = death_event(die(b))
            assert sorted((i['item'], i['amount']) for i in e['items']) == [(NORMAL, 3), (NEWBIE, 2)] and e['gold'] == 0, e

            # Arena (trigger 6): no se cae nada.
            c = Peer(port, key, 'Gamma'); peers.append(c)
            equip(c, 20, 50000, everything, 70, 70)
            assert death_event(die(c)) is None

            # Otro jugador levanta lo que se cayó.
            d = Peer(port, key, 'Delta'); peers.append(d)
            equip(d, 1, 0, [], 48, 52)
            loot = next(l for l in d.until(lambda m: m['type'] == 'state' and m['loot'])['loot'] if (l['x'], l['y']) == (49, 51))
            time.sleep(.05)
            got = d.action('pickup', target=loot['id']); assert got['ok'], got
            assert any(ev['type'] == 'item' and ev['item'] == NORMAL and ev['amount'] == 3 for ev in got['events'])
            assert process.poll() is None
            print('PASS: oro sobre 1000 x nivel y pilas que se pueden tirar; NoSeCae/Intirable/Destruye/Instransferible/llave '
                  'no caen; newbie protegido hasta 12; orden de Tilelibre; arena; sin duplicados; otro jugador lo levanta.')
        finally:
            for p in peers: p.close()
            if process.poll() is None:
                process.terminate(); process.wait(5)
            else:
                print((root/'server.log').read_text()[-1000:])


if __name__ == '__main__':
    run()
