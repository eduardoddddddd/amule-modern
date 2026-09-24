using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AmuleModern.Desktop;

internal static class Ed2kProtocol
{
    private const string KeyPath = @"Software\Classes\ed2k";

    public static bool Supported => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    public static string? CurrentExecutable()
    {
        if (OperatingSystem.IsMacOS())
        {
            string? path = Environment.ProcessPath;
            return path != null && path.Contains(".app/Contents/MacOS/", StringComparison.Ordinal)
                ? "org.amule-modern.desktop"
                : null;
        }
        if (!OperatingSystem.IsWindows()) return null;
        return string.IsNullOrWhiteSpace(Environment.ProcessPath) ? null : Environment.ProcessPath;
    }

    public static ProtocolSnapshot Read()
    {
        if (OperatingSystem.IsWindows()) return ReadWindows();
        if (OperatingSystem.IsMacOS()) return ReadMac();
        return new(false, null, null, false);
    }

    public static void Apply(AssociationResult result, string exePath)
    {
        switch (result.Action)
        {
            case AssociationAction.WriteOurs:
                Write(Ed2kAssociation.Description, Ed2kAssociation.HandlerFor(exePath), true);
                break;
            case AssociationAction.Restore when result.Restore is ProtocolSnapshot previous:
                if (!previous.Exists) Delete();
                else Write(previous.Description ?? "", previous.Command ?? "", previous.UrlProtocol);
                break;
            case AssociationAction.Delete:
                Delete();
                break;
        }
        UiSettings.SaveEd2k(result.OwnedCommand, result.Previous);
    }

    [SupportedOSPlatform("windows")]
    public static string? ForeignDefaultWarning()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\ed2k\UserChoice");
            string? progId = key?.GetValue("ProgId") as string;
            if (string.IsNullOrWhiteSpace(progId) || progId.Equals("ed2k", StringComparison.OrdinalIgnoreCase)) return null;
            return "Windows sigue abriendo el enlace con otro programa hasta que lo confirmes en Aplicaciones predeterminadas. No cambiamos esa elección por nuestra cuenta.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static ProtocolSnapshot ReadWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (key is null) return new(false, null, null, false);
        using var commandKey = key.OpenSubKey(@"shell\open\command");
        return new(true, key.GetValue("") as string, commandKey?.GetValue("") as string, key.GetValue("URL Protocol") is not null);
    }

    private static void Write(string description, string command, bool urlProtocol)
    {
        if (OperatingSystem.IsWindows())
        {
            WriteWindows(description, command, urlProtocol);
            return;
        }
        if (OperatingSystem.IsMacOS()) MacWrite(command);
    }

    private static void Delete()
    {
        if (OperatingSystem.IsWindows())
        {
            DeleteWindows();
            return;
        }
        if (OperatingSystem.IsMacOS()) MacClear();
    }

    [SupportedOSPlatform("windows")]
    private static void WriteWindows(string description, string command, bool urlProtocol)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue("", description);
        if (urlProtocol) key.SetValue("URL Protocol", "");
        using var commandKey = key.CreateSubKey(@"shell\open\command");
        commandKey.SetValue("", command);
    }

    [SupportedOSPlatform("windows")]
    private static void DeleteWindows() => Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false);

    private static ProtocolSnapshot ReadMac() => MacLaunch.Read();
    private static void MacWrite(string command) => MacLaunch.Set(Ed2kAssociation.ExecutableFromCommand(command));
    private static void MacClear() => MacLaunch.Set(null);
}

internal static class MacLaunch
{
    private const string Scheme = "ed2k";
    private const uint Utf8 = 0x08000100;

    public static ProtocolSnapshot Read()
    {
        IntPtr scheme = IntPtr.Zero, handler = IntPtr.Zero;
        try
        {
            scheme = CFStringCreateWithCString(IntPtr.Zero, Scheme, Utf8);
            handler = LSCopyDefaultHandlerForURLScheme(scheme);
            if (handler == IntPtr.Zero) return new(false, null, null, false);
            return new(true, null, CFStringToString(handler), true);
        }
        finally
        {
            if (handler != IntPtr.Zero) CFRelease(handler);
            if (scheme != IntPtr.Zero) CFRelease(scheme);
        }
    }

    public static void Set(string? bundleOrExe)
    {
        string? handler = string.IsNullOrWhiteSpace(bundleOrExe) ? null : bundleOrExe.Contains('/')
            ? "org.amule-modern.desktop"
            : bundleOrExe;
        IntPtr scheme = IntPtr.Zero, bundle = IntPtr.Zero;
        try
        {
            scheme = CFStringCreateWithCString(IntPtr.Zero, Scheme, Utf8);
            if (handler != null) bundle = CFStringCreateWithCString(IntPtr.Zero, handler, Utf8);
            int status = LSSetDefaultHandlerForURLScheme(scheme, bundle);
            if (status != 0) throw new InvalidOperationException("macOS no dejó cambiar el esquema ed2k (" + status + ").");
        }
        finally
        {
            if (bundle != IntPtr.Zero) CFRelease(bundle);
            if (scheme != IntPtr.Zero) CFRelease(scheme);
        }
    }

    private static string CFStringToString(IntPtr value)
    {
        IntPtr utf = CFStringGetCStringPtr(value, Utf8);
        if (utf != IntPtr.Zero) return System.Runtime.InteropServices.Marshal.PtrToStringUTF8(utf) ?? "";
        int length = (int)CFStringGetLength(value);
        var buffer = new byte[length * 4 + 1];
        return CFStringGetCString(value, buffer, buffer.Length, Utf8)
            ? System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0')
            : "";
    }

    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string text, uint encoding);
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr value);
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringGetCStringPtr(IntPtr value, uint encoding);
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFStringGetLength(IntPtr value);
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern bool CFStringGetCString(IntPtr value, byte[] buffer, nint capacity, uint encoding);
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreServices.framework/CoreServices")]
    private static extern IntPtr LSCopyDefaultHandlerForURLScheme(IntPtr scheme);
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreServices.framework/CoreServices")]
    private static extern int LSSetDefaultHandlerForURLScheme(IntPtr scheme, IntPtr handler);
}
