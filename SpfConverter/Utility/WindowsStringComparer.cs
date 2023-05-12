using System.Runtime.InteropServices;

namespace SpfConverter.Utility;

public class WindowsStringComparer : IComparer<string>
{
    internal static class SafeNativeMethods
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        #pragma warning disable SYSLIB1054
        public static extern int StrCmpLogicalW(string? psz1, string? psz2);
        #pragma warning restore SYSLIB1054
    }

    public int Compare(string? a, string? b) => SafeNativeMethods.StrCmpLogicalW(a, b);
}