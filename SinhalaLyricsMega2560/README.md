# Sinhala Lyrics OLED — Mega 2560 + SH1106

The sketch displays these seven Sinhala lyric lines on a 19.40-second timeline matching the supplied video. The black-and-white emoji and progress bar animate while each lyric stays still.

1. නපුරුයි කතා
2. සොඳුරුයි සිනා
3. මගේ හිත පාරනා
4. සායම් එපා
5. මායම් එපා
6. පියාඹන්නේ
7. තනියෙන් තමා

The glyphs are **actual shaped Noto Sans Sinhala bitmap pixels**, stored in the Mega's flash memory (`PROGMEM`). No Sinhala font is needed on the Arduino, so all conjuncts and vowel marks remain correct on the 128×64 OLED.

## Parts and wiring

| OLED pin | Arduino Mega 2560 |
|---|---|
| VCC | 5V (or 3.3V if your module specifically requires it) |
| GND | GND |
| SDA | pin 20 |
| SCL | pin 21 |

Use short wires. The common 1.3-inch JMD SH1106 I2C module is usually at `0x3C`; this project starts with that address.

## Upload

1. In the Arduino IDE, install **U8g2**: **Tools → Manage Libraries**, search for `U8g2`, then install it.
2. Open `SinhalaLyricsMega2560.ino` from this folder. Arduino will also open the nearby `SinhalaLyricsBitmaps.h` automatically.
3. Select **Tools → Board → Arduino Mega or Mega 2560**, and select the correct processor/port.
4. Upload.

If the screen stays blank, change this line in `SinhalaLyricsMega2560.ino`:

```cpp
constexpr uint8_t OLED_I2C_ADDRESS = 0x3C;
```

to `0x3D`, then upload again. Also verify that your OLED is SH1106, not SSD1306, and that SDA/SCL are pins 20/21 on the Mega.

## Play the video and sync the OLED

1. Connect the Mega to the PC over USB. Close Arduino Serial Monitor if it is open so the player can use the port.
2. Open `OLEDVideoPlayer-Fixed.exe` from this project's ZIP. The 19.40-second clip is bundled in a compatible format and loads automatically. **Use bundled clip** brings it back if you have opened another file.
3. Wait for **OLED connected**. The player tries COM3 automatically. If the Mega is on another port, select it in the player and click **Connect**.
4. Press **Play** in the player. The OLED follows the video's position when you play, pause, move the slider, or replay.

The included Windows player is the app that sends playback times over USB. To sync the display, play the video in this app.

Use **Full screen** or **F11** to expand the video. In full screen, move the mouse to show the controls or use **Space** to play or pause. Press **Esc** to go back to the regular window.

The matching `SinhalaLyrics.srt` file uses the same cue times. Load it as a subtitle track in your video player or editor. `LyricTiming.h` lists the OLED cue times in milliseconds from the start of the clip.

## Technical notes

- The SH1106 is driven by U8g2 in full-buffer I2C mode. It needs about 1 KB of RAM, which is safe on the Mega 2560 (8 KB SRAM).
- The lookup table and bitmap bytes live in `PROGMEM`. The sketch reads the table with `memcpy_P()` and draws the bitmaps directly from flash with U8g2's `drawXBMP()`.
- The source font is [Noto Sans Sinhala](https://fonts.google.com/noto/specimen/Noto+Sans+Sinhala), licensed under the SIL Open Font License. `GenerateSinhalaBitmaps.py` is provided only for changing lyrics; it is not needed for upload.
- The generated bitmaps use correct complex-text shaping through Pillow/libraqm before being converted to U8g2-compatible LSB-first XBM data.
