#include "gameinput_c.h"

#include <windows.h>
#include <GameInput.h>

#include <atomic>
#include <cstring>
#include <mutex>
#include <unordered_map>

using namespace GameInput::v3;

struct GameInputcDevice_T
{
    IGameInputDevice* device = nullptr;
    std::atomic<uint32_t> references{1};
    std::atomic<uint32_t> systemButtons{0};
};

struct GameInputcContext_T
{
    IGameInput* input = nullptr;
    GameInputCallbackToken deviceToken = 0;
    GameInputCallbackToken systemButtonToken = 0;
    GameInputcDeviceCallback callback = nullptr;
    void* userData = nullptr;
    std::mutex lock;
    std::unordered_map<IGameInputDevice*, GameInputcDevice> devices;
};

static void Retain(GameInputcDevice device)
{
    device->references.fetch_add(1);
}

static void Release(GameInputcDevice device)
{
    if (device->references.fetch_sub(1) == 1)
    {
        device->device->Release();
        delete device;
    }
}

static void CALLBACK OnDevice(
    GameInputCallbackToken,
    void* context,
    IGameInputDevice* device,
    uint64_t,
    GameInputDeviceStatus currentStatus,
    GameInputDeviceStatus previousStatus)
{
    auto self = static_cast<GameInputcContext_T*>(context);
    const bool connected = (currentStatus & GameInputDeviceConnected) != 0;
    const bool wasConnected = (previousStatus & GameInputDeviceConnected) != 0;
    if (connected == wasConnected)
    {
        return;
    }

    GameInputcDevice handle = nullptr;
    {
        std::lock_guard<std::mutex> guard(self->lock);
        if (connected)
        {
            handle = new GameInputcDevice_T();
            device->AddRef();
            handle->device = device;
            self->devices[device] = handle;
            Retain(handle);
        }
        else
        {
            auto found = self->devices.find(device);
            if (found == self->devices.end())
            {
                return;
            }
            handle = found->second;
            self->devices.erase(found);
        }
    }

    self->callback(self->userData, handle, connected ? 1 : 0);

    if (!connected)
    {
        Release(handle);
    }
}

static void CALLBACK OnSystemButtons(
    GameInputCallbackToken,
    void* context,
    IGameInputDevice* device,
    uint64_t,
    GameInputSystemButtons currentButtons,
    GameInputSystemButtons)
{
    auto self = static_cast<GameInputcContext_T*>(context);
    std::lock_guard<std::mutex> guard(self->lock);
    auto found = self->devices.find(device);
    if (found != self->devices.end())
    {
        found->second->systemButtons.store(static_cast<uint32_t>(currentButtons));
    }
}

int32_t gameinputc_create(GameInputcContext* outContext)
{
    *outContext = nullptr;
    IGameInput* input = nullptr;
    const HRESULT result = GameInputCreate(&input);
    if (FAILED(result))
    {
        return result;
    }

    auto context = new GameInputcContext_T();
    context->input = input;
    *outContext = context;
    return S_OK;
}

void gameinputc_release(GameInputcContext context)
{
    if (context == nullptr)
    {
        return;
    }

    if (context->systemButtonToken != 0)
    {
        context->input->UnregisterCallback(context->systemButtonToken);
    }
    if (context->deviceToken != 0)
    {
        context->input->UnregisterCallback(context->deviceToken);
    }

    for (auto& entry : context->devices)
    {
        Release(entry.second);
    }
    context->devices.clear();
    context->input->Release();
    delete context;
}

int32_t gameinputc_watch_gamepads(GameInputcContext context, GameInputcDeviceCallback callback, void* userData)
{
    if (context->deviceToken != 0)
    {
        return E_ILLEGAL_METHOD_CALL;
    }

    context->callback = callback;
    context->userData = userData;

    const HRESULT result = context->input->RegisterDeviceCallback(
        nullptr,
        GameInputKindGamepad,
        GameInputDeviceConnected,
        GameInputBlockingEnumeration,
        context,
        OnDevice,
        &context->deviceToken);
    if (FAILED(result))
    {
        return result;
    }

    context->input->RegisterSystemButtonCallback(
        nullptr,
        GameInputSystemButtonGuide | GameInputSystemButtonShare,
        context,
        OnSystemButtons,
        &context->systemButtonToken);
    return S_OK;
}

int32_t gameinputc_read_gamepad(GameInputcContext context, GameInputcDevice device, GameInputcGamepadState* outState)
{
    std::memset(outState, 0, sizeof(*outState));

    IGameInputReading* reading = nullptr;
    if (FAILED(context->input->GetCurrentReading(GameInputKindGamepad, device->device, &reading)))
    {
        return 0;
    }

    GameInputGamepadState state{};
    const bool read = reading->GetGamepadState(&state);
    reading->Release();
    if (!read)
    {
        return 0;
    }

    outState->buttons = static_cast<GameInputcGamepadButtons>(state.buttons);
    outState->leftTrigger = state.leftTrigger;
    outState->rightTrigger = state.rightTrigger;
    outState->leftThumbstickX = state.leftThumbstickX;
    outState->leftThumbstickY = state.leftThumbstickY;
    outState->rightThumbstickX = state.rightThumbstickX;
    outState->rightThumbstickY = state.rightThumbstickY;
    return 1;
}

GameInputcSystemButtons gameinputc_system_buttons(GameInputcDevice device)
{
    return static_cast<GameInputcSystemButtons>(device->systemButtons.load());
}

void gameinputc_device_info(GameInputcDevice device, GameInputcDeviceInfo* outInfo)
{
    std::memset(outInfo, 0, sizeof(*outInfo));

    const GameInputDeviceInfo* info = nullptr;
    if (FAILED(device->device->GetDeviceInfo(&info)) || info == nullptr)
    {
        return;
    }

    outInfo->vendorId = info->vendorId;
    outInfo->productId = info->productId;
    static_assert(sizeof(outInfo->deviceId) == sizeof(info->deviceId), "APP_LOCAL_DEVICE_ID is 32 bytes");
    std::memcpy(outInfo->deviceId, &info->deviceId, sizeof(outInfo->deviceId));
    outInfo->supportedSystemButtons = static_cast<GameInputcSystemButtons>(info->supportedSystemButtons);
    outInfo->supportedRumbleMotors = static_cast<GameInputcRumbleMotors>(info->supportedRumbleMotors);

    const GameInputGamepadInfo* gamepad = info->gamepadInfo;
    if (gamepad == nullptr)
    {
        return;
    }

    outInfo->supportedButtons = static_cast<GameInputcGamepadButtons>(gamepad->supportedLayout);
    outInfo->menuLabel = static_cast<GameInputcLabel>(gamepad->menuButtonLabel);
    outInfo->viewLabel = static_cast<GameInputcLabel>(gamepad->viewButtonLabel);
    outInfo->aLabel = static_cast<GameInputcLabel>(gamepad->aButtonLabel);
    outInfo->bLabel = static_cast<GameInputcLabel>(gamepad->bButtonLabel);
    outInfo->xLabel = static_cast<GameInputcLabel>(gamepad->xButtonLabel);
    outInfo->yLabel = static_cast<GameInputcLabel>(gamepad->yButtonLabel);
    outInfo->dpadUpLabel = static_cast<GameInputcLabel>(gamepad->dpadUpLabel);
    outInfo->dpadDownLabel = static_cast<GameInputcLabel>(gamepad->dpadDownLabel);
    outInfo->dpadLeftLabel = static_cast<GameInputcLabel>(gamepad->dpadLeftLabel);
    outInfo->dpadRightLabel = static_cast<GameInputcLabel>(gamepad->dpadRightLabel);
    outInfo->leftShoulderLabel = static_cast<GameInputcLabel>(gamepad->leftShoulderButtonLabel);
    outInfo->rightShoulderLabel = static_cast<GameInputcLabel>(gamepad->rightShoulderButtonLabel);
    outInfo->leftThumbstickLabel = static_cast<GameInputcLabel>(gamepad->leftThumbstickButtonLabel);
    outInfo->rightThumbstickLabel = static_cast<GameInputcLabel>(gamepad->rightThumbstickButtonLabel);
}

const char* gameinputc_device_name(GameInputcDevice device)
{
    const GameInputDeviceInfo* info = nullptr;
    if (FAILED(device->device->GetDeviceInfo(&info)) || info == nullptr)
    {
        return nullptr;
    }
    return info->displayName;
}

void gameinputc_set_rumble(
    GameInputcDevice device, float lowFrequency, float highFrequency, float leftTrigger, float rightTrigger)
{
    const GameInputRumbleParams rumble{lowFrequency, highFrequency, leftTrigger, rightTrigger};
    device->device->SetRumbleState(&rumble);
}

void gameinputc_device_release(GameInputcDevice device)
{
    if (device != nullptr)
    {
        Release(device);
    }
}
