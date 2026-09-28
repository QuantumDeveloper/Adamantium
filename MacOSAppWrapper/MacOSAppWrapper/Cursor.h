//
//  Cursor.h
//  MacOSAppWrapper
//
//  Created by Denys Zaporozhets on 9/14/19.
//  Copyright © 2019 Denys Zaporozhets. All rights reserved.
//

#ifndef Cursor_h
#define Cursor_h

#import <AppKit/AppKit.h>

typedef enum CursorType : NSUInteger
{
    ArrowCursor = 0,
    IBeamCursor = 1,
    PointingHandCursor = 2,
    ClosedHandCursor = 3,
    OpenHandCursor = 4,
    ResizeLeftCursor = 5,
    ResizeRightCursor = 6,
    ResizeLeftRightCursor = 7,
    ResizeUpCursor = 8,
    ResizeDownCursor = 9,
    ResizeUpDownCursor = 10,
    CrosshairCursor = 11,
    DisappearingItemCursor = 12,
    OperationNotAllowedCursor = 13,
    DragLinkCursor = 14,
    DragCopyCursor = 15,
    ContextualMenuCursor = 16,
    IBeamCursorForVerticalLayout = 17
} CursorType;

APPKIT_EXTERN void Hide(void);

APPKIT_EXTERN void Unhide(void);

APPKIT_EXTERN void SetHiddenUntilMouseMoves(bool flag);

APPKIT_EXTERN void Pop(void);

APPKIT_EXTERN id GetCurrentCursor(void);

APPKIT_EXTERN id GetCursorType(unsigned int cursorType);

APPKIT_EXTERN id SetCursorType(unsigned int cursorType);

#endif /* Cursor_h */
