namespace DetourSharp;

/// <summary>Defines the valid states of a <see cref="Detour"/>.</summary>
public enum DetourState
{
    /// <summary>Specifies that the target address is not detoured.</summary>
    Detached,

    /// <summary>Specifies that the target address is detoured.</summary>
    Attached
}
