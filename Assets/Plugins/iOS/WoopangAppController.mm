#import "UnityAppController.h"
#import <UIKit/UIKit.h>
#include <unistd.h>

// ============================================================================
// 우팡 전용 AppController.
//
// 예전에는 홈 디렉터리의 UnityAppController.mm 을 빌드 후 Classes/ 에 통째로
// 덮어써서 아래 동작을 넣고 있었다. 그 방식은 두 가지 문제가 있었다.
//   1) 파일이 저장소에 없어 그 맥에서만 빌드가 재현된다.
//      못 찾아도 경고만 남기고 빌드가 계속돼 조용히 누락된다.
//   2) UnityAppController.mm 은 Unity 가 매 빌드 생성하는 산출물이다.
//      Unity 를 올리면 원본 API 가 바뀌는데(startUnity → startUnity: ,
//      initUnityWithApplication: → initUnityWithScene:) 옛 사본으로 덮어쓰면
//      메서드가 사라져 실행 즉시 크래시한다.
//
// Unity 6000.4.6f1 원본과 오버라이드본을 실측 비교한 결과, 실제 우팡 고유
// 동작은 **APNs 토큰을 NSUserDefaults 에 저장하는 것 하나뿐**이었다.
// (Assets/Scripts/Firebase/FirebaseNotification.cs 가 "APNSDeviceToken" 을 읽는다)
// 나머지 119줄 차이는 Unity 버전 간 API 변경분이었고,
// shouldUseMetalDisplayLink 강제 NO 는 플레이어 설정
// metalUseMetalDisplayLink=0 이 이미 같은 결과를 내므로 불필요했다.
//
// 그래서 그 하나만 서브클래스로 옮긴다. Unity 트램폴린 원본은 그대로 유지되어
// 엔진 업그레이드에 깨지지 않는다.
//
// 2026-11-09 추가: 화면에 떠 있지 않을 때 iOS 가 앱을 끝내면 엔진 정리를 건너뛴다
// (applicationWillTerminate 아래 설명 — TestFlight 「앱 충돌」 0x8BADF00D).
// ============================================================================

@interface WoopangAppController : UnityAppController
@end

@implementation WoopangAppController

- (void)application:(UIApplication*)application
    didRegisterForRemoteNotificationsWithDeviceToken:(NSData*)deviceToken
{
    // Unity 기본 처리(엔진으로 토큰 전달)를 먼저 그대로 수행한다.
    [super application:application didRegisterForRemoteNotificationsWithDeviceToken:deviceToken];

    // APNs 토큰을 hex 문자열로 바꿔 PlayerPrefs(NSUserDefaults)에 저장한다.
    // FirebaseNotification.cs 가 이 값을 읽는다.
    const unsigned char* bytes = (const unsigned char*)[deviceToken bytes];
    NSMutableString* tokenString = [NSMutableString string];
    for (NSInteger i = 0; i < (NSInteger)[deviceToken length]; i++)
    {
        [tokenString appendFormat: @"%02x", bytes[i]];
    }

    if ([tokenString length] > 0)
    {
        NSUserDefaults* defaults = [NSUserDefaults standardUserDefaults];
        [defaults setObject: tokenString forKey: @"APNSDeviceToken"];
        [defaults setObject: tokenString forKey: @"apns_token"];
        [defaults synchronize];
        NSLog(@"[WOOPANG] APNs token saved (%lu chars)", (unsigned long)[tokenString length]);
    }
}

- (void)application:(UIApplication*)application
    didFailToRegisterForRemoteNotificationsWithError:(NSError*)error
{
    [super application:application didFailToRegisterForRemoteNotificationsWithError:error];
    NSLog(@"[WOOPANG] APNs 등록 실패: %@ (code %ld, domain %@)",
          [error localizedDescription], (long)[error code], [error domain]);
}

// 백그라운드(위치 · 알림 · 백그라운드 새로 고침으로 깨어 있던 때)나 앱 전환기에서 iOS 가 앱을 끝낼 때 —
// 엔진 정리를 건너뛰고 바로 끝낸다. iOS 는 이 함수가 5초 안에 안 돌아오면 앱을 강제로 죽이고 「충돌」로 남긴다.
//
// 1.2.66 TestFlight 충돌 2건(11-06 · 11-08, 같은 아이폰 15)이 이것이었다: 메인 스레드가
// UnityCleanup → il2cpp Runtime::Shutdown → os::Thread::Shutdown 에서 멈춰 있었다.
// il2cpp 는 끝날 때 C# 에 들렀던 스레드 기록마다 10ms 씩 기다린다(libil2cpp os/Thread.cpp). 네이티브 스레드가
// 콜백으로 잠깐 들렀다 나가면 그 기록은 GC 가 치울 때까지 남는다(vm/ScopedThreadAttacher). 아이폰은 렌더 스레드를
// 따로 쓰고 AR 카메라 배경이 매 프레임 그 스레드에서 C# 을 부르므로, 기록이 수백 개 쌓인 채 끝나 5초를 넘긴 것으로 본다.
//
// 곧 프로세스가 사라지므로 정리할 것이 없다 — 설정(PlayerPrefs = NSUserDefaults)은 저장돼 있고,
// 화면을 내릴 때 Unity 가 이미 OnApplicationPause(true) 를 불렀다.
// 건너뛰는 OnApplicationQuit 중 남는 일은 CrashReporter 의 「화면을 켠 채 끝남」 표식 지우기 하나 —
// 백그라운드로 켜졌다 끝나면(화면을 내린 적이 없다) 다음 실행에 거짓 충돌로 보고되므로 여기서 지운다.
// _exit 는 정적 소멸자를 돌리지 않는다 — 정리하지 않은 엔진 위에서 소멸자가 돌다 멈추거나 죽지 않게.
// 화면에 떠 있는 채 끝날 때만 원래대로 정리한다.
- (void)applicationWillTerminate:(UIApplication*)application
{
    if (application.applicationState != UIApplicationStateActive)
    {
        NSLog(@"[WOOPANG] terminate while not active - skip engine cleanup");
        NSUserDefaults* defaults = [NSUserDefaults standardUserDefaults];
        [defaults removeObjectForKey: @"Crash_Live"];   // CrashReporter.LiveKey
        [defaults synchronize];
        _exit(0);
    }
    [super applicationWillTerminate:application];
}

@end

IMPL_APP_CONTROLLER_SUBCLASS(WoopangAppController)
