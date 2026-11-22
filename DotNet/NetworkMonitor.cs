using System.Diagnostics;

namespace NetSpeedOverlay;

internal unsafe struct NetworkMonitor
{
    private const uint IfOperStatusUp = 1;
    private const uint IfTypeLoopback = 24;
    private const byte FlagHardwareInterface = 0x01;
    private const byte FlagFilterInterface = 0x02;

    private ulong _lastRx;
    private ulong _lastTx;
    private uint _lastSignature;
    private long _lastTimestamp;
    private bool _initialized;

    public (float DownloadMBs, float UploadMBs) Sample()
    {
        ReadTotals(out ulong rx, out ulong tx, out uint signature);
        long now = Stopwatch.GetTimestamp();

        // در اولین اجرا یا هنگام قطع/وصل شدن کارت شبکه، مبنا را ریست می‌کنیم تا عدد کاذب ثبت نشود
        if (!_initialized || signature != _lastSignature)
        {
            _lastRx = rx;
            _lastTx = tx;
            _lastSignature = signature;
            _lastTimestamp = now;
            _initialized = true;
            return (0f, 0f);
        }

        double dt = Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
        _lastTimestamp = now;

        ulong dRx = rx >= _lastRx ? rx - _lastRx : 0;
        ulong dTx = tx >= _lastTx ? tx - _lastTx : 0;
        _lastRx = rx;
        _lastTx = tx;

        if (dt <= 0.005)
            return (0f, 0f);

        double invFactor = 1.0 / (1048576.0 * dt);
        return ((float)(dRx * invFactor), (float)(dTx * invFactor));
    }

    private static void ReadTotals(out ulong totalRx, out ulong totalTx, out uint signature)
    {
        totalRx = 0;
        totalTx = 0;
        signature = 0;

        if (NativeMethods.GetIfTable2(out nint tablePtr) != 0 || tablePtr == 0)
            return;

        try
        {
            uint count = *(uint*)tablePtr;
            // فیلد Table در MIB_IF_TABLE2 به دلیل تراز ۸ بایتی از آفست ۸ شروع می‌شود
            byte* rowsBase = (byte*)tablePtr + 8;

            ulong hwRx = 0, hwTx = 0;
            uint hwSig = 0;
            ulong fbRx = 0, fbTx = 0;
            uint fbSig = 0;
            bool hasHardware = false;

            for (uint i = 0; i < count; i++)
            {
                ref readonly MIB_IF_ROW2 row = ref *(MIB_IF_ROW2*)(rowsBase + (nint)i * MIB_IF_ROW2.StructSize);

                if (row.OperStatus != IfOperStatusUp ||
                    row.Type == IfTypeLoopback ||
                    (row.InterfaceAndOperStatusFlags & FlagFilterInterface) != 0)
                {
                    continue;
                }

                if ((row.InterfaceAndOperStatusFlags & FlagHardwareInterface) != 0)
                {
                    hasHardware = true;
                    hwRx += row.InOctets;
                    hwTx += row.OutOctets;
                    hwSig ^= row.InterfaceIndex * 2654435761u;
                }
                else
                {
                    fbRx += row.InOctets;
                    fbTx += row.OutOctets;
                    fbSig ^= row.InterfaceIndex * 2654435761u;
                }
            }

            if (hasHardware)
            {
                totalRx = hwRx;
                totalTx = hwTx;
                signature = hwSig;
            }
            else
            {
                totalRx = fbRx;
                totalTx = fbTx;
                signature = fbSig;
            }
        }
        finally
        {
            NativeMethods.FreeMibTable(tablePtr);
        }
    }
}
