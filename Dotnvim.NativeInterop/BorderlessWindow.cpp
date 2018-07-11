#include <stdafx.h>
#include <windows.h>
#include <windowsx.h>
#include <shellapi.h>
#include <uxtheme.h>
#include <dwmapi.h>
#include <versionhelpers.h>
#include <stdlib.h>
#include <stdbool.h>

extern IMAGE_DOS_HEADER __ImageBase;
#define HINST_THISCOMPONENT ((HINSTANCE)&__ImageBase)

#ifndef WM_NCUAHDRAWCAPTION
#define WM_NCUAHDRAWCAPTION (0x00AE)
#endif
#ifndef WM_NCUAHDRAWFRAME
#define WM_NCUAHDRAWFRAME (0x00AF)
#endif

struct window {
	HWND hwnd;

	unsigned width;
	unsigned height;

	RECT rgn;

	bool theme_enabled;
	bool composition_enabled;
};

static void update_region(struct window *data)
{
	RECT old_rgn = data->rgn;

	if (IsMaximized(data->hwnd)) {
		WINDOWINFO wi = { .cbSize = sizeof wi };
		GetWindowInfo(data->hwnd, &wi);

		/* For maximized windows, a region is needed to cut off the non-client
		borders that hang over the edge of the screen */
		data->rgn = RECT();
		data->rgn.left = wi.rcClient.left - wi.rcWindow.left;
		data->rgn.top = wi.rcClient.top - wi.rcWindow.top;
		data->rgn.right = wi.rcClient.right - wi.rcWindow.left;
		data->rgn.bottom = wi.rcClient.bottom - wi.rcWindow.top;
	}
	else if (!data->composition_enabled) {
		/* For ordinary themed windows when composition is disabled, a region
		is needed to remove the rounded top corners. Make it as large as
		possible to avoid having to change it when the window is resized. */
		data->rgn.left = 0;
		data->rgn.top = 0;
		data->rgn.right = 32767;
		data->rgn.bottom = 32767;
	}
	else {
		/* Don't mess with the region when composition is enabled and the
		window is not maximized, otherwise it will lose its shadow */
		data->rgn = { 0 };
	}

	/* Avoid unnecessarily updating the region to avoid unnecessary redraws */
	if (EqualRect(&data->rgn, &old_rgn))
		return;
	/* Treat empty regions as NULL regions */
	if (EqualRect(&data->rgn, &(RECT) { 0 }))
		SetWindowRgn(data->hwnd, NULL, TRUE);
	else
		SetWindowRgn(data->hwnd, CreateRectRgnIndirect(&data->rgn), TRUE);
}

static void handle_nccreate(HWND hwnd, CREATESTRUCTW *cs)
{
	struct window *data = (window*)cs->lpCreateParams;
	SetWindowLongPtrW(hwnd, GWLP_USERDATA, (LONG_PTR)data);
}


static void handle_themechanged(struct window *data)
{
	data->theme_enabled = IsThemeActive();
}

static LRESULT handle_message_invisible(HWND window, UINT msg, WPARAM wparam,
	LPARAM lparam)
{
	LONG_PTR old_style = GetWindowLongPtrW(window, GWL_STYLE);

	/* Prevent Windows from drawing the default title bar by temporarily
	toggling the WS_VISIBLE style. This is recommended in:
	https://blogs.msdn.microsoft.com/wpfsdk/2008/09/08/custom-window-chrome-in-wpf/ */
	SetWindowLongPtrW(window, GWL_STYLE, old_style & ~WS_VISIBLE);
	LRESULT result = DefWindowProcW(window, msg, wparam, lparam);
	SetWindowLongPtrW(window, GWL_STYLE, old_style);

	return result;
}

static LRESULT CALLBACK borderless_window_proc(HWND window, UINT msg,
	WPARAM wparam, LPARAM lparam)
{
	struct window *data = (void*)GetWindowLongPtrW(window, GWLP_USERDATA);
	if (!data) {
		/* Due to a longstanding Windows bug, overlapped windows will receive a
		WM_GETMINMAXINFO message before WM_NCCREATE. This is safe to ignore.
		It doesn't need any special handling anyway. */
		if (msg == WM_NCCREATE)
			handle_nccreate(window, (CREATESTRUCTW*)lparam);
		return DefWindowProcW(window, msg, wparam, lparam);
	}

	switch (msg) {
	case WM_CLOSE:
		DestroyWindow(window);
		return 0;
	case WM_DESTROY:
		PostQuitMessage(0);
		return 0;
	case WM_DWMCOMPOSITIONCHANGED:
		// handle_compositionchanged(data);
		return 0;
	case WM_KEYDOWN:
		// if (handle_keydown(data, wparam))
		// 	return 0;
		break;
	case WM_LBUTTONDOWN:
		/* Allow window dragging from any point */
		ReleaseCapture();
		SendMessageW(window, WM_NCLBUTTONDOWN, HTCAPTION, 0);
		return 0;
	case WM_NCACTIVATE:
		/* DefWindowProc won't repaint the window border if lParam (normally a
		HRGN) is -1. This is recommended in:
		https://blogs.msdn.microsoft.com/wpfsdk/2008/09/08/custom-window-chrome-in-wpf/ */
		return DefWindowProcW(window, msg, wparam, -1);
	case WM_NCCALCSIZE:
		// handle_nccalcsize(data, wparam, lparam);
		return 0;
	case WM_NCHITTEST:
		// return handle_nchittest(data, GET_X_LPARAM(lparam),
		// 	GET_Y_LPARAM(lparam));
		break;
	case WM_NCPAINT:
		/* Only block WM_NCPAINT when composition is disabled. If it's blocked
		when composition is enabled, the window shadow won't be drawn. */
		if (!data->composition_enabled)
			return 0;
		break;
	case WM_NCUAHDRAWCAPTION:
	case WM_NCUAHDRAWFRAME:
		/* These undocumented messages are sent to draw themed window borders.
		Block them to prevent drawing borders over the client area. */
		return 0;
	case WM_PAINT:
		handle_paint(data);
		return 0;
	case WM_SETICON:
	case WM_SETTEXT:
		/* Disable painting while these messages are handled to prevent them
		from drawing a window caption over the client area, but only when
		composition and theming are disabled. These messages don't paint
		when composition is enabled and blocking WM_NCUAHDRAWCAPTION should
		be enough to prevent painting when theming is enabled. */
		if (!data->composition_enabled && !data->theme_enabled)
			return handle_message_invisible(window, msg, wparam, lparam);
		break;
	case WM_THEMECHANGED:
		handle_themechanged(data);
		break;
	case WM_WINDOWPOSCHANGED:
		handle_windowposchanged(data, (WINDOWPOS*)lparam);
		return 0;
	}

	return DefWindowProcW(window, msg, wparam, lparam);
}