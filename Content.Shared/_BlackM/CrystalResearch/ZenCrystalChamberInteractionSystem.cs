using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Interaction;

namespace Content.Shared._BlackM.CrystalResearch;

/// <summary>Routes lid clicks identically on the predicting client and authoritative server.</summary>
public sealed class ZenCrystalChamberInteractionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZenCrystalChamberComponent, InteractHandEvent>(OnHand,
            after: new[] { typeof(ItemSlotsSystem) });
        SubscribeLocalEvent<ZenCrystalChamberComponent, InteractUsingEvent>(OnUsing,
            after: new[] { typeof(ItemSlotsSystem) });
    }

    private void OnHand(Entity<ZenCrystalChamberComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Resonator || !ent.Comp.LidOpen)
            return;

        args.Handled = true;
        var close = new ZenCrystalCloseLidEvent(args.User);
        RaiseLocalEvent(ent, ref close);
    }

    private void OnUsing(Entity<ZenCrystalChamberComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Resonator || !ent.Comp.LidOpen ||
            HasComp<ZenCrystalResearchComponent>(args.Used) || HasComp<ZenGrowthMediumComponent>(args.Used) ||
            HasComp<ZenCrystalLabToolComponent>(args.Used) || HasComp<NetworkConfiguratorComponent>(args.Used))
            return;

        args.Handled = true;
        var close = new ZenCrystalCloseLidEvent(args.User);
        RaiseLocalEvent(ent, ref close);
    }
}

[ByRefEvent]
public readonly record struct ZenCrystalCloseLidEvent(EntityUid User);
