"""R-2/R-3 of docs/claude/demo/pruebas.md: the Unity client in a real duel against protocol bots.

Isolated `--test --demo` server (temporary port, key and data; real catalog: hub 1000, arenas 1001).
R-2: Unity challenges the bot Pepe (bet 1.000); Unity wins rounds 1 and 3, the bot wins round 2.
R-3: Unity watches Pepe vs Juan from outside ring 1.
Unity side: Assets/AOMigrator/Editor/AODuelQA284.cs -> MigrationReports/duel_v284.json.
Coordination: Temp/duel_qa_bridge.json (bots -> Unity) and Temp/duel_qa_progress.txt (Unity -> bots).
Local saves must not change and no new save file may appear."""
import hashlib
import json
import os
import pathlib
import secrets
import socket
import subprocess
import sys
import time

from test_coop_server import ROOT, DLL, snapshot
from test_duel_server import DuelPeer, drain

UNITY = 'UnityPrueba'
PLAZA = (50, 60)
TEMP = ROOT / 'Temp'
FLAG = TEMP / 'run_duel_qa.json'
BRIDGE = TEMP / 'duel_qa_bridge.json'
PROGRESS = TEMP / 'duel_qa_progress.txt'
REPORT = ROOT / 'MigrationReports/duel_v284.json'


class Bot(DuelPeer):
    """Protocol bot that can switch between dodging everything and being easy to hit."""
    evasion = 10000

    def player(self):
        return dict(super().player(), evasion=self.evasion)


def bot_save(name, x):
    save = snapshot(name)
    save['world'].update(map=1001, x=x, y=PLAZA[1])
    save['combat'].update(gold=5000, hp=10)
    save['rpg'].update(maxHp=10)
    return save


def saves_snapshot():
    source = pathlib.Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/DefaultCompany/My project (1)'
    return source, {str(p.relative_to(source)): hashlib.sha256(p.read_bytes()).hexdigest() for p in source.rglob('*.json')}


def ensure_server_dll():
    sources = [p for p in (ROOT / 'OnlineServer').rglob('*.cs') if 'bin' not in p.parts and 'obj' not in p.parts]
    sources += list((ROOT / 'Assets/AOMigrator/Runtime/Shared').glob('*.cs'))
    sources += [ROOT / 'Assets/AOMigrator/Runtime/AOCoopProtocolV250.cs', ROOT / 'OnlineServer/AOOnlineServer.csproj']
    newest = max(p.stat().st_mtime for p in sources)
    if DLL.exists() and DLL.stat().st_mtime >= newest:
        return
    print('Server DLL older than its sources: building OnlineServer.', flush=True)
    subprocess.run(['dotnet', 'build', str(ROOT / 'OnlineServer/AOOnlineServer.csproj'), '-v:q', '-clp:ErrorsOnly', '-nologo'], check=True)


def wait_unity_import():
    sources = list((ROOT / 'Assets/AOMigrator').rglob('*.cs'))
    newest = (max(p.stat().st_mtime for p in sources if 'Editor' not in p.parts),
              max(p.stat().st_mtime for p in sources if 'Editor' in p.parts))
    assemblies = [ROOT / 'Library/ScriptAssemblies' / n for n in ('Assembly-CSharp.dll', 'Assembly-CSharp-Editor.dll')]
    (TEMP / 'refresh_online_client').write_text('', encoding='utf-8')
    deadline = time.monotonic() + 300
    print('Waiting for Unity import; leave the editor open outside Play.', flush=True)
    while not all(a.exists() and a.stat().st_mtime >= n for a, n in zip(assemblies, newest)):
        if time.monotonic() > deadline:
            raise TimeoutError('Unity has not imported the latest scripts')
        time.sleep(1)
    time.sleep(4)


def idle(peers, seconds, until=None):
    """Keeps every bot reading and acking (an unread socket is dropped) until the condition holds."""
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        drain(*peers)
        if until and until():
            return True
        time.sleep(.3)
    if until:
        raise TimeoutError('condition not met in %d s' % seconds)
    return True


def progress():
    try:
        return PROGRESS.read_text(encoding='utf-8').strip()
    except OSError:
        return ''


def position_of(peer, player_id):
    for _ in range(10):
        state = peer.until(lambda m: m['type'] == 'state')
        for p in state['players']:
            if p['id'] == player_id:
                return p['x'], p['y']
    raise AssertionError('player %d not seen in state' % player_id)


def next_to(peer, target):
    tx, ty = target
    for x, y in ((tx + 1, ty), (tx - 1, ty), (tx, ty - 1), (tx, ty + 1)):
        peer.send(dict(type='position', player=dict(peer.player(), map=1001, x=x, y=y)))
        if peer.seen_at(x, y):
            return x, y
    raise AssertionError('no free cell next to %s' % (target,))


def round_end(peer, round_no, others, seconds=120):
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        for i, m in enumerate(peer.log):
            if m['type'] == 'duelRoundEnd' and m['duel'].get('round') == round_no:
                return peer.log.pop(i)['duel']
        drain(peer, *others)
        time.sleep(.3)
    raise AssertionError('round %d never ended' % round_no)


def fight_round(bot, target_id, round_no, others, attack, give_up=45):
    """Moves next to the target once the fight starts; attacks it or stands still to be hit.
    If the target cannot finish a passive round in `give_up` s, the bot ends it so the rest of the test runs."""
    bot.push('duelRoundStart', round=round_no)
    bot.until(lambda m: m['type'] == 'duelRingState' and m['duel'].get('phase') == 'fight', timeout=30)
    bot.evasion = 10000 if attack else 0
    next_to(bot, position_of(bot, target_id))
    if not attack:
        try:
            return round_end(bot, round_no, others, give_up)
        except AssertionError:
            print('Round %d: Unity did not finish it in %d s; the bot ends it.' % (round_no, give_up), flush=True)
            bot.evasion = 10000
    for _ in range(60):
        text = bot.act('attack', id=target_id).get('text') or ''
        if any(m['type'] == 'duelRoundEnd' for m in bot.log) or 'conteo' in text or 'reto' in text:
            break
        drain(*others); time.sleep(.6)
    return round_end(bot, round_no, others)


def run():
    source, hashes = saves_snapshot()
    ensure_server_dll()
    wait_unity_import()
    for path in (BRIDGE, PROGRESS, TEMP / 'stop_duel_qa'):
        path.unlink(missing_ok=True)
    if REPORT.exists():
        REPORT.replace(TEMP / f'duel_v284-previous-{time.time_ns()}.json')
    work = TEMP / f'duel-unity-qa-{time.time_ns()}'
    work.mkdir(parents=True)
    key = secrets.token_hex(18)
    (work / 'key.txt').write_text(key)
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0)); port = probe.getsockname()[1]
    log = (work / 'server.log').open('w')
    server = subprocess.Popen(['dotnet', str(DLL), '--port', str(port), '--key-file', str(work / 'key.txt'),
                               '--catalog', str(ROOT / 'OnlineServer/Data/catalog.json.gz'), '--data', str(work / 'saves'),
                               '--test', '--demo', '--test-time-scale', '0.3'],
                              stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    peers = []
    try:
        for _ in range(100):
            try: socket.create_connection(('127.0.0.1', port), timeout=.1).close(); break
            except OSError: time.sleep(.1)
        pepe = Bot(port, key, 'Pepe', save=bot_save('Pepe', PLAZA[0] + 2)); peers.append(pepe)
        FLAG.write_text(json.dumps(dict(port=port, key=key)), encoding='utf-8')
        print('Isolated demo server ready; waiting for Unity.', flush=True)

        unity = {}
        def unity_seen():
            state = pepe.until(lambda m: m['type'] == 'state')
            for p in state['players']:
                if p.get('name') == UNITY: unity['id'] = p['id']
            return 'id' in unity
        idle(peers, 240, unity_seen)
        print('Unity connected as player %d.' % unity['id'], flush=True)

        # ---- R-2
        # The server drops a socket that sends nothing for 60 s: keep syncing while Unity prepares the challenge.
        idle(peers, 180, lambda: any(m['type'] == 'duelInvite' for m in pepe.log))
        invite = pepe.push('duelInvite')['duel']
        assert invite['from'] == UNITY and invite['bet'] == 1000, invite
        assert pepe.act('duelAccept', name=UNITY)['ok']
        start = pepe.push('duelStart')['duel']
        BRIDGE.write_text(json.dumps(dict(seed=start['seed'], sala=start['sala'], fighters=[])), encoding='utf-8')
        results = [fight_round(pepe, unity['id'], r, [], attack=(r == 2)) for r in (1, 2, 3)]
        winners = [r['winner'] for r in results]
        bot_failures = []
        if winners != [0, 1, 0]:
            bot_failures.append('R-2: round winners (0 = Unity) %s, expected [0, 1, 0]' % winners)
        print('R-2 bot side: round winners %s, bot result %s.' % (winners, pepe.duel_end()['result']), flush=True)

        # ---- R-3: the spectator goes next to whichever ring the server picks (sala in the bridge).
        idle(peers, 120, lambda: progress() == 'r2_done')
        juan = Bot(port, key, 'Juan', save=bot_save('Juan', PLAZA[0] + 3)); peers.append(juan)
        juan.evasion = 0
        assert pepe.act('duelChallenge', text='Juan', gold=0, item=-1)['ok']
        juan.push('duelInvite'); assert juan.act('duelAccept', name='Pepe')['ok']
        start = pepe.push('duelStart')['duel']; juan.push('duelStart')
        BRIDGE.write_text(json.dumps(dict(seed=start['seed'], sala=start['sala'], fighters=[pepe.id, juan.id])), encoding='utf-8')
        idle(peers, 60, lambda: progress() == 'spectator_ready')
        for r in (1, 2):
            juan.push('duelRoundStart', round=r)
            pepe.push('duelRoundStart', round=r)
            pepe.until(lambda m: m['type'] == 'duelRingState' and m['duel'].get('phase') == 'fight', timeout=30)
            idle(peers, 4)  # Unity takes its "fight" screenshot before the kill.
            next_to(pepe, position_of(pepe, juan.id))
            for _ in range(40):
                text = pepe.act('attack', id=juan.id).get('text') or ''
                if any(m['type'] == 'duelRoundEnd' for m in pepe.log) or 'conteo' in text or 'reto' in text: break
                drain(juan); time.sleep(.6)
            round_end(pepe, r, [juan])
        print('R-3 bot side: Pepe beat Juan 2-0 next to the spectator.', flush=True)

        idle(peers, 240, lambda: REPORT.exists())
        time.sleep(1)
        result = json.loads(REPORT.read_text(encoding='utf-8'))
        print(json.dumps(result, ensure_ascii=True, indent=1), flush=True)
        for failure in bot_failures:
            print('BOT FAIL: ' + failure, flush=True)
        if not result['passed'] or bot_failures:
            raise AssertionError('Unity duel QA failed: %d Unity failures, %d log errors, %d bot failures'
                                 % (len(result['failures']), len(result['errors']), len(bot_failures)))
    finally:
        for peer in peers:
            try: peer.close()
            except OSError: pass
        server.terminate(); server.wait(5); log.close()
        for path in (FLAG, BRIDGE, PROGRESS):
            path.unlink(missing_ok=True)
        if sys.exc_info()[0]:
            print((work / 'server.log').read_text(encoding='utf-8', errors='replace')[-1500:])
        _, now = saves_snapshot()
        changed = [n for n, h in hashes.items() if now.get(n) != h]
        added = [n for n in now if n not in hashes]
        if changed or added:
            raise AssertionError(f'Local saves changed: {changed}; new save files: {added}')
        print(f'PASS: {len(hashes)} original save files unchanged, no new save files.', flush=True)


if __name__ == '__main__':
    run()
