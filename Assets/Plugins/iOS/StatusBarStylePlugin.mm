#import <UIKit/UIKit.h>

extern "C" {
    void _SetStatusBarStyle(bool lightContent) {
        dispatch_async(dispatch_get_main_queue(), ^{
            #pragma clang diagnostic push
            #pragma clang diagnostic ignored "-Wdeprecated-declarations"
            UIStatusBarStyle style = lightContent ? UIStatusBarStyleLightContent : UIStatusBarStyleDefault;
            [[UIApplication sharedApplication] setStatusBarStyle:style animated:YES];
            #pragma clang diagnostic pop
        });
    }

    // R0926Notch — 노치 옆에 날씨 · 안내가 떠 있는 동안 시계 · 배터리를 숨긴다 (노치 양옆 같은 자리에 그려져 겹친다)
    void _SetStatusBarHidden(bool hidden) {
        dispatch_async(dispatch_get_main_queue(), ^{
            #pragma clang diagnostic push
            #pragma clang diagnostic ignored "-Wdeprecated-declarations"
            [[UIApplication sharedApplication] setStatusBarHidden:hidden withAnimation:UIStatusBarAnimationFade];
            #pragma clang diagnostic pop
        });
    }
}
