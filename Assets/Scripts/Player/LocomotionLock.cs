using System.Collections.Generic;
using UnityEngine;

// One shared on/off switch for the player's locomotion providers. The shop
// and the menus each used to snapshot "was movement on?" before turning it
// off, then restore that snapshot -- but whenever one opened while the other
// already had movement off, it snapshotted the OTHER system's "off" and
// restored it later, leaving the player stuck (e.g. unable to move after
// leaving the shop). Now each system just takes or drops a lock: movement is
// off while anyone holds one, and goes back to its normal state once the
// last lock is dropped.
public static class LocomotionLock
{
    static readonly HashSet<object> owners = new HashSet<object>();

    // Each provider's enabled state from before the first lock was taken
    static readonly Dictionary<Behaviour, bool> normalState = new Dictionary<Behaviour, bool>();

    public static bool IsLocked => owners.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        owners.Clear();
        normalState.Clear();
    }

    public static void Lock(object owner, Behaviour[] providers)
    {
        if (providers != null)
        {
            foreach (Behaviour provider in providers)
            {
                if (provider == null) continue;
                if (!normalState.ContainsKey(provider))
                    normalState[provider] = provider.enabled;
                provider.enabled = false;
            }
        }

        owners.Add(owner);
    }

    public static void Unlock(object owner)
    {
        if (!owners.Remove(owner) || owners.Count > 0) return;

        foreach (KeyValuePair<Behaviour, bool> entry in normalState)
            if (entry.Key != null) entry.Key.enabled = entry.Value;

        normalState.Clear();
    }
}
