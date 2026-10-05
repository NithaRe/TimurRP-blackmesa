using System.Numerics;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._BlackM.WelcomeMessage;

[Prototype("welcomeMessageSet")]
public sealed partial class WelcomeMessageSetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId Sender = "blackm-welcome-sender-default";

    [DataField]
    public LocId Title = "blackm-welcome-title-default";

    [DataField]
    public SpriteSpecifier Portrait = new SpriteSpecifier.Texture(new ResPath("/Textures/_BlackM/Interface/welcome_portrait.png"));

    [DataField]
    public Vector2i PortraitCropPos = Vector2i.Zero;

    [DataField]
    public Vector2i PortraitCropSize = Vector2i.Zero;

    [DataField]
    public List<ProtoId<JobPrototype>> Jobs = new();

    [DataField(required: true)]
    public List<LocId> Messages = new();

    [DataField]
    public float Duration = 6f;
}

[Serializable, NetSerializable]
public sealed class WelcomeMessageEvent : EntityEventArgs
{
    public string Sender;
    public string Title;
    public string Text;
    public SpriteSpecifier Portrait;
    public Vector2i CropPos;
    public Vector2i CropSize;
    public float Duration;

    public WelcomeMessageEvent(string sender, string title, string text, SpriteSpecifier portrait,
        Vector2i cropPos, Vector2i cropSize, float duration)
    {
        Sender = sender;
        Title = title;
        Text = text;
        Portrait = portrait;
        CropPos = cropPos;
        CropSize = cropSize;
        Duration = duration;
    }
}