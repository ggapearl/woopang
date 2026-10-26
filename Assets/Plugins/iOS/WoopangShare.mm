#import <UIKit/UIKit.h>

// 시스템 공유 창 (UIActivityViewController) — 보물찾기 '공유하기' (R0926Share.Text)
// 맨 위에 떠 있는 화면에서 띄운다 · 아이패드는 화면 가운데 팝오버

extern UIViewController* UnityGetGLViewController();

extern "C" void _WoopangShareText(const char* text)
{
    if (text == NULL) return;
    NSString* s = [NSString stringWithUTF8String:text];
    if (s == nil) return;
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* top = UnityGetGLViewController();
        while (top.presentedViewController != nil) top = top.presentedViewController;
        UIActivityViewController* vc = [[UIActivityViewController alloc] initWithActivityItems:@[s] applicationActivities:nil];
        UIPopoverPresentationController* pop = vc.popoverPresentationController;
        if (pop != nil)
        {
            pop.sourceView = top.view;
            pop.sourceRect = CGRectMake(CGRectGetMidX(top.view.bounds), CGRectGetMidY(top.view.bounds), 0, 0);
            pop.permittedArrowDirections = 0;
        }
        [top presentViewController:vc animated:YES completion:nil];
    });
}
