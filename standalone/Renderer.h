#pragma once
#include "Runtime.h"
#include "Core.h"

namespace gameui {
// Installs DXGI hooks once. The swap chain and its interfaces come from Windows,
// not game offsets. All Unity cursor work is delegated to the game-window thread.
void install(HWND gameWindow,void (*mainThreadTick)());
// Uses a separately mapped view so reconnecting cannot unmap the render thread's data.
void bind(HANDLE mapping);
bool capturing();
}
