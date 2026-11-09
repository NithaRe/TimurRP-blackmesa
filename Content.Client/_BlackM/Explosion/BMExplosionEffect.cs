using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._BlackM.Explosion;

public sealed class BMExplosionEffect
{
    public EntityUid Source;
    public MapId Map;
    public Vector2 Epicenter;
    public float Start;
    public int Iterations;
    public float Scale;
    public int Reached;
    public bool Lod;

    public Color Hot;
    public Color Mid;
    public Color Late;
    public Color Smoke;
    public Color LightColor;

    public EntityUid Light;
    public float LightPeak;
    public float LightSeed;

    public readonly List<BMBurnGroup> Groups = new();
    public readonly List<BMBurnTile> Tiles = new();
    public readonly List<BMParticle> Particles = new();
    public List<BMBurnTile>[] ByIteration = Array.Empty<List<BMBurnTile>>();
}

public sealed class BMBurnGroup
{
    public EntityUid Grid;
    public float TileSize = 1f;
    public Matrix3x2 Matrix = Matrix3x2.Identity;
    public Vector2 LocalEpicenter;
    public bool Valid = true;
}

public sealed class BMBurnTile
{
    public int Group;
    public Vector2i Index;
    public Vector2 Local;
    public int Iteration;
    public float Heat;
    public float Ignite = float.MaxValue;
    public float Jitter;
    public float Life;
    public float Seed;
    public float Spin;
    public int Variant;
    public bool Lit;
    public float SmokeAt = -1f;
    public float ScorchAt = -1f;
}

public enum BMParticleKind : byte
{
    Spark,
    Ember,
    Debris,
    Smoke,
}

public sealed class BMParticle
{
    public BMParticleKind Kind;
    // -1 means world space, otherwise an index into Groups.
    public int Group = -1;
    public Vector2 Pos;
    public Vector2 Vel;
    public float Age;
    public float Life;
    public float Drag;
    public float Size;
    public float EndSize;
    public float Rot;
    public float Spin;
    public float Alpha = 1f;
    public float Seed;
    public int Variant;
}
