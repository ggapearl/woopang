#import <UIKit/UIKit.h>

// UIImpactFeedbackGenerator 기반 Taptic Engine 제어
// style: 0=Light, 1=Medium, 2=Heavy

extern "C" {
    void _WoopangTriggerHaptic(int style) {
        if (@available(iOS 10.0, *)) {
            UIImpactFeedbackStyle feedbackStyle;
            switch (style) {
                case 0:
                    feedbackStyle = UIImpactFeedbackStyleLight;
                    break;
                case 1:
                    feedbackStyle = UIImpactFeedbackStyleMedium;
                    break;
                case 2:
                    feedbackStyle = UIImpactFeedbackStyleHeavy;
                    break;
                default:
                    feedbackStyle = UIImpactFeedbackStyleLight;
                    break;
            }
            UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:feedbackStyle];
            [generator prepare];
            [generator impactOccurred];
        }
    }

    void _WoopangTriggerSelectionHaptic() {
        if (@available(iOS 10.0, *)) {
            UISelectionFeedbackGenerator *generator = [[UISelectionFeedbackGenerator alloc] init];
            [generator prepare];
            [generator selectionChanged];
        }
    }
}
