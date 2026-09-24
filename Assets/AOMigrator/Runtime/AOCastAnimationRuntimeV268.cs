using UnityEngine;

public static class AOCastAnimationRuntimeV268
{
    public static void PlayPlayer(GameObject caster, AOSpellDatabaseV120.SpellDef spell)
    {
        if (caster == null) return;
        AOCharacterRenderer visual = caster.GetComponentInChildren<AOCharacterRenderer>(true);
        if (visual == null) return;
        visual.PlayGenericCast(DurationFor(spell), 12f);
    }

    public static bool PlayNpc(GameObject npcObject, AOSpellDatabaseV120.SpellDef spell = null)
    {
        if (npcObject == null) return false;
        AONPCMetadata meta = npcObject.GetComponent<AONPCMetadata>();
        AOCharacterRenderer visual = npcObject.GetComponentInChildren<AOCharacterRenderer>(true);
        if (meta == null || visual == null) return false;

        if (!AOCastAnimationDatabaseV268.TryBuildNpc(
                meta.NpcIndex,
                out AOCharacterRenderer.DirectionVisual[] dirs,
                out float fps,
                out float headX,
                out float headY,
                out float bodyX))
            return false;

        int frameCount = 1;
        if (dirs != null)
            foreach (AOCharacterRenderer.DirectionVisual d in dirs)
                if (d != null && d.body != null)
                    frameCount = Mathf.Max(frameCount, d.body.Length);

        float duration = Mathf.Clamp(frameCount / Mathf.Max(1f, fps), .28f, .9f);
        if (spell != null && spell.particleTravel > 0)
            duration = Mathf.Min(duration, .55f);

        visual.PlayCastAnimation(dirs, fps, duration, headX, headY, bodyX, false);
        return true;
    }


    public static bool PlayNearestNpc(Vector3 from, AOSpellDatabaseV120.SpellDef spell)
    {
        float best = .16f;
        AONPCMetadata bestNpc = null;
        foreach (AONPCMetadata npc in Object.FindObjectsByType<AONPCMetadata>(FindObjectsSortMode.None))
        {
            if (npc == null) continue;
            float d = (npc.transform.position - from).sqrMagnitude;
            if (d <= best)
            {
                best = d;
                bestNpc = npc;
            }
        }
        return bestNpc != null && PlayNpc(bestNpc.gameObject, spell);
    }

    public static void PlayNearestCaster(Vector3 from, AOSpellDatabaseV120.SpellDef spell)
    {
        float best = .16f;
        AONPCMetadata bestNpc = null;
        foreach (AONPCMetadata npc in Object.FindObjectsByType<AONPCMetadata>(FindObjectsSortMode.None))
        {
            if (npc == null) continue;
            float d = (npc.transform.position - from).sqrMagnitude;
            if (d <= best)
            {
                best = d;
                bestNpc = npc;
            }
        }
        if (bestNpc != null)
        {
            PlayNpc(bestNpc.gameObject, spell);
            return;
        }

        AOTestPlayer player = Object.FindFirstObjectByType<AOTestPlayer>();
        if (player != null && (player.transform.position - from).sqrMagnitude <= .16f)
            PlayPlayer(player.gameObject, spell);
    }

    public static void Stop(GameObject caster)
    {
        if (caster == null) return;
        AOCharacterRenderer visual = caster.GetComponentInChildren<AOCharacterRenderer>(true);
        visual?.StopCastAnimation();
    }

    static float DurationFor(AOSpellDatabaseV120.SpellDef spell)
    {
        if (spell == null) return .42f;
        if (spell.particleTravel > 0) return .34f;
        if (spell.type == 5 || spell.areaRadius > 0) return .48f;
        if (spell.duration > 1) return .46f;
        return .40f;
    }
}
