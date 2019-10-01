//
//  Cursor.m
//  MacOSAppWrapper
//
//  Created by Denys Zaporozhets on 9/14/19.
//  Copyright © 2019 Denys Zaporozhets. All rights reserved.
//

#import <Foundation/Foundation.h>
#import <AppKit/AppKit.h>
#import <AppKit/NSCursor.h>
#import "Cursor.h"

void Hide()
{
    [NSCursor hide];
}

void Unhide()
{
    [NSCursor unhide];
}

void SetHiddenUntilMouseMoves(bool flag)
{
    [NSCursor setHiddenUntilMouseMoves:flag];
}

void Pop()
{
    [NSCursor pop];
}

id GetCurrentCursor()
{
    return [NSCursor currentCursor];
}

id GetCursorType(unsigned int cursorType)
{
    CursorType type = (CursorType)cursorType;
    switch (type) {
        case IBeamCursor:
            return [NSCursor arrowCursor];
        case PointingHandCursor:
            return [NSCursor pointingHandCursor];
        case ClosedHandCursor:
            return [NSCursor closedHandCursor];
        case OpenHandCursor:
            return [NSCursor openHandCursor];
        case ResizeLeftCursor:
            return [NSCursor resizeLeftCursor];
        case ResizeRightCursor:
            return [NSCursor resizeRightCursor];
        case ResizeLeftRightCursor:
            return [NSCursor resizeLeftRightCursor];
        case ResizeUpCursor:
            return [NSCursor resizeUpCursor];
        case ResizeDownCursor:
            return [NSCursor resizeDownCursor];
        case ResizeUpDownCursor:
            return [NSCursor resizeUpDownCursor];
        case CrosshairCursor:
            return [NSCursor crosshairCursor];
        case DisappearingItemCursor:
            return [NSCursor disappearingItemCursor];
        case OperationNotAllowedCursor:
            return [NSCursor operationNotAllowedCursor];
        case DragLinkCursor:
            return [NSCursor dragLinkCursor];
        case DragCopyCursor:
            return [NSCursor dragCopyCursor];
        case ContextualMenuCursor:
            return [NSCursor contextualMenuCursor];
        case IBeamCursorForVerticalLayout:
            return [NSCursor IBeamCursorForVerticalLayout];
        default:
            return [NSCursor arrowCursor];
    }
}

id SetCursorType(unsigned int cursorType)
{
    CursorType type = (CursorType)cursorType;
    switch (type) {
        case IBeamCursor:
            [[NSCursor arrowCursor] set];
            break;
        case PointingHandCursor:
            [[NSCursor pointingHandCursor] set];
            break;
        case ClosedHandCursor:
            [[NSCursor closedHandCursor] set];
            break;
        case OpenHandCursor:
            [[NSCursor openHandCursor] set];
            break;
        case ResizeLeftCursor:
            [[NSCursor resizeLeftCursor] set];
            break;
        case ResizeRightCursor:
            [[NSCursor resizeRightCursor] set];
            break;
        case ResizeLeftRightCursor:
            [[NSCursor resizeLeftRightCursor] set];
            break;
        case ResizeUpCursor:
            [[NSCursor resizeUpCursor] set];
            break;
        case ResizeDownCursor:
            [[NSCursor resizeDownCursor] set];
            break;
        case ResizeUpDownCursor:
            [[NSCursor resizeUpDownCursor] set];
            break;
        case CrosshairCursor:
            [[NSCursor crosshairCursor] set];
            break;
        case DisappearingItemCursor:
            [[NSCursor disappearingItemCursor] set];
            break;
        case OperationNotAllowedCursor:
            [[NSCursor operationNotAllowedCursor] set];
            break;
        case DragLinkCursor:
            [[NSCursor dragLinkCursor] set];
            break;
        case DragCopyCursor:
            [[NSCursor dragCopyCursor] set];
            break;
        case ContextualMenuCursor:
            [[NSCursor contextualMenuCursor] set];
            break;
        case IBeamCursorForVerticalLayout:
            [[NSCursor IBeamCursorForVerticalLayout] set];
            break;
        default:
            [[NSCursor arrowCursor] set];
            break;
    }
    
    return [NSCursor currentCursor];
}
