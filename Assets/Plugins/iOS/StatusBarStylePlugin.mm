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
}
