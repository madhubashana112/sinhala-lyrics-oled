#pragma once
#include <Arduino.h>

// Milliseconds from video time zero. End times are exclusive.
struct LyricCue {
  uint16_t startMs;
  uint16_t endMs;
};

constexpr uint16_t VIDEO_DURATION_MS = 19400;
constexpr LyricCue lyricCues[] = {
  {433, 2500},  // නපුරුයි කතා
  {2500, 5800},  // සොඳුරුයි සිනා
  {5800, 9367},  // මගේ හිත පාරනා
  {9367, 11700},  // සායම් එපා
  {11700, 14567},  // මායම් එපා
  {14567, 16300},  // පියාඹන්නේ
  {16300, VIDEO_DURATION_MS},  // තනියෙන් තමා
};
