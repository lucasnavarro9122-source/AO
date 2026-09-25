using System.Collections.Generic;
using UnityEngine;

public partial class AOInterfaceV0101
{
#if UNITY_EDITOR
    public void ShowControlsForQA(int section)
    {topDialog=TopDialog.Settings;settingsTab=SettingsTab.Controls;controlsSection=section;choosingSlot=-1;bindingAction=null;}
    public void CloseControlsForQA(){topDialog=TopDialog.None;}
#endif
    int controlsSection;
    int choosingSlot=-1;
    int assignmentPage;
    string SettingsCharacter=>player==null?"Aventurero":player.GetComponent<AOCharacterIdentityV170>()?.CharacterName??"Aventurero";

    void DrawControlProfiles()
    {
        GUI.Label(R(208,208,130,30),"Modo de juego");
        for(int i=0;i<2;i++) {
            var profile=(AOControlProfile)i;
            if(GUI.Button(R(353+i*180,206,170,34),(AOPlayerSettingsV230.Profile==profile?"✓ ":"")+profile)) {
                AOPlayerSettingsV230.Profile=profile;bindingAction=null;choosingSlot=-1;controlsPage=0;settingsStatus="Perfil "+profile+" activo. Preferencias guardadas.";
                player?.GetComponent<AOActionBarV260>()?.CancelOrder();
            }
        }
        string[] sections={"Teclas","Hechizos","Consumibles"};
        for(int i=0;i<3;i++)if(GUI.Button(R(208+i*204,251,194,32),sections[i]))
        {controlsSection=i;choosingSlot=-1;bindingAction=null;controlsPage=0;settingsStatus="";}
        var small=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=Mathf.Max(10,Mathf.RoundToInt(14*scale))};
        if(controlsSection==0) {
            GUI.Label(R(208,291,604,38),AOPlayerSettingsV230.IsMoba?"Clic contextual: caminar, perseguir/atacar o acercarse a interactuar.":"Controles AO. Las macros se habilitan en la pestaña Hechizos.",small);
            var actions=new List<AOGameAction>();
            foreach(AOGameAction action in System.Enum.GetValues(typeof(AOGameAction)))
                if(AOPlayerSettingsV230.ShowAction(action)&&action<AOGameAction.Spell1)actions.Add(action);
            int pages=Mathf.Max(1,(actions.Count+4)/5);controlsPage=Mathf.Clamp(controlsPage,0,pages-1);
            for(int row=0;row<5&&controlsPage*5+row<actions.Count;row++) {
                var action=actions[controlsPage*5+row];float y=333+row*44;
                GUI.Label(R(208,y,324,36),AOPlayerSettingsV230.ActionName(action),small);DrawBinding(action,534,y);
            }
            DrawControlPages(pages);
            if(GUI.Button(R(585,568,225,33),"Restablecer perfil")) {
                AOPlayerSettingsV230.RestoreKeys();bindingAction=null;settingsStatus="Teclas del perfil restauradas; asignaciones conservadas.";
            }
            return;
        }
        bool spell=controlsSection==1;
        if(spell) {
            bool enabled=GUI.Toggle(R(208,292,290,28),AOPlayerSettingsV230.SpellMacrosEnabled,"Habilitar macros de hechizos");
            if(enabled!=AOPlayerSettingsV230.SpellMacrosEnabled&&!AOPlayerSettingsV230.SetSpellMacros(enabled,out settingsStatus)){}
            bool quick=GUI.Toggle(R(513,292,295,28),AOPlayerSettingsV230.QuickCast,"Lanzar sobre el cursor");
            if(quick!=AOPlayerSettingsV230.QuickCast)AOPlayerSettingsV230.QuickCast=quick;
        }
        else GUI.Label(R(208,292,604,32),"Elegí objetos del inventario. Cada tecla usa una unidad.",small);
        if(choosingSlot>=0){DrawAssignmentChoices(spell,small);return;}
        for(int slot=0;slot<4;slot++) {
            float y=334+slot*55;var action=(spell?AOGameAction.Spell1:AOGameAction.Consumable1)+slot;
            int id=AOPlayerSettingsV230.SlotAssignment(SettingsCharacter,spell,slot);
            string name=id==0?"Sin asignar":spell?AOSpellDatabaseV120.Get(id)?.name:AOItemDatabaseV10.Get(id)?.name;
            GUI.Label(R(208,y,110,34),(spell?"Hechizo ":"Objeto ")+(slot+1),small);
            if(GUI.Button(R(323,y,204,35),new GUIContent(ShortControlText(name??"No disponible",24),name)))
            {choosingSlot=slot;assignmentPage=0;bindingAction=null;}
            DrawBinding(action,534,y);
        }
        GUI.Label(R(208,559,605,39),spell?"Elegí un hechizo aprendido. Sin lanzamiento al cursor, confirmás el objetivo con clic izquierdo.":"La asignación sigue al objeto aunque cambie de casilla. Se guarda por personaje y perfil.",small);
    }
    // Recuadro de la tecla que se está cambiando: un botón del mouse solo se toma si se hace clic ahí.
    Rect bindingRect;
    void DrawBinding(AOGameAction action,float x,float y)
    {
        if(bindingAction==action)bindingRect=R(x,y,225,35);
        if(GUI.Button(R(x,y,225,35),bindingAction==action?"Tecla o clic...":AOPlayerSettingsV230.KeyName(action)))
        {bindingAction=action;bindingRect=R(x,y,225,35);settingsStatus="Presioná una tecla o hacé clic sobre el recuadro con el botón del mouse. Escape cancela.";}
        if(GUI.Button(R(x+233,y,42,35),"×")) {
            AOPlayerSettingsV230.SetKey(action,KeyCode.None,out settingsStatus);bindingAction=null;
        }
    }
    void DrawControlPages(int pages)
    {
        if(GUI.Button(R(208,568,112,33),"Anterior")){controlsPage=Mathf.Max(0,controlsPage-1);bindingAction=null;}
        GUI.Label(R(336,570,100,28),(controlsPage+1)+" / "+pages);
        if(GUI.Button(R(448,568,112,33),"Siguiente")){controlsPage=Mathf.Min(pages-1,controlsPage+1);bindingAction=null;}
    }
    void DrawAssignmentChoices(bool spell,GUIStyle small)
    {
        if(magicV120==null&&player!=null)magicV120=player.GetComponent<AOPlayerMagicV120>();
        var ids=new List<int>{0};
        if(spell&&magicV120!=null)ids.AddRange(magicV120.GetKnownSpellIdsCopy());
        if(!spell&&inventory!=null)for(int i=0;i<inventory.SlotCount;i++) {
            int id=inventory.GetSlotItemIndex(i);var item=AOItemDatabaseV10.Get(id);
            if(item!=null&&item.Consumable&&!ids.Contains(id))ids.Add(id);
        }
        int pages=Mathf.Max(1,(ids.Count+4)/5);assignmentPage=Mathf.Clamp(assignmentPage,0,pages-1);
        for(int row=0;row<5&&assignmentPage*5+row<ids.Count;row++) {
            int id=ids[assignmentPage*5+row];string name=id==0?"Quitar asignación":spell?AOSpellDatabaseV120.Get(id)?.name:AOItemDatabaseV10.Get(id)?.name;
            if(GUI.Button(R(208,332+row*43,602,36),name??"No disponible")) {
                AOPlayerSettingsV230.AssignSlot(SettingsCharacter,spell,choosingSlot,id);choosingSlot=-1;
                settingsStatus="Asignación guardada.";return;
            }
        }
        if(ids.Count==1)GUI.Label(R(208,390,603,75),spell?"Todavía no conocés hechizos. Aprendé uno para asignarlo aquí.":"No tenés consumibles disponibles en el inventario.",small);
        if(GUI.Button(R(208,568,112,33),"Anterior"))assignmentPage=Mathf.Max(0,assignmentPage-1);
        GUI.Label(R(334,570,100,28),(assignmentPage+1)+" / "+pages);
        if(GUI.Button(R(448,568,112,33),"Siguiente"))assignmentPage=Mathf.Min(pages-1,assignmentPage+1);
        if(GUI.Button(R(585,568,225,33),"Volver"))choosingSlot=-1;
    }
    static string ShortControlText(string text,int max)=>text.Length<=max?text:text.Substring(0,max-1)+"…";
}
