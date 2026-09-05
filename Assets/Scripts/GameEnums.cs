/// <summary>The two modes from the GDD: one player versus the coop, or two players versus each other.</summary>
public enum GameMode
{
    Single,
    Duo
}

/// <summary>Where the match currently is. The menu drives everything else.</summary>
public enum GamePhase
{
    Menu,
    Playing,
    Won,
    Lost
}

/// <summary>
/// The egg kinds from the GDD. Normal eggs are the ones you catch and throw
/// back; the rest are power ups. Freeze, Reverse and Golden are Duo only.
/// </summary>
public enum EggType
{
    Normal,
    Speed,
    Freeze,
    Reverse,
    Golden
}
