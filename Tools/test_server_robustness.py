"""Un guardado ilegible no debe tirar la sala ni quedar guardado. Puerto, clave y guardados temporales."""
import json
import pathlib
import secrets
import socket
import subprocess
import tempfile
import time
import uuid

from test_coop_server import DLL, PROTOCOL, Peer, fixture, snapshot


def hello(port, key, save):
    with socket.create_connection(('127.0.0.1', port), timeout=5) as sock:
        sock.sendall((json.dumps(dict(type='hello', version=PROTOCOL, key=key, characterId=uuid.uuid4().hex,
                                      token=secrets.token_hex(32), snapshot=json.dumps(save),
                                      player=dict(save['world']))) + '\n').encode())
        reply = json.loads(sock.makefile('r', encoding='utf-8').readline() or '{}')
    assert reply.get('type') == 'error', reply


def run():
    with tempfile.TemporaryDirectory(prefix='ao-robust-test-') as temp:
        root = pathlib.Path(temp); key = secrets.token_hex(18)
        (root/'key').write_text(key); (root/'catalog.json').write_text(json.dumps(fixture()))
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        log = (root/'server.log').open('w')
        process = subprocess.Popen(['dotnet', str(DLL), '--port', str(port), '--key-file', str(root/'key'),
                                    '--catalog', str(root/'catalog.json'), '--data', str(root/'saves')],
                                   stdout=log, stderr=log)
        peers = []
        try:
            for _ in range(60):
                try:
                    socket.create_connection(('127.0.0.1', port), timeout=.1).close(); break
                except OSError: time.sleep(.1)
            for bad in ('x', 1e20):
                save = snapshot('Malo'); save['rpg']['raceId'] = bad
                hello(port, key, save)
                time.sleep(.5)
                assert process.poll() is None, 'la sala se cayó con raceId=%r' % bad
            a = Peer(port, key, 'Alpha'); peers.append(a)
            a.save['rpg']['raceId'] = 'x'
            assert not a.action('sync')['ok']
            a.close(); peers.remove(a); time.sleep(.3)
            again = Peer(port, key, 'Alpha', identity=a.identity); peers.append(again)
            assert again.save['rpg']['raceId'] == 1, 'se guardó el snapshot ilegible'
            assert process.poll() is None
            # Revisión #2: a lo sumo 4 conexiones sin saludar por dirección; la 5.ª se cierra al instante,
            # y un saludo trabado se corta a los ~5 s (antes: 60 s por byte).
            idle = [socket.create_connection(('127.0.0.1', port)) for _ in range(5)]
            try:
                idle[4].settimeout(3)
                try: assert idle[4].recv(1) == b''
                except ConnectionResetError: pass
                start = time.monotonic(); idle[0].settimeout(15)
                try:
                    while idle[0].recv(4096): pass
                except ConnectionResetError: pass
                assert 3 < time.monotonic() - start < 13, time.monotonic() - start
            finally:
                for s in idle: s.close()
            time.sleep(.3); late = Peer(port, key, 'Tarde'); peers.append(late)
            # Revisión #11: nombres nuevos como ValidarNombre (3 a 18, A–Z y espacios); nadie se hace pasar por Alpha.
            for fake in ('Аlpha', 'Álpha', 'ALPHA', 'Alpha2', 'Al  pha', 'Nombrelarguisimoxxx', 'Ñandu'):
                try: Peer(port, key, fake)
                except RuntimeError: pass
                else: raise AssertionError('nombre aceptado: %r' % fake)
            peers.append(Peer(port, key, 'Al pha'))
            print('PASS: un hello o guardado ilegible se rechaza, la sala sigue y el personaje no queda roto; '
                  'saludo con plazo y tope de conexiones sin saludar por dirección.')
        finally:
            for p in peers: p.close()
            if process.poll() is None:
                process.terminate(); process.wait(5)
            else:
                print((root/'server.log').read_text()[-1000:])
            log.close()  # Windows cannot delete the temp folder while the log is open


if __name__ == '__main__':
    run()
