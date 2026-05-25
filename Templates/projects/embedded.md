# Project Context: Embedded / ESP32

## Domain
Arduino-ESP32 firmware, hardware interfaces, real-time data acquisition.

## Stack
- Arduino-ESP32 core
- FreeRTOS tasks
- CAN bus / UART / SPI
- LVGL for displays

## Guidelines
- Minimize heap fragmentation; prefer stack/static allocation.
- Use hardware timers over software delays.
- Handle watchdog resets gracefully.
- Keep ISR routines short and non-blocking.
