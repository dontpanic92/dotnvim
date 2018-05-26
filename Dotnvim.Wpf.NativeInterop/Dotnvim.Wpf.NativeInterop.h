#pragma once
#include <Windows.h>

using namespace System;

namespace Dotnvim {
	namespace Wpf {
		namespace NativeInterop {
			public ref class Methods
			{
			public:
				static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
				{
					MINMAXINFO* minMaxInfo = (MINMAXINFO*)lParam.ToPointer();
					HMONITOR monitor = MonitorFromWindow((HWND)hwnd.ToPointer(), MONITOR_DEFAULTTONEAREST);

					if (monitor != 0)
					{
						MONITORINFO monitorInfo;
						monitorInfo.cbSize = sizeof(monitorInfo);
						GetMonitorInfo(monitor, &monitorInfo);

						minMaxInfo->ptMaxPosition.x = abs(monitorInfo.rcWork.left - monitorInfo.rcMonitor.left);
						minMaxInfo->ptMaxPosition.y = abs(monitorInfo.rcWork.top - monitorInfo.rcMonitor.top);
						minMaxInfo->ptMaxSize.x = abs(monitorInfo.rcWork.right - monitorInfo.rcWork.left);
						minMaxInfo->ptMaxSize.y = abs(monitorInfo.rcWork.bottom - monitorInfo.rcWork.top);
					}
				}

				static String^ VirtualKeyToStringWithModifiers(int virtualKey)
				{
					BYTE keyboardState[256];
					GetKeyboardState(keyboardState);
					UINT scanCode = MapVirtualKey((UINT)virtualKey, MAPVK_VK_TO_VSC);

					WCHAR buffer[2];

					int result = ToUnicode((UINT)virtualKey, scanCode, keyboardState, buffer, 2, 0);
					if (result <= 0)
					{
						return nullptr;
					}

					return gcnew String(buffer, 0, result);
				}
				
				static String^ VirtualKeyToStringWithoutModifiers(int virtualKey)
				{
					BYTE keyboardState[256];
					GetKeyboardState(keyboardState);
					keyboardState[VK_CONTROL] = 0;
					keyboardState[VK_SHIFT] = 0;
					keyboardState[VK_MENU] = 0;

					UINT scanCode = MapVirtualKey((UINT)virtualKey, MAPVK_VK_TO_VSC);

					WCHAR buffer[2];

					int result = ToUnicode((UINT)virtualKey, scanCode, keyboardState, buffer, 2, 0);
					if (result <= 0)
					{
						return nullptr;
					}

					return gcnew String(buffer, 0, result);
				}
			};
		}
	}
}
