/*
  Sinhala Lyrics OLED — Arduino Mega 2560 + 1.3 inch SH1106 I2C OLED

  The Sinhala text is stored as pre-rendered 1-bit glyph bitmaps in PROGMEM,
  because the compact fonts supplied by U8g2 do not contain Sinhala glyphs.
*/

#include <Arduino.h>
#include <Wire.h>
#include <U8g2lib.h>
#include "SinhalaLyricsBitmaps.h"
#include "LyricEmojis.h"
#include "LyricTiming.h"

// Most JMD 1.3 in SH1106 I2C modules use 0x3C.  Change only if an I2C scan
// shows a different address (for example, 0x3D).
constexpr uint8_t OLED_I2C_ADDRESS = 0x3C;
constexpr uint8_t OLED_WIDTH = 128;

// Full-buffer mode keeps text and animation updates together.
U8G2_SH1106_128X64_NONAME_F_HW_I2C u8g2(U8G2_R0, U8X8_PIN_NONE);

constexpr uint16_t NO_VIDEO_POSITION = UINT16_MAX;
uint16_t videoPosition = NO_VIDEO_POSITION;
uint16_t displayedPosition = NO_VIDEO_POSITION;
uint8_t displayedCue = 0;

bool readingTime = false;
bool hasDigits = false;
uint16_t receivedTime = 0;

static_assert(sizeof(lyricEmojis) / sizeof(lyricEmojis[0]) == SINHALA_LINE_COUNT,
              "Each lyric needs a matching emoji");
static_assert(sizeof(lyricCues) / sizeof(lyricCues[0]) == SINHALA_LINE_COUNT,
              "Each lyric needs a timing cue");

void drawMusicNote(uint8_t x, uint8_t y) {
  u8g2.drawVLine(x + 3, y, 7);
  u8g2.drawHLine(x + 3, y, 4);
  u8g2.drawPixel(x + 6, y + 1);
  u8g2.drawBox(x, y + 5, 4, 2);
}

void drawCenteredText(const char *text, uint8_t y) {
  const uint8_t w = u8g2.getStrWidth(text);
  u8g2.drawStr((OLED_WIDTH - w) / 2, y, text);
}

void drawPlaybackCard(const char *message) {
  u8g2.drawRFrame(11, 6, 106, 51, 6);
  drawMusicNote(61, 12);
  u8g2.setFont(u8g2_font_6x12_tr);
  drawCenteredText("SINHALA LYRICS", 34);
  u8g2.setFont(u8g2_font_5x8_tr);
  drawCenteredText(message, 47);
}

void drawLyricEmoji(uint8_t index, uint16_t localTime) {
  const uint8_t phase = (localTime / 180) % 4;
  const uint8_t lift = phase < 2 ? phase : 4 - phase;
  u8g2.drawXBMP((OLED_WIDTH - LYRIC_EMOJI_SIZE) / 2, 41 - lift,
                 LYRIC_EMOJI_SIZE, LYRIC_EMOJI_SIZE, lyricEmojis[index]);

  // Equalizer bars sit beside the emoji and below the Sinhala glyphs.
  for (uint8_t i = 0; i < 3; ++i) {
    const uint8_t height = 2 + ((localTime / 120 + 2 * i) % 5);
    const uint8_t leftX = 34 + i * 5;
    const uint8_t rightX = OLED_WIDTH - leftX - 2;
    u8g2.drawBox(leftX, 51 - height, 2, height);
    u8g2.drawBox(rightX, 51 - height, 2, height);
  }
}

void drawProgress(uint8_t index, uint16_t localTime, uint16_t cueDuration) {
  constexpr uint8_t STEP_WIDTH = 14;
  constexpr uint8_t STEP_GAP = 2;
  const uint8_t startX =
    (OLED_WIDTH - SINHALA_LINE_COUNT * (STEP_WIDTH + STEP_GAP) + STEP_GAP) / 2;
  for (uint8_t i = 0; i < SINHALA_LINE_COUNT; ++i) {
    const uint8_t x = startX + i * (STEP_WIDTH + STEP_GAP);
    u8g2.drawFrame(x, 59, STEP_WIDTH, 4);
    uint8_t fill = 0;
    if (i < index) {
      fill = STEP_WIDTH - 2;
    } else if (i == index) {
      fill = ((uint32_t)localTime * (STEP_WIDTH - 2)) / cueDuration;
    }
    if (fill > 0) u8g2.drawBox(x + 1, 60, fill, 2);
  }
}

void drawLyric(uint8_t index, uint16_t localTime, uint16_t cueDuration) {
  // Read the dimensions and bitmap address from the flash lookup table.
  SinhalaLine line;
  memcpy_P(&line, &sinhalaLines[index], sizeof(line));
  const uint8_t x = (OLED_WIDTH - line.width) / 2;
  const uint8_t y = 26 - line.height / 2;

  drawMusicNote(6, 3);
  u8g2.drawHLine(6, 13, 116);
  u8g2.drawRFrame(101, 0, 21, 11, 2);
  u8g2.setFont(u8g2_font_5x8_tr);
  u8g2.drawStr(17, 9, "NOW PLAYING");
  u8g2.setCursor(104, 9);
  u8g2.print(index + 1);
  u8g2.print('/');
  u8g2.print(SINHALA_LINE_COUNT);

  // The bitmap bytes are still in flash. drawXBMP reads them from PROGMEM.
  u8g2.drawXBMP(x, y, line.width, line.height, line.bitmap);
  drawLyricEmoji(index, localTime);
  drawProgress(index, localTime, cueDuration);
}

void setup() {
  Serial.begin(115200);
  Wire.begin(); // Mega 2560 hardware I2C: SDA = pin 20, SCL = pin 21.
  u8g2.setI2CAddress(OLED_I2C_ADDRESS << 1); // U8g2 expects the 8-bit address.
  u8g2.begin();
  u8g2.setBusClock(400000); // Smooth on normally wired short I2C leads.
  u8g2.clearBuffer();
  drawPlaybackCard("OPEN VIDEO PLAYER");
  u8g2.sendBuffer();
}

// The player sends T<milliseconds> followed by a newline.
// '?' identifies the sketch. 'Q' reports the most recently drawn cue.
void receivePlayerByte(char incoming) {
  if (incoming == 'T') {
    readingTime = true;
    hasDigits = false;
    receivedTime = 0;
  } else if (incoming == '\n') {
    if (readingTime && hasDigits) videoPosition = receivedTime;
    readingTime = false;
  } else if (readingTime && incoming >= '0' && incoming <= '9') {
    const uint32_t nextTime = (uint32_t)receivedTime * 10 + incoming - '0';
    if (nextTime > VIDEO_DURATION_MS) {
      readingTime = false;
    } else {
      receivedTime = nextTime;
      hasDigits = true;
    }
  } else {
    readingTime = false;
    if (incoming == '?') Serial.println(F("OLED SYNC 1"));
    if (incoming == 'Q') {
      Serial.print(F("STATE "));
      Serial.print(displayedPosition);
      Serial.print(' ');
      Serial.println(displayedCue);
    }
  }
}

void loop() {
  while (Serial.available() > 0) receivePlayerByte(Serial.read());
  if (videoPosition == displayedPosition) return;

  const uint16_t elapsed = videoPosition;
  uint8_t cueNumber = 0;
  u8g2.clearBuffer();
  if (elapsed >= VIDEO_DURATION_MS) {
    drawPlaybackCard("READY TO REPLAY");
    cueNumber = SINHALA_LINE_COUNT + 1;
  } else {
    uint8_t line = 0;
    while (line < SINHALA_LINE_COUNT && elapsed >= lyricCues[line].endMs) ++line;
    if (line < SINHALA_LINE_COUNT && elapsed >= lyricCues[line].startMs) {
      const LyricCue &cue = lyricCues[line];
      drawLyric(line, elapsed - cue.startMs, cue.endMs - cue.startMs);
      cueNumber = line + 1;
    } else {
      drawPlaybackCard("VIDEO SYNC");
    }
  }
  u8g2.sendBuffer();
  displayedPosition = elapsed;
  displayedCue = cueNumber;
}
