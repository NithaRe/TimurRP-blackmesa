using Content.Shared.Examine;
using Robust.Shared.Utility;

namespace Content.Shared._BlackM.CityScanner;

public sealed class ScannerPhotoSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScannerPhotoComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<ScannerPhotoComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Subject.Length == 0)
            return;

        args.PushMarkup(Loc.GetString("bm-scanner-photo-examine",
            ("subject", FormattedMessage.EscapeText(ent.Comp.Subject))));
    }
}
