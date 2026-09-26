/*
 * gameinput-c-shared: a thin flat-C facade over Microsoft GameInput (API v3).
 *
 * GameInput's public API is C++/COM-style; QuantumBinding only parses C headers, so this shim exposes what the
 * engine needs from gamepads - arrival and removal, the reading with the Elite paddles, the Guide/Share system
 * buttons, what is printed on the buttons, and rumble - as opaque-handle C functions.
 */
#ifndef GAMEINPUT_C_SHARED_H
#define GAMEINPUT_C_SHARED_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#if defined(_WIN32)
#define GAMEINPUTC_API __declspec(dllexport)
#else
#define GAMEINPUTC_API __attribute__((visibility("default")))
#endif

/* Opaque handles. */
typedef struct GameInputcContext_T* GameInputcContext;
typedef struct GameInputcDevice_T*  GameInputcDevice;

/* Gamepad buttons. Values mirror GameInputGamepadButtons. */
typedef enum GameInputcGamepadButtons
{
    GAMEINPUTC_GAMEPAD_NONE                  = 0x00000000,
    GAMEINPUTC_GAMEPAD_MENU                  = 0x00000001,
    GAMEINPUTC_GAMEPAD_VIEW                  = 0x00000002,
    GAMEINPUTC_GAMEPAD_A                     = 0x00000004,
    GAMEINPUTC_GAMEPAD_B                     = 0x00000008,
    GAMEINPUTC_GAMEPAD_C                     = 0x00004000,
    GAMEINPUTC_GAMEPAD_X                     = 0x00000010,
    GAMEINPUTC_GAMEPAD_Y                     = 0x00000020,
    GAMEINPUTC_GAMEPAD_Z                     = 0x00008000,
    GAMEINPUTC_GAMEPAD_DPAD_UP               = 0x00000040,
    GAMEINPUTC_GAMEPAD_DPAD_DOWN             = 0x00000080,
    GAMEINPUTC_GAMEPAD_DPAD_LEFT             = 0x00000100,
    GAMEINPUTC_GAMEPAD_DPAD_RIGHT            = 0x00000200,
    GAMEINPUTC_GAMEPAD_LEFT_SHOULDER         = 0x00000400,
    GAMEINPUTC_GAMEPAD_RIGHT_SHOULDER        = 0x00000800,
    GAMEINPUTC_GAMEPAD_LEFT_TRIGGER_BUTTON   = 0x00010000,
    GAMEINPUTC_GAMEPAD_RIGHT_TRIGGER_BUTTON  = 0x00020000,
    GAMEINPUTC_GAMEPAD_LEFT_THUMBSTICK       = 0x00001000,
    GAMEINPUTC_GAMEPAD_LEFT_THUMBSTICK_UP    = 0x00040000,
    GAMEINPUTC_GAMEPAD_LEFT_THUMBSTICK_DOWN  = 0x00080000,
    GAMEINPUTC_GAMEPAD_LEFT_THUMBSTICK_LEFT  = 0x00100000,
    GAMEINPUTC_GAMEPAD_LEFT_THUMBSTICK_RIGHT = 0x00200000,
    GAMEINPUTC_GAMEPAD_RIGHT_THUMBSTICK      = 0x00002000,
    GAMEINPUTC_GAMEPAD_RIGHT_THUMBSTICK_UP   = 0x00400000,
    GAMEINPUTC_GAMEPAD_RIGHT_THUMBSTICK_DOWN = 0x00800000,
    GAMEINPUTC_GAMEPAD_RIGHT_THUMBSTICK_LEFT = 0x01000000,
    GAMEINPUTC_GAMEPAD_RIGHT_THUMBSTICK_RIGHT = 0x02000000,
    GAMEINPUTC_GAMEPAD_PADDLE_LEFT1          = 0x04000000,
    GAMEINPUTC_GAMEPAD_PADDLE_LEFT2          = 0x08000000,
    GAMEINPUTC_GAMEPAD_PADDLE_RIGHT1         = 0x10000000,
    GAMEINPUTC_GAMEPAD_PADDLE_RIGHT2         = 0x20000000
} GameInputcGamepadButtons;

/* Buttons the system watches rather than the reading. Values mirror GameInputSystemButtons. */
typedef enum GameInputcSystemButtons
{
    GAMEINPUTC_SYSTEM_BUTTON_NONE  = 0x00000000,
    GAMEINPUTC_SYSTEM_BUTTON_GUIDE = 0x00000001,
    GAMEINPUTC_SYSTEM_BUTTON_SHARE = 0x00000002
} GameInputcSystemButtons;

/* Rumble motors. Values mirror GameInputRumbleMotors. */
typedef enum GameInputcRumbleMotors
{
    GAMEINPUTC_RUMBLE_NONE           = 0x00000000,
    GAMEINPUTC_RUMBLE_LOW_FREQUENCY  = 0x00000001,
    GAMEINPUTC_RUMBLE_HIGH_FREQUENCY = 0x00000002,
    GAMEINPUTC_RUMBLE_LEFT_TRIGGER   = 0x00000004,
    GAMEINPUTC_RUMBLE_RIGHT_TRIGGER  = 0x00000008
} GameInputcRumbleMotors;

/* What is printed on a button - a 1:1 mirror of GameInputLabel (values must match exactly). */
typedef enum GameInputcLabel
{
    GAMEINPUTC_LABEL_Unknown = -1,
    GAMEINPUTC_LABEL_None = 0,
    GAMEINPUTC_LABEL_XboxGuide = 1,
    GAMEINPUTC_LABEL_XboxBack = 2,
    GAMEINPUTC_LABEL_XboxStart = 3,
    GAMEINPUTC_LABEL_XboxMenu = 4,
    GAMEINPUTC_LABEL_XboxView = 5,
    GAMEINPUTC_LABEL_XboxA = 7,
    GAMEINPUTC_LABEL_XboxB = 8,
    GAMEINPUTC_LABEL_XboxX = 9,
    GAMEINPUTC_LABEL_XboxY = 10,
    GAMEINPUTC_LABEL_XboxDPadUp = 11,
    GAMEINPUTC_LABEL_XboxDPadDown = 12,
    GAMEINPUTC_LABEL_XboxDPadLeft = 13,
    GAMEINPUTC_LABEL_XboxDPadRight = 14,
    GAMEINPUTC_LABEL_XboxLeftShoulder = 15,
    GAMEINPUTC_LABEL_XboxLeftTrigger = 16,
    GAMEINPUTC_LABEL_XboxLeftStickButton = 17,
    GAMEINPUTC_LABEL_XboxRightShoulder = 18,
    GAMEINPUTC_LABEL_XboxRightTrigger = 19,
    GAMEINPUTC_LABEL_XboxRightStickButton = 20,
    GAMEINPUTC_LABEL_XboxPaddle1 = 21,
    GAMEINPUTC_LABEL_XboxPaddle2 = 22,
    GAMEINPUTC_LABEL_XboxPaddle3 = 23,
    GAMEINPUTC_LABEL_XboxPaddle4 = 24,
    GAMEINPUTC_LABEL_LetterA = 25,
    GAMEINPUTC_LABEL_LetterB = 26,
    GAMEINPUTC_LABEL_LetterC = 27,
    GAMEINPUTC_LABEL_LetterD = 28,
    GAMEINPUTC_LABEL_LetterE = 29,
    GAMEINPUTC_LABEL_LetterF = 30,
    GAMEINPUTC_LABEL_LetterG = 31,
    GAMEINPUTC_LABEL_LetterH = 32,
    GAMEINPUTC_LABEL_LetterI = 33,
    GAMEINPUTC_LABEL_LetterJ = 34,
    GAMEINPUTC_LABEL_LetterK = 35,
    GAMEINPUTC_LABEL_LetterL = 36,
    GAMEINPUTC_LABEL_LetterM = 37,
    GAMEINPUTC_LABEL_LetterN = 38,
    GAMEINPUTC_LABEL_LetterO = 39,
    GAMEINPUTC_LABEL_LetterP = 40,
    GAMEINPUTC_LABEL_LetterQ = 41,
    GAMEINPUTC_LABEL_LetterR = 42,
    GAMEINPUTC_LABEL_LetterS = 43,
    GAMEINPUTC_LABEL_LetterT = 44,
    GAMEINPUTC_LABEL_LetterU = 45,
    GAMEINPUTC_LABEL_LetterV = 46,
    GAMEINPUTC_LABEL_LetterW = 47,
    GAMEINPUTC_LABEL_LetterX = 48,
    GAMEINPUTC_LABEL_LetterY = 49,
    GAMEINPUTC_LABEL_LetterZ = 50,
    GAMEINPUTC_LABEL_Number0 = 51,
    GAMEINPUTC_LABEL_Number1 = 52,
    GAMEINPUTC_LABEL_Number2 = 53,
    GAMEINPUTC_LABEL_Number3 = 54,
    GAMEINPUTC_LABEL_Number4 = 55,
    GAMEINPUTC_LABEL_Number5 = 56,
    GAMEINPUTC_LABEL_Number6 = 57,
    GAMEINPUTC_LABEL_Number7 = 58,
    GAMEINPUTC_LABEL_Number8 = 59,
    GAMEINPUTC_LABEL_Number9 = 60,
    GAMEINPUTC_LABEL_ArrowUp = 61,
    GAMEINPUTC_LABEL_ArrowUpRight = 62,
    GAMEINPUTC_LABEL_ArrowRight = 63,
    GAMEINPUTC_LABEL_ArrowDownRight = 64,
    GAMEINPUTC_LABEL_ArrowDown = 65,
    GAMEINPUTC_LABEL_ArrowDownLeft = 66,
    GAMEINPUTC_LABEL_ArrowLeft = 67,
    GAMEINPUTC_LABEL_ArrowUpLeft = 68,
    GAMEINPUTC_LABEL_ArrowUpDown = 69,
    GAMEINPUTC_LABEL_ArrowLeftRight = 70,
    GAMEINPUTC_LABEL_ArrowUpDownLeftRight = 71,
    GAMEINPUTC_LABEL_ArrowClockwise = 72,
    GAMEINPUTC_LABEL_ArrowCounterClockwise = 73,
    GAMEINPUTC_LABEL_ArrowReturn = 74,
    GAMEINPUTC_LABEL_IconBranding = 75,
    GAMEINPUTC_LABEL_IconHome = 76,
    GAMEINPUTC_LABEL_IconMenu = 77,
    GAMEINPUTC_LABEL_IconCross = 78,
    GAMEINPUTC_LABEL_IconCircle = 79,
    GAMEINPUTC_LABEL_IconSquare = 80,
    GAMEINPUTC_LABEL_IconTriangle = 81,
    GAMEINPUTC_LABEL_IconStar = 82,
    GAMEINPUTC_LABEL_IconDPadUp = 83,
    GAMEINPUTC_LABEL_IconDPadDown = 84,
    GAMEINPUTC_LABEL_IconDPadLeft = 85,
    GAMEINPUTC_LABEL_IconDPadRight = 86,
    GAMEINPUTC_LABEL_IconDialClockwise = 87,
    GAMEINPUTC_LABEL_IconDialCounterClockwise = 88,
    GAMEINPUTC_LABEL_IconSliderLeftRight = 89,
    GAMEINPUTC_LABEL_IconSliderUpDown = 90,
    GAMEINPUTC_LABEL_IconWheelUpDown = 91,
    GAMEINPUTC_LABEL_IconPlus = 92,
    GAMEINPUTC_LABEL_IconMinus = 93,
    GAMEINPUTC_LABEL_IconSuspension = 94,
    GAMEINPUTC_LABEL_Home = 95,
    GAMEINPUTC_LABEL_Guide = 96,
    GAMEINPUTC_LABEL_Mode = 97,
    GAMEINPUTC_LABEL_Select = 98,
    GAMEINPUTC_LABEL_Menu = 99,
    GAMEINPUTC_LABEL_View = 100,
    GAMEINPUTC_LABEL_Back = 101,
    GAMEINPUTC_LABEL_Start = 102,
    GAMEINPUTC_LABEL_Options = 103,
    GAMEINPUTC_LABEL_Share = 104,
    GAMEINPUTC_LABEL_Up = 105,
    GAMEINPUTC_LABEL_Down = 106,
    GAMEINPUTC_LABEL_Left = 107,
    GAMEINPUTC_LABEL_Right = 108,
    GAMEINPUTC_LABEL_LB = 109,
    GAMEINPUTC_LABEL_LT = 110,
    GAMEINPUTC_LABEL_LSB = 111,
    GAMEINPUTC_LABEL_L1 = 112,
    GAMEINPUTC_LABEL_L2 = 113,
    GAMEINPUTC_LABEL_L3 = 114,
    GAMEINPUTC_LABEL_RB = 115,
    GAMEINPUTC_LABEL_RT = 116,
    GAMEINPUTC_LABEL_RSB = 117,
    GAMEINPUTC_LABEL_R1 = 118,
    GAMEINPUTC_LABEL_R2 = 119,
    GAMEINPUTC_LABEL_R3 = 120,
    GAMEINPUTC_LABEL_PaddleLeft1 = 121,
    GAMEINPUTC_LABEL_PaddleLeft2 = 122,
    GAMEINPUTC_LABEL_PaddleRight1 = 123,
    GAMEINPUTC_LABEL_PaddleRight2 = 124
} GameInputcLabel;

/* One gamepad reading. Triggers are 0..1, thumbsticks -1..1 with up positive. */
typedef struct GameInputcGamepadState
{
    GameInputcGamepadButtons buttons;
    float leftTrigger;
    float rightTrigger;
    float leftThumbstickX;
    float leftThumbstickY;
    float rightThumbstickX;
    float rightThumbstickY;
} GameInputcGamepadState;

/* What a gamepad is and has. Labels say what is printed on each button: an Xbox A reads XboxA, a DualSense
 * cross reads IconCross. */
typedef struct GameInputcDeviceInfo
{
    uint16_t vendorId;
    uint16_t productId;
    uint8_t deviceId[32];
    GameInputcGamepadButtons supportedButtons;
    GameInputcSystemButtons supportedSystemButtons;
    GameInputcRumbleMotors supportedRumbleMotors;
    GameInputcLabel menuLabel;
    GameInputcLabel viewLabel;
    GameInputcLabel aLabel;
    GameInputcLabel bLabel;
    GameInputcLabel xLabel;
    GameInputcLabel yLabel;
    GameInputcLabel dpadUpLabel;
    GameInputcLabel dpadDownLabel;
    GameInputcLabel dpadLeftLabel;
    GameInputcLabel dpadRightLabel;
    GameInputcLabel leftShoulderLabel;
    GameInputcLabel rightShoulderLabel;
    GameInputcLabel leftThumbstickLabel;
    GameInputcLabel rightThumbstickLabel;
} GameInputcDeviceInfo;

/*
 * A gamepad arrived (connected = 1) or left (connected = 0). Called on GameInput's own thread. On arrival the device
 * comes with a reference the receiver owns and gives back with gameinputc_device_release; on removal it is the same
 * handle, borrowed for the call.
 */
typedef void (*GameInputcDeviceCallback)(void* userData, GameInputcDevice device, int32_t connected);

/* Returns an HRESULT: fails when GameInput is not installed. */
GAMEINPUTC_API int32_t gameinputc_create(GameInputcContext* outContext);
GAMEINPUTC_API void gameinputc_release(GameInputcContext context);

/* Starts reporting gamepads; the ones already connected are reported before it returns. Returns an HRESULT. */
GAMEINPUTC_API int32_t gameinputc_watch_gamepads(
    GameInputcContext context, GameInputcDeviceCallback callback, void* userData);

/* The latest reading of the gamepad. Returns 1 when there was one. */
GAMEINPUTC_API int32_t gameinputc_read_gamepad(
    GameInputcContext context, GameInputcDevice device, GameInputcGamepadState* outState);

/* The system buttons held on the gamepad now. */
GAMEINPUTC_API GameInputcSystemButtons gameinputc_system_buttons(GameInputcDevice device);

GAMEINPUTC_API void gameinputc_device_info(GameInputcDevice device, GameInputcDeviceInfo* outInfo);

/* Valid while the device is. May be null. */
GAMEINPUTC_API const char* gameinputc_device_name(GameInputcDevice device);

/* Each motor 0..1; zeros stop it. */
GAMEINPUTC_API void gameinputc_set_rumble(
    GameInputcDevice device, float lowFrequency, float highFrequency, float leftTrigger, float rightTrigger);

GAMEINPUTC_API void gameinputc_device_release(GameInputcDevice device);

#ifdef __cplusplus
}
#endif

#endif /* GAMEINPUT_C_SHARED_H */
