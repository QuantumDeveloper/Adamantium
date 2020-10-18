//
//  AppWrapper.h
//  MacOSAppWrapper
//
//  Created by Denys Zaporozhets on 9/13/19.
//  Copyright © 2019 Denys Zaporozhets. All rights reserved.
//

#ifndef AppWrapper_h
#define AppWrapper_h

#import <AppKit/AppKit.h>

struct Rectangle
{
    unsigned int left;
    unsigned int top;
    unsigned int width;
    unsigned int height;
};

struct SizeF
{
    float width;
    float height;
};

typedef void (* WindowWillResizeCallback)(struct SizeF current, struct SizeF future);

typedef void (* WindowDidResizeCallback)(struct SizeF windowSize);

typedef void (* ApplicationClosedCallback) (void);

void makeViewMetalCompatible(void *handle);


APPKIT_EXTERN id CreateWindow(struct Rectangle windowRect, unsigned int windowStyle, char * title);

APPKIT_EXTERN void* GetViewPtr(void* window);

APPKIT_EXTERN struct SizeF GetViewSizeF(void* window);

APPKIT_EXTERN void ShowWindow(NSWindow *window);

APPKIT_EXTERN void ShowWindow2(NSWindow *window, id owner);

APPKIT_EXTERN void AddWindowToAppDelegate(id appDelegate, id window);

APPKIT_EXTERN id CreateApplication(id appDelegate);

APPKIT_EXTERN id CreateApplicationDelegate(void);

APPKIT_EXTERN id CreateWindowDelegate(void);

APPKIT_EXTERN void SetWindowDelegate(id window, id windowDelegate);

APPKIT_EXTERN id GetDelegateFromApp(id app);

APPKIT_EXTERN int RunApplication(NSApplication* app);

APPKIT_EXTERN void AddWindowWillResizeCallback(id windowDelegate, WindowWillResizeCallback windowWillResize);

APPKIT_EXTERN void AddWindowDidResizeCallback(id windowDelegate, WindowDidResizeCallback windowdidResize);

APPKIT_EXTERN void AddApplicationClosedCallback(ApplicationClosedCallback appClosed);

#endif /* AppWrapper_h */
