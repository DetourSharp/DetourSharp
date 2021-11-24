# DetourSharp
DetourSharp is a fully managed .NET library for interception of binary functions.

# Sample
```cs
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using DetourSharp;

unsafe
{
    // Before we install any hooks into it, we need to ensure
    // the target method has been jitted so it can be modified.
    RuntimeHelpers.PrepareMethod(typeof(Methods)
        .GetMethod(nameof(Methods.Target))!
        .MethodHandle
    );

    var pTarget = (delegate* unmanaged<void>)&Methods.Target;
    var pDetour = (delegate* unmanaged<void>)&Methods.Detour;

    using (var detour = new Detour(pTarget, pDetour))
    {
        // The Detour class provides a trampoline that can be used to bypass the hook.
        var pBypass = (delegate* unmanaged<void>)detour.TrampolineAddress;

        // Simply creating a Detour will not attach the hook to the target address.
        // In order to do that, you need to call the Attach() method it provides.
        pTarget(); // "Target"
        pDetour(); // "Detour"
        pBypass(); // "Target"

        // Once Attach() has been called, our method is hooked.
        detour.Attach();
        pTarget(); // "Detour"
        pDetour(); // "Detour"
        pBypass(); // "Target"
    }

    // When the Detour is disposed, it will be automatically detached.
    pTarget(); // "Target"
    pDetour(); // "Detour"
}

static class Methods
{
    [UnmanagedCallersOnly]
    public static void Target() => Console.WriteLine(nameof(Target));

    [UnmanagedCallersOnly]
    public static void Detour() => Console.WriteLine(nameof(Detour));
}
```
