//
//  AppWrapper.m
//  MacOSAppWrapper
//
//  Created by Denys Zaporozhets on 9/13/19.
//  Copyright © 2019 Denys Zaporozhets. All rights reserved.
//

#import <Foundation/Foundation.h>
#import <AppKit/AppKit.h>
#import "AppWrapper.h"
#import <QuartzCore/QuartzCore.h>
#import <Metal/Metal.h>

@interface WindowDelegate: NSObject<NSWindowDelegate>
@property WindowWillResizeCallback willResizeCallback;
@property WindowDidResizeCallback didResizeCallback;

//- (BOOL)windowShouldClose:(NSWindow *)sender;
//- (nullable id)windowWillReturnFieldEditor:(NSWindow *)sender toObject:(nullable id)client;
//- (NSSize)windowWillResize:(NSWindow *)sender toSize:(NSSize)frameSize;
//- (NSRect)windowWillUseStandardFrame:(NSWindow *)window defaultFrame:(NSRect)newFrame;
//- (BOOL)windowShouldZoom:(NSWindow *)window toFrame:(NSRect)newFrame;
//- (nullable NSUndoManager *)windowWillReturnUndoManager:(NSWindow *)window;
@end

@interface AppDelegate : NSObject<NSApplicationDelegate>
@property (strong) NSMutableArray* windows;
@end

@implementation WindowDelegate

//- (NSSize)windowWillResize:(NSWindow *)sender toSize:(NSSize)frameSize
//{
//    struct SizeF current;
//    struct SizeF future;
//    current.width = sender.frame.size.width;
//    current.height = sender.frame.size.height;
//
//    future.width = frameSize.width;
//    future.height = frameSize.height;
//    _willResizeCallback(current, future);
//    return frameSize;
//}

- (void)windowDidResize:(NSNotification *)notification
{
    NSWindow *wnd = notification.object;
    struct SizeF current;
    current.width = wnd.frame.size.width;
    current.height = wnd.frame.size.height;
    _didResizeCallback(current);
}

- (void)addWindowWillResizeCallback: (WindowWillResizeCallback) callback
{
    _willResizeCallback = callback;
}

- (void)addWindowDidResizeCallback: (WindowDidResizeCallback) callback
{
    _didResizeCallback = callback;
}

@end

@interface AdamantiumWindow : NSWindow

@end

@interface AdamantiumView : NSView

@end

@implementation AdamantiumView

/** Indicates that the view wants to draw using the backing layer instead of using drawRect:.  */
-(BOOL) wantsUpdateLayer { return YES; }

/** Returns a Metal-compatible layer. */
+ (Class) layerClass { return [CAMetalLayer class]; }

/** If the wantsLayer property is set to YES, this method will be invoked to return a layer instance. */
- (CALayer *) makeBackingLayer {
    NSLog(@"entered makeBackingLayer");
    CALayer* layer = [self.class.layerClass layer];
    CGSize viewScale = [self convertSizeToBacking: CGSizeMake(1.0, 1.0)];
    layer.contentsScale = MIN(viewScale.width, viewScale.height);
    return layer;
}

@end

@implementation AdamantiumWindow

- (void) dealloc
{
    NSLog(@"window is deallocated");
}

@end

@implementation AppDelegate

-(id)init
{
//    if(self = [super init]) {
//        self.window = CreateWindow(100, 100, 1280, 720);
//    }
    _windows = [[NSMutableArray alloc] init];
    NSLog(@"App initialized");
    return self;
}

- (void)applicationWillFinishLaunching:(NSNotification *)notification
{
    NSLog(@"App will finish launching");
}

- (void)applicationDidFinishLaunching:(NSNotification *)notification
{
    NSLog(@"App did finish launching");
    for(NSWindow *window in _windows)
    {
        NSLog(@"trying to show window");
        ShowWindow2(window, self);
        //ShowWindow(window);
    }
}

- (void)applicationWillTerminate:(NSNotification *)notification
{
    NSLog(@"app will be terminated");
}

- (BOOL) applicationShouldTerminateAfterLastWindowClosed: (NSApplication *) theApplication
{
    return YES;
}

- (void)addWindow: (NSWindow *)window
{
    [_windows addObject:window];
}

@end

id CreateWindow(struct Rectangle windowRect, unsigned int windowStyle, char * title)
{
    NSRect contentSize = NSMakeRect(windowRect.left, windowRect.top, windowRect.width, windowRect.height);
    AdamantiumWindow *window = [[AdamantiumWindow alloc] initWithContentRect:contentSize styleMask:windowStyle backing:NSBackingStoreBuffered defer:YES];
    window.backgroundColor = [NSColor whiteColor];
    window.title = @(title);
    
    // Create a view
//    AdamantiumView *view = [[AdamantiumView alloc] initWithFrame:CGRectMake(0, 0, window.contentLayoutRect.size.width, window.contentLayoutRect.size.height)];
    AdamantiumView *view = [[AdamantiumView alloc] init];
    [view setAutoresizingMask:NSViewWidthSizable|NSViewHeightSizable];
    [window setContentView:view];
    
    void *viewHandle = (__bridge void*) [window contentView];
    makeViewMetalCompatible(viewHandle);
    return window;
}

void makeViewMetalCompatible(void *handle)
{
    NSView *view = (__bridge NSView *)handle;
    assert([view isKindOfClass:[NSView class]]);
    
    if (![view.layer isKindOfClass:[CAMetalLayer class]])
    {
        view.wantsLayer = YES;
        [view setLayer:[CAMetalLayer layer]];
    }
}

void* GetViewPtr(void* window)
{
    NSWindow *wnd = (__bridge NSWindow *)window;
    void *viewHandle = (__bridge void*) [wnd contentView];
    //makeViewMetalCompatible(viewHandle);
    NSLog(@"View pointer is: %@", viewHandle);
    return viewHandle;
}

struct SizeF GetViewSize(void* window)
{
    NSWindow *wnd = (__bridge NSWindow *)window;
    struct SizeF SizeF;
    NSRect bounds = [wnd.contentView bounds];
    SizeF.width = bounds.size.width;
    SizeF.height = bounds.size.height;
    return SizeF;
}

void AddWindowToAppDelegate(id appDelegate, id window)
{
    AppDelegate *appDel = appDelegate;
    NSWindow *wnd = window;
    [appDel addWindow:wnd];
    NSLog(@"Added window to app delegate %@", window);
}

void ShowWindow(id window)
{
    NSLog(@"try to show window");
    NSWindow *wnd = window;
    [wnd makeKeyAndOrderFront: 0];
}

void ShowWindow2(id window, id owner)
{
    NSLog(@"try to show window");
    NSWindow *wnd = window;
    [wnd makeKeyAndOrderFront: owner];
}

void AddWindowWillResizeCallback(id windowDelegate, WindowWillResizeCallback resizeCallback)
{
    WindowDelegate *wndDel = windowDelegate;
    [wndDel addWindowWillResizeCallback:resizeCallback];
}

void AddWindowDidResizeCallback(id windowDelegate, WindowDidResizeCallback resizeCallback)
{
    WindowDelegate *wndDel = windowDelegate;
    [wndDel addWindowDidResizeCallback:resizeCallback];
}

id CreateApplicationDelegate()
{
    AppDelegate *applicationDelegate = [[AppDelegate alloc] init];
    return applicationDelegate;
}

id CreateApplication(id appDelegate)
{
    NSApplication *application = [NSApplication sharedApplication];
    AppDelegate *applicationDelegate = appDelegate;
    [application setDelegate:applicationDelegate];
    return application;
}

id CreateWindowDelegate()
{
    return [[WindowDelegate alloc] init];
}

void SetWindowDelegate(id window, id windowDelegate)
{
    NSWindow *wnd = window;
    WindowDelegate *wndDelegate = windowDelegate;
    [wnd setDelegate: wndDelegate];
}

id GetDelegateFromApp(id app)
{
    NSApplication *application = app;
    return [application delegate];
}

int RunApplication(id app)
{
    @autoreleasepool {
        NSApplication *application = app;
        
        [NSApp setActivationPolicy:NSApplicationActivationPolicyRegular];
        id menubar = [NSMenu new];
        id appMenuItem = [NSMenuItem new];
        [menubar addItem:appMenuItem];
        [NSApp setMainMenu:menubar];
        id appMenu = [NSMenu new];
        id appName = [[NSProcessInfo processInfo] processName];
        id quitTitle = [@"Quit " stringByAppendingString:appName];
        id quitMenuItem = [[NSMenuItem alloc] initWithTitle:quitTitle
                                                      action:@selector(terminate:) keyEquivalent:@"q"];
        [appMenu addItem:quitMenuItem];
        [appMenuItem setSubmenu:appMenu];
        
        [application run];
    }
    return 0;
}
