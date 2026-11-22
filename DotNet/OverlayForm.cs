using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;

namespace NetSpeedOverlay;

internal sealed class OverlayForm : Form
{
    // تنظیمات پاسخ‌گویی و ابعاد (قابل تنظیم)
    private const int SampleIntervalMs = 250; // ۴ بار در ثانیه (۴ برابر سریع‌تر از نسخه ۱ ثانیه‌ای)
    private const float EmaAlpha = 0.68f;     // ضریب واکنش سریع؛ ۱.۰ یعنی بدون هیچ هموارسازی
    private const int LogicalWidth = 210;
    private const int LogicalHeight = 110;
    private const int HistoryLen = 30;
    private const float GraphLeft = 9.0f;
    private const float GraphBottom = LogicalHeight - 10.0f;
    private const float GraphHeight = 22.0f;

    // رنگ‌ها و قلم‌های از پیش ساخته‌شده (بدون تخصیص حافظه در هر فریم)
    private static readonly Color GreenColor = Color.FromArgb(255, 0, 255, 136);
    private static readonly Color BlueColor = Color.FromArgb(255, 0, 180, 255);

    private readonly SolidBrush _greenBrush = new(GreenColor);
    private readonly SolidBrush _blueBrush = new(BlueColor);
    private readonly SolidBrush _greenGlowBrush = new(Color.FromArgb(65, 0, 255, 136));
    private readonly SolidBrush _blueGlowBrush = new(Color.FromArgb(65, 0, 180, 255));

    private readonly Pen _penDlGlow = CreateRoundPen(Color.FromArgb(45, 0, 255, 136), 3.0f);
    private readonly Pen _penDlMain = CreateRoundPen(GreenColor, 1.3f);
    private readonly Pen _penUlGlow = CreateRoundPen(Color.FromArgb(45, 0, 180, 255), 3.0f);
    private readonly Pen _penUlMain = CreateRoundPen(BlueColor, 1.3f);

    private readonly Font _numFont = new("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly StringFormat _centerFormat = new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
        FormatFlags = StringFormatFlags.NoClip
    };

    private readonly RectangleF _dlTextRect = new(8, 28, 92, 28);
    private readonly RectangleF _ulTextRect = new(108, 28, 92, 28);
    private readonly RectangleF _dlGlowRect = new(8, 29, 92, 28);
    private readonly RectangleF _ulGlowRect = new(108, 29, 92, 28);

    // بافر حلقوی و نقاط از پیش تخصیص‌یافته برای نمودار
    private readonly float[] _dlHistory = new float[HistoryLen];
    private readonly float[] _ulHistory = new float[HistoryLen];
    private readonly float[] _xCoords = new float[HistoryLen];
    private readonly PointF[] _curvePoints = new PointF[HistoryLen];
    private readonly PointF[] _bezierPoints = new PointF[3 * (HistoryLen - 1) + 1];
    private readonly GraphicsPath _graphPath = new();

    private int _head;
    private float _dlSpeed;
    private float _ulSpeed;
    private int _lastDlCenti = -1;
    private int _lastUlCenti = -1;
    private string _dlText = "0.00";
    private string _ulText = "0.00";
    private bool _isIdle;

    private NetworkMonitor _monitor;
    private readonly System.Windows.Forms.Timer _timer;

    // منابع ثابت GDI و DIB Section برای رندر فوق‌سریع
    private nint _memDc;
    private nint _hBitmap;
    private nint _oldBitmap;
    private nint _dibBits;
    private Bitmap? _frameBitmap;
    private Graphics? _frameGraphics;
    private byte[]? _staticBgBytes;
    private int _pixelWidth;
    private int _pixelHeight;

    private bool _isDragging;
    private Point _dragOffset;

    public OverlayForm()
    {
        Text = "NetSpeed Overlay";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(LogicalWidth, LogicalHeight);

        float graphWidth = (LogicalWidth - 9.0f) - GraphLeft;
        float step = graphWidth / (HistoryLen - 1);
        for (int i = 0; i < HistoryLen; i++)
            _xCoords[i] = GraphLeft + i * step;

        // قرارگیری اولیه در گوشه پایین-راست صفحه بالای تسک‌بار
        Rectangle workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(workArea.Right - LogicalWidth - 16, workArea.Bottom - LogicalHeight - 16);

        _monitor.Sample(); // ثبت نقطه مبنای اولیه

        _timer = new System.Windows.Forms.Timer { Interval = SampleIntervalMs };
        _timer.Tick += OnSampleTick;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_LAYERED
                        | NativeMethods.WS_EX_TOPMOST
                        | NativeMethods.WS_EX_TOOLWINDOW
                        | NativeMethods.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RebuildSurface(DeviceDpi / 96f);
        RenderFrame();
        _timer.Start();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        RebuildSurface(e.DeviceDpiNew / 96f);
        RenderFrame();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_RBUTTONDBLCLK)
        {
            Close();
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragOffset = e.Location;
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging)
        {
            Point cursor = Cursor.Position;
            Location = new Point(cursor.X - _dragOffset.X, cursor.Y - _dragOffset.Y);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _isDragging)
        {
            _isDragging = false;
            Capture = false;
        }
    }

    private void OnSampleTick(object? sender, EventArgs e)
    {
        (float rawDl, float rawUl) = _monitor.Sample();

        // فیلتر هموارسازی سریع با صفرشدن تمیز هنگام توقف ترافیک
        _dlSpeed = rawDl < 0.005f && _dlSpeed < 0.015f
            ? 0f
            : _dlSpeed + EmaAlpha * (rawDl - _dlSpeed);

        _ulSpeed = rawUl < 0.005f && _ulSpeed < 0.015f
            ? 0f
            : _ulSpeed + EmaAlpha * (rawUl - _ulSpeed);

        _dlHistory[_head] = _dlSpeed;
        _ulHistory[_head] = _ulSpeed;
        _head = (_head + 1) % HistoryLen;

        float maxHist = 0f;
        for (int i = 0; i < HistoryLen; i++)
        {
            if (_dlHistory[i] > maxHist) maxHist = _dlHistory[i];
            if (_ulHistory[i] > maxHist) maxHist = _ulHistory[i];
        }

        if (_dlSpeed < 0.005f && _ulSpeed < 0.005f && maxHist < 0.005f)
        {
            if (!_isIdle)
            {
                _isIdle = true;
                _dlSpeed = 0f;
                _ulSpeed = 0f;
                RenderFrame();
            }
            return;
        }

        _isIdle = false;
        RenderFrame();
    }

    private unsafe void RebuildSurface(float dpiScale)
    {
        ReleaseSurface();

        _pixelWidth = Math.Max(1, (int)MathF.Round(LogicalWidth * dpiScale));
        _pixelHeight = Math.Max(1, (int)MathF.Round(LogicalHeight * dpiScale));

        nint screenDc = NativeMethods.GetDC(0);
        try
        {
            _memDc = NativeMethods.CreateCompatibleDC(screenDc);
            var bmi = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = _pixelWidth,
                biHeight = -_pixelHeight, // منفی برای ترتیب Top-Down منطبق با Format32bppPArgb
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0
            };

            _hBitmap = NativeMethods.CreateDIBSection(screenDc, in bmi, 0, out _dibBits, 0, 0);
            _oldBitmap = NativeMethods.SelectObject(_memDc, _hBitmap);
        }
        finally
        {
            NativeMethods.ReleaseDC(0, screenDc);
        }

        int stride = _pixelWidth * 4;
        _frameBitmap = new Bitmap(_pixelWidth, _pixelHeight, stride, PixelFormat.Format32bppPArgb, _dibBits);
        _frameGraphics = Graphics.FromImage(_frameBitmap);
        _frameGraphics.SmoothingMode = SmoothingMode.AntiAlias;
        _frameGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        _frameGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        _frameGraphics.ScaleTransform(dpiScale, dpiScale);

        // رسم یک‌باره پس‌زمینه ایستا و ذخیره بایت‌های خام آن
        DrawStaticBackground(_frameGraphics);
        _frameGraphics.Flush();

        int byteCount = stride * _pixelHeight;
        _staticBgBytes = new byte[byteCount];
        fixed (byte* dst = _staticBgBytes)
        {
            Buffer.MemoryCopy((void*)_dibBits, dst, byteCount, byteCount);
        }
    }

    private void DrawStaticBackground(Graphics g)
    {
        g.Clear(Color.Transparent);

        var rect = new RectangleF(4, 4, LogicalWidth - 8, LogicalHeight - 8);
        using GraphicsPath bgPath = CreateRoundedRectPath(rect, 12f);
        using var bgBrush = new LinearGradientBrush(
            new PointF(rect.Left, rect.Top),
            new PointF(rect.Right, rect.Bottom),
            Color.FromArgb(220, 18, 28, 45),
            Color.FromArgb(238, 10, 16, 28));
        using var borderPen = new Pen(Color.FromArgb(45, 255, 255, 255), 1.0f);

        g.FillPath(bgBrush, bgPath);
        g.DrawPath(borderPen, bgPath);

        // خط جداکننده میانی
        using var dividerPen = new Pen(Color.FromArgb(35, 255, 255, 255), 1.0f);
        g.DrawLine(dividerPen, LogicalWidth / 2f, 18f, LogicalWidth / 2f, 68f);

        // فلش‌های دانلود و آپلود
        DrawArrow(g, 28f, 19f, isDown: true, GreenColor);
        DrawArrow(g, 126f, 19f, isDown: false, BlueColor);

        // برچسب‌های ثابت
        using var labelFont = new Font("Segoe UI", 7f, FontStyle.Bold, GraphicsUnit.Point);
        using var unitFont = new Font("Segoe UI", 6.2f, FontStyle.Regular, GraphicsUnit.Point);
        using var textWhite = new SolidBrush(Color.FromArgb(235, 240, 250));
        using var unitBrush = new SolidBrush(Color.FromArgb(200, 210, 225));
        using var leftMidFormat = new StringFormat { LineAlignment = StringAlignment.Center };

        g.DrawString("Download", labelFont, textWhite, new RectangleF(34, 13, 66, 13), leftMidFormat);
        g.DrawString("Upload", labelFont, textWhite, new RectangleF(132, 13, 66, 13), leftMidFormat);

        g.DrawString("MB/s", unitFont, unitBrush, new RectangleF(8, 56, 92, 14), _centerFormat);
        g.DrawString("MB/s", unitFont, unitBrush, new RectangleF(108, 56, 92, 14), _centerFormat);

        // نشانگر وای‌فای گوشه بالا-راست
        DrawWifiBadge(g, rect, GreenColor);

        // نوارهای رنگی کنار نمودار
        using var leftBarPen = CreateRoundPen(GreenColor, 1.5f);
        using var rightBarPen = CreateRoundPen(BlueColor, 1.5f);
        g.DrawLine(leftBarPen, rect.Left + 1f, rect.Bottom - 28f, rect.Left + 1f, rect.Bottom - 11f);
        g.DrawLine(rightBarPen, rect.Right - 1f, rect.Bottom - 28f, rect.Right - 1f, rect.Bottom - 11f);
    }

    private static void DrawArrow(Graphics g, float x, float y, bool isDown, Color color)
    {
        using var pen = CreateRoundPen(color, 1.8f);
        if (isDown)
        {
            g.DrawLine(pen, x, y - 3f, x, y + 4f);
            g.DrawLine(pen, x - 3f, y + 1f, x, y + 4f);
            g.DrawLine(pen, x + 3f, y + 1f, x, y + 4f);
        }
        else
        {
            g.DrawLine(pen, x, y + 4f, x, y - 3f);
            g.DrawLine(pen, x - 3f, y, x, y - 3f);
            g.DrawLine(pen, x + 3f, y, x, y - 3f);
        }
    }

    private static void DrawWifiBadge(Graphics g, RectangleF rect, Color color)
    {
        float bx = rect.Right - 24f;
        float by = rect.Top;

        using var badgePath = new GraphicsPath();
        badgePath.AddLine(bx, by, rect.Right - 12f, by);
        badgePath.AddArc(rect.Right - 24f, by, 24f, 24f, 270f, 90f);
        badgePath.AddLine(rect.Right, by + 12f, rect.Right, by + 18f);
        badgePath.AddLine(rect.Right, by + 18f, bx + 6f, by + 18f);
        badgePath.AddArc(bx, by + 6f, 12f, 12f, 90f, 90f);
        badgePath.CloseFigure();

        using var badgeFill = new SolidBrush(Color.FromArgb(180, 8, 14, 24));
        using var badgePen = new Pen(Color.FromArgb(30, 255, 255, 255), 1f);
        g.FillPath(badgeFill, badgePath);
        g.DrawPath(badgePen, badgePath);

        float cx = rect.Right - 11f;
        float cy = rect.Top + 13f;
        using var arcPen = CreateRoundPen(color, 1.3f);
        foreach (float r in new[] { 3f, 6f, 9f })
            g.DrawArc(arcPen, cx - r, cy - r, r * 2f, r * 2f, 225f, 90f);

        using var dotBrush = new SolidBrush(color);
        g.FillEllipse(dotBrush, cx - 1f, cy - 1f, 2f, 2f);
    }

    private unsafe void RenderFrame()
    {
        if (!IsHandleCreated || _dibBits == 0 || _frameGraphics is null || _staticBgBytes is null)
            return;

        // ۱. بازگردانی پس‌زمینه ایستا با کپی مستقیم حافظه
        fixed (byte* src = _staticBgBytes)
        {
            Buffer.MemoryCopy(src, (void*)_dibBits, _staticBgBytes.Length, _staticBgBytes.Length);
        }

        // ۲. فرمت متن فقط در صورت تغییر عدد
        int dlCenti = (int)MathF.Round(_dlSpeed * 100f);
        if (dlCenti != _lastDlCenti)
        {
            _lastDlCenti = dlCenti;
            _dlText = (dlCenti * 0.01f).ToString("0.00", CultureInfo.InvariantCulture);
        }

        int ulCenti = (int)MathF.Round(_ulSpeed * 100f);
        if (ulCenti != _lastUlCenti)
        {
            _lastUlCenti = ulCenti;
            _ulText = (ulCenti * 0.01f).ToString("0.00", CultureInfo.InvariantCulture);
        }

        _frameGraphics.DrawString(_dlText, _numFont, _greenGlowBrush, _dlGlowRect, _centerFormat);
        _frameGraphics.DrawString(_dlText, _numFont, _greenBrush, _dlTextRect, _centerFormat);

        _frameGraphics.DrawString(_ulText, _numFont, _blueGlowBrush, _ulGlowRect, _centerFormat);
        _frameGraphics.DrawString(_ulText, _numFont, _blueBrush, _ulTextRect, _centerFormat);

        // ۳. محاسبه مقیاس نمودار
        float maxVal = 0.5f;
        for (int i = 0; i < HistoryLen; i++)
        {
            if (_dlHistory[i] > maxVal) maxVal = _dlHistory[i];
            if (_ulHistory[i] > maxVal) maxVal = _ulHistory[i];
        }

        float invMax = 1.0f / (maxVal * 1.2f);
        float dlBase = GraphBottom - 6.0f;
        float ulBase = GraphBottom - 1.0f;
        float dlScale = (GraphHeight - 7.0f) * invMax;
        float ulScale = (GraphHeight - 9.0f) * invMax;

        // ۴. رسم منحنی‌های نرم دانلود و آپلود
        DrawSmoothSeries(_frameGraphics, _dlHistory, dlBase, dlScale, _penDlGlow, _penDlMain);
        DrawSmoothSeries(_frameGraphics, _ulHistory, ulBase, ulScale, _penUlGlow, _penUlMain);

        _frameGraphics.Flush();

        // ۵. اعمال مستقیم روی پنجره شفاف ویندوز
        nint screenDc = NativeMethods.GetDC(0);
        try
        {
            var size = new SIZE { Width = _pixelWidth, Height = _pixelHeight };
            var srcPoint = new POINT { X = 0, Y = 0 };
            var blend = new BLENDFUNCTION
            {
                BlendOp = NativeMethods.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = NativeMethods.AC_SRC_ALPHA
            };

            NativeMethods.UpdateLayeredWindow(
                Handle, screenDc, 0, ref size, _memDc, ref srcPoint, 0, ref blend, NativeMethods.ULW_ALPHA);
        }
        finally
        {
            NativeMethods.ReleaseDC(0, screenDc);
        }
    }

    private void DrawSmoothSeries(Graphics g, float[] history, float baseY, float scale, Pen glowPen, Pen mainPen)
    {
        for (int i = 0; i < HistoryLen; i++)
        {
            int idx = (_head + i) % HistoryLen;
            _curvePoints[i] = new PointF(_xCoords[i], baseY - history[idx] * scale);
        }

        _bezierPoints[0] = _curvePoints[0];
        for (int i = 0; i < HistoryLen - 1; i++)
        {
            PointF p1 = _curvePoints[i];
            PointF p2 = _curvePoints[i + 1];
            float midX = (p1.X + p2.X) * 0.5f;
            int b = i * 3;
            _bezierPoints[b + 1] = new PointF(midX, p1.Y);
            _bezierPoints[b + 2] = new PointF(midX, p2.Y);
            _bezierPoints[b + 3] = p2;
        }

        _graphPath.Reset();
        _graphPath.AddBeziers(_bezierPoints);

        g.DrawPath(glowPen, _graphPath);
        g.DrawPath(mainPen, _graphPath);
    }

    private static GraphicsPath CreateRoundedRectPath(RectangleF r, float radius)
    {
        float d = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180f, 90f);
        path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
        path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    private static Pen CreateRoundPen(Color color, float width) => new(color, width)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round
    };

    private void ReleaseSurface()
    {
        _frameGraphics?.Dispose();
        _frameGraphics = null;
        _frameBitmap?.Dispose();
        _frameBitmap = null;

        if (_memDc != 0)
        {
            if (_oldBitmap != 0)
                NativeMethods.SelectObject(_memDc, _oldBitmap);
            if (_hBitmap != 0)
                NativeMethods.DeleteObject(_hBitmap);
            NativeMethods.DeleteDC(_memDc);
        }

        _memDc = 0;
        _hBitmap = 0;
        _oldBitmap = 0;
        _dibBits = 0;
        _staticBgBytes = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            ReleaseSurface();

            _greenBrush.Dispose();
            _blueBrush.Dispose();
            _greenGlowBrush.Dispose();
            _blueGlowBrush.Dispose();
            _penDlGlow.Dispose();
            _penDlMain.Dispose();
            _penUlGlow.Dispose();
            _penUlMain.Dispose();
            _numFont.Dispose();
            _centerFormat.Dispose();
            _graphPath.Dispose();
        }
        base.Dispose(disposing);
    }
}
