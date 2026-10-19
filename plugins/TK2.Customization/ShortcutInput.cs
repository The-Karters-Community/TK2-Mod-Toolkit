using System;
using Rewired;

namespace TK2.Customization;

// Template buttons use the assigned player's controller, independent of game action mappings.
internal static class ShortcutInput
{
    internal static bool Read(Ant_KartInput input, string selection, bool held = false)
    {
        if (string.IsNullOrWhiteSpace(selection) || selection == "None" || input.player == null) return false;
        if (!selection.StartsWith("Gamepad: ") && !selection.StartsWith("Raw button "))
            return held ? input.player.GetButton(selection) : input.player.GetButtonDown(selection);
        var joysticks = input.player.controllers.Joysticks;
        for (int i = 0; i < input.player.controllers.joystickCount; i++) {
            var joystick = joysticks[i];
            if (selection.StartsWith("Raw button ") && int.TryParse(selection.Substring(11), out int index)) {
                if (index >= 0 && index < joystick.buttonCount && (held ? joystick.GetButton(index) : joystick.GetButtonDown(index))) return true;
                continue;
            }
            var pad = joystick.GetTemplate<IGamepadTemplate>();
            if (pad == null) continue;
            if (selection == "Gamepad: LT / L2") { if (held ? pad.leftTrigger.value > .5f : pad.leftTrigger.value > .5f && pad.leftTrigger.valuePrev <= .5f) return true; continue; }
            if (selection == "Gamepad: RT / R2") { if (held ? pad.rightTrigger.value > .5f : pad.rightTrigger.value > .5f && pad.rightTrigger.valuePrev <= .5f) return true; continue; }
            IControllerTemplateButton? button = selection switch {
                "Gamepad: A / Cross" => pad.a, "Gamepad: B / Circle" => pad.b,
                "Gamepad: X / Square" => pad.x, "Gamepad: Y / Triangle" => pad.y,
                "Gamepad: LB / L1" => pad.leftBumper, "Gamepad: RB / R1" => pad.rightBumper,
                "Gamepad: Back / Share" => pad.back, "Gamepad: Start / Options" => pad.start,
                "Gamepad: Left stick press" => pad.leftStick.press, "Gamepad: Right stick press" => pad.rightStick.press,
                "Gamepad: D-pad left" => pad.dPad.left, "Gamepad: D-pad right" => pad.dPad.right,
                "Gamepad: D-pad up" => pad.dPad.up, "Gamepad: D-pad down" => pad.dPad.down,
                _ => null
            };
            if (button != null && (held ? button.value : button.justPressed)) return true;
        }
        return false;
    }
}
