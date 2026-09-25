using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// V272 · Backend de prueba de los retos (solo editor y en Play). Imita los mensajes que mandará el
// servidor (docs/claude/demo/red.md §8) para ver la interfaz sin sala online. No va en el juego armado.
public static class AOInterfaceDuelSimV272
{
    const string Menu = "AO Migrator/Demo AO BATTLESERVER/Retos (simulación)/";
    const int SimCountdown = 5;

    static readonly List<KeyValuePair<double, Action>> timeline = new List<KeyValuePair<double, Action>>();
    static bool hooked;

    sealed class SimBackend : IAODuelBackend
    {
        public bool Available => true;

        public void Challenge(string[] players, int bet, int maxRedPotions)
        {
            var mates = new List<string> { AODuelUI.LocalName };
            var rivals = new List<string>();
            for (int i = 0; i < players.Length; i++)
                (i % 2 == 0 ? rivals : mates).Add(players[i]);
            Schedule(1f, () => AODuelUI.ReceiveWaiting(players));
            Schedule(3f, () => StartDuel(mates.ToArray(), rivals.ToArray(), bet, 0));
        }

        public void Accept(string challenger)
        {
            AODuelUI.Invite invite = AODuelUI.FindInvite(challenger);
            if (invite == null) return;
            string[] teamA = invite.teamA, teamB = invite.teamB;
            int bet = invite.bet;
            Schedule(1f, () => AODuelUI.ReceiveWaiting(new string[0]));
            Schedule(2.5f, () => StartDuel(teamA, teamB, bet, Array.IndexOf(teamA, AODuelUI.LocalName) >= 0 ? 0 : 1));
        }

        public void Reject(string challenger) { }

        public void Cancel()
        {
            timeline.Clear();
            Schedule(0.3f, () => AODuelUI.ReceiveInviteClosed(AODuelUI.LocalName, "cancelado"));
        }

        public void Abandon()
        {
            timeline.Clear();
            Schedule(0.3f, () => AODuelUI.ReceiveEnd("abandono", 0, 0));
        }

        public void List()
        {
            Schedule(0.3f, () => AODuelUI.ReceiveRingState(3, "pelea", "Pepe vs Juan", 10000, 2, 1, 0, 420));
        }
    }

    [MenuItem(Menu + "Activar backend de prueba")]
    public static void Activate()
    {
        Hook();
        AODuelUI.Backend = new SimBackend();
        AOInterfaceV0101.PushMessage("Retos: backend de prueba activo (solo editor).");
    }

    [MenuItem(Menu + "Recibir invitación")]
    public static void SimulateInvite()
    {
        Activate();
        AODuelUI.ReceiveInvite("Pepe", 32, new[] { "Pepe" }, new[] { AODuelUI.LocalName }, 10000, 5, 60f);
    }

    [MenuItem(Menu + "Ver ring como espectador")]
    public static void SimulateSpectator()
    {
        Activate();
        AOTestPlayer player = UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
        if (player != null)
        {
            int x = player.TileX, y = player.TileY - 3;
            AODuelUI.RingLabelTile = ring => ring == 3 ? new Vector2Int(x, y) : (Vector2Int?)null;
        }
        AODuelUI.ReceiveAnnounce("Retos » Sala 3: Pepe vs Juan por 10.000 monedas de oro.");
        AODuelUI.ReceiveRingState(3, "pelea", "Pepe vs Juan", 10000, 2, 1, 0, 420);
    }

    [MenuItem(Menu + "Jugador remoto bajo el mouse")]
    public static void SimulateRemotePlayer()
    {
        Activate();
        AODuelUI.RemotePlayerAtGUI = gui => "Pepe";
        AOInterfaceV0101.PushMessage("Retos: cualquier clic en el mundo abre el menú de 'Pepe'.");
    }

    [MenuItem(Menu + "Error del servidor al aceptar")]
    public static void SimulateError()
    {
        AODuelUI.ReceiveError("Necesitas al menos 10.000 monedas de oro para aceptar este reto.");
    }

    [MenuItem(Menu + "Desactivar")]
    public static void Deactivate()
    {
        timeline.Clear();
        AODuelUI.Backend = null;
        AODuelUI.RemotePlayerAtGUI = null;
        AODuelUI.RingLabelTile = null;
        AODuelUI.ResetAll();
    }

    [MenuItem(Menu + "Activar backend de prueba", true)]
    [MenuItem(Menu + "Recibir invitación", true)]
    [MenuItem(Menu + "Ver ring como espectador", true)]
    [MenuItem(Menu + "Jugador remoto bajo el mouse", true)]
    [MenuItem(Menu + "Error del servidor al aceptar", true)]
    [MenuItem(Menu + "Desactivar", true)]
    static bool InPlay() => EditorApplication.isPlaying && AOMainMenuV140.SessionActive;

    // Reto al mejor de 3 que gana el equipo "winner" 2–0, con el conteo acortado a 5 s.
    static void StartDuel(string[] teamA, string[] teamB, int bet, int winner)
    {
        AODuelUI.ReceiveStart(1, teamA, teamB, bet, 600);
        AODuelUI.ReceiveAnnounce("Retos » Sala 1: " + AODuelUI.Names(teamA) + " vs " + AODuelUI.Names(teamB) +
                                 (bet > 0 ? " por " + AODuelUI.Gold(bet) + " monedas de oro." : ", amistoso."));

        int players = teamA.Length + teamB.Length;
        int winners = winner == 0 ? teamA.Length : teamB.Length;
        int pot = bet * players;
        int tax = pot / 10;
        int prize = winners > 0 ? (pot - tax) / winners : 0;
        bool localWins = Array.IndexOf(winner == 0 ? teamA : teamB, AODuelUI.LocalName) >= 0;

        double t = 0.5;
        for (int round = 1; round <= 2; round++)
        {
            int r = round;
            Schedule(t, () => AODuelUI.ReceiveRoundStart(r, SimCountdown, 0));
            t += SimCountdown + 3;
            if (!localWins && r == 1) Schedule(t - 1, () => AODuelUI.ReceiveDown(true));
            Schedule(t, () => AODuelUI.ReceiveRoundEnd(r, winner, winner == 0 ? r : 0, winner == 1 ? r : 0));
            t += 3;
        }
        Schedule(t, () => AODuelUI.ReceiveEnd(localWins ? "victoria" : "derrota", localWins ? prize : 0, localWins ? tax : 0));
        Schedule(t + 0.2, () => AODuelUI.ReceiveAnnounce("Retos » " + AODuelUI.Names(winner == 0 ? teamA : teamB) +
            " venció a " + AODuelUI.Names(winner == 0 ? teamB : teamA) + " y se quedó con el botín de: " +
            AODuelUI.Gold(pot) + " monedas de oro."));
    }

    static void Schedule(double delay, Action action)
    {
        Hook();
        timeline.Add(new KeyValuePair<double, Action>(EditorApplication.timeSinceStartup + delay, action));
    }

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        EditorApplication.update += Pump;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Deactivate();
        };
    }

    static void Pump()
    {
        if (!EditorApplication.isPlaying)
        {
            timeline.Clear();
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        for (int i = 0; i < timeline.Count; i++)
        {
            if (timeline[i].Key > now) continue;
            Action action = timeline[i].Value;
            timeline.RemoveAt(i--);
            action();
        }
    }
}
