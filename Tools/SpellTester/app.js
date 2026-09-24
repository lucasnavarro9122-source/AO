(() => {
  const spells = window.AO_SPELLS || [];
  const meditations = window.AO_MEDITATIONS || [];
  const $ = id => document.getElementById(id);
  const select=$('spellSelect'), search=$('search'), launch=$('launch'), stop=$('stop'), arena=$('arena');
  const projectile=$('projectile'), impact=$('impact'), persistent=$('persistent'), audio=$('audio');
  const castText=$('castText'), magicWords=$('magicWords'), status=$('status'), progress=$('progress');
  const meditationFx=$('meditationFx'), medAudio=$('medAudio'), medSelect=$('meditationSelect');
  let current=null, runToken=0, persistentTimer=null, mode='spells';
  let meditating=false, medToken=0, mana=15, manaTimer=null;

  const wait = ms => new Promise(r=>setTimeout(r,ms));
  const speedFactor=()=>100/Number($('speed').value||100);
  const medSpeedFactor=()=>100/Number($('medSpeed').value||100);

  function filtered(){
    const q=search.value.trim().toLowerCase();
    if(!q) return spells;
    return spells.filter(s=>String(s.id).includes(q)||s.name.toLowerCase().includes(q));
  }
  function populate(preserve=true){
    const old=current?.id; select.innerHTML='';
    filtered().forEach(s=>{const o=document.createElement('option');o.value=s.id;o.textContent=String(s.id).padStart(3,'0')+' · '+s.name;select.appendChild(o)});
    let s=spells.find(x=>x.id===old) || filtered()[0] || spells[0];
    if(s){select.value=s.id; showSpell(s)}
  }
  function showSpell(s){
    current=s;
    $('spellName').textContent=s.name; $('spellId').textContent='Hechizo #'+s.id; $('spellDesc').textContent=s.description||'Sin descripción.';
    $('spellIcon').src=s.icon||''; $('spellIcon').style.visibility=s.icon?'visible':'hidden';
    const aud=s.wav>0?(s.audioAvailable?`WAV ${s.wav} · original disponible`:`WAV ${s.wav} · archivo faltante`):'Sin WAV asignado';
    $('meta').innerHTML=`<div class="row"><span>Audio</span><b class="${s.audioAvailable?'good':'bad'}">${aud}</b></div>`+
      `<div class="row"><span>Mana / stamina</span><b>${s.mana} / ${s.stamina}</b></div>`+
      `<div class="row"><span>Cooldown</span><b>${s.cooldown||0}s</b></div>`+
      `<div class="row"><span>Viaje / impacto</span><b>${s.projectile.length} / ${s.impact.length} frames</b></div>`;
    $('chips').innerHTML=s.resourceKinds.map(x=>`<span class="chip">${x}</span>`).join('');
  }
  function actorPoint(el, yOffset=90){
    const r=el.getBoundingClientRect(), a=arena.getBoundingClientRect();
    return {x:r.left-a.left+r.width/2,y:r.top-a.top+r.height-yOffset};
  }
  function place(el,p){el.style.left=p.x+'px';el.style.top=p.y+'px'}
  function hide(el){el.style.display='none'}
  function show(el){el.style.display='block'}

  async function animateFrames(el, frames, duration, token, loop=false){
    if(!frames || !frames.length) return;
    show(el); let i=0; const frameMs=Math.max(28,(duration/frames.length)*speedFactor());
    do{
      for(i=0;i<frames.length;i++){
        if(token!==runToken) return;
        el.src=frames[i]; await wait(frameMs);
      }
    }while(loop && token===runToken);
  }
  async function animateProjectile(s, from, to, token){
    let frames=s.projectile.length?s.projectile:(s.impact.length?[s.impact[0]]:(s.icon?[s.icon]:[]));
    if(!frames.length) return;
    show(projectile); const dur=650*speedFactor(); const start=performance.now();
    while(true){
      if(token!==runToken)return;
      const n=Math.min(1,(performance.now()-start)/dur); const eased=n<.5?2*n*n:1-Math.pow(-2*n+2,2)/2;
      place(projectile,{x:from.x+(to.x-from.x)*eased,y:from.y+(to.y-from.y)*eased-40*Math.sin(Math.PI*n)});
      projectile.src=frames[Math.min(frames.length-1,Math.floor(n*frames.length))];
      progress.style.width=(20+45*n)+'%'; if(n>=1)break; await new Promise(requestAnimationFrame);
    }
    hide(projectile);
  }
  function stopSpell(){
    runToken++; clearInterval(persistentTimer);persistentTimer=null; audio.pause();audio.currentTime=0;[projectile,impact,persistent].forEach(hide);castText.style.opacity=0;magicWords.style.opacity=0;progress.style.width='0';
  }
  function stopAll(){stopSpell(); if(meditating) stopMeditation(); status.textContent='Detenido.';}
  async function cast(){
    if(!current)return;
    if(meditating) await stopMeditation();
    stopSpell(); const token=++runToken; const s=current;
    const from=actorPoint($('caster'),105), to=actorPoint($('target'),105); place(impact,to);place(persistent,to);place(projectile,from);
    castText.textContent=s.name;magicWords.textContent=s.magicWords||'';castText.style.opacity=1;magicWords.style.opacity=s.magicWords?1:0;status.textContent='Lanzando '+s.name+'...';progress.style.width='8%';
    if($('audioOn').checked && s.audio){audio.src=s.audio;audio.volume=.85;audio.play().catch(()=>{});} else if(s.wav>0&&!s.audioAvailable){status.textContent='Lanzando · audio original '+s.wav+' no está en los recursos.'}
    await wait(180*speedFactor()); if(token!==runToken)return;
    if(s.projectile.length){await animateProjectile(s,from,to,token)} else {await wait(180*speedFactor());progress.style.width='58%'}
    if(token!==runToken)return;
    if(s.impact.length){progress.style.width='70%';await animateFrames(impact,s.impact,Math.max(380,s.impact.length*65),token,false);hide(impact)}
    else if(s.icon){impact.src=s.icon;show(impact);await wait(320*speedFactor());hide(impact)}
    if(token!==runToken)return;
    progress.style.width='100%';status.textContent='Animación completada.';castText.style.opacity=0;magicWords.style.opacity=0;
    if($('persistentOn').checked && s.persistent.length){show(persistent);let i=0;persistent.src=s.persistent[0];persistentTimer=setInterval(()=>{if(token!==runToken)return clearInterval(persistentTimer);i=(i+1)%s.persistent.length;persistent.src=s.persistent[i]},Math.max(70,120*speedFactor()));status.textContent='Completado · efecto persistente activo.'}
    setTimeout(()=>{if(token===runToken)progress.style.width='0'},550);
  }

  function populateMeditations(){
    medSelect.innerHTML='';
    meditations.forEach((m,i)=>{
      const o=document.createElement('option');
      o.value=i;
      o.textContent=`Variante ${i+1} · FX ${m.startFx} → loop ${m.loopFx}`;
      medSelect.appendChild(o);
    });
    medSelect.value=0;
    showMeditationInfo();
  }
  function currentMeditation(){return meditations[Number(medSelect.value)||0] || meditations[0];}
  function showMeditationInfo(){
    const m=currentMeditation(); if(!m)return;
    $('medMeta').innerHTML=
      `<div class="row"><span>ComposedAnimation</span><b>${m.composedIndex}</b></div>`+
      `<div class="row"><span>FX inicio / loop</span><b>${m.startFx} / ${m.loopFx}</b></div>`+
      `<div class="row"><span>GRH inicio / loop</span><b>${m.startAnimGrh} / ${m.loopAnimGrh}</b></div>`+
      `<div class="row"><span>Duración inicio</span><b>${m.startDurationMs} ms</b></div>`+
      `<div class="row"><span>Duración loop</span><b>${m.loopDurationMs} ms</b></div>`+
      `<div class="row"><span>Cierre original</span><b>${m.exitDurationMs} ms · reversa</b></div>`;
  }
  function updateManaUI(){
    $('manaText').textContent=`${Math.round(mana)} / 100`;
    $('manaFill').style.width=Math.max(0,Math.min(100,mana))+'%';
  }
  async function playMedFrames(frames,duration,token,reverse=false){
    if(!frames?.length)return;
    show(meditationFx);
    const pos=actorPoint($('caster'),105); place(meditationFx,{x:pos.x,y:pos.y-25});
    const seq=reverse?[...frames].reverse():frames;
    const frameMs=Math.max(30,(duration/seq.length)*medSpeedFactor());
    for(const frame of seq){
      if(token!==medToken)return false;
      meditationFx.src=frame;
      await wait(frameMs);
    }
    return token===medToken;
  }
  async function medLoop(m,token){
    const frameMs=Math.max(30,(m.loopDurationMs/m.loopFrames.length)*medSpeedFactor());
    let i=0;
    show(meditationFx);
    while(token===medToken && meditating){
      const pos=actorPoint($('caster'),105); place(meditationFx,{x:pos.x,y:pos.y-25});
      meditationFx.src=m.loopFrames[i++ % m.loopFrames.length];
      await wait(frameMs);
    }
  }
  async function startMeditation(){
    if(meditating)return;
    stopSpell();
    const m=currentMeditation(); if(!m)return;
    meditating=true; const token=++medToken;
    $('meditateBtn').textContent='DEJAR DE MEDITAR (B)';
    status.textContent=`Comienzas a meditar · FX ${m.startFx}.`;
    progress.style.width='15%';
    if($('medAudioOn').checked){
      medAudio.src='assets/audio/158.wav'; medAudio.volume=.75; medAudio.loop=true; medAudio.play().catch(()=>{});
    }
    clearInterval(manaTimer);
    const rate=()=>Number($('manaRate').value||6);
    manaTimer=setInterval(()=>{
      if(!meditating)return;
      mana=Math.min(100,mana+rate()/4);
      updateManaUI();
      if(mana>=100){status.textContent='Maná completo · la meditación puede detenerse con B.';}
    },250);
    const ok=await playMedFrames(m.startFrames,m.startDurationMs,token,false);
    if(!ok || !meditating)return;
    progress.style.width='55%';
    medLoop(m,token);
  }
  async function stopMeditation(){
    if(!meditating){hide(meditationFx);return;}
    const m=currentMeditation();
    meditating=false;
    medToken++;
    const closeToken=medToken;
    clearInterval(manaTimer);manaTimer=null;
    medAudio.pause();medAudio.currentTime=0;
    $('meditateBtn').textContent='MEDITAR (B)';
    status.textContent='Dejas de meditar · cierre en reversa.';
    progress.style.width='85%';
    await playMedFrames(m.startFrames,m.exitDurationMs,closeToken,true);
    if(closeToken===medToken){
      hide(meditationFx);progress.style.width='0%';status.textContent='Meditación finalizada.';
    }
  }
  function toggleMeditation(){return meditating?stopMeditation():startMeditation();}

  function setMode(next){
    mode=next;
    $('tabSpells').classList.toggle('active',mode==='spells');
    $('tabMeditation').classList.toggle('active',mode==='meditation');
    $('spellPanel').classList.toggle('hidden',mode!=='spells');
    $('meditationPanel').classList.toggle('hidden',mode!=='meditation');
    $('target').classList.toggle('hidden',mode==='meditation');
    if(mode==='spells' && meditating) stopMeditation();
    if(mode==='meditation') stopSpell();
    status.textContent=mode==='meditation'?'Meditación lista · B para comenzar.':'Tester de hechizos listo.';
  }

  select.addEventListener('change',()=>showSpell(spells.find(s=>s.id===Number(select.value))));
  search.addEventListener('input',()=>populate(false));
  launch.addEventListener('click',cast);
  stop.addEventListener('click',stopAll);
  medSelect.addEventListener('change',()=>{if(meditating)stopMeditation();showMeditationInfo()});
  $('meditateBtn').addEventListener('click',toggleMeditation);
  $('resetMana').addEventListener('click',()=>{mana=15;updateManaUI();});
  $('tabSpells').addEventListener('click',()=>setMode('spells'));
  $('tabMeditation').addEventListener('click',()=>setMode('meditation'));
  window.addEventListener('resize',()=>{if(meditating){const pos=actorPoint($('caster'),105);place(meditationFx,{x:pos.x,y:pos.y-25})}});
  document.addEventListener('keydown',e=>{
    if(e.code==='KeyB'&&!e.repeat&&document.activeElement!==search){
      e.preventDefault(); setMode('meditation'); toggleMeditation(); return;
    }
    if(e.code==='Space'&&!e.repeat&&document.activeElement!==search&&mode==='spells'){
      e.preventDefault();cast();
    }
  });

  populate(false);
  populateMeditations();
  updateManaUI();
})();