using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// V272 · Retos de la Demo AO BATTLESERVER: contrato y estado de la interfaz (docs/claude/demo/ui.md).
// La red llama a los Receive* (mensajes de red.md §8) y la interfaz manda órdenes por Backend.
// La interfaz no decide nada del reto: solo muestra lo que llega y pide.

public interface IAODuelBackend
{
    bool Available { get; }
    void Challenge(string[] players, int bet, int maxRedPotions);
    void Accept(string challenger);
    void Reject(string challenger);
    void Cancel();
    void Abandon();
    void List();
}

public enum AODuelPhase { None, Countdown, Fight, BetweenRounds }

public static class AODuelUI
{
    public const int MaxPerTeam = 5;
    public const int MinBet = 1000;            // Decisión 4 de Lucas: 1.000, o 0 para un reto amistoso.
    public const int MaxBet = 100000000;       // APUESTA_MAXIMA original.
    public const int MaxRedPotions = 10000000;

    // Enganches de otros sectores.
    public static IAODuelBackend Backend;                    // Servidor: cliente online de la demo.
    public static Func<long> ServerNowMs;                    // Servidor: hora estimada del servidor en ms.
    public static Func<int, Vector2Int?> RingLabelTile;      // Programación: casilla sobre el borde superior del ring.
    public static Func<Vector2, string> RemotePlayerAtGUI;   // Servidor: nombre del jugador bajo el mouse (coordenadas GUI).

    public static string LocalName = "";

    public sealed class Invite
    {
        public string from = "";
        public int level;
        public string[] teamA = new string[0];
        public string[] teamB = new string[0];
        public int bet;
        public int maxPotions = -1;
        public float expiresAt;
        public bool accepted;
        public bool acceptAnnounced;
        public string status = "";
        public string error = "";
    }

    public sealed class RingInfo
    {
        public int ring;
        public string phase = "";
        public string names = "";
        public int bet, round, scoreA, scoreB, remaining;
        public float receivedAt;
    }

    static readonly List<Invite> invites = new List<Invite>();
    static readonly Dictionary<int, RingInfo> rings = new Dictionary<int, RingInfo>();

    public static IReadOnlyList<Invite> Invites => invites;
    public static IEnumerable<RingInfo> Rings => rings.Values;

    public static bool ChallengePending { get; private set; }
    public static string PendingSummary { get; private set; } = "";
    public static string PendingStatus { get; private set; } = "";

    public static bool InDuel { get; private set; }
    public static AODuelPhase Phase { get; private set; }
    public static int Ring { get; private set; }
    public static string[] TeamA { get; private set; } = new string[0];
    public static string[] TeamB { get; private set; } = new string[0];
    public static int Bet { get; private set; }
    public static int Round { get; private set; }
    public static int ScoreA { get; private set; }
    public static int ScoreB { get; private set; }
    public static bool Down { get; private set; }

    public static string BannerTitle { get; private set; } = "";
    public static string BannerDetail { get; private set; } = "";
    public static float BannerUntil { get; private set; }

    // Rectángulos dibujados en el último Repaint (coordenadas GUI), para el chequeo de superposición de QA.
    // Rect.zero = no se dibujó.
    public static Rect ScoreboardRect;
    public static Rect CenterRect;
    public static Rect NoticeRect;
    public static Rect UserMenuRect;
    public static Rect ViewportRect;   // viewport del juego (lo pone la interfaz)
    public static Rect HotbarRect;     // hotbar (la pone AOShortcutHUDV260 en su propio OnGUI)
    public static readonly List<Rect> RingLabelRects = new List<Rect>();

    public static void ClearDrawnRects()
    {
        ScoreboardRect = CenterRect = NoticeRect = UserMenuRect = ViewportRect = Rect.zero;
        RingLabelRects.Clear();
    }

    public static string LastError { get; private set; } = "";
    public static float LastErrorAt { get; private set; } = -100f;

    static float countdownEndsAt;
    static float fightSecondsLeft;
    static float listRequestedAt = -1f;
    static bool listAnswered;
    static string awaitingAcceptFrom = "";

    public static bool CountdownActive => Phase == AODuelPhase.Countdown;
    public static int CountdownSecondsLeft =>
        Phase == AODuelPhase.Countdown ? Mathf.Max(0, Mathf.CeilToInt(countdownEndsAt - Time.unscaledTime)) : 0;
    public static int FightSecondsLeft => Mathf.Max(0, Mathf.CeilToInt(fightSecondsLeft));
    public static bool BannerVisible => Time.unscaledTime < BannerUntil && BannerTitle.Length > 0;
    public static bool Available => Backend != null && Backend.Available;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Backend = null;
        ServerNowMs = null;
        RingLabelTile = null;
        RemotePlayerAtGUI = null;
        ResetAll();
    }

    public static void ResetAll()
    {
        invites.Clear();
        rings.Clear();
        ChallengePending = false;
        PendingSummary = PendingStatus = "";
        ClearDuel();
        BannerTitle = BannerDetail = "";
        BannerUntil = 0f;
        LastError = "";
        LastErrorAt = -100f;
        listRequestedAt = -1f;
        awaitingAcceptFrom = "";
        ClearDrawnRects();
    }

    static void ClearDuel()
    {
        InDuel = false;
        Phase = AODuelPhase.None;
        Ring = 0;
        TeamA = new string[0];
        TeamB = new string[0];
        Bet = Round = ScoreA = ScoreB = 0;
        Down = false;
        countdownEndsAt = 0f;
        fightSecondsLeft = 0f;
    }

    // La interfaz lo llama una vez por frame.
    public static void Tick()
    {
        float now = Time.unscaledTime;
        if (Phase == AODuelPhase.Countdown && now >= countdownEndsAt)
        {
            Phase = AODuelPhase.Fight;
            Chat("¡YA!");
            SetBanner("¡YA!", "", 1f);
        }
        else if (Phase == AODuelPhase.Fight)
            fightSecondsLeft = Mathf.Max(0f, fightSecondsLeft - Time.unscaledDeltaTime);

        for (int i = invites.Count - 1; i >= 0; i--)
            if (!invites[i].accepted && now > invites[i].expiresAt)
                invites.RemoveAt(i);

        if (listRequestedAt >= 0f && now - listRequestedAt > 3f)
        {
            if (!listAnswered) Chat("No hay retos activos.");
            listRequestedAt = -1f;
        }
    }

    // ---------- Eventos de la red (red.md §8) ----------

    public static void ReceiveInvite(string from, int level, string[] teamA, string[] teamB, int bet, int maxPotions, float expiresInSeconds)
    {
        if (string.IsNullOrWhiteSpace(from)) return;
        Invite invite = FindInvite(from);
        bool resent = invite != null;
        if (invite == null)
        {
            invite = new Invite();
            invites.Add(invite);
        }
        invite.from = from.Trim();
        invite.level = level;
        invite.teamA = teamA ?? new string[0];
        invite.teamB = teamB ?? new string[0];
        invite.bet = Mathf.Max(0, bet);
        invite.maxPotions = maxPotions;
        invite.expiresAt = Time.unscaledTime + Mathf.Max(1f, expiresInSeconds);
        if (resent) return;

        // Texto original de ModRetos.bas (CrearReto).
        Chat(invite.from + "(" + level + ") te invita a jugar el siguiente reto:");
        Chat(Summary(invite.teamA, invite.teamB, invite.bet, invite.maxPotions));
        Chat("Escribe /ACEPTAR " + invite.from.ToUpperInvariant() + " para participar en el reto.");
    }

    public static void ReceiveInviteClosed(string from, string reason)
    {
        Invite invite = FindInvite(from);
        bool wasInvited = invite != null;
        if (invite != null) invites.Remove(invite);
        bool mine = !wasInvited && ChallengePending;
        string why = (reason ?? "").Trim().ToLowerInvariant();

        if (why == "iniciado")
        {
            ChallengePending = false;
            return;
        }
        if (mine) ChallengePending = false;

        if (why == "cancelado")
            Chat("El reto ha sido cancelado.");
        else if (why == "rechazado" && mine)
            Chat(from + " rechazó el reto.");
        else if (why == "vencido")
            Chat(mine ? "Tu reto venció: no todos aceptaron a tiempo." : "La invitación de " + from + " venció.");
    }

    public static void ReceiveWaiting(string[] missing)
    {
        string text = missing == null || missing.Length == 0
            ? "Buscando una sala libre…"
            : "Esperando a " + Names(missing) + ".";
        PendingStatus = text;
        foreach (Invite invite in invites)
        {
            if (!invite.accepted) continue;
            invite.status = text;
            if (!invite.acceptAnnounced)
            {
                invite.acceptAnnounced = true;
                Chat("Has aceptado el reto de " + invite.from + ".");
            }
        }
    }

    public static void ReceiveStart(int ring, string[] teamA, string[] teamB, int bet, int maxSeconds)
    {
        bool resent = InDuel && Ring == ring;
        InDuel = true;
        Ring = ring;
        TeamA = teamA ?? new string[0];
        TeamB = teamB ?? new string[0];
        Bet = Mathf.Max(0, bet);
        ChallengePending = false;
        invites.Clear();
        awaitingAcceptFrom = "";
        if (resent) return;

        Round = ScoreA = ScoreB = 0;
        Down = false;
        Phase = AODuelPhase.BetweenRounds;
        fightSecondsLeft = Mathf.Max(0, maxSeconds);
        if (Bet > 0) Chat("Otorgas " + Gold(Bet) + " monedas de oro al pozo del reto.");
        Chat("¡Ha comenzado el reto!");
        Chat("Para admitir la derrota escribe /ABANDONAR.");
    }

    public static void ReceiveRoundStart(int round, int countdownSeconds, long serverTimeMs)
    {
        bool resent = InDuel && Round == round && (Phase == AODuelPhase.Countdown || Phase == AODuelPhase.Fight);
        InDuel = true;
        Round = round;
        Down = false;

        float late = 0f;
        if (ServerNowMs != null && serverTimeMs > 0)
            late = Mathf.Max(0f, (ServerNowMs() - serverTimeMs) / 1000f);
        countdownEndsAt = Time.unscaledTime + Mathf.Max(0f, countdownSeconds - late);
        Phase = countdownEndsAt > Time.unscaledTime ? AODuelPhase.Countdown : AODuelPhase.Fight;

        if (!resent) Chat("Comienza la ronda N°" + round);
    }

    // winnerTeam: 0 = equipo A, 1 = equipo B, -1 = ronda nula (decisión 10).
    public static void ReceiveRoundEnd(int round, int winnerTeam, int scoreA, int scoreB)
    {
        ScoreA = scoreA;
        ScoreB = scoreB;
        Phase = AODuelPhase.BetweenRounds;
        Down = false;
        if (winnerTeam == 0 || winnerTeam == 1)
        {
            string names = Names(winnerTeam == 0 ? TeamA : TeamB);
            Chat("Esta ronda es para " + names + ".");
            SetBanner("Ronda para " + names, "", 2.5f);
        }
        else
        {
            Chat("Ronda nula: se repite.");
            SetBanner("Ronda nula", "Se repite con conteo", 2.5f);
        }
    }

    // resultado: victoria · derrota · empate · tiempo · abandono (red.md §8).
    public static void ReceiveEnd(string result, int prize, int tax)
    {
        bool delayed = !InDuel;
        string kind = (result ?? "").Trim().ToLowerInvariant();
        string prefix = delayed ? "Resultado de tu último reto: " : "";
        string title, detail, line;

        switch (kind)
        {
            case "victoria":
                title = "¡VICTORIA!";
                detail = prize > 0 ? "+" + Gold(prize) + " monedas de oro" + TaxText(tax) : "Reto amistoso";
                line = prize > 0 ? "Has ganado " + Gold(prize) + " monedas de oro." : "Ganaste el reto.";
                break;
            case "derrota":
                title = "DERROTA";
                detail = Bet > 0 ? "Perdiste la apuesta" : "";
                line = "Perdiste el reto.";
                break;
            case "empate":
                title = "EMPATE";
                detail = prize > 0 ? "Recuperás " + Gold(prize) + " monedas de oro" + TaxText(tax) : "Nadie pudo vencer";
                line = prize > 0 ? "Nadie pudo vencer. Recuperás " + Gold(prize) + " monedas de oro." : "Nadie pudo vencer.";
                break;
            // Empate por tiempo (si alguien iba ganando, el servidor manda victoria o derrota).
            case "tiempo":
                title = "EMPATE";
                detail = prize > 0 ? "Se agotó el tiempo · recuperás " + Gold(prize) + " monedas de oro" : "Se agotó el tiempo";
                line = prize > 0
                    ? "Se ha agotado el tiempo del reto. Recuperás " + Gold(prize) + " monedas de oro."
                    : "Se ha agotado el tiempo del reto.";
                break;
            case "abandono":
                title = "ABANDONO";
                detail = "";
                line = "Has abandonado el reto.";
                break;
            default:
                title = "FIN DEL RETO";
                detail = "";
                line = "Terminó el reto.";
                break;
        }

        // En vivo, la derrota no suma una línea: el original solo avisaba al ganador.
        if (delayed || kind != "derrota") Chat(prefix + line);
        SetBanner(title, detail, 4f);
        ClearDuel();
    }

    public static void ReceiveDown(bool down)
    {
        if (InDuel) Down = down;
    }

    public static void ReceiveAnnounce(string text)
    {
        Chat(text);
    }

    // fase "libre" o vacía = la sala no tiene reto.
    public static void ReceiveRingState(int ring, string phase, string names, int bet, int round, int scoreA, int scoreB, int remaining)
    {
        string p = (phase ?? "").Trim().ToLowerInvariant();
        if (p.Length == 0 || p == "libre")
            rings.Remove(ring);
        else
        {
            if (!rings.TryGetValue(ring, out RingInfo info))
                rings[ring] = info = new RingInfo { ring = ring };
            info.phase = p;
            info.names = names ?? "";
            info.bet = bet;
            info.round = round;
            info.scoreA = scoreA;
            info.scoreB = scoreB;
            info.remaining = remaining;
            info.receivedAt = Time.unscaledTime;
        }

        if (listRequestedAt >= 0f && p.Length > 0 && p != "libre")
        {
            listAnswered = true;
            Chat(RingLine(rings[ring]));
        }
    }

    // Los errores llegan como result con ok=false y el texto del servidor tal cual.
    public static void ReceiveError(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        LastError = text.Trim();
        LastErrorAt = Time.unscaledTime;
        Chat(LastError);
        if (awaitingAcceptFrom.Length > 0)
        {
            Invite invite = FindInvite(awaitingAcceptFrom);
            if (invite != null)
            {
                invite.accepted = false;
                invite.status = "";
                invite.error = LastError;
            }
            awaitingAcceptFrom = "";
        }
    }

    // ---------- Órdenes de la interfaz ----------

    // players: sin el retador, en el orden de frmRetos (rival, compañero, rival, …).
    public static bool SendChallenge(string[] players, int bet, int maxPotions, out string error)
    {
        if (!CanSend(out error)) return false;
        if (InDuel)
        {
            error = "Ya te encuentras en un reto.";
            return false;
        }

        Backend.Challenge(players, bet, maxPotions);
        var mates = new List<string> { LocalName };
        var rivals = new List<string>();
        for (int i = 0; i < players.Length; i++)
            (i % 2 == 0 ? rivals : mates).Add(players[i]);

        ChallengePending = true;
        PendingSummary = Summary(mates.ToArray(), rivals.ToArray(), bet, maxPotions);
        PendingStatus = "Esperando a " + Names(players) + ".";
        Chat("Has enviado una solicitud para el siguiente reto:");
        Chat(PendingSummary);
        Chat("Escribe /CANCELAR para anular la solicitud.");
        return true;
    }

    public static bool SendAccept(string from, out string error)
    {
        if (!CanSend(out error)) return false;
        Invite invite = FindInvite(from);
        if (invite != null)
        {
            invite.accepted = true;
            invite.error = "";
            invite.status = "Aceptando…";
        }
        awaitingAcceptFrom = from ?? "";
        Backend.Accept(from);
        return true;
    }

    public static void SendReject(string from)
    {
        Invite invite = FindInvite(from);
        if (invite != null) invites.Remove(invite);
        if (Available) Backend.Reject(from);
    }

    public static bool SendCancel(out string error)
    {
        if (!CanSend(out error)) return false;
        Backend.Cancel();
        ChallengePending = false;
        return true;
    }

    public static bool SendAbandon(out string error)
    {
        if (!CanSend(out error)) return false;
        if (!InDuel)
        {
            error = "No estás en un reto.";
            return false;
        }
        Backend.Abandon();
        return true;
    }

    public static bool SendList(out string error)
    {
        if (!CanSend(out error)) return false;
        listRequestedAt = Time.unscaledTime;
        listAnswered = false;
        Chat("Retos activos:");
        Backend.List();
        return true;
    }

    static bool CanSend(out string error)
    {
        error = "";
        if (Available) return true;
        error = "Los retos se juegan en la Demo AO BATTLESERVER con amigos.";
        return false;
    }

    // ---------- Textos ----------

    public static Invite FindInvite(string from)
    {
        if (string.IsNullOrWhiteSpace(from)) return null;
        string key = from.Trim();
        foreach (Invite invite in invites)
            if (string.Equals(invite.from, key, StringComparison.OrdinalIgnoreCase))
                return invite;
        return null;
    }

    public static int MyTeam()
    {
        if (Contains(TeamA, LocalName)) return 0;
        if (Contains(TeamB, LocalName)) return 1;
        return -1;
    }

    // "A, B y C" como PonerNombres del original.
    public static string Names(string[] names)
    {
        if (names == null || names.Length == 0) return "";
        if (names.Length == 1) return names[0];
        return string.Join(", ", names, 0, names.Length - 1) + " y " + names[names.Length - 1];
    }

    public static string Summary(string[] teamA, string[] teamB, int bet, int maxPotions)
    {
        string text = Names(teamA) + " vs " + Names(teamB) + ". " +
            (bet > 0 ? "Apuesta: " + Gold(bet) + " monedas de oro." : "Reto amistoso, sin apuesta.");
        if (maxPotions >= 0) text += " Máximo " + maxPotions + " pociones rojas.";
        return text;
    }

    public static string RingLine(RingInfo info)
    {
        string text = "Sala " + info.ring + ": " + info.names;
        text += info.bet > 0 ? " · " + Gold(info.bet) + " oro" : " · amistoso";
        if (info.phase == "conteo") return text + " · por empezar";
        text += " · Ronda " + Mathf.Max(1, info.round) + " · " + info.scoreA + "–" + info.scoreB;
        int left = info.remaining;
        if (info.phase == "pelea")
            left = Mathf.Max(0, info.remaining - Mathf.FloorToInt(Time.unscaledTime - info.receivedAt));
        return text + " · " + Clock(left);
    }

    public static string Clock(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return (seconds / 60) + ":" + (seconds % 60).ToString("00");
    }

    // PonerPuntos del original: 10000 → 10.000.
    public static string Gold(long amount) =>
        amount.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');

    static string TaxText(int tax) => tax > 0 ? " (impuesto " + Gold(tax) + ")" : "";

    static bool Contains(string[] names, string name)
    {
        if (names == null || string.IsNullOrEmpty(name)) return false;
        foreach (string n in names)
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    static void SetBanner(string title, string detail, float seconds)
    {
        BannerTitle = title ?? "";
        BannerDetail = detail ?? "";
        BannerUntil = Time.unscaledTime + seconds;
    }

    static void Chat(string text) => AOInterfaceV0101.PushMessage(text);
}
