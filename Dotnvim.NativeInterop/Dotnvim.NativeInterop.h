#pragma once
#include <Windows.h>
#include <windowsx.h>
#include <Dwmapi.h>

using namespace System;

namespace Dotnvim {
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

			static String^ GetUnicode(UINT virtualKey, UINT scanCode, BYTE keyboardState[256])
			{
				WCHAR buffer[2];

				int result = ToUnicode((UINT)virtualKey, scanCode, keyboardState, buffer, 2, 0);
				if (result <= 0)
				{
					return nullptr;
				}

				return gcnew String(buffer, 0, result);
			}

			static String^ VirtualKeyToString(int virtualKey)
			{
				BYTE keyboardState[256];
				GetKeyboardState(keyboardState);
				bool control = (keyboardState[VK_CONTROL] & 0x80) != 0;
				bool shift = (keyboardState[VK_SHIFT] & 0x80) != 0;
				bool alt = (keyboardState[VK_MENU] & 0x80) != 0;
				keyboardState[VK_CONTROL] &= 0x7F;
				keyboardState[VK_SHIFT] &= 0x7F;
				keyboardState[VK_MENU] &= 0x7F;

				UINT scanCode = MapVirtualKey((UINT)virtualKey, MAPVK_VK_TO_VSC);
				String^ text = GetUnicode((UINT)virtualKey, scanCode, keyboardState);

				if (control)
				{
					keyboardState[VK_CONTROL] |= 0x80;
					String^ textWithControl = GetUnicode((UINT)virtualKey, scanCode, keyboardState);
					if (!String::IsNullOrEmpty(textWithControl))
					{
						text = textWithControl;
						control = false;
					}
					
					keyboardState[VK_CONTROL] &= 0x7F;
				}

				if (shift)
				{
					keyboardState[VK_SHIFT] |= 0x80;
					String^ textWithShift = GetUnicode((UINT)virtualKey, scanCode, keyboardState);
					if (!String::IsNullOrEmpty(textWithShift))
					{
						text = textWithShift;
						shift = false;
					}
					
					keyboardState[VK_SHIFT] &= 0x7F;
				}

				if (String::Equals(text, "<"))
				{
					text = "lt";
					return DecorateInput(text, control, shift, alt);
				} 
				else if (String::Equals(text, "\\"))
				{
					text = "Bslash";
					return DecorateInput(text, control, shift, alt);
				}
				else if ((control || shift || alt) && !String::IsNullOrEmpty(text))
				{
					return DecorateInput(text, control, shift, alt);
				}

				return text;
			}

			static String^ DecorateInput(String^ input, bool control, bool shift, bool alt)
			{
				String^ output = gcnew String("<");

				if (control != 0)
				{
					output += "C-";
				}

				if (shift != 0)
				{
					output += "S-";
				}

				if (alt != 0)
				{
					output += "A-";
				}

				output += input + ">";

				return output;
			}

			static void ExtendFrame(IntPtr handle, int dwmBorderSizeX, int dwmBorderSizeY)
			{
				HWND hwnd = (HWND)(handle.ToPointer());
				MARGINS margins = { dwmBorderSizeX, dwmBorderSizeX, dwmBorderSizeY, dwmBorderSizeY };
				int val = 2;

				DwmSetWindowAttribute(hwnd, 2, &val, 4);
				DwmExtendFrameIntoClientArea(hwnd, &margins);
			}

			static IntPtr NCHitTest(IntPtr handle, IntPtr lParam, int xBorderWidth, int yBorderWidth, int titleBarHeight, Func<int, int, bool>^ clientAreaHitTest)
			{
				HWND hwnd = (HWND)handle.ToPointer();
				int x = GET_X_LPARAM((LPARAM)lParam.ToPointer());
				int y = GET_Y_LPARAM((LPARAM)lParam.ToPointer());

				POINT point = { x, y };
				ScreenToClient(hwnd, &point);

				if (clientAreaHitTest(point.x, point.y))
				{
					return (IntPtr)HTCLIENT;
				}

				WINDOWINFO windowInfo;
				windowInfo.cbSize = sizeof(WINDOWINFO);
				GetWindowInfo(hwnd, &windowInfo);
				int height = windowInfo.rcWindow.bottom - windowInfo.rcWindow.top;
				int width = windowInfo.rcWindow.right - windowInfo.rcWindow.left;

				if (!IsZoomed(hwnd))
				{
					if (point.x < xBorderWidth)
					{
						if (point.y < yBorderWidth)
						{
							return (IntPtr)HTTOPLEFT;
						}
						else if (point.y > height - yBorderWidth)
						{
							return (IntPtr)HTBOTTOMLEFT;
						}
						else
						{
							return (IntPtr)HTLEFT;
						}
					}
					else if (point.x > width - xBorderWidth)
					{
						if (point.y < yBorderWidth)
						{
							return (IntPtr)HTTOPRIGHT;
						}
						else if (point.y > height - yBorderWidth)
						{
							return (IntPtr)HTBOTTOMRIGHT;
						}
						else
						{
							return (IntPtr)HTRIGHT;
						}
					}
					else if (point.y < yBorderWidth)
					{
						return (IntPtr)HTTOP;
					}
					else if (point.y > height - yBorderWidth)
					{
						return (IntPtr)HTBOTTOM;
					}
				}

				if (point.y < titleBarHeight + yBorderWidth)
				{
					return (IntPtr)HTCAPTION;
				}

				return (IntPtr)HTCLIENT;
			}
		};
	}
}
