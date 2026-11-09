package com.que.woopang;

import android.app.Activity;
import android.app.Dialog;
import android.graphics.Rect;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.view.View;
import android.view.ViewTreeObserver;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowInsetsAnimation;
import android.view.WindowInsetsController;
import java.lang.reflect.Field;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import java.lang.reflect.Proxy;
import java.util.List;
import java.util.concurrent.Semaphore;

/**
 * 안드로이드 키보드를 아이폰처럼 미끄러지듯 내린다 (2026-11-08 대표님 「iOS 는 괜찮은데 안드로이드는 키패드가 사라질 때 슬라이드가 아니라 컷아웃으로」).
 *
 * 왜 잘려 사라지나: 유니티 6 (Activity 진입점) 은 키보드를 따로 띄운 대화상자 창의 입력칸에 붙이고, 키보드를 내릴 때(뒤로 · 완료 · 칸을 놓을 때 모두)
 * 그 창을 바로 닫는다. 안드로이드 11 부터 키보드가 내려가는 움직임은 키보드를 쥔 창이 돌리므로, 창이 먼저 없어지면 키보드가 그 자리에서 사라진다.
 *
 * 고치는 법: 유니티가 키보드를 닫는 한 곳(mSoftInput.hide)을 감싸서 키보드를 그 창에서 먼저 내리고, 다 내려간 뒤 창을 닫는다.
 *  · 유니티는 hide 하는 동안 「창이 닫히고 화면이 포커스를 되찾을 때」까지 자기 스레드를 세워 둔다 — 창을 늦게 닫으니 그 기다림은 바로 풀어 준다
 *    (안 그러면 내려가는 동안 화면이 멈춘다). 유니티는 키보드가 내려갔다고 바로 알아 창 위치 등은 지금과 같은 때에 움직인다.
 *  · 그러면 유니티는 키보드를 이미 치운 셈(mSoftInput = null)이라, 나중에 창을 닫아 화면이 포커스를 되찾을 때 그걸 앱 포커스로 알린다
 *    (원래는 키보드 때문인 포커스라 알리지 않는다) — 창을 닫는 동안만 감싼 것을 다시 넣어 두었다가 포커스를 되찾으면 뺀다.
 *  · 내려가는 사이 유니티가 키보드 크기를 다시 받았을 수 있다 — 다 닫은 뒤 「키보드 없음」으로 한 번 더 알린다.
 * 유니티 속(mUnityPlayer · mSoftInput · invokeOnClose · raiseFocusListener · reportSoftInput*)을 리플렉션으로 쓴다.
 * 하나라도 못 찾거나 어긋나면 손대지 않는다 — 예전처럼 닫힌다. 유니티를 올린 뒤엔 docs/claude/unity-upgrade.md 의 점검 항목대로 기기에서 다시 볼 것.
 */
public final class KeyboardSlide {
    /** 다 내려갔다는 소식이 없을 때 창을 닫는 시간 — 안드로이드 키보드 내리기(0.34초)보다 조금 길게. 개발자 옵션의 애니메이션 배율을 따른다 */
    private static final int FALLBACK_MS = 420;
    /** 창을 닫은 뒤 포커스가 이만큼 안 돌아오면 다시 넣어 둔 것을 그냥 뺀다 */
    private static final int FOCUS_WAIT_MS = 1000;

    private static final Handler UI = new Handler(Looper.getMainLooper());
    private static Field softField;
    private static Method raiseFocus;
    private static Method reportArea;
    private static Method reportVisible;
    private static volatile boolean broken;

    private KeyboardSlide() {}

    /** 키보드가 떠 있는 동안 가끔 부른다(R0926KeyboardSlide) — 지금 키보드를 감싼다. 이미 감쌌거나 감쌀 수 없으면 그대로 */
    public static void hook(final Activity activity) {
        if (Build.VERSION.SDK_INT < 30 || activity == null || broken) return;
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                try { install(activity); } catch (Throwable t) { broken = true; }
            }
        });
    }

    private static void install(Activity a) throws Exception {
        Field pf = declaredField(a.getClass(), "mUnityPlayer");
        Object player = pf == null ? null : pf.get(a);
        if (player == null) return;
        if (softField == null) {
            Class<?> pc = player.getClass();
            Field f = declaredField(pc, "mSoftInput");
            raiseFocus = declaredMethod(pc, "raiseFocusListener", boolean.class);
            reportArea = declaredMethod(pc, "reportSoftInputArea", Rect.class);
            reportVisible = declaredMethod(pc, "reportSoftInputIsVisible", boolean.class);
            if (f == null || !f.getType().isInterface() || raiseFocus == null) { broken = true; return; }
            softField = f;
        }
        Object soft = softField.get(player);
        if (soft == null || Proxy.isProxyClass(soft.getClass())) return;
        Dialog d = dialogOf(soft);
        if (d == null || !d.isShowing()) return;   // 대화상자를 안 쓰는 키보드 방식
        Class<?> iface = softField.getType();
        Slide s = new Slide(a, player, soft, d, iface.getMethod("hide"), soft.getClass().getMethod("invokeOnClose"));
        s.self = Proxy.newProxyInstance(iface.getClassLoader(), new Class<?>[]{ iface }, s);
        softField.set(player, s.self);
    }

    // 유니티 키보드 대화상자 — 소프트 입력 안의 Dialog (대화상자를 안 쓰는 방식이면 null)
    private static Dialog dialogOf(Object soft) throws Exception {
        for (Class<?> c = soft.getClass(); c != null && c != Object.class; c = c.getSuperclass()) {
            for (Field f : c.getDeclaredFields()) {
                if (Modifier.isStatic(f.getModifiers()) || !Dialog.class.isAssignableFrom(f.getType())) continue;
                f.setAccessible(true);
                Object v = f.get(soft);
                if (v != null) return (Dialog) v;
            }
        }
        return null;
    }

    // 유니티가 「닫히면 알려 달라」며 건네는 것인가 — 세마포어를 든 것을 쥐고 있다 (유니티가 그 세마포어로 기다린다)
    private static boolean holdsWaiter(Object o) {
        if (o == null || o.getClass().getName().startsWith("java.")) return false;
        for (Class<?> c = o.getClass(); c != null && c != Object.class; c = c.getSuperclass()) {
            for (Field f : c.getDeclaredFields()) {
                Class<?> t = f.getType();
                if (t.isPrimitive() || t.isArray() || t.getName().startsWith("java.") || t.getName().startsWith("android.")) continue;
                for (Field g : t.getDeclaredFields()) {
                    if (g.getType() == Semaphore.class) return true;
                }
            }
        }
        return false;
    }

    private static boolean imeVisible(Dialog d) {
        try {
            Window w = d.getWindow();
            View decor = w == null ? null : w.peekDecorView();
            WindowInsets in = decor == null ? null : decor.getRootWindowInsets();
            return in != null && in.isVisible(WindowInsets.Type.ime());
        } catch (Throwable t) {
            return false;
        }
    }

    private static Field declaredField(Class<?> t, String name) {
        for (Class<?> c = t; c != null && c != Object.class; c = c.getSuperclass()) {
            try {
                Field f = c.getDeclaredField(name);
                f.setAccessible(true);
                return f;
            } catch (NoSuchFieldException ignored) { }
        }
        return null;
    }

    private static Method declaredMethod(Class<?> t, String name, Class<?>... params) {
        for (Class<?> c = t; c != null && c != Object.class; c = c.getSuperclass()) {
            try {
                Method m = c.getDeclaredMethod(name, params);
                m.setAccessible(true);
                return m;
            } catch (NoSuchMethodException ignored) { }
        }
        return null;
    }

    // 유니티 소프트 입력을 감싼 것 — 유니티가 기다리는 닫기 한 번만 미끄러뜨리고, 나머지는 그대로 넘긴다
    private static final class Slide implements InvocationHandler {
        private final Activity activity;
        private final Object player;
        private final Object real;
        private final Dialog dialog;
        private final Method hide;
        private final Method onClose;
        Object self;              // 유니티 mSoftInput 에 넣은 이 감싼 것
        private boolean waiting;  // 바로 앞에 유니티가 「닫히면 알려 달라」를 걸었다 — 이번 hide 는 유니티가 기다리는 닫기 (다른 칸 키보드로 바로 넘어갈 때는 아니다)
        private boolean slid;
        private boolean closed;

        private final Runnable closer = new Runnable() {
            @Override public void run() { close(); }
        };

        Slide(Activity activity, Object player, Object real, Dialog dialog, Method hide, Method onClose) {
            this.activity = activity;
            this.player = player;
            this.real = real;
            this.dialog = dialog;
            this.hide = hide;
            this.onClose = onClose;
        }

        @Override public Object invoke(Object proxy, Method m, Object[] args) throws Throwable {
            if ("hide".equals(m.getName()) && (args == null || args.length == 0)) {
                if (!slid) {
                    slid = true;
                    if (waiting && slide()) { waiting = false; return null; }
                } else if (waiting && !dialog.isShowing()) {
                    // 이미 닫은 창에 유니티가 닫기를 또 걸었다 — 닫힘 소식이 다시 안 오니 직접 알린다 (안 그러면 유니티가 1초 멈춘다)
                    waiting = false;
                    try { onClose.invoke(real); } catch (Throwable ignored) { }
                    return null;
                }
            }
            waiting = args != null && args.length == 1 && holdsWaiter(args[0]);
            try {
                return m.invoke(real, args);
            } catch (InvocationTargetException e) {
                Throwable c = e.getCause();
                throw c != null ? c : e;
            }
        }

        // 키보드를 그 창에서 먼저 내리고(창이 남아 있는 동안 기본 움직임으로 미끄러진다) 유니티의 기다림을 풀어 준 뒤, 다 내려가면 창을 닫는다
        private boolean slide() {
            Window w = dialog.getWindow();
            final View decor = w == null ? null : w.peekDecorView();
            if (decor == null || !dialog.isShowing()) return false;
            try {
                decor.setWindowInsetsAnimationCallback(new WindowInsetsAnimation.Callback(WindowInsetsAnimation.Callback.DISPATCH_MODE_STOP) {
                    @Override public WindowInsets onProgress(WindowInsets insets, List<WindowInsetsAnimation> running) { return insets; }
                    @Override public void onEnd(WindowInsetsAnimation anim) {
                        if ((anim.getTypeMask() & WindowInsets.Type.ime()) != 0) UI.post(closer);
                    }
                });
                if (imeVisible(dialog)) {
                    WindowInsetsController c = w.getInsetsController();
                    if (c != null) c.hide(WindowInsets.Type.ime());
                }   // 완료 키는 안드로이드가 이미 내리는 중 — 창만 늦게 닫으면 된다
                decor.setAlpha(0f);                // 유니티 입력줄을 숨기지 않는 칸이면 그 줄은 예전처럼 바로 없앤다 — 키보드만 미끄러진다
                onClose.invoke(real);              // 유니티가 기다리는 것 ① 창이 닫혔다
                raiseFocus.invoke(player, true);   // ② 화면이 포커스를 되찾았다 → 유니티가 바로 다음으로 넘어간다
            } catch (Throwable t) {
                try { decor.setWindowInsetsAnimationCallback(null); } catch (Throwable ignored) { }
                try { decor.setAlpha(1f); } catch (Throwable ignored) { }
                return false;
            }
            UI.postDelayed(closer, scaled(FALLBACK_MS));
            return true;
        }

        private void close() {
            if (closed) return;
            closed = true;
            UI.removeCallbacks(closer);
            Window w = dialog.getWindow();
            View decor = w == null ? null : w.peekDecorView();
            if (decor != null) {
                try { decor.setWindowInsetsAnimationCallback(null); } catch (Throwable ignored) { }
            }
            boolean ghost = false;
            try {
                if (softField.get(player) == null) {   // 새 키보드가 안 떴다 — 창을 닫아 포커스가 돌아올 때 유니티가 앱 포커스로 알리지 않게
                    softField.set(player, self);
                    ghost = true;
                }
            } catch (Throwable ignored) { }
            if (ghost) watchFocus();
            try { hide.invoke(real); } catch (Throwable ignored) { }
            if (!ghost) settle();
        }

        // 유니티 화면이 포커스를 되찾으면(유니티가 먼저 받은 뒤) 다시 넣어 둔 것을 뺀다
        private void watchFocus() {
            final View decor = activity.getWindow() == null ? null : activity.getWindow().peekDecorView();
            final ViewTreeObserver.OnWindowFocusChangeListener[] listener = new ViewTreeObserver.OnWindowFocusChangeListener[1];
            final Runnable release = new Runnable() {
                private boolean ran;
                @Override public void run() {
                    if (ran) return;
                    ran = true;
                    UI.removeCallbacks(this);
                    if (decor != null && listener[0] != null) {
                        try { decor.getViewTreeObserver().removeOnWindowFocusChangeListener(listener[0]); } catch (Throwable ignored) { }
                    }
                    try {
                        if (softField.get(player) == self) softField.set(player, null);
                    } catch (Throwable ignored) { }
                    settle();
                }
            };
            if (decor != null) {
                listener[0] = new ViewTreeObserver.OnWindowFocusChangeListener() {
                    @Override public void onWindowFocusChanged(boolean hasFocus) {
                        if (hasFocus) release.run();
                    }
                };
                try { decor.getViewTreeObserver().addOnWindowFocusChangeListener(listener[0]); } catch (Throwable ignored) { }
            }
            UI.postDelayed(release, FOCUS_WAIT_MS);
        }

        // 내려가는 사이 유니티가 키보드 크기를 다시 받았을 수 있다 — 새 키보드가 없으면 「키보드 없음」으로 한 번 더 알린다
        private void settle() {
            try {
                if (softField.get(player) != null) return;
                if (reportArea != null) reportArea.invoke(player, new Rect());
                if (reportVisible != null) reportVisible.invoke(player, false);
            } catch (Throwable ignored) { }
        }

        private long scaled(int ms) {
            float s = 1f;
            try {
                s = Settings.Global.getFloat(activity.getContentResolver(), Settings.Global.ANIMATOR_DURATION_SCALE, 1f);
            } catch (Throwable ignored) { }
            return Math.round(ms * Math.max(0f, Math.min(s, 4f)));
        }
    }
}
