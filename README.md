# Sinhala Lyrics OLED

A Windows video player that keeps seven Sinhala lyric lines and animated pixel emoji in sync with a 19.40-second clip on an Arduino Mega 2560 and a 128×64 SH1106 I²C OLED.

## Run the project

1. Wire the OLED's SDA to Mega pin 20, SCL to pin 21, and GND to GND. Power the OLED at its rated voltage. The sketch defaults to I²C address `0x3C`.
2. Install **U8g2** in Arduino IDE and upload [the Arduino sketch](SinhalaLyricsMega2560/SinhalaLyricsMega2560.ino) with **Arduino Mega or Mega 2560** selected. Close Serial Monitor so the player can access the board over USB.
3. Download `OLEDVideoPlayer-Fixed.exe` from [Releases](https://github.com/madhubashana112/sinhala-lyrics-oled/releases/latest), or build the player using the steps below. The included clip opens automatically.
4. Wait for **OLED connected**, then press **Play**. The display follows Play, Pause, scrubbing, and Replay. If the Mega is on a port other than COM3, select its port and click **Connect**.

Use **F11** or **Full screen** to expand the video and **Esc** to return. Move the mouse to show the controls in fullscreen.

## Build the Windows player

On Windows with the .NET Framework 4.x desktop libraries available, run this from the repository root in PowerShell.

```powershell
.\Build-Player.ps1
```

The script compiles the WPF app and embeds the compatible H.264/AAC clip. It writes `dist\OLEDVideoPlayer-Fixed.exe`. Use **Use bundled clip** to reload it after opening another file. You can pass `-VideoPath` to embed a different compatible copy of the same timed clip.

## Project files

- [Arduino sketch and OLED setup](SinhalaLyricsMega2560/README.md)
- [Fullscreen Windows player source](player/OLEDVideoPlayer.cs)
- [Matching Sinhala subtitles](SinhalaLyrics.srt)
- [Cue times for the Arduino](SinhalaLyricsMega2560/LyricTiming.h)
- [Compatible copy of the supplied clip](assets/compatible-clip.mp4)

The Sinhala glyphs use Noto Sans Sinhala and are stored as pre-rendered bitmaps in AVR flash. The player sends time in milliseconds over USB at 115200 baud. The OLED advances to the cue associated with that timestamp.
