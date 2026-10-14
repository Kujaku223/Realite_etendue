#import <UIKit/UIKit.h>
#import <objc/runtime.h>

static UIInterfaceOrientation CurrentInterfaceOrientation(void)
{
    for (UIScene* scene in UIApplication.sharedApplication.connectedScenes)
    {
        if (![scene isKindOfClass:UIWindowScene.class])
            continue;

        UIWindowScene* windowScene = (UIWindowScene*)scene;
        if (@available(iOS 16.0, *))
            return windowScene.effectiveGeometry.interfaceOrientation;
        return windowScene.interfaceOrientation;
    }
    return UIInterfaceOrientationPortrait;
}

@implementation UIApplication (StatusBarOrientationFix)

+ (void)load
{
    Method original = class_getInstanceMethod(self, @selector(statusBarOrientation));
    IMP replacement = imp_implementationWithBlock(^UIInterfaceOrientation(id application) {
        return CurrentInterfaceOrientation();
    });
    method_setImplementation(original, replacement);
}

@end
