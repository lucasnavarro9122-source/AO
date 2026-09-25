/* AO Spell Tester v1.3
 * Visor de FX de hechizos con zoom pixelado, frame a frame, pivote/grilla,
 * ajuste y exportación de spell_visual_tuning.json y comparación Original vs HD.
 *
 * Reproduce la lógica visual del juego (Assets/AOMigrator/Runtime):
 *  - AOSpellVisualOverridesV130: sprites con pivote abajo-centro (0.5, 0) y 32 px por unidad (1 casilla = 32 px).
 *  - AOGridMap.TileToWorld: el ancla del personaje es el borde inferior-centro de su casilla.
 *  - AOSpellFXV120: proyectil lineal a projectileSpeed (casillas/s, duración limitada a 0,12–1,2 s) con
 *    projectileYOffset/projectileScale; impacto en objetivo + impactYOffset, escala impactScale, dura impactDuration
 *    (0 = automática: 1 frame 0,35 s; si no N/14 s limitado a 0,28–1,2 s).
 *  - AOSpellPersistentVisualV130: EOT en loop a persistentFps; tick/on-hit una vez a transientFps;
 *    ambos con persistentScale y persistentYOffset. Escalas mínimas 0,1; fps y velocidad mínimos 1.
 * No toca archivos del proyecto: el tuning se descarga con "Exportar tuning".
 */
(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const SPELLS = Array.isArray(window.AO_SPELLS) ? window.AO_SPELLS : [];
  const EMBEDDED = window.AO_VISUAL_TUNING || { version: '', entries: [] };
  const EMBEDDED_META = window.AO_VISUAL_TUNING_META || {};
  const SKILLSHOT = (window.AO_SKILLSHOT_VISUAL && window.AO_SKILLSHOT_VISUAL.entries) || {};
  const TILE = 32;
  const STORE_KEY = 'aoSpellTesterV13';
  const ZOOMS = [1, 2, 4, 8];

  // Mismos valores por defecto que AOSpellVisualOverridesV130.VisualTuning (hechizo sin entrada).
  const DEFAULTS = { projectileScale: 1, impactScale: 1, persistentScale: 1, projectileYOffset: 0.35, impactYOffset: 0.25, persistentYOffset: 0.25, projectileSpeed: 18, impactDuration: 0.35, persistentFps: 8, transientFps: 14 };
  const FIELD_INFO = {
    projectileScale: { label: 'Escala', step: 0.05, min: 0.1, max: 20 },
    projectileYOffset: { label: 'Offset Y', step: 0.01, min: -8, max: 8, offset: true },
    projectileSpeed: { label: 'Velocidad (casillas/s)', step: 0.5, min: 1, max: 200 },
    impactScale: { label: 'Escala', step: 0.05, min: 0.1, max: 20 },
    impactYOffset: { label: 'Offset Y', step: 0.01, min: -8, max: 8, offset: true },
    impactDuration: { label: 'Duración total (s)', step: 0.01, min: 0, max: 20 },
    persistentScale: { label: 'Escala', step: 0.05, min: 0.1, max: 20 },
    persistentYOffset: { label: 'Offset Y', step: 0.01, min: -8, max: 8, offset: true },
    persistentFps: { label: 'FPS', step: 1, min: 1, max: 120 },
    transientFps: { label: 'FPS', step: 1, min: 1, max: 120 }
  };
  const GROUPS = [
    { id: 'projectile', title: 'Proyectil', keys: ['projectileScale', 'projectileYOffset', 'projectileSpeed'], layers: ['projectile'] },
    { id: 'impact', title: 'Impacto', keys: ['impactScale', 'impactYOffset', 'impactDuration'], layers: ['impact'] },
    { id: 'persistent', title: 'Persistente / EOT', keys: ['persistentScale', 'persistentYOffset', 'persistentFps'], layers: ['persistent', 'tick', 'onhit'] },
    { id: 'transient', title: 'Transitorio (tick, on-hit, skillshot)', keys: ['transientFps'], layers: ['tick', 'onhit', 'projectile'] }
  ];
  const LAYERS = [
    { id: 'projectile', label: 'Proyectil' },
    { id: 'impact', label: 'Impacto' },
    { id: 'persistent', label: 'Persistente (EOT)' },
    { id: 'tick', label: 'Tick EOT' },
    { id: 'onhit', label: 'On-hit EOT' }
  ];
  const layerLabel = id => (LAYERS.find(l => l.id === id) || { label: id }).label;

  // ---------- utilidades ----------
  const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
  const round5 = v => Math.round(v * 1e5) / 1e5;
  const num = v => String(round5(v)).replace('.', ',');
  const ms = v => (v >= 100 ? Math.round(v) : Math.round(v * 10) / 10).toString().replace('.', ',') + ' ms';
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  function storeGet() { try { return JSON.parse(localStorage.getItem(STORE_KEY) || 'null'); } catch (e) { return null; } }
  function storeSet(v) { try { localStorage.setItem(STORE_KEY, JSON.stringify(v)); } catch (e) { /* sin almacenamiento: seguir */ } }

  // ---------- estado ----------
  const view = { zoom: 2, grid: true, pivot: true, actors: true, compare: false, distance: 5, speed: 1, camX: 0, camY: 1.5 };
  const tl = { mode: 'idle', layer: 'impact', t: 0, playing: false, done: false };
  let current = null;
  let statusMsg = 'Listo.';
  let dirty = true;
  const markDirty = () => { dirty = true; };

  // ---------- tuning: base + cambios ----------
  const tuningState = { base: null, byId: new Map(), keys: [], floatFields: new Set(), fmt: { indent: 2, eol: '\n', trailing: false }, label: '', edits: {} };

  function setBase(obj, meta, label) {
    if (!obj || !Array.isArray(obj.entries)) throw new Error('El JSON no tiene "entries".');
    const byId = new Map();
    const keys = [];
    for (const e of obj.entries) {
      if (!e || typeof e.spellId !== 'number') throw new Error('Entrada sin spellId numérico.');
      byId.set(e.spellId, e);
      for (const k of Object.keys(e)) if (k !== 'spellId' && !keys.includes(k)) keys.push(k);
    }
    tuningState.base = obj;
    tuningState.byId = byId;
    tuningState.keys = keys;
    const fl = new Set(meta.floatFields || []);
    // En C# todos los campos (salvo spellId) son float: si el generador no los detectó, igual se escriben como float.
    for (const k of keys) if (k in DEFAULTS) fl.add(k);
    fl.delete('spellId');
    tuningState.floatFields = fl;
    tuningState.fmt = { indent: meta.indent || 2, eol: meta.eol || '\n', trailing: !!meta.trailingNewline };
    tuningState.label = label;
    pruneEdits();
  }

  function detectMeta(text) {
    const floatFields = [];
    for (const m of text.matchAll(/"([A-Za-z0-9_]+)"\s*:\s*-?\d+\.\d/g)) if (!floatFields.includes(m[1])) floatFields.push(m[1]);
    const ind = /\n( +)"/.exec(text);
    return { floatFields, indent: ind ? ind[1].length : 2, eol: text.includes('\r\n') ? '\r\n' : '\n', trailingNewline: /\n$/.test(text) };
  }

  const baseEntry = id => tuningState.byId.get(id) || null;
  const baseVal = (id, k) => { const e = baseEntry(id); return e && typeof e[k] === 'number' ? e[k] : DEFAULTS[k]; };
  function tune(id) {
    const t = Object.assign({}, DEFAULTS, baseEntry(id) || {}, tuningState.edits[id] || {});
    return t;
  }
  function pruneEdits() {
    for (const id of Object.keys(tuningState.edits)) {
      const ed = tuningState.edits[id];
      for (const k of Object.keys(ed)) if (typeof ed[k] !== 'number' || !isFinite(ed[k]) || ed[k] === baseVal(Number(id), k)) delete ed[k];
      if (!Object.keys(ed).length) delete tuningState.edits[id];
    }
  }
  function saveEdits() { storeSet({ edits: tuningState.edits, baseSha1: EMBEDDED_META.sha1 || '' }); }

  function setEdit(id, key, value) {
    const keep = frameRef();
    const ed = tuningState.edits[id] || (tuningState.edits[id] = {});
    if (value === baseVal(id, key)) delete ed[key]; else ed[key] = value;
    if (!Object.keys(ed).length) delete tuningState.edits[id];
    saveEdits();
    restoreFrameRef(keep);
    refreshTuningInputs();
    renderChanges();
    markDirty();
  }

  function editedCount() {
    let spells = 0, fields = 0;
    for (const id of Object.keys(tuningState.edits)) { spells++; fields += Object.keys(tuningState.edits[id]).length; }
    return { spells, fields };
  }

  // Serializa igual que el archivo original (json indent 2, floats con ".0").
  function serialize(value) {
    const { indent, eol } = tuningState.fmt;
    const pad = n => ' '.repeat(indent * n);
    const fl = tuningState.floatFields;
    function numStr(v, key) {
      if (!isFinite(v)) return '0';
      if (fl.has(key) && Number.isInteger(v)) return v.toFixed(1);
      return String(v);
    }
    function ser(v, depth, key) {
      if (Array.isArray(v)) {
        if (!v.length) return '[]';
        return '[' + eol + v.map(x => pad(depth + 1) + ser(x, depth + 1, key)).join(',' + eol) + eol + pad(depth) + ']';
      }
      if (v && typeof v === 'object') {
        const ks = Object.keys(v);
        if (!ks.length) return '{}';
        return '{' + eol + ks.map(k => pad(depth + 1) + JSON.stringify(k) + ': ' + ser(v[k], depth + 1, k)).join(',' + eol) + eol + pad(depth) + '}';
      }
      if (typeof v === 'number') return numStr(v, key);
      return JSON.stringify(v);
    }
    return ser(value, 0, '') + (tuningState.fmt.trailing ? eol : '');
  }

  function buildExport() {
    const base = tuningState.base;
    const edits = tuningState.edits;
    const entries = base.entries.map(e => {
      const ed = edits[e.spellId];
      if (!ed) return e;
      const n = {};
      for (const k of Object.keys(e)) n[k] = k in ed ? ed[k] : e[k];
      for (const k of Object.keys(ed)) if (!(k in n)) n[k] = ed[k];
      return n;
    });
    // Hechizos sin entrada en el archivo: se agregan con los valores por defecto del juego + cambios, en orden de spellId.
    const order = tuningState.keys.length ? tuningState.keys : Object.keys(DEFAULTS);
    for (const idStr of Object.keys(edits)) {
      const id = Number(idStr);
      if (tuningState.byId.has(id)) continue;
      const n = { spellId: id };
      for (const k of order) n[k] = k in edits[idStr] ? edits[idStr][k] : DEFAULTS[k];
      let at = entries.findIndex(x => x.spellId > id);
      if (at < 0) at = entries.length;
      entries.splice(at, 0, n);
    }
    const out = {};
    for (const k of Object.keys(base)) out[k] = k === 'entries' ? entries : base[k];
    return serialize(out);
  }

  function exportTuning() {
    const text = buildExport();
    const blob = new Blob([text], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'spell_visual_tuning.json';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 4000);
    const c = editedCount();
    setStatus(c.spells ? `Tuning exportado: ${c.spells} hechizo(s), ${c.fields} campo(s) cambiados.` : 'Tuning exportado sin cambios (idéntico a la base).');
  }

  // ---------- hechizos ----------
  function framesOf(s, id) {
    if (!s) return [];
    if (id === 'impact') return s.impact && s.impact.length ? s.impact : (s.icon ? [s.icon] : []);
    return Array.isArray(s[id]) ? s[id] : [];
  }

  // Parámetros de una capa según el tuning actual (misma lógica que el juego).
  function params(id, s, tu) {
    const N = Math.max(1, framesOf(s, id).length);
    if (id === 'projectile') {
      const sp = Math.max(1, tu.projectileSpeed);
      const dur = clamp(view.distance / sp, 0.12, 1.2) * 1000;
      return { N, dur, per: dur / N, loop: false, scale: Math.max(0.1, tu.projectileScale), yKey: 'projectileYOffset', y: tu.projectileYOffset,
        why: `viaje ${ms(dur)} (${view.distance} casillas ÷ projectileSpeed ${num(tu.projectileSpeed)}, límite 0,12–1,2 s) ÷ ${N}` };
    }
    if (id === 'impact') {
      const auto = !(tu.impactDuration > 0);
      const d = auto ? (N <= 1 ? 0.35 : clamp(N / 14, 0.28, 1.2)) : tu.impactDuration;
      const dur = d * 1000;
      return { N, dur, per: dur / N, loop: false, scale: Math.max(0.1, tu.impactScale), yKey: 'impactYOffset', y: tu.impactYOffset,
        why: auto ? `impactDuration 0 → automática ${num(d)} s ÷ ${N}` : `impactDuration ${num(d)} s ÷ ${N}` };
    }
    if (id === 'persistent') {
      const fps = Math.max(1, tu.persistentFps);
      return { N, dur: Infinity, per: 1000 / fps, loop: true, scale: Math.max(0.1, tu.persistentScale), yKey: 'persistentYOffset', y: tu.persistentYOffset,
        why: `1 ÷ persistentFps ${num(tu.persistentFps)} (loop)` };
    }
    const fps = Math.max(1, tu.transientFps);
    return { N, dur: (1000 / fps) * N, per: 1000 / fps, loop: false, scale: Math.max(0.1, tu.persistentScale), yKey: 'persistentYOffset', y: tu.persistentYOffset,
      why: `1 ÷ transientFps ${num(tu.transientFps)}` };
  }

  // ---------- línea de tiempo ----------
  function phases() {
    const s = current;
    if (!s || tl.mode === 'idle') return [];
    const tu = tune(s.id);
    if (tl.mode === 'cast') {
      const out = [];
      let t = 0;
      for (const id of ['projectile', 'impact']) {
        if (!framesOf(s, id).length) continue;
        const p = params(id, s, tu);
        out.push(Object.assign({ id, start: t }, p));
        t += p.dur;
      }
      if ($('persistentOn').checked && s.persistent.length) out.push(Object.assign({ id: 'persistent', start: t }, params('persistent', s, tu)));
      return out;
    }
    if (!framesOf(s, tl.layer).length) return [];
    return [Object.assign({ id: tl.layer, start: 0 }, params(tl.layer, s, tu))];
  }

  function stateAt(t) {
    const ph = phases();
    for (let k = 0; k < ph.length; k++) {
      const p = ph[k];
      if (t >= p.start && t < p.start + p.dur) {
        const rel = t - p.start;
        const abs = Math.floor(rel / p.per);
        const i = p.dur === Infinity ? abs % p.N : Math.min(p.N - 1, abs);
        return { ph, p, k, abs, i, rel };
      }
    }
    return { ph, p: null };
  }

  function frameRef() {
    if (tl.playing) return null;
    const st = stateAt(tl.t);
    return st.p ? { id: st.p.id, abs: st.abs } : null;
  }
  function restoreFrameRef(ref) {
    if (!ref) return;
    const p = phases().find(x => x.id === ref.id);
    if (p) tl.t = p.start + (Math.min(ref.abs, p.dur === Infinity ? ref.abs : p.N - 1) + 0.5) * p.per;
  }

  function setPlaying(on) {
    tl.playing = on;
    const audio = $('audio');
    if (!on && !audio.paused) { audio.pause(); audio.dataset.resume = '1'; }
    if (on && audio.dataset.resume === '1' && tl.mode === 'cast') { audio.play().catch(() => {}); }
    if (on) delete audio.dataset.resume;
    $('btnPause').textContent = on ? '⏸ Pausa' : '▶ Reproducir';
    markDirty();
  }

  function stopAudio() { const a = $('audio'); a.pause(); try { a.currentTime = 0; } catch (e) { /* sin fuente */ } delete a.dataset.resume; }

  function cast() {
    if (!current) return;
    stopAudio();
    tl.mode = 'cast'; tl.t = 0; tl.done = false;
    const s = current;
    if ($('audioOn').checked && s.audio && s.audioAvailable) {
      const a = $('audio'); a.src = s.audio; a.volume = 0.85; a.play().catch(() => {});
      setStatus('Lanzando ' + s.name + '…');
    } else if (s.wav > 0 && !s.audioAvailable) setStatus('Lanzando · audio original ' + s.wav + ' no está en los recursos.');
    else setStatus('Lanzando ' + s.name + '…');
    if (!phases().length) setStatus('Este hechizo no tiene frames de viaje, impacto ni EOT.');
    setPlaying(true);
  }

  function stopAll() {
    stopAudio();
    tl.mode = 'idle'; tl.t = 0; tl.done = false;
    setPlaying(false);
    setStatus('Detenido.');
  }

  function inspectLayer(id, playing) {
    if (!current || !framesOf(current, id).length) return;
    tl.mode = 'layer'; tl.layer = id; tl.t = 0; tl.done = false;
    stopAudio();
    setPlaying(!!playing);
    const p = phases()[0];
    if (p) tl.t = p.per * 0.5;
    markDirty();
  }

  function togglePause() {
    if (tl.mode === 'idle') { inspectLayer($('layerSelect').value, true); return; }
    if (tl.mode === 'cast' && tl.done) { cast(); return; }
    setPlaying(!tl.playing);
  }

  function step(dir) {
    if (tl.mode === 'idle') { inspectLayer($('layerSelect').value, false); return; }
    setPlaying(false);
    const st = stateAt(tl.t);
    const ph = st.ph;
    if (!ph.length) return;
    let p, abs;
    if (!st.p) {
      if (dir < 0) { p = ph[ph.length - 1]; abs = p.N - 1; } else { p = ph[0]; abs = 0; }
    } else {
      p = st.p; abs = st.abs + dir;
      const k = st.k;
      if (abs < 0) {
        if (k > 0) { p = ph[k - 1]; abs = p.N - 1; } else abs = tl.mode === 'cast' ? 0 : p.N - 1;
      } else if (p.dur !== Infinity && abs >= p.N) {
        if (k < ph.length - 1) { p = ph[k + 1]; abs = 0; } else abs = tl.mode === 'cast' ? p.N - 1 : 0;
      }
    }
    tl.t = p.start + (abs + 0.5) * p.per;
    tl.done = false;
    markDirty();
  }

  function gotoFrame(i) {
    const st = stateAt(tl.t);
    const id = st.p ? st.p.id : $('layerSelect').value;
    let p = st.ph.find(x => x.id === id);
    if (!p) { inspectLayer(id, false); p = phases()[0]; }
    if (!p) return;
    setPlaying(false);
    tl.t = p.start + (i + 0.5) * p.per;
    markDirty();
  }

  function nudgeOffset(dir) {
    if (!current) return;
    const st = stateAt(tl.t);
    const id = st.p ? st.p.id : $('layerSelect').value;
    const key = params(id, current, tune(current.id)).yKey;
    if (!tuningState.keys.includes(key)) return;
    const v = round5(tune(current.id)[key] + dir / TILE);
    setEdit(current.id, key, clamp(v, FIELD_INFO[key].min, FIELD_INFO[key].max));
    setStatus(`${key} = ${num(tune(current.id)[key])} (${num(tune(current.id)[key] * TILE)} px)`);
  }

  // ---------- HD ----------
  const hd = { urls: [], byPath: new Map(), byName: new Map(), byGrh: new Map(), count: 0 };
  const normName = n => n.toLowerCase().replace(/\.(png|webp)$/i, '').replace(/(?:[_\-@. ]?(?:hd|4x|x4))+$/i, '');
  function grhOf(name) { let last = null; for (const m of name.matchAll(/grh(\d+)/gi)) last = m[1]; return last; }

  function clearHd() {
    for (const u of hd.urls) URL.revokeObjectURL(u);
    hd.urls = []; hd.byPath.clear(); hd.byName.clear(); hd.byGrh.clear(); hd.count = 0;
  }
  function loadHd(fileList) {
    clearHd();
    for (const f of Array.from(fileList || [])) {
      if (!/\.(png|webp)$/i.test(f.name)) continue;
      const url = URL.createObjectURL(f);
      hd.urls.push(url);
      const parts = (f.webkitRelativePath || f.name).split('/');
      const name = parts.pop();
      const folder = (parts.pop() || '').toLowerCase();
      const key = normName(name);
      if (folder && !hd.byPath.has(folder + '/' + key)) hd.byPath.set(folder + '/' + key, url);
      if (!hd.byName.has(key)) hd.byName.set(key, url);
      const g = grhOf(name);
      if (g && !hd.byGrh.has(g)) hd.byGrh.set(g, url);
      hd.count++;
    }
    setStatus(hd.count ? `HD cargado: ${hd.count} imagen(es).` : 'No se encontraron PNG/WebP en la selección.');
    if (hd.count && !view.compare) setCompare(true);
    refreshHdInfo();
    buildStrip(true);
    markDirty();
  }
  function hdFor(src) {
    if (!hd.count || !src) return null;
    const parts = src.split('/');
    const name = parts.pop();
    const folder = (parts.pop() || '').toLowerCase();
    const key = normName(name);
    const g = grhOf(name);
    return hd.byPath.get(folder + '/' + key) || hd.byName.get(key) || (g && hd.byGrh.get(g)) || null;
  }
  function refreshHdInfo() {
    const box = $('hdInfo');
    if (!hd.count) { box.innerHTML = 'Sin HD cargado: la vista HD muestra el original escalado (vecino más cercano) con el cartel <b>sin HD</b>.'; return; }
    let html = `HD cargado: <b>${hd.count}</b> imagen(es).`;
    if (current) {
      const parts = LAYERS.map(l => { const f = framesOf(current, l.id); if (!f.length || (l.id === 'impact' && !current.impact.length)) return ''; const n = f.filter(x => hdFor(x)).length; return `${l.label} <b class="${n === f.length ? 'good' : (n ? 'warn' : 'bad')}">${n}/${f.length}</b>`; }).filter(Boolean);
      html += '<br>Este hechizo: ' + (parts.join(' · ') || 'sin frames');
    }
    box.innerHTML = html;
  }

  // ---------- imágenes ----------
  const imgCache = new Map();
  function getImg(src) {
    if (!src) return null;
    let im = imgCache.get(src);
    if (!im) {
      im = new Image();
      im.onload = markDirty;
      im.onerror = () => { im.dataset.err = '1'; markDirty(); };
      im.src = src;
      imgCache.set(src, im);
    }
    return im.complete && im.naturalWidth ? im : null;
  }

  // ---------- escenario ----------
  const canvases = [{ cv: $('cvOrig'), variant: 'orig' }, { cv: $('cvHd'), variant: 'hd' }];

  function defaultCam() {
    view.camX = view.zoom <= 2 ? -view.distance / 2 : 0;
    view.camY = view.zoom >= 8 ? 1 : 1.5;
  }

  function setZoom(z) {
    view.zoom = z;
    defaultCam();
    document.querySelectorAll('[data-zoom]').forEach(b => b.classList.toggle('on', Number(b.dataset.zoom) === z));
    markDirty();
  }

  function setCompare(on) {
    view.compare = on;
    $('stages').classList.toggle('compare', on);
    $('boxHd').hidden = !on;
    $('btnCompare').classList.toggle('on', on);
    markDirty();
  }

  function activeFx() {
    // Qué dibujar ahora: capa, frame, posición (casillas) y parámetros.
    const st = stateAt(tl.t);
    if (!st.p || !current) return null;
    const p = st.p;
    const frames = framesOf(current, p.id);
    let x = 0;
    if (p.id === 'projectile' && tl.mode === 'cast') x = -view.distance + view.distance * clamp(st.rel / p.dur, 0, 1);
    return { p, i: st.i, abs: st.abs, src: frames[st.i], x, y: p.y, frames };
  }

  function drawStage(cv, variant, fx) {
    const box = cv.parentElement;
    const W = box.clientWidth, H = box.clientHeight;
    if (!W || !H) return;
    const dpr = window.devicePixelRatio || 1;
    const bw = Math.round(W * dpr), bh = Math.round(H * dpr);
    if (cv.width !== bw || cv.height !== bh) { cv.width = bw; cv.height = bh; }
    const ctx = cv.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.imageSmoothingEnabled = false;
    ctx.fillStyle = '#0d1117';
    ctx.fillRect(0, 0, W, H);
    const T = TILE * view.zoom;
    const sx = wx => Math.round(W / 2 + (wx - view.camX) * T);
    const sy = wy => Math.round(H / 2 - (wy - view.camY) * T);

    // Casillas del objetivo y del lanzador.
    ctx.fillStyle = '#d5b45d14';
    ctx.fillRect(sx(-0.5), sy(1), T, T);
    ctx.fillStyle = '#78b7ff10';
    ctx.fillRect(sx(-view.distance - 0.5), sy(1), T, T);

    if (view.grid) {
      const x0 = view.camX - W / 2 / T, x1 = view.camX + W / 2 / T;
      const y0 = view.camY - H / 2 / T, y1 = view.camY + H / 2 / T;
      ctx.lineWidth = 1;
      ctx.strokeStyle = '#ffffff1c';
      ctx.beginPath();
      for (let k = Math.floor(x0 - 0.5); k <= x1; k++) { const X = sx(k + 0.5) + 0.5; ctx.moveTo(X, 0); ctx.lineTo(X, H); }
      for (let k = Math.floor(y0); k <= y1 + 1; k++) { const Y = sy(k) + 0.5; ctx.moveTo(0, Y); ctx.lineTo(W, Y); }
      ctx.stroke();
      ctx.strokeStyle = '#d5b45d66';
      ctx.strokeRect(sx(-0.5) + 0.5, sy(1) + 0.5, T, T);
    }

    if (view.actors) {
      drawActor(ctx, getImg('assets/ui/player.png'), sx(-view.distance), sy(0), false);
      drawActor(ctx, getImg('assets/ui/enemy.png'), sx(0), sy(0), true);
    }

    let noteHd = null;
    if (fx && fx.src) {
      const orig = getImg(fx.src);
      if (orig) {
        const w = orig.naturalWidth * fx.p.scale * view.zoom;
        let h = orig.naturalHeight * fx.p.scale * view.zoom;
        let img = orig;
        if (variant === 'hd') {
          const url = hdFor(fx.src);
          const hdImg = url ? getImg(url) : null;
          if (hdImg) {
            const ratio = hdImg.naturalWidth / orig.naturalWidth;
            h = (hdImg.naturalHeight / ratio) * fx.p.scale * view.zoom;
            img = hdImg;
            noteHd = { ok: true, ratio, ratioH: hdImg.naturalHeight / orig.naturalHeight, w: hdImg.naturalWidth, h: hdImg.naturalHeight, ow: orig.naturalWidth, oh: orig.naturalHeight };
          } else noteHd = { ok: false, pending: !!url };
        }
        const px = sx(fx.x), py = sy(fx.y);
        const dx = Math.round(px - w / 2), dy = Math.round(py - h);
        ctx.imageSmoothingEnabled = img !== orig && w < img.naturalWidth;
        ctx.drawImage(img, dx, dy, Math.round(w), Math.round(h));
        ctx.imageSmoothingEnabled = false;
        if (view.pivot) {
          ctx.setLineDash([4, 3]);
          ctx.strokeStyle = '#ff4fd866';
          ctx.strokeRect(dx + 0.5, dy + 0.5, Math.round(w) - 1, Math.round(h) - 1);
          ctx.setLineDash([]);
        }
      }
    }

    if (view.pivot) {
      drawCross(ctx, sx(0), sy(0), 7, '#5fe3ff');
      if (tl.mode === 'cast' || (fx && fx.p.id === 'projectile')) drawCross(ctx, sx(-view.distance), sy(0), 7, '#5fe3ff');
      if (fx) {
        const ax = sx(fx.x), ay = sy(0), py = sy(fx.y);
        ctx.setLineDash([3, 3]);
        ctx.strokeStyle = '#ff4fd8aa';
        ctx.beginPath(); ctx.moveTo(ax + 0.5, ay); ctx.lineTo(ax + 0.5, py); ctx.stroke();
        ctx.setLineDash([]);
        drawCross(ctx, ax, py, 11, '#ff4fd8');
        const label = `${fx.p.yKey} ${num(fx.y)} = ${num(fx.y * TILE)} px`;
        ctx.font = '12px Segoe UI, Arial, sans-serif';
        ctx.fillStyle = '#08090cd0';
        ctx.fillRect(ax + 12, py - 20, ctx.measureText(label).width + 8, 17);
        ctx.fillStyle = '#ffb3f0';
        ctx.fillText(label, ax + 16, py - 7);
      }
    }
    return noteHd;
  }

  function drawActor(ctx, im, x, y, flip) {
    if (!im) return;
    // Silueta de referencia: la imagen de UI se dibuja a 1/4 (~1,5 casillas de alto), anclada abajo-centro.
    const w = (im.naturalWidth / 4) * view.zoom, h = (im.naturalHeight / 4) * view.zoom;
    ctx.save();
    ctx.globalAlpha = 0.85;
    ctx.imageSmoothingEnabled = true;
    ctx.translate(x, y);
    if (flip) ctx.scale(-1, 1);
    ctx.drawImage(im, Math.round(-w / 2), Math.round(-h), Math.round(w), Math.round(h));
    ctx.restore();
  }

  function drawCross(ctx, x, y, r, color) {
    ctx.strokeStyle = '#000a';
    ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(x - r, y + 0.5); ctx.lineTo(x + r + 1, y + 0.5); ctx.moveTo(x + 0.5, y - r); ctx.lineTo(x + 0.5, y + r + 1); ctx.stroke();
    ctx.strokeStyle = color;
    ctx.lineWidth = 1;
    ctx.beginPath(); ctx.moveTo(x - r, y + 0.5); ctx.lineTo(x + r + 1, y + 0.5); ctx.moveTo(x + 0.5, y - r); ctx.lineTo(x + 0.5, y + r + 1); ctx.stroke();
  }

  let stripKey = '';
  function buildStrip(force) {
    const strip = $('strip');
    const st = stateAt(tl.t);
    const id = st.p ? st.p.id : (tl.mode === 'layer' ? tl.layer : $('layerSelect').value);
    const frames = framesOf(current, id);
    const key = (current ? current.id : 0) + ':' + id + ':' + frames.length;
    if (!force && key === stripKey) return;
    stripKey = key;
    if (!frames.length) { strip.innerHTML = '<span class="empty">Esta capa no tiene frames.</span>'; return; }
    strip.innerHTML = frames.map((f, i) => `<div class="fr${hdFor(f) ? ' hd' : ''}" data-i="${i}" title="${esc(f.split('/').pop())}"><img src="${esc(f)}" alt=""><i>${i + 1}</i></div>`).join('');
  }

  function render() {
    const fx = activeFx();
    drawStage($('cvOrig'), 'orig', fx);
    const hdState = view.compare ? drawStage($('cvHd'), 'hd', fx) : null;

    // Barra de frames.
    const st = stateAt(tl.t);
    const layerSel = $('layerSelect');
    if (st.p && layerSel.value !== st.p.id) layerSel.value = st.p.id;
    buildStrip(false);
    const cur = st.p ? st.i : -1;
    document.querySelectorAll('#strip .fr').forEach((el, i) => el.classList.toggle('cur', i === cur));
    const info = $('frameInfo');
    if (st.p) {
      const speedTxt = view.speed !== 1 ? ` · reproducción al ${Math.round(view.speed * 100)}%` : '';
      info.innerHTML = `<b>Frame ${st.i + 1}/${st.p.N}</b> · ${layerLabel(st.p.id)}${tl.playing ? '' : ' · PAUSA'}` +
        `<span class="ms">${ms(st.p.per)} por frame · ${esc(st.p.why)}${speedTxt}</span>`;
    } else if (tl.mode === 'cast' && st.ph.length) {
      info.innerHTML = '<b>Animación completada</b><span class="ms">← para revisar frames · P o Espacio para repetir</span>';
    } else {
      info.innerHTML = '<b>Sin reproducción</b><span class="ms">LANZAR, P para reproducir la capa elegida o → para verla frame a frame</span>';
    }

    // Cartel HD.
    const badge = $('noHd');
    if (view.compare) {
      let txt = '', cls = '';
      if (!hd.count) txt = 'sin HD';
      else if (fx && hdState && hdState.ok) {
        const good = Math.abs(hdState.ratio - 4) < 0.01 && Math.abs(hdState.ratioH - hdState.ratio) < 0.01;
        txt = `HD ${hdState.w}×${hdState.h} = ${num(Math.round(hdState.ratio * 100) / 100)}×` + (good ? '' : (Math.abs(hdState.ratioH - hdState.ratio) >= 0.01 ? ' · proporción distinta (el pivote se corre)' : ' · se espera 4×'));
        cls = good ? 'ok' : 'part';
      } else if (fx) { txt = hdState && hdState.pending ? 'cargando HD…' : 'sin HD (este frame)'; cls = 'part'; } else txt = 'HD cargado';
      badge.textContent = txt;
      badge.className = 'noHd' + (cls ? ' ' + cls : '');
    }

    $('hud').textContent = `Zoom ${view.zoom}× · 1 casilla = ${TILE * view.zoom} px` + (st.p ? ` · ${layerLabel(st.p.id)} ${st.i + 1}/${st.p.N}` : '') + ' — ' + statusMsg;
    $('tagOrig').textContent = view.compare ? 'ORIGINAL' : 'ORIGINAL · ' + view.zoom + '×';
  }

  function setStatus(msg) { statusMsg = msg; markDirty(); }

  let lastNow = performance.now();
  function loop(now) {
    const dt = Math.min(100, Math.max(0, now - lastNow));
    lastNow = now;
    if (tl.playing) {
      tl.t += dt * view.speed;
      const ph = phases();
      if (tl.mode === 'cast') {
        const last = ph[ph.length - 1];
        const end = last ? last.start + last.dur : 0;
        if (tl.t >= end) { tl.playing = false; tl.done = true; $('btnPause').textContent = '▶ Reproducir'; setStatus(ph.length ? 'Animación completada.' : statusMsg); }
      } else if (ph.length && ph[0].dur !== Infinity) tl.t %= ph[0].dur;
      dirty = true;
    }
    if (dirty) { dirty = false; render(); }
    requestAnimationFrame(loop);
  }

  // ---------- panel: hechizo ----------
  function filtered() {
    const q = $('search').value.trim().toLowerCase();
    if (!q) return SPELLS;
    return SPELLS.filter(s => String(s.id).includes(q) || s.name.toLowerCase().includes(q));
  }
  function populate() {
    const sel = $('spellSelect');
    const old = current && current.id;
    sel.innerHTML = '';
    for (const s of filtered()) {
      const o = document.createElement('option');
      o.value = s.id;
      o.textContent = String(s.id).padStart(3, '0') + ' · ' + s.name + (tuningState.edits[s.id] ? ' •' : '');
      sel.appendChild(o);
    }
    const list = filtered();
    const s = list.find(x => x.id === old) || list[0];
    if (s) { sel.value = s.id; if (!current || s.id !== current.id) showSpell(s); }
  }
  function markListEdits() {
    for (const o of $('spellSelect').options) {
      const s = SPELLS.find(x => x.id === Number(o.value));
      if (s) o.textContent = String(s.id).padStart(3, '0') + ' · ' + s.name + (tuningState.edits[s.id] ? ' •' : '');
    }
  }

  function showSpell(s) {
    current = s;
    stopAudio();
    const icon = $('spellIcon');
    if (s.icon) { icon.src = s.icon; icon.style.visibility = 'visible'; } else { icon.removeAttribute('src'); icon.style.visibility = 'hidden'; }
    $('spellName').textContent = s.name;
    $('spellId').textContent = 'Hechizo #' + s.id + (s.magicWords ? ' · ' + s.magicWords : '');
    $('spellDesc').textContent = s.description || 'Sin descripción.';
    const aud = s.wav > 0 ? (s.audioAvailable ? `WAV ${s.wav} · original disponible` : `WAV ${s.wav} · archivo faltante`) : 'Sin WAV asignado';
    const sk = SKILLSHOT[String(s.id)];
    $('meta').innerHTML =
      `<div class="row"><span>Audio</span><b class="${s.audioAvailable ? 'good' : 'bad'}">${aud}</b></div>` +
      `<div class="row"><span>Mana / stamina</span><b>${esc(s.mana)} / ${esc(s.stamina)}</b></div>` +
      `<div class="row"><span>Frames viaje / impacto / EOT</span><b>${s.projectile.length} / ${s.impact.length}${!s.impact.length && s.icon ? ' (ícono)' : ''} / ${s.persistent.length}</b></div>` +
      (s.tick.length || s.onhit.length ? `<div class="row"><span>Tick / on-hit</span><b>${s.tick.length} / ${s.onhit.length}</b></div>` : '') +
      `<div class="row"><span>Entrada de tuning</span><b class="${baseEntry(s.id) ? 'good' : 'warn'}">${baseEntry(s.id) ? 'sí' : 'no (valores por defecto)'}</b></div>` +
      (sk !== undefined ? `<div class="row"><span>Skillshot (V267)</span><b>visualScale ${num(sk)} × projectileScale</b></div>` : '');
    // Capas disponibles.
    const ls = $('layerSelect');
    ls.innerHTML = LAYERS.map(l => { const n = framesOf(s, l.id).length; return `<option value="${l.id}"${n ? '' : ' disabled'}>${l.label} (${n})</option>`; }).join('');
    const first = ['impact', 'projectile', 'persistent', 'tick', 'onhit'].find(id => framesOf(s, id).length);
    for (const l of LAYERS) for (const f of framesOf(s, l.id)) getImg(f);
    buildTuningFields();
    refreshHdInfo();
    stripKey = '';
    if (first) { ls.value = first; inspectLayer(first, false); } else { tl.mode = 'idle'; setPlaying(false); }
    setStatus(first ? 'Listo · LANZAR o → para ver frame a frame.' : 'Este hechizo no tiene frames.');
  }

  // ---------- panel: tuning ----------
  function buildTuningFields() {
    const box = $('tuningFields');
    if (!current) { box.innerHTML = ''; return; }
    const s = current;
    const known = new Set(GROUPS.flatMap(g => g.keys));
    const groups = GROUPS.map(g => Object.assign({}, g, { keys: g.keys.filter(k => tuningState.keys.includes(k)) }));
    const extra = tuningState.keys.filter(k => !known.has(k) && typeof baseVal(s.id, k) === 'number');
    if (extra.length) groups.push({ id: 'extra', title: 'Otros campos del archivo', keys: extra, layers: [] });
    box.innerHTML = groups.filter(g => g.keys.length).map(g => {
      const n = g.layers.reduce((a, id) => a + (id === 'impact' ? s.impact.length : framesOf(s, id).length), 0);
      const dim = g.id !== 'extra' && !n;
      return `<div class="tgroup${dim ? ' dim' : ''}"><h4>${g.title}<small>${dim ? 'sin frames en este hechizo' : ''}</small></h4>` +
        g.keys.map(k => {
          const fi = FIELD_INFO[k] || { label: k, step: 0.01, min: -1e6, max: 1e6 };
          return `<div class="trow"><label for="tf_${k}">${fi.label} <code>${k}</code><span id="tp_${k}"></span></label>` +
            `<input type="number" id="tf_${k}" data-key="${k}" step="${fi.step}" min="${fi.min}" max="${fi.max}">` +
            `<span class="orig" id="to_${k}"></span></div>`;
        }).join('') + '</div>';
    }).join('');
    box.querySelectorAll('input[data-key]').forEach(inp => {
      inp.addEventListener('input', () => applyInput(inp, false));
      inp.addEventListener('change', () => applyInput(inp, true));
    });
    refreshTuningInputs();
  }

  function applyInput(inp, commit) {
    if (!current) return;
    const k = inp.dataset.key;
    const fi = FIELD_INFO[k] || { min: -1e6, max: 1e6 };
    const v = Number(String(inp.value).replace(',', '.'));
    if (inp.value === '' || !isFinite(v)) { if (commit) refreshTuningInputs(); return; }
    if (!commit && (v < fi.min || v > fi.max)) return;
    setEdit(current.id, k, round5(clamp(v, fi.min, fi.max)));
    if (commit) inp.value = tune(current.id)[k];
  }

  function refreshTuningInputs() {
    if (!current) return;
    const tu = tune(current.id);
    for (const k of tuningState.keys) {
      const inp = $('tf_' + k);
      if (!inp) continue;
      const b = baseVal(current.id, k);
      const mod = tuningState.edits[current.id] && k in tuningState.edits[current.id];
      if (document.activeElement !== inp) inp.value = tu[k];
      inp.classList.toggle('mod', !!mod);
      const o = $('to_' + k);
      o.textContent = 'archivo: ' + num(b);
      o.classList.toggle('mod', !!mod);
      if ((FIELD_INFO[k] || {}).offset) $('tp_' + k).textContent = ` · ${num(tu[k] * TILE)} px`;
    }
    const c = editedCount();
    $('exportBtn').textContent = c.spells ? `Exportar tuning (${c.spells} hechizo${c.spells > 1 ? 's' : ''} con cambios)` : 'Exportar tuning';
  }

  function renderChanges() {
    const box = $('changes');
    const ids = Object.keys(tuningState.edits).map(Number).sort((a, b) => a - b);
    if (!ids.length) { box.innerHTML = '<div class="none">Sin cambios respecto de la base.</div>'; markListEdits(); return; }
    box.innerHTML = ids.map(id => {
      const s = SPELLS.find(x => x.id === id);
      const ed = tuningState.edits[id];
      return Object.keys(ed).map(k => `<div data-id="${id}">#${id} ${esc(s ? s.name : '')}: <code>${k}</code> ${num(baseVal(id, k))} → <b>${num(ed[k])}</b></div>`).join('');
    }).join('');
    markListEdits();
  }

  function baseLabel() {
    const b = tuningState.base;
    $('tuningBase').innerHTML = `Base: <b>${esc(tuningState.label)}</b> · versión ${esc(b.version || '—')} · ${b.entries.length} entradas. ` +
      'Solo campos visuales de <code>spell_visual_tuning.json</code>; speed/range/hitRadius de skillshot son gameplay y no se tocan acá.';
  }

  // ---------- eventos ----------
  function bind() {
    $('search').addEventListener('input', populate);
    $('spellSelect').addEventListener('change', () => { const s = SPELLS.find(x => x.id === Number($('spellSelect').value)); if (s) showSpell(s); });
    $('launch').addEventListener('click', cast);
    $('stop').addEventListener('click', stopAll);
    $('btnPause').addEventListener('click', togglePause);
    $('btnPrev').addEventListener('click', () => step(-1));
    $('btnNext').addEventListener('click', () => step(1));
    $('layerSelect').addEventListener('change', () => inspectLayer($('layerSelect').value, tl.playing));
    $('strip').addEventListener('click', e => { const fr = e.target.closest('.fr'); if (fr) gotoFrame(Number(fr.dataset.i)); });
    document.querySelectorAll('[data-zoom]').forEach(b => b.addEventListener('click', () => setZoom(Number(b.dataset.zoom))));
    $('optGrid').addEventListener('change', e => { view.grid = e.target.checked; markDirty(); });
    $('optPivot').addEventListener('change', e => { view.pivot = e.target.checked; markDirty(); });
    $('optActors').addEventListener('change', e => { view.actors = e.target.checked; markDirty(); });
    $('persistentOn').addEventListener('change', markDirty);
    $('optDistance').addEventListener('change', e => {
      const keep = frameRef();
      view.distance = clamp(Math.round(Number(e.target.value) || 5), 1, 12);
      e.target.value = view.distance;
      if (view.zoom <= 2) defaultCam();
      restoreFrameRef(keep);
      markDirty();
    });
    $('speed').addEventListener('input', e => { view.speed = Number(e.target.value) / 100; $('speedTxt').textContent = e.target.value + '%'; markDirty(); });
    $('btnCompare').addEventListener('click', () => setCompare(!view.compare));
    $('btnCenter').addEventListener('click', () => { defaultCam(); markDirty(); });
    $('exportBtn').addEventListener('click', exportTuning);
    $('resetSpell').addEventListener('click', () => {
      if (!current) return;
      const keep = frameRef();
      delete tuningState.edits[current.id];
      saveEdits(); restoreFrameRef(keep); refreshTuningInputs(); renderChanges(); markDirty();
      setStatus('Tuning del hechizo restaurado.');
    });
    let resetArmed = 0;
    $('resetAll').addEventListener('click', e => {
      if (Date.now() - resetArmed > 3000) { resetArmed = Date.now(); e.target.textContent = '¿Seguro? clic de nuevo'; setTimeout(() => { e.target.textContent = 'Restaurar todo'; }, 3000); return; }
      resetArmed = 0; e.target.textContent = 'Restaurar todo';
      tuningState.edits = {};
      saveEdits(); refreshTuningInputs(); renderChanges(); markDirty();
      setStatus('Se descartaron todos los cambios de tuning.');
    });
    $('changes').addEventListener('click', e => {
      const row = e.target.closest('[data-id]');
      if (!row) return;
      const s = SPELLS.find(x => x.id === Number(row.dataset.id));
      if (!s) return;
      $('search').value = '';
      populate();
      $('spellSelect').value = s.id;
      showSpell(s);
    });
    $('baseFile').addEventListener('change', e => {
      const f = e.target.files && e.target.files[0];
      if (!f) return;
      const r = new FileReader();
      r.onload = () => {
        try {
          const text = String(r.result).replace(/^﻿/, '');
          setBase(JSON.parse(text), detectMeta(text), f.name + ' (cargado)');
          saveEdits(); baseLabel(); if (current) showSpell(current); renderChanges();
          setStatus('Base de tuning cargada desde ' + f.name + '.');
        } catch (err) { setStatus('No se pudo cargar ' + f.name + ': ' + err.message); }
      };
      r.readAsText(f);
    });
    $('hdDir').addEventListener('change', e => loadHd(e.target.files));
    $('hdFiles').addEventListener('change', e => loadHd(e.target.files));
    $('hdClear').addEventListener('click', () => { clearHd(); $('hdDir').value = ''; $('hdFiles').value = ''; refreshHdInfo(); buildStrip(true); markDirty(); setStatus('HD quitado.'); });

    // Cámara: arrastrar y doble clic.
    for (const { cv } of canvases) {
      let drag = null;
      cv.addEventListener('pointerdown', e => { drag = { x: e.clientX, y: e.clientY, cx: view.camX, cy: view.camY }; cv.setPointerCapture(e.pointerId); cv.classList.add('drag'); });
      cv.addEventListener('pointermove', e => {
        if (!drag) return;
        const T = TILE * view.zoom;
        view.camX = drag.cx - (e.clientX - drag.x) / T;
        view.camY = drag.cy + (e.clientY - drag.y) / T;
        markDirty();
      });
      const end = () => { drag = null; cv.classList.remove('drag'); };
      cv.addEventListener('pointerup', end);
      cv.addEventListener('pointercancel', end);
      cv.addEventListener('dblclick', () => { defaultCam(); markDirty(); });
    }
    if (window.ResizeObserver) { const ro = new ResizeObserver(markDirty); ro.observe($('boxOrig')); ro.observe($('boxHd')); } else window.addEventListener('resize', markDirty);

    document.addEventListener('keydown', e => {
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      const t = e.target;
      const tag = t && t.tagName;
      const typing = tag === 'TEXTAREA' || (tag === 'INPUT' && !['checkbox', 'button', 'file'].includes(t.type));
      if (typing) return;
      const inSelect = tag === 'SELECT';
      switch (e.code) {
        case 'Space': e.preventDefault(); if (!e.repeat) cast(); break;
        case 'KeyP': e.preventDefault(); togglePause(); break;
        case 'ArrowLeft': e.preventDefault(); step(-1); break;
        case 'ArrowRight': e.preventDefault(); step(1); break;
        case 'ArrowUp': case 'ArrowDown': if (inSelect) return; e.preventDefault(); nudgeOffset(e.code === 'ArrowUp' ? 1 : -1); break;
        case 'Digit1': case 'Digit2': case 'Digit3': case 'Digit4': if (inSelect) return; e.preventDefault(); setZoom(ZOOMS[Number(e.code.slice(-1)) - 1]); break;
        case 'KeyG': if (inSelect) return; e.preventDefault(); $('optGrid').checked = !$('optGrid').checked; view.grid = $('optGrid').checked; markDirty(); break;
        default:
      }
    });
  }

  // ---------- arranque ----------
  function init() {
    setBase(EMBEDDED, EMBEDDED_META, 'spell_visual_tuning.json' + (EMBEDDED_META.generated ? ' embebido el ' + EMBEDDED_META.generated : ''));
    const stored = storeGet();
    let recovered = 0;
    if (stored && stored.edits && typeof stored.edits === 'object') {
      for (const id of Object.keys(stored.edits)) {
        const ed = stored.edits[id];
        if (!ed || typeof ed !== 'object' || !SPELLS.some(s => s.id === Number(id))) continue;
        tuningState.edits[id] = {};
        for (const k of Object.keys(ed)) if (typeof ed[k] === 'number' && isFinite(ed[k]) && tuningState.keys.includes(k)) tuningState.edits[id][k] = ed[k];
      }
      pruneEdits();
      recovered = editedCount().fields;
    }
    bind();
    baseLabel();
    setZoom(view.zoom);
    setCompare(false);
    populate();
    renderChanges();
    refreshHdInfo();
    if (recovered) setStatus(`Se recuperaron ${recovered} cambio(s) de tuning sin exportar (guardados en este navegador).`);
    if (!SPELLS.length) setStatus('No se cargó spell_data.js.');
    requestAnimationFrame(loop);
  }

  // Acceso para pruebas automáticas (solo lectura del estado + exportación como texto).
  window.AOSpellTesterV13 = {
    buildExport,
    state: () => ({ spell: current && current.id, mode: tl.mode, playing: tl.playing, t: tl.t, zoom: view.zoom, compare: view.compare, edits: JSON.parse(JSON.stringify(tuningState.edits)), frame: (() => { const st = stateAt(tl.t); return st.p ? { layer: st.p.id, i: st.i, n: st.p.N, perMs: st.p.per } : null; })() })
  };

  init();
})();
