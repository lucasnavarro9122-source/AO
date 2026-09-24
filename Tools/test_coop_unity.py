"""Serve an isolated room and a companion for the opt-in Unity Play-mode QA."""
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import time
from test_coop_server import ROOT, DLL, Peer

root=ROOT/'Temp/coop-unity-qa'
root.mkdir(parents=True,exist_ok=True)
key=secrets.token_hex(18)
(root/'key.txt').write_text(key)
report=ROOT/'MigrationReports/unity_coop_v250.json'
if report.exists():report.rename(root/f'report-{time.time_ns()}.json')
with socket.socket() as probe:
    probe.bind(('127.0.0.1',0));port=probe.getsockname()[1]
data=root/f'saves-{time.time_ns()}'
with (root/'server.log').open('w') as log:
    server=subprocess.Popen(['dotnet',str(DLL),'--port',str(port),'--catalog',str(ROOT/'OnlineServer/Data/catalog.json.gz'),
        '--key-file',str(root/'key.txt'),'--data',str(data)],stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    peer=None
    try:
        for _ in range(200):
            try:
                connection=socket.create_connection(('127.0.0.1',port),timeout=.1);connection.close();break
            except OSError:time.sleep(.1)
        peer=Peer(port,key,'CompaneroPrueba')
        peer.save['world'].update(x=58,y=44);peer.save['rpg']['level']=5;peer.action('sync')
        flag=ROOT/'Temp/run_coop_qa.json';flag.write_text(json.dumps(dict(port=port,key=key)))
        print('Isolated server ready; waiting for Unity QA.',flush=True)
        end=time.monotonic()+240;next_chat=0
        while time.monotonic()<end and not report.exists():
            peer.events(peer.state().get('events'))
            peer.action('sync')
            if time.monotonic()>next_chat:
                next_chat=time.monotonic()+5;peer.send(dict(type='chat',text='Listo para jugar juntos.'))
            time.sleep(.4)
        if not report.exists():raise TimeoutError('Unity did not finish QA')
        result=json.loads(report.read_text());print(json.dumps(result,ensure_ascii=False),flush=True)
        if not result['passed']:raise AssertionError('Unity QA failed')
        source=Path(os.environ['USERPROFILE'])/'AppData/LocalLow/DefaultCompany/My project (1)'
        hashes=json.loads((ROOT/'Temp/coop-qa-original-backup/manifest.json').read_text())
        changed=[rel for rel,value in hashes.items() if hashlib.sha256((source/rel).read_bytes()).hexdigest()!=value]
        assert not changed, f'Local saves changed: {changed}'
        print(f'PASS: {len(hashes)} original save files unchanged.',flush=True)
    finally:
        if peer:
            try:peer.close()
            except OSError:pass
        server.terminate();server.wait(5)
