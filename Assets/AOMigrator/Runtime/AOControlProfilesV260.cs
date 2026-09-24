using System;
using UnityEngine;

public enum AOControlProfile { AO, MOBA }

public static partial class AOPlayerSettingsV230
{
    public static AOControlProfile Profile {
        get => PlayerPrefs.GetInt(Prefix+"profile",0)==1?AOControlProfile.MOBA:AOControlProfile.AO;
        set { PlayerPrefs.SetInt(Prefix+"profile",value==AOControlProfile.MOBA?1:0);PlayerPrefs.Save(); }
    }
    public static bool IsMoba => Profile==AOControlProfile.MOBA;
    static string ControlsPrefix => Prefix+(IsMoba?"moba.":"");
    public static bool SpellMacrosEnabled => PlayerPrefs.GetInt(ControlsPrefix+"macros",IsMoba?1:0)!=0;
    public static bool QuickCast { get => PlayerPrefs.GetInt(ControlsPrefix+"quickcast",IsMoba?1:0)!=0;
        set {PlayerPrefs.SetInt(ControlsPrefix+"quickcast",value?1:0);PlayerPrefs.Save();} }

    public static bool SetSpellMacros(bool enabled,out string error)
    {
        Ensure();error="";
        if(enabled)for(int i=0;i<4;i++) {
            var action=AOGameAction.Spell1+i;var key=Key(action);
            if(key==KeyCode.None)continue;
            if(!IsMoba&&arrowMovement&&IsArrow(key)){error="Una macro usa una flecha de movimiento.";return false;}
            foreach(AOGameAction other in Enum.GetValues(typeof(AOGameAction)))
                if(other!=action&&(ActionEnabled(other)||IsSpellAction(other))&&Key(other)==key)
                {error="Tecla de hechizo repetida: "+ActionName(other)+".";return false;}
        }
        PlayerPrefs.SetInt(ControlsPrefix+"macros",enabled?1:0);PlayerPrefs.Save();return true;
    }
    static bool IsSpellAction(AOGameAction a)=>a>=AOGameAction.Spell1&&a<=AOGameAction.Spell4;
    public static bool ActionEnabled(AOGameAction a) =>
        (!IsMoba||a>AOGameAction.MoveRight) &&
        (IsMoba||a!=AOGameAction.WorldCommand) && (!IsSpellAction(a)||SpellMacrosEnabled);
    public static bool ShowAction(AOGameAction a) =>
        (!IsMoba||a>AOGameAction.MoveRight) && (IsMoba||a!=AOGameAction.WorldCommand);

    static KeyCode DefaultKey(AOGameAction a)
    {
        if(IsMoba) switch(a) {
            case AOGameAction.MoveUp:case AOGameAction.MoveDown:case AOGameAction.MoveLeft:case AOGameAction.MoveRight:return KeyCode.None;
            case AOGameAction.WorldCommand:return KeyCode.Mouse1;
            case AOGameAction.Interact:return KeyCode.F;
            case AOGameAction.Attack:return KeyCode.A;
            case AOGameAction.AttackAlternate:return KeyCode.None;
            case AOGameAction.Stop:return KeyCode.S;
            case AOGameAction.Quests:return KeyCode.J;
            case AOGameAction.Character:return KeyCode.C;
            case AOGameAction.Spell1:return KeyCode.Q;
            case AOGameAction.Spell2:return KeyCode.W;
            case AOGameAction.Spell3:return KeyCode.E;
            case AOGameAction.Spell4:return KeyCode.R;
        }
        return (int)a>=0&&(int)a<defaults.Length?defaults[(int)a]:KeyCode.None;
    }
    public static KeyCode Key(AOGameAction action)
    {
        Ensure();var value=(KeyCode)PlayerPrefs.GetInt(ControlsPrefix+"key."+(int)action,(int)DefaultKey(action));
        return ValidKey(value)?value:DefaultKey(action);
    }
    public static string KeyName(AOGameAction a)=>KeyName(Key(a));
    public static string KeyName(KeyCode key)
    {
        switch(key) {
            case KeyCode.None:return "Sin tecla";
            case KeyCode.Mouse0:return "Clic izquierdo";
            case KeyCode.Mouse1:return "Clic derecho";
            case KeyCode.Mouse2:return "Botón central";
            case KeyCode.Mouse3:return "Mouse lateral 1";
            case KeyCode.Mouse4:return "Mouse lateral 2";
            case KeyCode.LeftControl:return "Ctrl izquierdo";
            case KeyCode.RightControl:return "Ctrl derecho";
            case KeyCode.UpArrow:return "Flecha arriba";
            case KeyCode.DownArrow:return "Flecha abajo";
            case KeyCode.LeftArrow:return "Flecha izquierda";
            case KeyCode.RightArrow:return "Flecha derecha";
            default:return key.ToString().Replace("Alpha","");
        }
    }
    public static bool SetKey(AOGameAction action,KeyCode key,out string error)
    {
        Ensure();error="";
        if(!ValidKey(key)){error="Tecla no admitida. Enter y Escape quedan reservados.";return false;}
        if(key!=KeyCode.None) {
            if(ArrowMovement&&IsArrow(key)&&key!=MovementArrow(action))
            {error="Desactivá Flechas alternativas para usar esa tecla.";return false;}
            foreach(AOGameAction other in Enum.GetValues(typeof(AOGameAction)))
                if(other!=action&&ActionEnabled(other)&&Key(other)==key)
                {error="Tecla ya asignada a "+ActionName(other)+".";return false;}
        }
        PlayerPrefs.SetInt(ControlsPrefix+"key."+(int)action,(int)key);PlayerPrefs.Save();return true;
    }
    public static void RestoreKeys()
    {
        foreach(AOGameAction a in Enum.GetValues(typeof(AOGameAction)))PlayerPrefs.DeleteKey(ControlsPrefix+"key."+(int)a);
        PlayerPrefs.DeleteKey(ControlsPrefix+"macros");PlayerPrefs.DeleteKey(ControlsPrefix+"quickcast");PlayerPrefs.Save();
    }
    public static string ActionName(AOGameAction a)
    {
        string[] names={"Mover arriba","Mover abajo","Mover izquierda","Mover derecha","Interactuar","Atacar","Atacar alternativo","Recoger","Inventario","Mapa","Misiones","Personaje","Guardar","Cargar","Mover / atacar / interactuar","Detener orden","Meditar","Hechizo 1","Hechizo 2","Hechizo 3","Hechizo 4","Consumible 1","Consumible 2","Consumible 3","Consumible 4"};
        return (int)a>=0&&(int)a<names.Length?names[(int)a]:a.ToString();
    }
    static string LoadoutKey(string character,bool spell,int slot)=>ControlsPrefix+"loadout."+(character??"").ToLowerInvariant()+"."+(spell?"spell.":"item.")+Mathf.Clamp(slot,0,3);
    public static int SlotAssignment(string character,bool spell,int slot)=>Mathf.Max(0,PlayerPrefs.GetInt(LoadoutKey(character,spell,slot),0));
    public static void AssignSlot(string character,bool spell,int slot,int id)
    {PlayerPrefs.SetInt(LoadoutKey(character,spell,slot),Mathf.Max(0,id));PlayerPrefs.Save();}
}
