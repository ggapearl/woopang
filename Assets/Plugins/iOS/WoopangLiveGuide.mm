#import <Foundation/Foundation.h>

// 우팡 길안내 실시간 현황 — 유니티(R0926LiveGuide.cs)와 WoopangLiveGuide.swift 를 잇는다 (11-26 대표님 10번 ②).
// Swift 쪽은 이름(WoopangLiveGuideObjC)으로 찾는다 — 맥에서 실시간 현황을 빼고 빌드해도(WOOPANG_NO_LIVE_ACTIVITY=1 · Swift 파일이 빠짐)
// 이 함수들은 그대로 있어 앱이 그대로 돈다 (아무것도 하지 않는다).

@protocol WoopangLiveGuideAPI
+ (BOOL)beginWithName:(NSString *)name lat:(double)lat lon:(double)lon arrive:(double)arrive maxSeconds:(double)maxSeconds;
+ (void)finishArrived:(BOOL)arrived;
@end

static Class<WoopangLiveGuideAPI> WoopangLiveGuideClass(void) {
    return (Class<WoopangLiveGuideAPI>)NSClassFromString(@"WoopangLiveGuideObjC");
}

extern "C" {
    int _WoopangLiveGuideBegin(const char *name, double lat, double lon, double arriveM, double maxSec) {
        Class<WoopangLiveGuideAPI> c = WoopangLiveGuideClass();
        if (c == nil || ![(id)c respondsToSelector:@selector(beginWithName:lat:lon:arrive:maxSeconds:)]) return 0;
        NSString *n = name != NULL ? [NSString stringWithUTF8String:name] : @"";
        return [c beginWithName:(n ?: @"") lat:lat lon:lon arrive:arriveM maxSeconds:maxSec] ? 1 : 0;
    }

    void _WoopangLiveGuideEnd(int arrived) {
        Class<WoopangLiveGuideAPI> c = WoopangLiveGuideClass();
        if (c == nil || ![(id)c respondsToSelector:@selector(finishArrived:)]) return;
        [c finishArrived:(arrived != 0)];
    }
}
