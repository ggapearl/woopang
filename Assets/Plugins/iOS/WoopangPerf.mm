#import <Foundation/Foundation.h>

// 성능 기록(PerfLog.cs) — 발열 단계와 저전력 모드 (11-14)

extern "C" {
    // 0 nominal · 1 fair · 2 serious · 3 critical
    int _WoopangThermalState() {
        return (int)[[NSProcessInfo processInfo] thermalState];
    }

    int _WoopangLowPowerMode() {
        return [[NSProcessInfo processInfo] isLowPowerModeEnabled] ? 1 : 0;
    }
}
