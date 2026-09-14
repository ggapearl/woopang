#import "UnityAppController.h"
#import <UIKit/UIKit.h>

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

@end

IMPL_APP_CONTROLLER_SUBCLASS(WoopangAppController)
