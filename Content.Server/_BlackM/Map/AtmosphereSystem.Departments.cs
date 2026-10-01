using System.Linq;
using Content.Server.Atmos.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    public void TrimDepartmentAtmosphere(EntityUid grid, IReadOnlySet<Vector2i> tiles)
    {
        if (!TryComp(grid, out GridAtmosphereComponent? atmos))
            return;
        foreach (var index in atmos.Tiles.Keys.Where(i => !tiles.Contains(i)).ToArray())
            atmos.Tiles.Remove(index);
    }

    /// <summary>
    /// Copies saved roundstart gas into the department footprint. Adjacency is rebuilt after grid merging.
    /// </summary>
    public void CopyDepartmentAtmosphere(EntityUid source, EntityUid target,
        IReadOnlyDictionary<Vector2i, Vector2i> indices)
    {
        if (!TryComp(source, out GridAtmosphereComponent? sourceAtmos))
            return;

        var targetAtmos = EnsureComp<GridAtmosphereComponent>(target);
        foreach (var (from, to) in indices)
        {
            var tile = GetOrNewTile(target, targetAtmos, to);
            if (sourceAtmos.Tiles.TryGetValue(from, out var original))
            {
                tile.Air = original.Air?.Clone();
                tile.Temperature = original.Temperature;
                tile.HeatCapacity = original.HeatCapacity;
            }
            else
            {
                tile.Air = null;
            }

            InvalidateTile((target, targetAtmos), to);
            InvalidateVisuals(target, to);
        }
    }
}
