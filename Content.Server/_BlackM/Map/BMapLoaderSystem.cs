using Content.Server.GameTicking.Events;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.Map;

public sealed class BMapLoaderSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
    }

    private void OnRoundStart(RoundStartingEvent args)
    {
        LoadMap("/Maps/_BlackM/ZenMap/zen.yml");
        LoadMap("/Maps/_BlackM/HecuMap/hecuspawnmap.yml");
        LoadMap("/Maps/_BlackM/stationevacuation.yml");
    }

    private void LoadMap(string path)
    {
        var options = new DeserializationOptions
        {
            InitializeMaps = true,
            PauseMaps = false,
        };

        _mapLoader.TryLoadMap(new ResPath(path), out _, out _, options);
    }
}