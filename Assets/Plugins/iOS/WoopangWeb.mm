#import <UIKit/UIKit.h>
#import <AuthenticationServices/AuthenticationServices.h>

// 앱 안 브라우저 — 웹 로그인 · 계정 관리(비밀번호 변경·계정 삭제) (R0926Web.Open, 11-02)
// ASWebAuthenticationSession: 페이지가 <scheme>://… 로 가면 시트가 닫히며 그 주소를 Unity 로 보낸다 (R0926Web.OnWebReturn).
// 사용자가 닫으면 빈 글. ephemeral(사파리와 쿠키를 나누지 않음)이면 「…로 로그인하려고 합니다」 확인 창도 뜨지 않는다.

extern UIViewController* UnityGetGLViewController();   // UnitySendMessage 는 Unity 머리 파일(Prefix.pch)에 있다 — NativeCamera.mm 처럼 그냥 부른다

@interface WoopangWebAnchor : NSObject <ASWebAuthenticationPresentationContextProviding>
@end

@implementation WoopangWebAnchor
- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession*)session
{
    return UnityGetGLViewController().view.window;
}
@end

// 끝날 때까지 쥐고 있어야 한다 — 놓으면 시트가 안 뜨거나 바로 닫힌다
static ASWebAuthenticationSession* s_session;
static WoopangWebAnchor* s_anchor;

extern "C" bool _WoopangWebOpen(const char* url, const char* scheme, bool ephemeral)
{
    if (url == NULL || scheme == NULL) return false;
    NSURL* u = [NSURL URLWithString:[NSString stringWithUTF8String:url]];
    NSString* sc = [NSString stringWithUTF8String:scheme];
    if (u == nil || sc.length == 0) return false;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (s_session != nil) [s_session cancel];
        ASWebAuthenticationSession* session = [[ASWebAuthenticationSession alloc] initWithURL:u callbackURLScheme:sc
            completionHandler:^(NSURL* callbackURL, NSError* error) {
                NSString* back = callbackURL != nil ? callbackURL.absoluteString : @"";
                dispatch_async(dispatch_get_main_queue(), ^{
                    s_session = nil;
                    UnitySendMessage("R0926Web", "OnWebReturn", back.UTF8String);
                });
            }];
        if (s_anchor == nil) s_anchor = [WoopangWebAnchor new];
        session.presentationContextProvider = s_anchor;
        session.prefersEphemeralWebBrowserSession = ephemeral;
        s_session = session;
        if (![session start])
        {
            s_session = nil;
            UnitySendMessage("R0926Web", "OnWebFailed", u.absoluteString.UTF8String);
        }
    });
    return true;
}
