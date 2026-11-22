using System.Runtime.InteropServices;

namespace NetSpeedOverlay;

internal static unsafe partial class NativeMethods
{
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WM_RBUTTONDBLCLK = 0x0206;
    public const uint ULW_ALPHA = 0x00000002;
    public const byte AC_SRC_OVER = 0x00;
    public const byte AC_SRC_ALPHA = 0x01;

    [LibraryImport("iphlpapi.dll")]
    public static partial uint GetIfTable2(out nint table);

    [LibraryImport("iphlpapi.dll")]
    public static partial void FreeMibTable(nint memory);

    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hWnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hWnd, nint hDC);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateLayeredWindow(
        nint hwnd,
        nint hdcDst,
        nint pptDst,
        ref SIZE psize,
        nint hdcSrc,
        ref POINT pptSrc,
        uint crKey,
        ref BLENDFUNCTION pblend,
        uint dwFlags);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint h);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint ho);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateDIBSection(
        nint hdc,
        in BITMAPINFOHEADER pbmi,
        uint usage,
        out nint ppvBits,
        nint hSection,
        uint offset);
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SIZE
{
    public int Width;
    public int Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BLENDFUNCTION
{
    public byte BlendOp;
    public byte BlendFlags;
    public byte SourceConstantAlpha;
    public byte AlphaFormat;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BITMAPINFOHEADER
{
    public uint biSize;
    public int biWidth;
    public int biHeight;
    public ushort biPlanes;
    public ushort biBitCount;
    public uint biCompression;
    public uint biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public uint biClrUsed;
    public uint biClrImportant;
}

// نگاشت مستقیم فیلدهای ضروری از ساختار MIB_IF_ROW2 ویندوز بدون تخصیص حافظه اضافه
[StructLayout(LayoutKind.Explicit, Size = StructSize)]
internal struct MIB_IF_ROW2
{
    public const int StructSize = 1352;

    [FieldOffset(8)] public uint InterfaceIndex;
    [FieldOffset(1128)] public uint Type;
    [FieldOffset(1152)] public byte InterfaceAndOperStatusFlags;
    [FieldOffset(1156)] public uint OperStatus;
    [FieldOffset(1208)] public ulong InOctets;
    [FieldOffset(1280)] public ulong OutOctets;
}
