using Reactor.Motion;
using System.Runtime.CompilerServices;

namespace Reactor.Motion.Internals;

/// <summary>
/// One player owns each property of a view. A new player takes the properties
/// it writes and leaves the rest of the previous player running. A player with
/// nothing left is disposed, including a run that is still in flight.
/// </summary>
internal static class MotionPlayers
{
    static readonly ConditionalWeakTable<VisualElement, Dictionary<BindableProperty, MotionPlayer>> Owners = [];
    static readonly Lock Gate = new();

    public static void Register(VisualElement view, MotionPlayer player)
    {
        var claims = new HashSet<BindableProperty>();
        player.CollectClaims(view, claims);
        if (claims.Count == 0)
            return;

        var stolen = new List<Steal>();
        lock (Gate)
            Take(view, player, claims, stolen);

        if (stolen.Count == 0)
            return;

        System.Diagnostics.Debug.WriteLine("MotionPlayers: replacing a property on a view.");
        HandOff(player, stolen);
    }

    public static void Unregister(VisualElement view, MotionPlayer player)
    {
        lock (Gate)
        {
            if (!Owners.TryGetValue(view, out var map))
                return;

            List<BindableProperty>? drop = null;
            foreach (var (property, owner) in map)
            {
                if (ReferenceEquals(owner, player))
                    (drop ??= []).Add(property);
            }

            if (drop is null)
                return;

            foreach (var property in drop)
                map.Remove(property);
            if (map.Count == 0)
                Owners.Remove(view);
        }
    }

    static void Take(
        VisualElement view,
        MotionPlayer player,
        HashSet<BindableProperty> claims,
        List<Steal> stolen)
    {
        if (!Owners.TryGetValue(view, out var map))
        {
            map = [];
            Owners.Add(view, map);
        }

        var victims = new Dictionary<MotionPlayer, HashSet<BindableProperty>>();
        foreach (var property in claims)
        {
            if (map.TryGetValue(property, out var current) && !ReferenceEquals(current, player))
            {
                if (!victims.TryGetValue(current, out var taken))
                {
                    taken = [];
                    victims.Add(current, taken);
                }

                taken.Add(property);
            }

            map[property] = player;
        }

        foreach (var (victim, taken) in victims)
        {
            // A path or transform writes several properties. Taking one frees the rest.
            var side = new HashSet<BindableProperty>();
            victim.CollectSideProperties(view, taken, side);
            foreach (var property in side)
            {
                if (map.TryGetValue(property, out var owner) && ReferenceEquals(owner, victim))
                    map.Remove(property);
            }

            stolen.Add(new Steal(victim, view, taken));
        }
    }

    static void HandOff(MotionPlayer player, List<Steal> stolen)
    {
        var speeds = new List<PoseSpeed>();
        foreach (var steal in stolen)
        {
            speeds.AddRange(steal.Victim.SampleSpeeds());
            if (steal.Victim.Release(steal.View, steal.Properties))
                steal.Victim.Dispose();
        }

        player.AcceptHandoff(speeds);
    }

    readonly record struct Steal(MotionPlayer Victim, VisualElement View, HashSet<BindableProperty> Properties);
}
