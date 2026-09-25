"""Two-client cooperative regressions. Isolated port, identity and temporary saves."""
import copy
import json
import pathlib
import secrets
import socket
import subprocess
import tempfile
import time
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[1]
DLL = ROOT / 'OnlineServer/bin/Debug/net10.0/AOOnlineServer.dll'
PROTOCOL = 3


def fixture():
    npc = dict(npcIndex=100, x=50, y=50, heading=3, maxHp=2, attackable=True,
               hostile=False, movement=1, moveIntervalMs=500, visionRange=8,
               attackPower=0, defense=0, evasionPower=0, minHit=0, maxHit=0)
    vendor = dict(npc, npcIndex=200, x=49, y=50, attackable=False)
    map1 = dict(mapNumber=1, xmin=1, xmax=100, ymin=1, ymax=100,
                blocks=[], triggers=[], exits=[], npcs=[npc, vendor],
                doors=[dict(x=49, y=51, width=1, locked=False)])
    map2 = dict(copy.deepcopy(map1), mapNumber=2)
    return dict(maps=[map1, map2], items=[dict(index=i,name=str(i),value=30,objType=1) for i in (12,500,501)],
                loot=dict(goldItemIndex=12,npcs=[dict(npcIndex=100,giveExp=11,giveGold=20,
                     respawnMinSeconds=300,respawnMaxSeconds=300,inventoryDrops=[dict(itemIndex=500,amount=1)])]),
                spells=[dict(id=1,raiseHp=1,minHp=5,maxHp=5),dict(id=2,raiseHp=2,minHp=5,maxHp=5)],
                summons=dict(summons=[]), npcMagic=dict(npcs=[]),
                shops=dict(npcs=[dict(npcIndex=200,trades=True,itemType=100,stock=[dict(itemIndex=501,amount=1,infinite=False)])]),
                quests=[dict(id=7,rewardGold=40,repeatable=False),dict(id=8,rewardGold=5,repeatable=True)])


def snapshot(name):
    return dict(character=dict(name=name),rpg=dict(level=1,maxHp=100,mana=100,raceId=1,genderId=1,headIndex=1,
                 classId=1,experience=0,skills=[100]*24),combat=dict(hp=100,dead=False,gold=100),
                inventory=dict(itemIndices=[0]*20,amounts=[0]*20),world=dict(map=1,x=50,y=51,heading=1),
                quests=dict(active=[],completed=[]),magic=dict(learnedSpells=[1,2]))


class Peer:
    def __init__(self, port, key, name, identity=None, version=PROTOCOL, save=None):
        self.identity = identity or (uuid.uuid4().hex,secrets.token_hex(32))
        self.save=save or snapshot(name); self.ack=0; self.wallet=0; self.bank=0
        self.sock=socket.create_connection(('127.0.0.1',port),timeout=5)
        self.sock.settimeout(5); self.file=self.sock.makefile('r',encoding='utf-8')
        self.send(dict(type='hello',version=version,key=key,characterId=self.identity[0],token=self.identity[1],
                       snapshot=json.dumps(self.save),player=self.player()))
        welcome=self.until(lambda m:m['type'] in ('welcome','error'))
        if welcome['type']=='error': raise RuntimeError(welcome['text'])
        self.id=welcome['id'];self.save=json.loads(welcome['snapshot']);self.ack=welcome['ack']
        self.events(welcome.get('events'))

    def player(self):
        return dict(self.save['world'],attack=10000,evasion=10000,minHit=1000,maxHit=1000,strength=19,damageModifier=1,maxMana=100)

    def send(self,m): self.sock.sendall((json.dumps(m)+'\n').encode())
    def until(self,predicate):
        end=time.monotonic()+6
        while time.monotonic()<end:
            line=self.file.readline()
            if not line: raise RuntimeError('connection closed')
            m=json.loads(line)
            # Protocol 3: gold is whatever the server says (absolute), like the real client.
            if m.get('type') in ('welcome','result','state'):
                self.wallet=m.get('wallet',0);self.bank=m.get('bank',0);self.save['combat']['gold']=self.wallet
            if predicate(m): return m
        raise AssertionError('response timeout')

    def action(self,typ,**values):
        request=values.pop('request',uuid.uuid4().hex)
        self.send(dict(type=typ,request=request,snapshot=json.dumps(self.save),ack=self.ack,player=self.player(),**values))
        return self.until(lambda m:m['type']=='result' and m.get('request')==request)

    def item(self,item,amount):
        if item==12: return  # gold arrives as the absolute wallet

        inv=self.save['inventory'];ids=inv['itemIndices'];nums=inv['amounts']
        slot=ids.index(item) if item in ids else ids.index(0)
        ids[slot]=item;nums[slot]+=amount
        if nums[slot]==0:ids[slot]=0

    def events(self,events):
        for e in events or []:
            if e['seq']<=self.ack:continue
            assert e['seq']==self.ack+1
            if e['type']=='kill':self.save['rpg']['experience']+=e['exp']
            if e['type'] in ('item','buy'):self.item(e['item'],e['amount'])
            if e['type'] in ('remove','sell'):self.item(e['item'],-e['amount'])
            if e['type']=='hurt':self.save['combat']['hp']-=e['damage']
            self.ack=e['seq']

    def state(self):return self.until(lambda m:m['type']=='state')
    def close(self):
        self.sock.shutdown(socket.SHUT_RDWR);self.file.close();self.sock.close()


def run():
    with tempfile.TemporaryDirectory(prefix='ao-coop-test-') as temp:
        root=pathlib.Path(temp);key=secrets.token_hex(18)
        (root/'key').write_text(key);(root/'catalog.json').write_text(json.dumps(fixture()))
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1',0));port=reservation.getsockname()[1]
        args=['dotnet',str(DLL),'--port',str(port),'--key-file',str(root/'key'),'--catalog',str(root/'catalog.json'),'--data',str(root/'saves'),'--test']
        log=(root/'server.log').open('w')
        def launch():
            process=subprocess.Popen(args,stdout=log,stderr=log)
            for _ in range(60):
                try:
                    probe=socket.create_connection(('127.0.0.1',port),timeout=.1);probe.close();return process
                except OSError:time.sleep(.1)
            raise AssertionError('server did not start')
        process=launch();peers=[]
        try:
            a=Peer(port,key,'Alpha');b=Peer(port,key,'Beta');peers=[a,b]
            for _ in range(6):
                result=a.action('attack',target=1);assert result['ok'],result
                if any(e['type']=='kill' for e in result['events']):break
                time.sleep(.75)
            else:raise AssertionError('enemy never died')
            a.events(result['events']);sa=a.until(lambda m:m['type']=='state' and m['npcs'][0]['dead'])
            sb=b.until(lambda m:m['type']=='state' and m['npcs'][0]['dead']);b.events(sb['events'])
            assert a.save['rpg']['experience']+b.save['rpg']['experience']==11
            assert sorted([a.save['rpg']['experience'],b.save['rpg']['experience']])==[5,6]
            floor=next(x for x in sa['loot'] if x['item']==500)
            request=uuid.uuid4().hex
            result=a.action('pickup',target=floor['id'],request=request);assert result['ok'],result;a.events(result['events'])
            duplicate=a.action('pickup',target=floor['id'],request=request);assert duplicate['events']==result['events']
            assert not b.action('pickup',target=floor['id'])['ok']
            time.sleep(.04);result=a.action('drop',item=500,amount=1);assert result['ok'],result;a.events(result['events'])
            state=b.until(lambda m:m['type']=='state' and any(x['item']==500 for x in m['loot']))
            floor=next(x for x in state['loot'] if x['item']==500)
            time.sleep(.04);result=b.action('pickup',target=floor['id']);assert result['ok'],result;b.events(result['events'])
            assert 500 not in a.save['inventory']['itemIndices'] and 500 in b.save['inventory']['itemIndices']
            time.sleep(.04);result=a.action('buy',id=200,item=501,amount=1);assert result['ok'],result;a.events(result['events'])
            assert a.save['combat']['gold']==85
            time.sleep(.04);assert not b.action('buy',id=200,item=501,amount=1)['ok']
            time.sleep(.04);result=a.action('sell',id=200,item=501,amount=1);assert result['ok'],result;a.events(result['events']);assert a.save['combat']['gold']==95
            time.sleep(.04);assert a.action('door',x=49,y=51)['ok']
            assert b.until(lambda m:m['type']=='state' and any(d['open'] for d in m['doors']))
            time.sleep(.04);assert not a.action('cast',spell=2,id=b.id,x=50,y=51)['ok']
            time.sleep(.04);assert a.action('cast',spell=1,id=b.id,x=50,y=51)['ok']
            b.events(b.until(lambda m:m['type']=='state' and any(e['type']=='spell' for e in m['events']))['events'])
            # Skill shots send their flight time (amount, ms): the cooldown counts from the launch.
            ally=dict(spell=1,id=b.id,x=50,y=51)
            def cooling(**extra):return 'recuperaci' in a.action('cast',**extra,**ally).get('text','')
            time.sleep(1.2);assert a.action('cast',**ally)['ok']
            time.sleep(.04);assert cooling()
            time.sleep(1.3);assert cooling(amount=1000)
            time.sleep(.04);assert a.action('cast',amount=100,**ally)['ok']
            time.sleep(.04);assert cooling()
            # Meditation aura and cast animation reach the other players; unknown spells are ignored.
            def seen(fx,seq,spell):
                return b.until(lambda m:m['type']=='state' and any(p['id']==a.id and p['meditationFx']==fx and p['castSeq']==seq and p['castSpell']==spell for p in m['players']))
            a.send(dict(type='position',player=dict(a.player(),meditationFx=115,castSpell=1,castSeq=1)));seen(115,1,1)
            a.send(dict(type='position',player=dict(a.player(),meditationFx=115,castSpell=999,castSeq=2)))
            a.send(dict(type='position',player=dict(a.player(),meditationFx=0,castSpell=999,castSeq=2)));seen(0,1,1)
            a.action('sync');b.action('sync')
            a_id=a.identity;b_id=b.identity;expected_a=copy.deepcopy(a.save);expected_b=copy.deepcopy(b.save)
            a.close();b.close();peers=[];time.sleep(.25);process.terminate();process.wait(5)
            process=launch();a=Peer(port,key,'Alpha',a_id);b=Peer(port,key,'Beta',b_id);peers=[a,b]
            assert a.save==expected_a and b.save==expected_b
            assert not any(x['item']==500 for x in a.state()['loot'])
            assert not b.action('pickup',target=floor['id'])['ok']
            # Unacknowledged floor claim must replay after a lost connection.
            gold=next(x for x in a.state()['loot'] if x['item']==12)
            time.sleep(.04);result=a.action('pickup',target=gold['id']);assert result['ok']
            a.close();peers.remove(a);time.sleep(.2);a=Peer(port,key,'Alpha',a_id);peers.append(a)
            assert a.save['combat']['gold']==expected_a['combat']['gold']+20
            a.action('sync')
            # Protocol 3: gold belongs to the server. A client snapshot with more gold is ignored (A-07).
            def ledger():
                time.sleep(.04);result=a.action('testLedger');assert result['ok'],result;return json.loads(result['text'])
            before=a.wallet
            a.save['combat']['gold']=999999;time.sleep(.04);a.action('sync');assert a.wallet==before
            # Bank: deposit/withdraw move gold between two server accounts, once per request.
            request=uuid.uuid4().hex
            time.sleep(.04);assert a.action('bank',gold=50,request=request)['ok'] and a.wallet==before-50 and a.bank==50
            time.sleep(.04);a.action('bank',gold=50,request=request);assert a.wallet==before-50 and a.bank==50
            time.sleep(.04);assert 'banco' in a.action('bank',gold=-51).get('text','')
            time.sleep(.04);assert a.action('bank',gold=-50)['ok'] and a.wallet==before and a.bank==0
            # Quest gold comes from the catalog, once per quest (repeatables: once per completion, in order).
            time.sleep(.04);assert 'completada' in a.action('questReward',id=7).get('text','')
            a.save['quests']['completed']=[7]
            time.sleep(.04);assert a.action('questReward',id=7)['ok'] and a.wallet==before+40
            time.sleep(.04);a.action('questReward',id=7);assert a.wallet==before+40
            time.sleep(.04);assert a.action('questReward',id=8,amount=1)['ok'] and a.wallet==before+45
            time.sleep(.04);a.action('questReward',id=8,amount=1);assert a.wallet==before+45
            time.sleep(.04);assert 'orden' in a.action('questReward',id=8,amount=3).get('text','')
            time.sleep(.04);assert a.action('questReward',id=8,amount=2)['ok'] and a.wallet==before+50
            book=ledger();assert book['sum']==0,book
            assert book['balances']['player:'+a_id[0]]==a.wallet
            for name in ['Gamma','Delta','Epsilon','Zeta','Eta','Theta','Iota','Kappa','Lambda']:
                peers.append(Peer(port,key,name))
            try: Peer(port,key,'Overflow')
            except RuntimeError as e: assert 'completa' in str(e)
            else: raise AssertionError('12th player accepted')
            assert len(peers)==11
            try: Peer(port,key,'Viejo',version=2)
            except RuntimeError as e: assert 'incompatible' in str(e)
            else: raise AssertionError('protocol 2 client accepted')
            book=ledger()
            for peer in peers: peer.close()
            peers=[];time.sleep(.25);process.terminate();process.wait(5)
            # A torn last ledger line (crash mid-write) is cut on start; a changed NPC list resets that map.
            with open(root/'saves'/'ledger.jsonl','ab') as f: f.write(b'{"txId":99999,"op":"torn"')
            catalog=fixture();catalog['maps'][0]['npcs'][0]['npcIndex']=101;catalog['maps'][0]['npcLayoutVersion']='v2'
            (root/'catalog.json').write_text(json.dumps(catalog))
            process=launch();a=Peer(port,key,'Alpha',a_id);peers=[a]
            assert ledger()==book and a.save['combat']['gold']==a.wallet==before+50
            assert list(root.glob('saves/ledger.jsonl.torn-*'))
            assert a.state()['npcs'][0]['npc']==101
            print('PASS: shared NPC death, split XP, unique loot, idempotent pickup, transfer, finite shop, gold, doors, ally healing, no PvP, skill shot cooldown, meditation/cast sync, restart, replay, 11-player cap, '
                  'server gold (A-07), bank, quest gold, ledger invariant, torn ledger line, protocol 2 rejected, NPC layout reset.')
        finally:
            for peer in peers:
                try:peer.close()
                except OSError:pass
            process.terminate();process.wait(5);log.close()
            if process.returncode not in (0,1):print((root/'server.log').read_text()[-1000:])


def run_demo():
    """--demo: EXP multiplier by the receiver's level tier and OroMult x2 on NPC gold (AODemoRates), NPC level EXP
    penalty (decision 17) and death drops in the dungeon (decision 18, AODeathDropRules)."""
    with tempfile.TemporaryDirectory(prefix='ao-demo-test-') as temp:
        root=pathlib.Path(temp);key=secrets.token_hex(18)
        catalog=fixture();catalog['loot']['npcs'][0]['level']=20;catalog['maps'][0]['dropOnDeath']=True
        catalog['items'].append(dict(index=502,name='Daga newbie',value=1,objType=2,newbie=True))
        (root/'key').write_text(key);(root/'catalog.json').write_text(json.dumps(catalog))
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1',0));port=reservation.getsockname()[1]
        log=(root/'server.log').open('w')
        process=subprocess.Popen(['dotnet',str(DLL),'--port',str(port),'--key-file',str(root/'key'),'--catalog',str(root/'catalog.json'),
                                  '--data',str(root/'saves'),'--test','--demo'],stdout=log,stderr=log)
        peer=None
        try:
            for _ in range(60):
                try:probe=socket.create_connection(('127.0.0.1',port),timeout=.1);probe.close();break
                except OSError:time.sleep(.1)
            save=snapshot('Veterano');save['rpg']['level']=27
            peer=Peer(port,key,'Veterano',save=save)
            for _ in range(6):
                result=peer.action('attack',target=1);assert result['ok'],result
                kill=[e for e in result['events'] if e['type']=='kill']
                if kill:break
                time.sleep(.75)
            else:raise AssertionError('enemy never died')
            # Level 27 vs NPC level 20: 3 levels over the margin → 11 × 0.85 = 9.35 → 9 (VB6 rounding), then ×44.
            assert kill[0]['exp']==9*44,kill
            gold=next(x for x in peer.until(lambda m:m['type']=='state' and m['npcs'][0]['dead'])['loot'] if x['item']==12)
            assert gold['amount']==40,gold
            # Death in a dungeon map: all but 1,000 gold per level and every item fall (newbie kept up to level 12).
            save=snapshot('Caido');save['rpg']['level']=5;save['combat']['gold']=8000
            save['inventory']['itemIndices'][:2]=[500,502];save['inventory']['amounts'][:2]=[2,1]
            fallen=Peer(port,key,'Caido',save=save);others=[fallen]
            assert fallen.wallet==8000
            def die(dead):
                fallen.save['combat'].update(dead=dead,hp=0 if dead else 100);time.sleep(.04)
                result=fallen.action('sync');fallen.events(result['events']);return result
            removed=[e for e in die(True)['events'] if e['type']=='remove']
            assert fallen.wallet==5000 and [(e['item'],e['amount']) for e in removed]==[(500,2)],removed
            floor=fallen.until(lambda m:m['type']=='state' and any(x['item']==500 for x in m['loot']))['loot']
            assert any(x['item']==12 and x['amount']==3000 for x in floor) and 502 in fallen.save['inventory']['itemIndices']
            die(True);assert fallen.wallet==5000   # still the same death: nothing falls twice
            die(False);die(True);assert fallen.wallet==5000   # a new death with only protected gold left
            print('PASS: demo EXP x44 at level 27 with NPC level penalty, NPC gold x2, dungeon death drop once per death.')
        finally:
            for other in (locals().get('others') or []):other.close()
            if peer:peer.close()
            process.terminate();process.wait(5);log.close()


def run_ranged():
    """Bows online (arco-original.md §3): ±11/±9, the equipped munition of the right subtype, one unit per shot, 1200 ms."""
    with tempfile.TemporaryDirectory(prefix='ao-bow-test-') as temp:
        root=pathlib.Path(temp);key=secrets.token_hex(18)
        catalog=fixture();catalog['loot']['npcs'][0]['respawnMinSeconds']=catalog['loot']['npcs'][0]['respawnMaxSeconds']=300
        catalog['maps'][0]['npcs'][0]['maxHp']=100000   # stays alive for every shot
        catalog['items']+=[dict(index=600,name='Arco',value=1,objType=2,minHit=1,maxHit=3,projectile=1,munition=1),
                           dict(index=601,name='Flecha',value=1,objType=32,subType=1,minHit=1,maxHit=1),
                           dict(index=602,name='Bala',value=1,objType=32,subType=2,minHit=1,maxHit=1)]
        (root/'key').write_text(key);(root/'catalog.json').write_text(json.dumps(catalog))
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1',0));port=reservation.getsockname()[1]
        log=(root/'server.log').open('w')
        process=subprocess.Popen(['dotnet',str(DLL),'--port',str(port),'--key-file',str(root/'key'),'--catalog',str(root/'catalog.json'),
                                  '--data',str(root/'saves'),'--test'],stdout=log,stderr=log)
        peer=None
        try:
            for _ in range(60):
                try:probe=socket.create_connection(('127.0.0.1',port),timeout=.1);probe.close();break
                except OSError:time.sleep(.1)
            save=snapshot('Arquera');save['world'].update(x=55,y=51)   # the NPC is at (50,50): 5 tiles away
            save['inventory']['itemIndices'][:3]=[600,601,602];save['inventory']['amounts'][:3]=[1,2,5]
            peer=Peer(port,key,'Arquera',save=save)
            def shoot():time.sleep(.04);return peer.action('attack',target=1)
            assert 'alcance' in (shoot().get('text') or '')                       # no weapon: melee only
            peer.save['inventory']['weapon']=600
            assert 'equipada' in (shoot().get('text') or '')                      # bow without munition
            peer.save['inventory']['munition']=602
            assert 'equipada' in (shoot().get('text') or '')                      # bullets do not fit a bow
            peer.save['inventory']['munition']=601
            first=shoot();assert first['ok'],first
            assert [(e['item'],e['amount']) for e in first['events'] if e['type']=='remove']==[(601,1)]
            assert 'atacar' in (shoot().get('text') or '')                        # 1200 ms between arrows
            time.sleep(1.25);assert shoot()['ok']
            time.sleep(1.25);assert 'munición' in (shoot().get('text') or '')     # both arrows spent (acks pending)
            peer.save['world'].update(x=62);peer.save['inventory']['amounts'][1]=5
            time.sleep(1.25);assert 'alcance' in (shoot().get('text') or '')      # 12 columns away
            print('PASS: bow range ±11/±9, equipped munition subtype, one arrow per shot, 1200 ms, melee without bow.')
        finally:
            if peer:peer.close()
            process.terminate();process.wait(5);log.close()


if __name__=='__main__':run();run_demo();run_ranged()
