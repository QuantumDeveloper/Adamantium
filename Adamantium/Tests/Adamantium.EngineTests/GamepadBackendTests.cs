using Adamantium.GameInput;
using Adamantium.Multiverse.Input;
using Adamantium.XInput;
using NUnit.Framework;
using EngineButton = Adamantium.Multiverse.Input.GamepadButton;
using XInputButton = Adamantium.XInput.GamepadButton;

namespace Adamantium.EngineTests;

[TestFixture]
public class GamepadBackendTests
{
    [Test]
    public void XInputSticks_CoverMinusOneToOne()
    {
        Assert.That(XInputGamepad.ToAxis(short.MinValue), Is.EqualTo(-1f));
        Assert.That(XInputGamepad.ToAxis(short.MaxValue), Is.EqualTo(1f));
        Assert.That(XInputGamepad.ToAxis(0), Is.EqualTo(0f));
    }

    [Test]
    public void XInputButtons_MapToTheEngineButtonsOnTheSamePlace()
    {
        var buttons = XInputButton.A | XInputButton.Y | XInputButton.DpadUp | XInputButton.Start | XInputButton.Back;

        Assert.That(XInputGamepad.ToEngine(buttons),
            Is.EqualTo(EngineButton.A | EngineButton.Y | EngineButton.DpadUp | EngineButton.Start | EngineButton.Back));
    }

    [Test]
    public void XInputMotorSpeed_IsClampedToTheMotorRange()
    {
        Assert.That(XInputGamepad.ToMotorSpeed(1f), Is.EqualTo(ushort.MaxValue));
        Assert.That(XInputGamepad.ToMotorSpeed(2f), Is.EqualTo(ushort.MaxValue));
        Assert.That(XInputGamepad.ToMotorSpeed(-1f), Is.EqualTo(0));
    }

    [Test]
    public void GameInputPaddles_MapToTheElitePaddleNumbers()
    {
        var paddles = GameInputGamepadButtons.PaddleRight1 | GameInputGamepadButtons.PaddleRight2 |
                      GameInputGamepadButtons.PaddleLeft1 | GameInputGamepadButtons.PaddleLeft2;

        Assert.That(GameInputGamepad.ToEngine(GameInputGamepadButtons.PaddleRight1), Is.EqualTo(EngineButton.Paddle1));
        Assert.That(GameInputGamepad.ToEngine(GameInputGamepadButtons.PaddleRight2), Is.EqualTo(EngineButton.Paddle2));
        Assert.That(GameInputGamepad.ToEngine(GameInputGamepadButtons.PaddleLeft1), Is.EqualTo(EngineButton.Paddle3));
        Assert.That(GameInputGamepad.ToEngine(GameInputGamepadButtons.PaddleLeft2), Is.EqualTo(EngineButton.Paddle4));
        Assert.That(GameInputGamepad.ToEngine(paddles),
            Is.EqualTo(EngineButton.Paddle1 | EngineButton.Paddle2 | EngineButton.Paddle3 | EngineButton.Paddle4));
    }

    [Test]
    public void GameInputMenuAndView_AreStartAndBack()
    {
        Assert.That(GameInputGamepad.ToEngine(GameInputGamepadButtons.Menu | GameInputGamepadButtons.View),
            Is.EqualTo(EngineButton.Start | EngineButton.Back));
    }

    [Test]
    public void GameInputSystemButtons_AreShareAndGuide()
    {
        Assert.That(GameInputGamepad.ToEngine(GameInputSystemButtons.Share | GameInputSystemButtons.Guide),
            Is.EqualTo(EngineButton.Share | EngineButton.Guide));
    }

    [Test]
    public void TheBottomButtonsLabel_SaysWhoseFaceItIs()
    {
        Assert.That(GameInputGamepad.ToFace(GameInputLabel.XboxA), Is.EqualTo(GamepadFace.Xbox));
        Assert.That(GameInputGamepad.ToFace(GameInputLabel.IconCross), Is.EqualTo(GamepadFace.PlayStation));
        Assert.That(GameInputGamepad.ToFace(GameInputLabel.LetterB), Is.EqualTo(GamepadFace.Nintendo));
        Assert.That(GameInputGamepad.ToFace(GameInputLabel.Unknown), Is.EqualTo(GamepadFace.Unknown));
    }

    [Test]
    public void StartingGameInput_NeverThrows_WhetherOrNotItIsThere()
    {
        Assert.DoesNotThrow(() =>
        {
            if (GameInputGamepadBackend.TryCreate(out var backend))
            {
                backend.Dispose();
            }
        });
    }
}
