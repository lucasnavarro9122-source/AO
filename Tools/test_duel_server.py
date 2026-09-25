"""Retos de la demo por protocolo (bots): custodia, rondas, pago con impuesto y resto, espectador,
abandono por desconexión, tiempo agotado y reinicio con reto abierto. Puerto, claves y guardados temporales."""
import json
import pathlib
import secrets
import socket
import subprocess
import sys
import tempfile
import time

from test_coop_server import DLL, Peer, fixture, snapshot


class DuelPeer(Peer):
    """Keeps pushes that arrive while waiting for something else (duelInvite, fx...)."""
    def __init__(self, *args, **kwargs):
        self.log = []; self.journal = []
        super().__init__(*args, **kwargs)

    def events(self, events):
        self.journal += [e for e in events or [] if e['seq'] > self.ack]
        super().events(events)

    def duel_end(self):
        return [e for e in self.journal + self.act('sync')['events'] if e['type'] == 'duelEnd'][-1]['duel']

    def until(self, predicate, timeout=8):
        for i, m in enumerate(self.log):
            if predicate(m): return self.log.pop(i)
        end = time.monotonic() + timeout
        while time.monotonic() < end:
            m = super().until(lambda m: True)
            if predicate(m): return m
            if m['type'] != 'state': self.log.append(m)
        raise AssertionError('timeout; last pushes: ' + str([m['type'] for m in self.log[-8:]]))

    def act(self, typ, **values):
        time.sleep(.04)
        return self.action(typ, **values)

    def push(self, typ, **match):
        return self.until(lambda m: m['type'] == typ and all((m.get('duel') or {}).get(k) == v for k, v in match.items()))

    def last_warp(self):
        warps = [e for e in self.journal + self.act('sync')['events'] if e['type'] == 'warp']
        return warps[-1]

    def seen_at(self, x, y):
        for _ in range(3):  # the first state may have been built before the position arrived
            state = self.until(lambda m: m['type'] == 'state')
            if any(p['id'] == self.id and p['x'] == x and p['y'] == y for p in state['players']): return True
        return False


def ledger(peer):
    result = peer.act('testLedger'); assert result['ok'], result
    return json.loads(result['text'])


def next_to(attacker, target):
    """Teleports the attacker (walkable cells only) next to the target's spawn."""
    tx, ty = target
    for x, y in ((tx, ty - 1), (tx, ty + 1), (tx - 1, ty), (tx + 1, ty)):
        attacker.send(dict(type='position', player=dict(attacker.player(), map=2, x=x, y=y)))
        if attacker.seen_at(x, y): return
    raise AssertionError('no free cell next to the target')


def drain(*peers):
    """Real clients read and ack all the time; an unread socket is dropped after the 1.5 s send timeout."""
    for peer in peers: peer.events(peer.act('sync')['events'])


def win_round(a, b, round_no, *watchers):
    a.push('duelRoundStart', round=round_no); b.push('duelRoundStart', round=round_no)
    spawn = b.last_warp()
    assert 'conteo' in (a.act('attack', id=b.id).get('text') or '')
    a.push('duelRingState', phase='fight')
    next_to(a, (spawn['x'], spawn['y']))
    for _ in range(20):
        result = a.act('attack', id=b.id); text = result.get('text') or ''
        # A kill starts the next countdown (or ends the duel) right away.
        if 'conteo' in text or 'reto' in text or any(m['type'] == 'duelRoundEnd' for m in a.log): break
        assert result['ok'] or 'atacar' in text, result
        drain(b, *watchers); time.sleep(.6)
    end = a.push('duelRoundEnd', round=round_no)
    assert end['duel']['winner'] == 0 and end['duel']['result'] == 'victoria', end


def run():
    with tempfile.TemporaryDirectory(prefix='ao-duel-test-') as temp:
        root = pathlib.Path(temp); key = secrets.token_hex(18)
        catalog = fixture()
        catalog['items'].append(dict(index=38, name='Poción de Vida', value=18, objType=11, potionType=3, minModifier=27, maxModifier=27))
        catalog['retos'] = dict(minBet=10, maxTeam=5, taxPercent=10, maxSeconds=400, countdownSeconds=40, inviteSeconds=60,
                                graceSeconds=30, maps=[1, 2], arenas=[dict(sala=1, map=2, x=10, y=10, theme=0)])
        (root/'key').write_text(key); (root/'catalog.json').write_text(json.dumps(catalog))
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        args = ['dotnet', str(DLL), '--port', str(port), '--key-file', str(root/'key'), '--catalog', str(root/'catalog.json'),
                '--data', str(root/'saves'), '--test', '--demo', '--test-time-scale', '0.05']
        log = (root/'server.log').open('w')

        def launch():
            process = subprocess.Popen(args, stdout=log, stderr=log)
            for _ in range(60):
                try: probe = socket.create_connection(('127.0.0.1', port), timeout=.1); probe.close(); return process
                except OSError: time.sleep(.1)
            raise AssertionError('server did not start')

        process = launch(); peers = []
        try:
            potions = snapshot('Alfa'); potions['inventory']['itemIndices'][0] = 38; potions['inventory']['amounts'][0] = 5
            a = DuelPeer(port, key, 'Alfa', save=potions); b = DuelPeer(port, key, 'Beto'); c = DuelPeer(port, key, 'Ceci')
            peers = [a, b, c]; ids = (a.identity, b.identity)
            # Validations (ModRetos.CrearReto) move no gold.
            assert 'no está conectado' in (a.act('duelChallenge', text='Nadie', gold=50, item=-1).get('text') or '')
            assert 'rival' in (a.act('duelChallenge', text='Beto;Ceci', gold=50, item=-1).get('text') or '')
            assert 'mínima' in (a.act('duelChallenge', text='Beto', gold=5, item=-1).get('text') or '')
            assert 'suficiente oro' in (a.act('duelChallenge', text='Beto', gold=500, item=-1).get('text') or '')
            assert 'pociones' in (a.act('duelChallenge', text='Beto', gold=50, item=2).get('text') or '')
            assert a.wallet == 100
            # Custody on challenge; a rejection returns it in full.
            assert a.act('duelChallenge', text='Beto', gold=50, item=-1)['ok'] and a.wallet == 50
            invite = b.push('duelInvite', **{'from': 'Alfa'})['duel']
            assert invite['teamA'] == ['Alfa'] and invite['teamB'] == ['Beto'] and invite['bet'] == 50
            assert b.act('duelReject', name='Alfa')['ok']
            assert a.push('duelInviteClosed', reason='rechazado')
            assert a.act('sync') and a.wallet == 100
            # Full 1 vs 1 with a bet: both pay on accept, best of 3, 10 % tax, remainder line, spectator.
            c.save['world'].update(map=2, x=5, y=5)  # its checkpoints keep it in the arena map
            c.send(dict(type='position', player=dict(c.player(), map=2, x=5, y=5)))
            assert c.push('duelRingState', phase='libre')
            assert a.act('duelChallenge', text='Beto', gold=50, item=-1)['ok']
            b.push('duelInvite'); assert b.act('duelAccept', name='Alfa')['ok'] and b.wallet == 50
            start = a.push('duelStart')['duel']; b.push('duelStart')
            assert start['seed'] != 0 and start['genVersion'] >= 1 and start['width'] == 23 and start['height'] == 19
            assert c.push('duelRingState', phase='countdown')['duel']['seed'] == start['seed']
            assert 'reto' in (a.act('pickup', target=1).get('text') or '')
            win_round(a, b, 1, c); win_round(a, b, 2, c)
            end = a.duel_end()
            assert end['result'] == 'victoria' and end['prize'] == 90 and end['tax'] == 10, end
            lost = b.duel_end()
            assert lost['result'] == 'derrota' and lost['prize'] == 0
            assert a.wallet == 140 and b.wallet == 50
            book = ledger(a); assert book['sum'] == 0 and book['balances']['tax'] == 10, book
            assert book['balances']['escrow:' + start['id']] == 0
            assert c.until(lambda m: m['type'] == 'fx' and m['text'] == 'death')
            assert c.until(lambda m: m['type'] == 'duelAnnounce' and 'venció' in m['text'])
            # Disconnection: after the 30 s grace (scaled) the one who left is disqualified.
            result = a.act('duelChallenge', text='Beto', gold=0, item=-1); assert result['ok'], result['text']
            b.push('duelInvite'); assert b.act('duelAccept', name='Alfa')['ok']
            a.push('duelRoundStart', round=1)
            b.close(); peers.remove(b)
            end = a.until(lambda m: m['type'] == 'duelNotice' and 'descalificado' in m['text'])
            b = DuelPeer(port, key, 'Beto', ids[1]); peers.append(b)
            assert b.duel_end()['result'] == 'abandono'
            # Time up (400 s scaled to 20 s) with 0-0: tie ("tiempo").
            result = a.act('duelChallenge', text='Beto', gold=0, item=-1); assert result['ok'], result['text']
            b.push('duelInvite'); assert b.act('duelAccept', name='Alfa')['ok']
            # Like a real client, Alfa keeps reading (and acking) while we wait on Beto: an unread socket
            # fills up and the server drops it after its 1.5 s send timeout.
            deadline = time.monotonic() + 40
            while True:
                try: b.until(lambda m: m['type'] == 'duelNotice' and 'tiempo' in m['text'], timeout=1); break
                except AssertionError:
                    assert time.monotonic() < deadline, 'time up never came'
                    a.events(a.act('sync')['events'])
            assert b.duel_end()['result'] == 'tiempo'
            # Restart with an open custody: everything is returned, nothing is lost or duplicated.
            result = a.act('duelChallenge', text='Beto', gold=20, item=-1); assert result['ok'], result['text']
            b.push('duelInvite'); assert b.act('duelAccept', name='Alfa')['ok']
            a.push('duelStart'); assert a.wallet == 120 and b.wallet == 30
            for peer in peers: peer.close()
            peers = []; time.sleep(.25); process.terminate(); process.wait(5)
            process = launch()
            a = DuelPeer(port, key, 'Alfa', ids[0]); b = DuelPeer(port, key, 'Beto', ids[1]); peers = [a, b]
            assert a.wallet == 140 and b.wallet == 50
            book = ledger(a); assert book['sum'] == 0 and book['balances']['tax'] == 10, book
            print('PASS: validations, custody on accept, rejection refund, best of 3, 10 % tax + remainder line, '
                  'spectator ring state/fx/announce, no looting in a duel, disconnection grace, time up, restart refund.')
        finally:
            for peer in peers:
                try: peer.close()
                except OSError: pass
            process.terminate(); process.wait(5); log.close()
            if sys.exc_info()[0] or process.returncode not in (0, 1): print((root/'server.log').read_text()[-2000:])


if __name__ == '__main__': run()
