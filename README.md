<div align="center">

  <img src="icon.ico" alt="Atena NetSpeed Overlay Logo" width="110" height="110" />

# 🌐 Atena NetSpeed Overlay
**A sleek, ultra-lightweight glassmorphism network speed monitor widget for Windows — available in both .NET 11 (C#) and Python (PyQt6)**

![Version](https://img.shields.io/badge/Version-1.0.0-00ff88?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-11.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-Win32%20%2F%20GDI+-239120?style=for-the-badge&logo=csharp&logoColor=white)
![Python](https://img.shields.io/badge/Python-3.10+-00b4ff?style=for-the-badge&logo=python&logoColor=white)
![PyQt6](https://img.shields.io/badge/GUI-PyQt6-41cd52?style=for-the-badge&logo=qt&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)

<br>

<!-- Replace 'screenshot.png' with the actual path/name of your preview image in the repo -->

</div>

---

## ✨ Features

* 🎨 **Modern Glassmorphism UI:** Translucent dark glass aesthetic paired with vibrant neon green (Download) and cyan blue (Upload) glowing accents.
* 📈 **Smooth Real-Time Wave Graphs:** Dynamic Bezier-curve line charts that visualize network traffic fluctuations in real time.
* ⚡ **Ultra-Low Resource Usage (Zero Bloat):**
  * **Static Background Caching:** All static elements, gradients, icons, and labels are rendered only once at startup (cached DIB Section in `.NET 11`, `QPixmap` in `Python`).
  * **Smart Idle Mode:** Automatically pauses screen repainting when network traffic is idle, dropping CPU usage to **0.0%**.
* 🚀 **Low-Latency Engine (.NET 11 Edition):**
  * Polls network counters every **250ms** (4x faster than the 1s Python version) using high-precision `Stopwatch` timing and fast EMA smoothing.
  * Reads 64-bit interface counters directly from Windows (`iphlpapi.dll` / `GetIfTable2`) with zero heap allocation, avoiding duplicate virtual-adapter counts and reconnect spikes.
  * Native `UpdateLayeredWindow` transparency with **Per-Monitor V2 High-DPI** support and single-instance protection.
* 📌 **Compact & Non-Intrusive:** Frameless `210x110` always-on-top window that stays out of your Windows taskbar.
* 🖱️ **Simple Mouse Controls:**
  * **Left-Click + Drag:** Move the overlay anywhere on your screen.
  * **Double Right-Click:** Instantly close the application.

---

## ⚖️ .NET 11 vs. Python Edition

This repository hosts both implementations side by side so you can choose between native Windows performance and quick Python customization:

| Feature | .NET 11 Edition *(Recommended)* | Python Edition *(Original)* |
| :--- | :--- | :--- |
| **Update Latency** | **250ms** (4 Hz + fast EMA smoothing) | 1000ms (1 Hz) |
| **Network Monitoring** | Native Win32 `iphlpapi.dll` (`GetIfTable2`) | `psutil.net_io_counters()` |
| **Rendering Pipeline** | Native `UpdateLayeredWindow` + DIB Section | `PyQt6` (`QPainter` + `QPixmap` cache) |
| **High-DPI Scaling** | Per-Monitor V2 DPI-aware | Qt default scaling |
| **Resource Footprint** | Minimal RAM, zero-allocation polling, 0% idle CPU | Low RAM, 0% idle CPU |
| **Distribution** | Inno Setup installer / Single-file `.exe` | PyInstaller standalone `.exe` / script |

---

## 📁 Repository Structure

```text
Atena-NetSpeed-Overlay/
├── DotNet/                      # .NET 11 (C#) Edition
│   ├── NetSpeedOverlay.csproj
│   ├── Program.cs
│   ├── NativeMethods.cs
│   ├── NetworkMonitor.cs
│   ├── OverlayForm.cs
│   ├── NetSpeedOverlay.iss      # Inno Setup installer script
│   └── icon.ico
├── Python/                      # Python (PyQt6) Edition
│   ├── app.py
│   ├── app.spec                 # PyInstaller build spec
│   └── icon.ico
├── icon.ico
└── README.md
```

---

## 🚀 Getting Started

### Option 1: Pre-built Releases (Recommended — No Runtime Setup)
1. Go to the [**Releases**](../../releases) section.
2. Choose your preferred build:
   * **.NET 11 Installer:** Download and run `NetSpeedOverlay-Setup-1.0.0-x64.exe` (includes optional Windows startup integration).
   * **Portable Executable:** Download the standalone `.NET 11` `NetSpeedOverlay.exe` or the Python `Atena-NetSpeed.exe` and run it directly.

---

### Option 2: Build & Run the .NET 11 Edition from Source
**Requirements:** [.NET 11 SDK](https://dotnet.microsoft.com/) on Windows.

```powershell
cd NetSpeedOverlay

# Run directly in Release mode
dotnet run -c Release

# Publish lightweight single-file executable (requires .NET 11 Desktop Runtime)
dotnet publish -c Release -r win-x64 --self-contained false

# Publish fully self-contained executable (no .NET runtime required on target PC)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true
```

> **Building the Installer:** After running `dotnet publish`, open `NetSpeedOverlay.iss` in **Inno Setup** and compile to generate the installer inside the `installer/` folder.

---

### Option 3: Run or Package the Python Edition from Source
**Requirements:** Python 3.10+

```powershell
cd Python

# Install dependencies
pip install PyQt6 psutil

# Run the widget
python app.py

# (Optional) Build standalone .exe using the included PyInstaller spec
pip install pyinstaller
pyinstaller app.spec
```

---

<div align="center">
  <img src="https://github.com/White-Wolf-dv/White-Wolf-dv/blob/main/Gif/14.gif" align="center" width="550" />
</div>
