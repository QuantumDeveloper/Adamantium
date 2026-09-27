//
//  Adamantium_OSXNative.h
//  Adamantium.OSXNative
//
//  Created by Denys Zaporozhets on 01.11.2019.
//  Copyright © 2019 Denys Zaporozhets. All rights reserved.
//

#import <Foundation/Foundation.h>
#import <AppKit/AppKit.h>
#import "AppWrapper.h"
#import <QuartzCore/QuartzCore.h>
#import <Metal/Metal.h>

//! Project version number for Adamantium_OSXNative.
FOUNDATION_EXPORT double Adamantium_OSXNativeVersionNumber;

//! Project version string for Adamantium_OSXNative.
FOUNDATION_EXPORT const unsigned char Adamantium_OSXNativeVersionString[];

// In this header, you should import all the public headers of your framework using statements like #import <Adamantium_OSXNative/PublicHeader.h>


public class AppDelegate : NSApplicationDelegate
{
    public NSMutableArray Windows { get; }
}


