using System.Linq;
using Content.Server.GameTicking.Events;
using Content.Server.Maps;
using Content.Server.Station.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.Map;

public sealed class BMapLoaderSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
    }

    private void OnRoundStart(RoundStartingEvent args)
    {
        LoadMap("/Maps/_BlackM/ZenMap/zen.yml");
        LoadMap("/Maps/_BlackM/HecuMap/hecuspawnmap.yml");
        LoadMap("/Maps/_BlackM/evacmap.yml");
    }

    private void LoadMap(string path)
    {
        if (path == "/Maps/_BlackM/ZenMap/zen.yml")
        {
            if (!_mapLoader.TryLoadMap(new ResPath(path), out _, out var grids, new DeserializationOptions
            {
                InitializeMaps = true,
                PauseMaps = false,
            }))
            {
                return;
            }

            var prototype = _prototypeManager.Index<GameMapPrototype>("ZenBlackM");
            var config = prototype.Stations["ZenStation"];
            _station.InitializeNewStation(config, grids.Select(grid => grid.Owner));

            return;
        }

        _mapLoader.TryLoadMap(new ResPath(path), out _, out _, new DeserializationOptions
        {
            InitializeMaps = true,
            PauseMaps = false,
        });
    }
}