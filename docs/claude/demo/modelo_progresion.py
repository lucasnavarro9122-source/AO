"""Modelo determinista de progresion del dungeon (Contenido, demo fase 1).

Reproduce `tiempoPorNivel` de dungeon-npcs.json con las formulas del servidor VB6 ao-org AO20.
Uso (desde la raiz del proyecto):  python docs/claude/demo/modelo_progresion.py [--check]
Solo lee: Archivos Originales/Recursos-master/.../Dat y docs/claude/demo/dungeon-npcs.json.
QA: el simulador Monte Carlo deberia dar la mediana dentro de +-20 % de estos valores
si usa los mismos supuestos (ver SUPUESTOS abajo y progresion.md secciones 2-3).
"""
import json, re, sys

DAT = "Archivos Originales/Recursos-master/Recursos-master/Dat/"
JSON = "docs/claude/demo/dungeon-npcs.json"

def ini(path, section_re):
    txt = open(path, encoding="cp1252").read(); out = {}
    for m in re.finditer(r"^\[" + section_re + r"(\d+)\][^\n]*\n(.*?)(?=^\[|\Z)", txt, re.M | re.S | re.I):
        kv = {}
        for line in m.group(2).splitlines():
            if "=" in line and not line.strip().startswith("'"):
                k, v = line.split("=", 1); kv[k.strip().lower()] = v.split("'")[0].strip()
        out[int(m.group(1))] = kv
    return out

def I(kv, k, d=0):
    try: return int(float(kv.get(k, d)))
    except (TypeError, ValueError): return d

NPC = ini(DAT + "npcs.dat", "NPC")
H = ini(DAT + "Hechizos.dat", "HECHIZO")
EXP = {int(a): int(b) for a, b in re.findall(r"^(\d+)=(\d+)", open(DAT + "Balance.dat", encoding="cp1252").read().split("[EXP]")[1], re.M)}

# ---------------- SUPUESTOS (progresion.md seccion 3) ----------------
CL = {  # Balance.dat: MODVIDA, MANA_INICIAL, MULT_MANA, GOLPE_PRE_36, MODATAQUEARMAS/PROYECTILES, MODDANO*, MODEVASION, MODESCUDO
 "Guerrero": dict(vida=10.5, mana0=0,    mmana=0,    hit=3, atkArm=1.1,  atkProy=0.85, dmgArm=1.05, dmgProy=0.87, eva=1.0),
 "Mago":     dict(vida=7.5,  mana0=8.33, mmana=2.65, hit=1, atkArm=0.5,  atkProy=0.5,  dmgArm=0.5,  dmgProy=0.5,  eva=0.2),
 "Clerigo":  dict(vida=8.5,  mana0=2.5,  mmana=2.0,  hit=2, atkArm=0.85, atkProy=0.7,  dmgArm=0.85, dmgProy=0.75, eva=0.8),
 "Cazador":  dict(vida=10.0, mana0=0,    mmana=0,    hit=3, atkArm=0.8,  atkProy=1.0,  dmgArm=0.85, dmgProy=1.0,  eva=0.9),
}
ATR = {"Guerrero": (19, 19, 18, 20), "Clerigo": (19, 19, 18, 20), "Cazador": (19, 19, 18, 20), "Mago": (16, 21, 22, 18)}  # FUE AGI INT CON
TRAVEL_S = 4.0            # caminar al grupo siguiente
EXPOSURE = {"arma": 1.0, "arco": 0.6, "hechizo": 0.35}  # fraccion de golpes NPC recibidos segun como ataca
POT_HP, POT_S = 27, 0.38  # pocion roja: 27 PV, intervalo U 380 ms
MAX_DPS_IN = 30           # farmeable solo si recibe <= 30 PV/s
BURST = {"Mago": 1.0}     # golpe max tras armadura <= 50 % vida (mago 100 %)
EXP_MULT = None           # None = usa multiplicadorExpPropuesto del JSON; 1 = original

def pts(L): return 10 + 5 * (L - 1)
def sk_main(L):  return min(100, int(2.5 * L + 0.4 * pts(L)))   # skill de ataque
def sk_magia(L): return min(100, int(2.5 * L + 0.6 * pts(L)))   # mago
def sk_tact(L):  return min(100, int(2.5 * L + 0.15 * pts(L)))
def sk_med(L):   return min(100, int(2.5 * L + 0.2 * pts(L)))

def weapon(c, L):  # (min, max, esProyectil)
    # Tope = lo que venden los comerciantes originales del hub (Hacha Dos Filos, Arco de Roble y
    # Baculo Engarzado solo los venden mercaderes que no estan en ningun mapa).
    if c == "Guerrero": return (2, 7, 0) if L < 5 else (5, 12, 0) if L < 10 else (8, 15, 0)
    if c == "Clerigo":  return (2, 7, 0) if L < 5 else (4, 9, 0) if L < 8 else (5, 12, 0) if L < 12 else (8, 15, 0)
    if c == "Cazador":
        bow = (1, 3) if L < 5 else (4, 7) if L < 10 else (7, 9)
        arrow = (1, 1) if L < 5 else (2, 3)
        return (bow[0] + arrow[0], bow[1] + arrow[1], 1)
    return (1, 2, 0)
def staff_mdb(L): return 3 if L < 10 else 5
def armor(c, L):
    body = 2 if L < 5 else 7 if L < 10 else 12 if L < 15 else 17 if L < 20 else 24 if L < 25 else 30
    if c == "Mago": body = int(body * 0.7)
    shield = 0 if c == "Mago" else (2 if L < 10 else 4 if L < 20 else 7)
    return body, shield
SPELLS = [(1, 0), (2, 12), (43, 20), (3, 33), (41, 60), (51, 85)]  # (hechizo, skill Magia minima); Descarga desde nivel 20

# ---------------- FORMULAS (servidor VB6) ----------------
def player(c, L):
    fue, agi, inte, con = ATR[c]; k = CL[c]; lv = 2.5 * max(L - 12, 0); s = sk_main(L); t = sk_tact(L)
    return dict(c=c, L=L, fue=fue, hp=(k["vida"] - (21 - con) * 0.5) * (L - 1) + con,
                mana=inte * k["mana0"] + k["mmana"] * inte * (L - 1),
                minhit=(L - 1) * k["hit"] + 1, maxhit=(L - 1) * k["hit"] + 2,
                pa_arm=(s + 3 * s / 100 * agi) * k["atkArm"] + lv, pa_proy=(s + 3 * s / 100 * agi) * k["atkProy"] + lv,
                eva=(t + 3 * t / 100 * agi) * k["eva"] + lv)

def spell_dmg(p, sid, npc):
    h = H[sid]; d = (I(h, "minhp") + I(h, "maxhp")) / 2; d += d * 3 * p["L"] / 100
    if p["c"] == "Mago": d += d * staff_mdb(p["L"]) / 100
    mr, md = I(npc, "magicresistance"), I(npc, "magicdef"); sk = sk_magia(p["L"]) if p["c"] == "Mago" else sk_main(p["L"])
    red = (md + max(0, mr - sk) * 2) if mr > 0 else md
    return max(0.0, d - d * max(0, red) / 100 - I(npc, "defm"))

def spell_dps(p, npc):
    sk = sk_magia(p["L"]) if p["c"] == "Mago" else sk_main(p["L"]); best = 0
    regen = p["mana"] * (3.5 + 0.035 * sk_med(p["L"])) / 100 / 0.4   # mana/s meditando
    for sid, req in SPELLS:
        cost = I(H[sid], "manarequerido")
        if sk < req or cost > p["mana"] or (sid == 51 and p["L"] < 20) or regen <= 0: continue
        t = 1.23 + cost / regen + 0.8 * cost / max(1, p["mana"])
        best = max(best, spell_dmg(p, sid, npc) / t)
    return best

def fight(c, L, idx):
    npc = NPC[idx]; p = player(c, L); k = CL[c]; opts = []
    if c != "Mago":
        mn, mx, proy = weapon(c, L); mod = k["dmgProy"] if proy else k["dmgArm"]
        d = (3 * (mn + mx) / 2 + mx * 0.2 * max(0, p["fue"] - 15) + (p["minhit"] + p["maxhit"]) / 2) * mod - I(npc, "def")
        hitp = max(5, min(95, 50 + ((p["pa_proy"] if proy else p["pa_arm"]) - I(npc, "poderevasion")) * 0.4)) / 100
        opts.append((max(0.0, d) * hitp / (1.2 if proy else 1.165), "arco" if proy else "arma"))
    if c in ("Mago", "Clerigo"): opts.append((spell_dps(p, npc), "hechizo"))
    dps, modo = max(opts)
    if dps <= 0: return None
    body, shield = armor(c, L); absorbed = body + shield * 5 / 6
    ph = max(10, min(90, 50 + (I(npc, "poderataque") - p["eva"]) * 0.4)) / 100
    dmg_in = ph * max(0, (I(npc, "minhit") + I(npc, "maxhit")) / 2 - absorbed) / ((I(npc, "intervaloataque") or 2000) / 1000) * EXPOSURE[modo]
    n = I(npc, "lanzaspells")
    if n:
        dm = [(I(H[s], "minhp") + I(H[s], "maxhp")) / 2 for s in (I(npc, f"sp{j}") for j in range(1, n + 1)) if s in H and H[s].get("subehp") == "2"]
        if dm: dmg_in += (sum(dm) / n) / ((I(npc, "intervalolanzarhechizo") or 8000) / 1000)
    ttk = I(npc, "maxhp") / dps; pots = dmg_in * ttk / POT_HP
    t = ttk + TRAVEL_S + pots * POT_S
    delta = L - I(npc, "npclvl"); pen = 1.0 if delta <= 4 else max(0.0, 1 - 0.05 * (delta - 4))
    burst = max(0, I(npc, "maxhit") - absorbed)
    ok = dmg_in <= MAX_DPS_IN and burst <= BURST.get(c, 0.5) * p["hp"]
    return dict(ok=ok, expmin=I(npc, "giveexp") * pen / t * 60, t=t, ttk=ttk, dmg_in=dmg_in, pots=pots)

def run():
    d = json.load(open(JSON, encoding="utf-8")); floors = d["pisos"]
    mult = {tuple(map(int, k.split("-"))): v for k, v in d["multiplicadorExpPropuesto"]["tramos"].items()}
    def m(L):
        if EXP_MULT: return EXP_MULT
        return next(v for (a, b), v in mult.items() if a <= L <= b)
    out = {}
    for c in CL:
        rows, acc = [], 0.0
        for L in range(1, 30):
            chosen = None
            for f in reversed([f for f in floors if f["nivelRecomendado"][0] <= L]):
                rs = [r for r in (fight(c, L, n["npcIndex"]) for n in f["npcs"]) if r and r["ok"]]
                if rs: chosen = (f, rs); break
            if chosen is None:
                f = floors[0]; rs = [r for r in (fight(c, L, n["npcIndex"]) for n in f["npcs"]) if r]; chosen = (f, rs)
            f, rs = chosen; em = sum(r["expmin"] for r in rs) / len(rs)
            mins = EXP[L] / em / m(L); acc += mins
            rows.append(dict(nivel=L, piso=f["id"], min_demo=round(mins, 1), acum_h_demo=round(acc / 60, 2)))
        out[c] = rows
    return d, out

if __name__ == "__main__":
    d, out = run()
    for c, rows in out.items():
        print(f"{c:9} total {rows[-1]['acum_h_demo']:5.1f} h | " + " ".join(f"{r['nivel']}:{r['piso']}/{r['min_demo']:.0f}" for r in rows))
    if "--check" in sys.argv:
        bad = [(c, r["nivel"]) for c, rows in out.items() for r, j in zip(rows, d["tiempoPorNivel"][c])
               if abs(r["min_demo"] - j["min_demo"]) > 0.15 or r["piso"] != j["piso"]]
        print("check vs dungeon-npcs.json:", "OK" if not bad else f"DIFIERE {bad[:10]}")
