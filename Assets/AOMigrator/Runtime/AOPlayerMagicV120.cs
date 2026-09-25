using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class AOPlayerMagicV120 : MonoBehaviour
{
    public enum LearnResult { Invalid, Learned, AlreadyKnown, Full }
    [Serializable] class SaveData { public int selected; public int[] spells; }

    const int RANGE_X=13;
    const int RANGE_Y=11;
    const float SUMMON_LIFETIME=1500f;

    [SerializeField] List<int> knownSpellIds=new List<int>();
    [SerializeField] int selectedSpellId;
    [SerializeField] bool targeting;
    [SerializeField] bool meditating;

    AOTestPlayer player;
    AOPlayerRPGV11 rpg;
    AOPlayerCombatV09 combat;
    AOPlayerMagicStatusV120 status;
    AOMagicEffectRuntimeV129 effects;
    AOInventoryV10 inventory;
    AOWorldManagerV07 world;
    Camera gameCamera;
    AOMeditationVisualV269 meditationVisual;

    int targetInputFrame = -1;
    public bool ConsumedInputThisFrame => targetInputFrame == Time.frameCount;
    float nextGlobalCastAt;
    float meditationStartedAt;
    float nextMeditationTick;
    readonly Dictionary<int,float> spellReadyAt=new Dictionary<int,float>();
    readonly List<AOSummonedPetV129> pets=new List<AOSummonedPetV129>();

    public int KnownSpellCount=>knownSpellIds==null?0:knownSpellIds.Count;
    public int SelectedSpellId=>selectedSpellId;
    public bool IsTargeting=>targeting;
    public bool IsMeditating=>meditating;
    public int ActivePetCount { get { CleanupPets(); return pets.Count; } }
    public AOSpellDatabaseV120.SpellDef SelectedSpell=>AOSpellDatabaseV120.Get(selectedSpellId);
    public string TargetPrompt { get { var s=SelectedSpell;if(!targeting||s==null)return "";return AOSkillShotConfigV267.IsSkillShot(s.id)?"Skill shot "+s.name+": apuntá la dirección | click mundo | Esc cancelar":"Objetivo de "+s.name+": "+s.TargetLabel+" | click mundo | Esc cancelar"; } }
    public string MeditationLabel=>meditating?"Meditando...":"Meditar";
    string SavePath=>Path.Combine(Application.persistentDataPath,"ao_magic_v129_spellbook.json");

    void Awake(){
        FindReferences();if(knownSpellIds==null)knownSpellIds=new List<int>();LoadSpellbook();CleanKnownSpells();
    }

    void Update(){
        if (AOOnlineClientV240.InputBlocked) return;
        FindReferences();CleanupPets();
        UpdateMeditation();
        if(AOInterfaceV0101.InputCaptured||AOCityUIV130.ModalOpen||AOQuestUIV150.ModalOpen){targeting=false;return;}
        if(!targeting)return;
        if(PressedCancel()){targeting=false;AOInterfaceV0101.PushMessage("Casteo cancelado.");return;}
        if(PressedPrimary())TryTargetMouse();
    }

    void FindReferences(){
        if(player==null)player=GetComponent<AOTestPlayer>();if(rpg==null)rpg=GetComponent<AOPlayerRPGV11>();if(combat==null)combat=GetComponent<AOPlayerCombatV09>();
        if(status==null)status=GetComponent<AOPlayerMagicStatusV120>();if(effects==null)effects=GetComponent<AOMagicEffectRuntimeV129>();if(inventory==null)inventory=GetComponent<AOInventoryV10>();
        if(world==null)world=UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        if(meditationVisual==null){
            meditationVisual=GetComponent<AOMeditationVisualV269>();
            if(meditationVisual==null)meditationVisual=gameObject.AddComponent<AOMeditationVisualV269>();
        }
        if(gameCamera==null){AOCameraFollow f=UnityEngine.Object.FindFirstObjectByType<AOCameraFollow>();if(f!=null)gameCamera=f.GetComponent<Camera>();if(gameCamera==null)gameCamera=Camera.main;}
    }

    public AOSpellDatabaseV120.SpellDef GetKnownSpellAt(int i){return knownSpellIds!=null&&i>=0&&i<knownSpellIds.Count?AOSpellDatabaseV120.Get(knownSpellIds[i]):null;}
    public bool KnowsSpell(int id)=>knownSpellIds!=null&&knownSpellIds.Contains(id);

    public LearnResult TryLearnSpell(int id){
        AOSpellDatabaseV120.SpellDef s=AOSpellDatabaseV120.Get(id);if(s==null)return LearnResult.Invalid;
        if(KnowsSpell(id))return LearnResult.AlreadyKnown;
        if(knownSpellIds.Count>=AOSpellDatabaseV120.MaxUserSpells)return LearnResult.Full;
        knownSpellIds.Add(id);knownSpellIds.Sort();if(selectedSpellId<=0)selectedSpellId=id;SaveSpellbook();return LearnResult.Learned;
    }

    public bool LearnSpell(int id){LearnResult r=TryLearnSpell(id);return r==LearnResult.Learned||r==LearnResult.AlreadyKnown;}

    public int[] GetKnownSpellIdsCopy()
    {
        if (knownSpellIds == null)
            return new int[0];

        return knownSpellIds.ToArray();
    }

    public bool RemoveKnownSpell(
        int id)
    {
        if (knownSpellIds == null ||
            !knownSpellIds.Remove(id))
            return false;

        if (selectedSpellId == id)
        {
            selectedSpellId =
                knownSpellIds.Count > 0
                ? knownSpellIds[0]
                : 0;
        }

        SaveSpellbook();
        return true;
    }

    public void RestoreSpellbookForSave(
        int[] spellIds,
        int savedSelectedSpell)
    {
        ResetRuntimeForLoad();

        if (knownSpellIds == null)
            knownSpellIds =
                new List<int>();

        knownSpellIds.Clear();

        if (spellIds != null)
        {
            foreach (int id in spellIds)
            {
                if (knownSpellIds.Count >=
                    AOSpellDatabaseV120.MaxUserSpells)
                    break;

                if (AOSpellDatabaseV120.Get(id) != null &&
                    !knownSpellIds.Contains(id))
                {
                    knownSpellIds.Add(id);
                }
            }
        }

        knownSpellIds.Sort();

        selectedSpellId =
            knownSpellIds.Contains(
                savedSelectedSpell)
            ? savedSelectedSpell
            : (knownSpellIds.Count > 0
                ? knownSpellIds[0]
                : 0);

        SaveSpellbook();
    }

    public void ClearSpellbookForNewGame()
    {
        ResetRuntimeForLoad();

        if (knownSpellIds == null)
            knownSpellIds =
                new List<int>();

        knownSpellIds.Clear();
        selectedSpellId = 0;

        SaveSpellbook();
    }

    public void ResetRuntimeForLoad()
    {
        targeting = false;
        meditating = false;
        meditationVisual?.End();

        nextGlobalCastAt = 0f;
        meditationStartedAt = 0f;
        nextMeditationTick = 0f;

        spellReadyAt.Clear();

        for (int i =
                 pets.Count - 1;
             i >= 0;
             i--)
        {
            AOSummonedPetV129 pet =
                pets[i];

            if (pet != null)
                Destroy(
                    pet.gameObject);
        }

        pets.Clear();

        if (effects == null)
            effects =
                GetComponent
                    <AOMagicEffectRuntimeV129>();

        if (effects != null)
            effects.ClearAll();

        if (status == null)
            status =
                GetComponent
                    <AOPlayerMagicStatusV120>();

        if (status != null)
        {
            status.RemoveDebuffs();
            status.RemoveInvisibility();
        }
    }

    public void SelectSpell(int id){if(!KnowsSpell(id))return;selectedSpellId=id;targeting=false;SaveSpellbook();}

    public float CooldownRemaining(int id){
        float a=Mathf.Max(0f,nextGlobalCastAt-Time.time);float b=spellReadyAt.TryGetValue(id,out float v)?Mathf.Max(0f,v-Time.time):0f;return Mathf.Max(a,b);
    }

    public void CancelTargeting(){targeting=false;}
    public void CastShortcut(int id,bool quickCast)
    {
        if(AOInterfaceV0101.InputCaptured||AOOnlineClientV240.InputBlocked)return;
        FindReferences();
        if(!KnowsSpell(id)){AOInterfaceV0101.PushMessage("Ese personaje todavía no aprendió el hechizo asignado.");return;}
        SelectSpell(id);
        if(!quickCast){BeginCastSelected();return;}
        if(SelectedSpell.target==5){CastSelectedOnPets();return;}
        if(SelectedSpell.target==1){CastSelectedOnSelf();return;}
        TryTargetMouse();
    }
    public void BeginCastSelected(){
        var s=SelectedSpell;if(!ValidateSpell(s,false,out string err)){AOInterfaceV0101.PushMessage(err);return;}
        StopMeditation();
        if(s.target==1){CastSelectedOnSelf();return;}
        if(s.target==5){CastSelectedOnPets();return;}
        targeting=true;AOInterfaceV0101.PushMessage(TargetPrompt);
    }

    public void CastSelectedOnSelf(){
        var s=SelectedSpell;if(s==null)return;
        if(s.target!=1&&s.target!=3){AOInterfaceV0101.PushMessage(s.name+" no admite objetivo usuario.");return;}
        if(!ValidateSpell(s,true,out string err)){AOInterfaceV0101.PushMessage(err);return;}
        StopMeditation();if(ApplyToPlayer(s))FinishCast(s,transform.position,"vos");else AOInterfaceV0101.PushMessage("El hechizo no tuvo efecto.");
    }

    public void CastSelectedOnPets(){
        var s=SelectedSpell;if(s==null||s.target!=5)return;
        if(!ValidateSpell(s,true,out string err)){AOInterfaceV0101.PushMessage(err);return;}
        StopMeditation();CleanupPets();bool ok=false;foreach(var p in pets)if(p!=null&&p.gameObject.activeInHierarchy)ok=ApplyToPet(s,p)||ok;
        if(ok)FinishCast(s,transform.position,pets.Count+" mascota(s)");else AOInterfaceV0101.PushMessage("No tenés mascotas activas para ese hechizo.");
    }

    public void ToggleMeditation(){
        FindReferences();if(rpg==null||rpg.MaxMana<=0){AOInterfaceV0101.PushMessage("Tu personaje no posee mana.");return;}
        if(meditating){StopMeditation();AOInterfaceV0101.PushMessage("Dejaste de meditar.");return;}
        if(rpg.Mana>=rpg.MaxMana){AOInterfaceV0101.PushMessage("Ya tenés el mana completo.");return;}
        meditating=true;meditationStartedAt=Time.time;nextMeditationTick=Time.time+.8f;meditationVisual?.Begin(rpg.Level);AOInterfaceV0101.PushMessage("Comenzás a meditar.");
    }

    public void InterruptMeditation(){if(!meditating)return;StopMeditation();AOInterfaceV0101.PushMessage("Dejaste de meditar.");}
    void StopMeditation(){meditating=false;meditationStartedAt=nextMeditationTick=0f;meditationVisual?.End();}
    void UpdateMeditation(){
        if(!meditating||rpg==null)return;
        if(combat!=null&&combat.IsDead){StopMeditation();return;}
        if(player!=null&&player.IsMoving){StopMeditation();AOInterfaceV0101.PushMessage("Interrumpiste la meditación al moverte.");return;}
        if(rpg.Mana>=rpg.MaxMana){StopMeditation();AOInterfaceV0101.PushMessage("Terminaste de meditar: mana completo.");return;}
        if(Time.time<nextMeditationTick)return;
        var cfg=AOSpellDatabaseV120.Settings;float basePct=cfg==null?7f:cfg.manaRecoveryBasePercent,recBase=cfg==null?50f:cfg.manaRecoveryBase,mult=cfg==null?.5f:cfg.manaRecoverySkillMultiplier;
        float nested=basePct*(recBase+rpg.GetSkill(5)*mult)/100f;int amount=Mathf.Max(1,Mathf.RoundToInt(rpg.MaxMana*nested/100f));int restored=rpg.RestoreManaAmount(amount);
        nextMeditationTick=Time.time+(cfg==null?.4f:Mathf.Max(.1f,cfg.meditationTickMs/1000f));
        if(restored>0)AOInterfaceV0101.PushMessage("Meditación: +"+restored+" mana.");
    }

    void TryTargetMouse(){
        var s=SelectedSpell;
        if(s!=null&&AOSkillShotConfigV267.IsSkillShot(s.id)){TryLaunchSkillShotMouse(s);return;}
        if(!AOActionBarV260.CursorTile(player,out int tx,out int ty))return;
        targetInputFrame=Time.frameCount;
        AOGridMap grid=player.CurrentGrid;if(!grid.InBounds(tx,ty)){AOInterfaceV0101.PushMessage("Objetivo fuera del mapa.");return;}
        if(!ValidateSpell(s,true,out string err)){targeting=false;AOInterfaceV0101.PushMessage(err);return;}
        if(!InRange(tx,ty)){AOInterfaceV0101.PushMessage("Objetivo fuera del rango de visión.");return;}
        if(s.RequiresLand&&grid.IsDeepWater(tx,ty)){AOInterfaceV0101.PushMessage("Este hechizo requiere un objetivo sobre tierra.");return;}
        bool ok=false;string targetName="";
        int duelTarget=AODuelUI.InDuel&&(s.target==1||s.target==3)?AOOnlineClientV240.PlayerAt(tx,ty):0;
        if(duelTarget>0&&AODuelClient.IsEnemy(duelTarget)){ok=AOOnlineClientV240.CastPlayer(s.id,duelTarget,tx,ty);targetName="rival";}
        else if(AOOnlineClientV240.Requested && (s.target==1||s.target==3) && AOOnlineClientV240.PlayerAt(tx,ty)>0){ok=AOOnlineClientV240.CastAlly(s.id,tx,ty);targetName="compañero";}
        else if(s.target==4){ok=ApplyToTerrain(s,tx,ty);targetName=tx+","+ty;}
        else if(s.target==1||s.target==3){
            if(tx==player.TileX&&ty==player.TileY){ok=ApplyToPlayer(s);targetName="vos";}
            else if(s.target==3){var n=FindNpcAt(tx,ty);if(n!=null){ok=ApplyToNpc(s,n);targetName=n.DisplayName;}}
        } else if(s.target==2){var n=FindNpcAt(tx,ty);if(n!=null){ok=ApplyToNpc(s,n);targetName=n.DisplayName;}}
        if(!ok){AOInterfaceV0101.PushMessage("El hechizo no tuvo un objetivo o efecto válido.");return;}
        targeting=false;StopMeditation();FinishCast(s,grid.TileToWorld(tx,ty),targetName);
    }

    // 4 direcciones como el original. Cerca de 45° conserva la actual (histéresis de 10°) para que no parpadee.
    static int AimHeading(Vector2 d,int current){
        float aim=Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg; // 0 = este, 90 = norte (en el mundo, y hacia arriba es norte)
        float facing=current==AOGridMap.EAST?0f:current==AOGridMap.NORTH?90f:current==AOGridMap.WEST?180f:-90f;
        if(Mathf.Abs(Mathf.DeltaAngle(aim,facing))<=55f)return current;
        if(Mathf.Abs(d.x)>=Mathf.Abs(d.y))return d.x>=0f?AOGridMap.EAST:AOGridMap.WEST;
        return d.y>=0f?AOGridMap.NORTH:AOGridMap.SOUTH;
    }
    void TryLaunchSkillShotMouse(AOSpellDatabaseV120.SpellDef s)
    {
        FindReferences();
        if(player==null||player.CurrentGrid==null||gameCamera==null)return;
        if(!ValidateSpell(s,true,out string err)){targeting=false;AOInterfaceV0101.PushMessage(err);return;}
        Vector2 mouse=MousePosition();
        if(!gameCamera.pixelRect.Contains(mouse))return;
        AOActionBarV260 bar=GetComponent<AOActionBarV260>();
        Vector2 guiMouse=new Vector2(mouse.x,Screen.height-mouse.y);
        if(bar!=null&&bar.ShowBarPublic()&&bar.GetBarRectGUI().Contains(guiMouse))return;
        Vector3 worldPoint=gameCamera.ScreenToWorldPoint(new Vector3(mouse.x,mouse.y,Mathf.Abs(gameCamera.transform.position.z)));
        Vector2 from=new Vector2(transform.position.x,transform.position.y);
        Vector2 direction=new Vector2(worldPoint.x,worldPoint.y)-from;
        if(direction.sqrMagnitude<.04f){AOInterfaceV0101.PushMessage("Apuntá más lejos del personaje.");return;}
        // Online: check before paying, the server would reject the hit anyway.
        if(AOOnlineClientV240.Requested&&s.mimic==0&&!AOOnlineClientV240.CanCastNpcSpell(s.id)){targeting=false;AOInterfaceV0101.PushMessage("Ese efecto sobre criaturas todavía no está disponible en la alpha cooperativa.");return;}
        if(!CommitCastResources(s))return;
        targetInputFrame=Time.frameCount;targeting=false;StopMeditation();
        if(!string.IsNullOrWhiteSpace(s.magicWords))AOInterfaceV0101.PushMessage(s.magicWords);
        AOInterfaceV0101.PushMessage("Lanzaste "+s.name+" como skill shot.");
        player.FaceHeading(AimHeading(direction,player.Heading));
        AOCastAnimationRuntimeV268.PlayPlayer(gameObject,s);AOOnlineClientV240.NotifyLocalCast(s.id);
        AOSpellFXV120.PlayCastSound(s);
        AOSkillShotProjectileV267.Launch(this,s,player.CurrentGrid,transform.position,direction.normalized);
    }

    public void ResolveSkillShotHit(AOSpellDatabaseV120.SpellDef s,AONPCCombatV09 npc,Vector3 impactWorld)
    {
        if(s==null||npc==null||!npc.IsAlive)return;
        bool ok=ApplyToNpc(s,npc);
        AOSpellFXV120.PlayImpact(s,impactWorld,false);
        if(ok)AOInterfaceV0101.PushMessage(s.name+" impactó a "+npc.DisplayName+".");
    }

    bool ValidateSpell(AOSpellDatabaseV120.SpellDef s,bool cooldown,out string error){
        error="";if(s==null){error="No hay hechizo seleccionado.";return false;}
        // Duel: nothing during the countdown or while down; no summons or invisibility (original ModRetos).
        if(AODuelClient.Frozen){error="Todavía no podés lanzar hechizos en este reto.";return false;}
        if(AODuelClient.Blocks&&(s.summonNpc>0||s.summonMode>0||s.invisibility!=0)){error=AODuelClient.BlockedMessage;return false;}if(!s.supportedLocal){error=s.name+" es una habilidad física/especial y no forma parte del núcleo mágico local.";return false;}
        if(combat!=null&&combat.IsDead){error="No podés lanzar hechizos muerto.";return false;}if(status!=null&&!status.CanCast){error="No podés castear mientras estás paralizado.";return false;}
        if(rpg==null){error="No encuentro AOPlayerRPGV11.";return false;}
        if(rpg.GetSkill(1)<s.minSkill){error="Requiere Magia "+s.minSkill+". Tenés "+rpg.GetSkill(1)+".";return false;}
        if(s.maxLevelCastable>0&&rpg.Level>s.maxLevelCastable){error="Sólo puede castearse hasta nivel "+s.maxLevelCastable+".";return false;}
        if(s.requiredHp>0&&(combat==null||combat.HP<s.requiredHp)){error="Necesitás al menos "+s.requiredHp+" HP.";return false;}
        if(inventory==null)inventory=GetComponent<AOInventoryV10>();
        if(s.needStaff>0&&rpg.ClassId==1){var w=inventory==null?null:inventory.GetWeapon();if(w==null||w.staffPower<s.needStaff){error="Necesitás un báculo de poder "+s.needStaff+" o superior.";return false;}}
        if(s.requiredObject>0&&(inventory==null||!inventory.HasItem(s.requiredObject))){error="Necesitás "+ItemName(s.requiredObject)+".";return false;}
        if(s.requiredObject2>0&&(inventory==null||!inventory.HasItem(s.requiredObject2))){error="Necesitás "+ItemName(s.requiredObject2)+".";return false;}
        if(s.requireWeaponType>0){var w=inventory==null?null:inventory.GetWeapon();if(w==null||w.weaponType!=s.requireWeaponType){error="El hechizo requiere un tipo de arma específico.";return false;}}
        if((s.requirementMask&1)!=0&&(inventory==null||inventory.GetWeapon()==null)){error="Requiere un arma equipada.";return false;}
        if((s.requirementMask&2)!=0&&(inventory==null||inventory.GetShield()==null)){error="Requiere un escudo equipado.";return false;}
        if((s.requirementMask&4)!=0&&(inventory==null||inventory.GetArmor()==null)){error="Requiere armadura equipada.";return false;}
        if((s.requirementMask&8)!=0&&(inventory==null||inventory.GetHelmet()==null)){error="Requiere casco equipado.";return false;}
        if((s.requirementMask&16)!=0&&(inventory==null||(inventory.GetMagicAccessory()==null&&inventory.GetAmulet()==null))){error="Requiere un objeto mágico equipado.";return false;}
        int mana=GetManaCost(s),sta=GetStaminaCost(s);if(!rpg.CanPayMagicCost(mana,sta)){error=rpg.Mana<mana?"Mana insuficiente: "+rpg.Mana+"/"+mana+".":"Stamina insuficiente: "+rpg.Stamina+"/"+sta+".";return false;}
        if(cooldown&&CooldownRemaining(s.id)>0f){error="Cooldown: "+CooldownRemaining(s.id).ToString("0.0")+" s.";return false;}return true;
    }

    int GetManaCost(AOSpellDatabaseV120.SpellDef s){
        float cost=Mathf.Max(0,s.manaRequired);if(rpg!=null&&rpg.ClassId==6&&inventory!=null){var a=inventory.GetMagicAccessory();if(a!=null&&a.index==40){if(s.mimic!=0)cost*=.5f;else if(s.type==4)cost*=.7f;else if(s.id!=5)cost*=.9f;}}
        return Mathf.Max(0,Mathf.RoundToInt(cost));
    }
    int GetStaminaCost(AOSpellDatabaseV120.SpellDef s){if(s==null||rpg==null)return 0;int pct=s.staminaPercentRequired>0?Mathf.Max(1,Mathf.CeilToInt(rpg.MaxStamina*s.staminaPercentRequired/100f)):0;return Mathf.Max(s.staminaRequired,pct);}

    public void ApplyOnlineSpell(int id)
    {
        FindReferences(); var s=AOSpellDatabaseV120.Get(id); if(s==null)return;
        if(s.resurrect!=0 && combat!=null && combat.IsDead) combat.ResurrectFromPriest();
        else ApplyToPlayer(s);
        AOSpellFXV120.Play(s,transform.position,transform.position);
    }
    bool ApplyToPlayer(AOSpellDatabaseV120.SpellDef s){
        bool a=false;
        if(s.raiseHp!=0){int n=Roll(s.minHp,s.maxHp);if(s.raiseHp==1){combat.RestoreHealth(MagicHealing(n));a=true;}else if(s.raiseHp==2){combat.ReceiveMagicDamage(MagicDamage(n,null,s),"Magia: "+s.name);a=true;}}
        if(s.raiseMana!=0){int n=Roll(s.minMana,s.maxMana)*(s.raiseMana==1?1:-1);rpg.ModifyMana(n);a=true;}
        if(s.raiseStamina!=0){int n=Roll(s.minStamina,s.maxStamina)*(s.raiseStamina==1?1:-1);rpg.ModifyStamina(n);a=true;}
        if(s.raiseHunger!=0){rpg.ModifyHunger(Roll(s.minHunger,s.maxHunger)*(s.raiseHunger==1?1:-1));a=true;}
        if(s.raiseThirst!=0){rpg.ModifyThirst(Roll(s.minThirst,s.maxThirst)*(s.raiseThirst==1?1:-1));a=true;}
        if(s.raiseAgility!=0){rpg.ApplySpellAttributeModifier(2,s.raiseAgility,s.minAgility,s.maxAgility,Mathf.Max(1,s.duration));a=true;}
        if(s.raiseStrength!=0){rpg.ApplySpellAttributeModifier(1,s.raiseStrength,s.minStrength,s.maxStrength,Mathf.Max(1,s.duration));a=true;}
        if(s.raiseCharisma!=0){rpg.ApplySpellAttributeModifier(5,s.raiseCharisma,s.minCharisma,s.maxCharisma,Mathf.Max(1,s.duration));a=true;}
        if(effects==null)effects=GetComponent<AOMagicEffectRuntimeV129>();if(s.speed>0f&&effects!=null){effects.ApplyTemporarySpeed(s.speed,Mathf.Max(1,s.duration));a=true;}
        if(status==null)status=GetComponent<AOPlayerMagicStatusV120>();if(status!=null){float d=Mathf.Max(1f,s.duration);
            if(s.paralyze!=0){status.ApplyParalysis(Mathf.Max(1f,d*.5f));a=true;}if(s.immobilize!=0){status.ApplyImmobilize(Mathf.Max(1f,d*.5f));a=true;}if(s.removeParalysis!=0){status.RemoveParalysis();a=true;}
            if(s.poison!=0){status.ApplyPoison(s.poison,s.duration>0?s.duration:20f);a=true;}if(s.curePoison!=0){status.CurePoison();a=true;}
            if(s.invisibility!=0){status.ApplyInvisibility(d);a=true;}if(s.removeInvisibility!=0){status.RemoveInvisibility();a=true;}
            if(s.incinerate!=0){status.ApplyIncinerate(d);a=true;}if(s.blindness!=0){status.ApplyBlind(d);a=true;}if(s.dumb!=0){status.ApplyDumb(d);a=true;}if(s.removeDumb!=0){status.RemoveDumb();a=true;}
            if(s.curse!=0){status.ApplyCurse(d);a=true;}if(s.removeCurse!=0){status.RemoveCurse();a=true;}if(s.removeDebuff!=0){status.RemoveDebuffs();a=true;}
        }
        if(s.eotId>0&&effects!=null)a=effects.ApplyEffect(s.eotId,combat)||a;
        if(a&&ShouldPersistSpellVisual(s))AOSpellPersistentVisualV130.ApplySpellBuff(gameObject,s.id,Mathf.Max(1f,s.duration));
        return a;
    }

    bool ApplyToNpc(AOSpellDatabaseV120.SpellDef s,AONPCCombatV09 npc){
        if(npc==null||!npc.IsAlive)return false;
        AOMagicEffectDatabaseV129.EffectDef eotDef =
            s.eotId>0
            ? AOMagicEffectDatabaseV129.Get(
                s.eotId)
            : null;

        bool harmful =
            s.raiseHp==2||
            s.paralyze!=0||
            s.immobilize!=0||
            s.poison!=0||
            s.incinerate!=0||
            s.curse!=0||
            s.stealBuff!=0||
            (eotDef!=null&&
             (eotDef.buffType==2||
              eotDef.buffType==4||
              (eotDef.type==1&&
               eotDef.tickPowerMax<0f)));
        if(harmful&&!npc.Attackable){AOInterfaceV0101.PushMessage(npc.DisplayName+" no es atacable.");return false;}
        AONPCMetadata meta=npc.GetComponent<AONPCMetadata>();var nd=meta==null?null:AONPCMagicDatabaseV129.Get(meta.NpcIndex);if(harmful&&nd!=null&&nd.immuneToSpells){AOInterfaceV0101.PushMessage("La criatura es inmune a hechizos.");return false;}
        if(AOOnlineClientV240.Requested && s.mimic==0) return AOOnlineClientV240.CastNpc(s.id,npc);
        bool a=false;
        if(s.raiseHp!=0){int n=Roll(s.minHp,s.maxHp);if(s.raiseHp==1){npc.HealMagic(MagicHealing(n));a=true;}else if(s.raiseHp==2){int dmg=MagicDamage(n,npc,s);npc.TakeMagicDamage(dmg,combat);a=true;}}
        AONPCMagicStatusV120 ns=npc.GetComponent<AONPCMagicStatusV120>();if(ns==null)ns=npc.gameObject.AddComponent<AONPCMagicStatusV120>();
        float dur=Mathf.Max(1f,s.duration);if(s.paralyze!=0){ns.ApplyParalysis(Mathf.Max(1f,dur*.5f));a=true;}if(s.immobilize!=0){ns.ApplyImmobilize(Mathf.Max(1f,dur*.5f));a=true;}
        if(s.removeParalysis!=0){ns.RemoveParalysis();a=true;}if(s.poison!=0){ns.ApplyPoison(s.poison,s.duration>0?s.duration:20f,combat);a=true;}if(s.curePoison!=0){ns.CurePoison();a=true;}
        if(s.incinerate!=0){ns.ApplyIncinerate(dur,combat);a=true;}if(s.removeDebuff!=0){ns.RemoveDebuffs();a=true;}
        AOMagicEffectRuntimeV129 ne=npc.GetComponent<AOMagicEffectRuntimeV129>();if(ne==null)ne=npc.gameObject.AddComponent<AOMagicEffectRuntimeV129>();
        if(s.speed>0f){ne.ApplyTemporarySpeed(s.speed,dur);a=true;}if(s.eotId>0)a=ne.ApplyEffect(s.eotId,combat)||a;
        if(s.stealBuff!=0&&effects!=null)a=ne.StealOneBuffTo(effects,combat)||a;
        if(s.mimic!=0){AOCharacterRenderer src=npc.GetComponentInChildren<AOCharacterRenderer>(true);AOMimicVisualV129 mimic=GetComponent<AOMimicVisualV129>();if(mimic==null)mimic=gameObject.AddComponent<AOMimicVisualV129>();mimic.Apply(src,30f);a=true;}
        if(a){if(ShouldPersistSpellVisual(s))AOSpellPersistentVisualV130.ApplySpellBuff(npc.gameObject,s.id,Mathf.Max(1f,s.duration));npc.NotifyProvoked();}return a;
    }

    bool ApplyToPet(AOSpellDatabaseV120.SpellDef s,AOSummonedPetV129 pet){
        if(pet==null)return false;AOMagicEffectRuntimeV129 e=pet.GetComponent<AOMagicEffectRuntimeV129>();if(e==null)e=pet.gameObject.AddComponent<AOMagicEffectRuntimeV129>();bool a=false;
        if(s.eotId>0)a=e.ApplyEffect(s.eotId,combat);if(s.speed>0f){e.ApplyTemporarySpeed(s.speed,Mathf.Max(1,s.duration));a=true;}if(a&&ShouldPersistSpellVisual(s))AOSpellPersistentVisualV130.ApplySpellBuff(pet.gameObject,s.id,Mathf.Max(1f,s.duration));return a;
    }

    bool ApplyToTerrain(AOSpellDatabaseV120.SpellDef s,int tx,int ty){
        if(AOOnlineClientV240.Requested && (s.type==3||s.type==5)) return AOOnlineClientV240.CastArea(s.id,tx,ty);
        if(s.type==3)return Materialize(s,tx,ty);
        if(s.type==4)return Summon(s,tx,ty);
        if(s.type==6)return Portal(s);
        bool a=false;
        if(s.type==5&&s.areaRadius>0){
            int radius=Mathf.Max(1,s.areaRadius/2);bool users=s.areaAffects==1||s.areaAffects==3,npcs=s.areaAffects==2||s.areaAffects==3;
            if(users&&Mathf.Abs(player.TileX-tx)<=radius&&Mathf.Abs(player.TileY-ty)<=radius)ApplyToPlayer(s);
            if(npcs)foreach(AONPCCombatV09 n in UnityEngine.Object.FindObjectsByType<AONPCCombatV09>(FindObjectsSortMode.None)){if(n==null||!n.IsAlive)continue;AONPCMovementV08 mv=n.GetComponent<AONPCMovementV08>();if(mv!=null&&Mathf.Abs(mv.TileX-tx)<=radius&&Mathf.Abs(mv.TileY-ty)<=radius){ApplyToNpc(s,n);AOSpellFXV120.Play(s,transform.position,n.transform.position);}}
            a=true;
        }
        if(s.removeInvisibility!=0||s.type==12){if(status!=null){status.RemoveInvisibility();a=true;}}
        return a;
    }

    bool Materialize(AOSpellDatabaseV120.SpellDef s,int tx,int ty){
        if(s.materializeObject<=0)return false;AOGridMap g=player.CurrentGrid;if(g==null||!g.InBounds(tx,ty)||AOInteractionRegistry.IsBlocked(tx,ty)||AOLootPickupV09.FindAt(tx,ty)!=null){AOInterfaceV0101.PushMessage("No hay espacio libre para materializar.");return false;}
        AOItemDatabaseV10.ItemDef it=AOItemDatabaseV10.Get(s.materializeObject);string name=it==null?"OBJ "+s.materializeObject:it.name;AOLootPickupV09.Create(s.materializeObject,name,Mathf.Max(1,s.materializeCount),tx,ty);return true;
    }

    bool Summon(AOSpellDatabaseV120.SpellDef s,int tx,int ty){
        CleanupPets();
        if(s.summonMode==2){if(pets.Count==0){AOInterfaceV0101.PushMessage("No tenés mascotas para invocar a tu lado.");return false;}AOSummonedPetV129 far=null;int best=-1;foreach(var p in pets){if(p==null)continue;int d=Mathf.Abs(p.TileX-player.TileX)+Mathf.Abs(p.TileY-player.TileY);if(d>best){best=d;far=p;}}if(far!=null){far.WarpNearOwner();return true;}return false;}
        if(s.summonNpc<=0)return false;AOSummonDatabaseV129.SummonDef def=AOSummonDatabaseV129.Get(s.summonNpc);if(def==null){AOInterfaceV0101.PushMessage("No hay visual/datos para NPC invocado "+s.summonNpc+".");return false;}
        int count=Mathf.Max(1,s.summonCount);bool any=false;for(int i=0;i<count;i++){CleanupPets();while(pets.Count>=3){AOSummonedPetV129 oldest=pets[0];pets.RemoveAt(0);if(oldest!=null)Destroy(oldest.gameObject);}
            Vector2Int pos=FindSummonTile(tx,ty,i);GameObject go=new GameObject("Pet_"+def.npcIndex+"_"+def.name);AOSummonedPetV129 pet=go.AddComponent<AOSummonedPetV129>();pet.Configure(player,combat,def,pos.x,pos.y,SUMMON_LIFETIME);pets.Add(pet);any=true;}
        return any;
    }

    Vector2Int FindSummonTile(int tx,int ty,int seed){
        AOGridMap g=player.CurrentGrid;int[,] off={{0,0},{1,0},{-1,0},{0,1},{0,-1},{1,1},{-1,1},{1,-1},{-1,-1}};
        for(int n=0;n<off.GetLength(0);n++){int i=(n+seed)%off.GetLength(0),x=tx+off[i,0],y=ty+off[i,1];if(g.InBounds(x,y)&&g.CanEnter(x,y,AOGridMap.SOUTH)&&!AOInteractionRegistry.IsBlocked(x,y))return new Vector2Int(x,y);}
        return new Vector2Int(player.TileX,player.TileY);
    }

    bool Portal(AOSpellDatabaseV120.SpellDef s){
        if(s.teleportMap<=0||world==null)return false;
        if(world.MagicTeleport(s.teleportMap,s.teleportX,s.teleportY,out string msg)){AOInterfaceV0101.PushMessage(msg);return true;}
        AOInterfaceV0101.PushMessage(msg);return false;
    }

    bool CommitCastResources(AOSpellDatabaseV120.SpellDef s)
    {
        if(s==null||rpg==null)return false;
        int mana=GetManaCost(s),sta=GetStaminaCost(s);
        if(!rpg.SpendMagicCost(mana,sta)){AOInterfaceV0101.PushMessage("No se pudo pagar el costo del hechizo.");return false;}
        if(s.requiredHp>0&&combat!=null&&!combat.PayHealthCost(s.requiredHp)){AOInterfaceV0101.PushMessage("No se pudo pagar el costo de vida.");return false;}
        float global=(AOSpellDatabaseV120.Settings==null?1230:AOSpellDatabaseV120.Settings.castIntervalMs)/1000f;nextGlobalCastAt=Time.time+global;
        float cd=Mathf.Max(0,s.cooldown);if(cd>0f&&HasElvenWood())cd*=.5f;if(cd>0f)spellReadyAt[s.id]=Time.time+cd;
        return true;
    }

    void FinishCast(AOSpellDatabaseV120.SpellDef s,Vector3 targetWorld,string targetName){
        if(!CommitCastResources(s))return;
        if(!string.IsNullOrWhiteSpace(s.magicWords))AOInterfaceV0101.PushMessage(s.magicWords);
        AOInterfaceV0101.PushMessage("Lanzaste "+s.name+(string.IsNullOrEmpty(targetName)?".":" sobre "+targetName+"."));
        AOCastAnimationRuntimeV268.PlayPlayer(gameObject,s);AOOnlineClientV240.NotifyLocalCast(s.id);
        AOSpellFXV120.Play(s,transform.position,targetWorld);
    }


    bool ShouldPersistSpellVisual(AOSpellDatabaseV120.SpellDef s){
        if(s==null||s.eotId>0||s.duration<=1||!AOSpellVisualOverridesV130.HasBuffVisual(s.id))return false;
        return s.raiseAgility!=0||s.raiseStrength!=0||s.raiseCharisma!=0||s.speed>0f||s.invisibility!=0||s.paralyze!=0||s.immobilize!=0||s.poison!=0||s.incinerate!=0||s.blindness!=0||s.dumb!=0||s.curse!=0;
    }

    int MagicHealing(int baseAmount){
        int v=baseAmount;if(rpg!=null)v+=Mathf.RoundToInt(v*(3f*rpg.Level)/100f);if(effects!=null)v=effects.ApplyOutgoingMagicHealing(v);return Mathf.Max(0,v);
    }

    int MagicDamage(int baseDamage,AONPCCombatV09 target,AOSpellDatabaseV120.SpellDef s){
        int d=baseDamage;if(rpg!=null)d+=Mathf.RoundToInt(d*(3f*rpg.Level)/100f);int penetration=0;
        AOItemDatabaseV10.ItemDef w=inventory==null?null:inventory.GetWeapon(),acc=inventory==null?null:inventory.GetMagicAccessory(),amu=inventory==null?null:inventory.GetAmulet();
        if(s.staffAffected!=0&&rpg!=null&&rpg.ClassId==1){if(w!=null&&w.staffPower>0)d=Mathf.RoundToInt(d*(70f+w.magicDamageBonus)/100f);else d=Mathf.RoundToInt(d*.7f);}
        else {if(w!=null){d+=Mathf.RoundToInt(d*w.magicDamageBonus/100f)+w.magicAbsoluteBonus;penetration+=w.magicPenetration;}if(acc!=null){d+=Mathf.RoundToInt(d*acc.magicDamageBonus/100f)+acc.magicAbsoluteBonus;penetration+=acc.magicPenetration;}if(amu!=null){d+=Mathf.RoundToInt(d*amu.magicDamageBonus/100f)+amu.magicAbsoluteBonus;penetration+=amu.magicPenetration;}}
        if(effects!=null)d=effects.ModifyOutgoingMagic(d);
        if(target!=null&&s.antiRm==0){AONPCMetadata m=target.GetComponent<AONPCMetadata>();var nd=m==null?null:AONPCMagicDatabaseV129.Get(m.NpcIndex);if(nd!=null&&nd.magicResistance>0){int diff=nd.magicResistance-(rpg==null?0:rpg.GetSkill(1));int pct=Mathf.Max(0,nd.magicDef+(diff>0?diff*2:0)-penetration);d-=Mathf.RoundToInt(d*Mathf.Clamp(pct,0,95)/100f);}}
        return Mathf.Max(0,d);
    }

    bool HasElvenWood(){var w=inventory==null?null:inventory.GetWeapon();var a=inventory==null?null:inventory.GetMagicAccessory();return (w!=null&&w.elvenWood>0)||(a!=null&&a.elvenWood>0);}
    static int Roll(int min,int max){int lo=Mathf.Min(min,max),hi=Mathf.Max(min,max);return UnityEngine.Random.Range(lo,hi+1);}
    string ItemName(int id){var i=AOItemDatabaseV10.Get(id);return i==null?"OBJ "+id:i.name;}

    AONPCCombatV09 FindNpcAt(int tx,int ty){
        foreach(AONPCCombatV09 n in UnityEngine.Object.FindObjectsByType<AONPCCombatV09>(FindObjectsSortMode.None)){if(n==null||!n.IsAlive)continue;AONPCMovementV08 m=n.GetComponent<AONPCMovementV08>();if(m!=null&&m.TileX==tx&&m.TileY==ty)return n;AOInteractable it=n.GetComponent<AOInteractable>();if(it!=null&&it.TileX==tx&&it.TileY==ty)return n;}return null;
    }
    bool InRange(int tx,int ty)=>Mathf.Abs(player.TileX-tx)<=RANGE_X&&Mathf.Abs(player.TileY-ty)<=RANGE_Y;
    void CleanupPets(){for(int i=pets.Count-1;i>=0;i--)if(pets[i]==null)pets.RemoveAt(i);}
    void CleanKnownSpells(){for(int i=knownSpellIds.Count-1;i>=0;i--)if(AOSpellDatabaseV120.Get(knownSpellIds[i])==null)knownSpellIds.RemoveAt(i);knownSpellIds.Sort();while(knownSpellIds.Count>AOSpellDatabaseV120.MaxUserSpells)knownSpellIds.RemoveAt(knownSpellIds.Count-1);if(selectedSpellId<=0&&knownSpellIds.Count>0)selectedSpellId=knownSpellIds[0];}

    void LoadSpellbook(){
        try{if(!File.Exists(SavePath))return;SaveData d=JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));if(d!=null){knownSpellIds.Clear();if(d.spells!=null)knownSpellIds.AddRange(d.spells);selectedSpellId=d.selected;}}
        catch(Exception e){Debug.LogWarning("[AO v0.12.9] No pude cargar spellbook: "+e.Message);}
    }
    void SaveSpellbook(){
        if (AOOnlineClientV240.ProtectLocalSave) return;
        try{Directory.CreateDirectory(Application.persistentDataPath);SaveData d=new SaveData{selected=selectedSpellId,spells=knownSpellIds.ToArray()};File.WriteAllText(SavePath,JsonUtility.ToJson(d,true));}
        catch(Exception e){Debug.LogWarning("[AO v0.12.9] No pude guardar spellbook: "+e.Message);}
    }

    bool PressedCancel(){
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
    bool PressedPrimary(){
#if ENABLE_INPUT_SYSTEM
        return Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
    Vector2 MousePosition(){
#if ENABLE_INPUT_SYSTEM
        return Mouse.current!=null?Mouse.current.position.ReadValue():Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }
}
