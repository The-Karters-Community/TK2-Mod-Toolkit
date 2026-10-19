"""Player-facing shortcut choices shared by built-in module catalogues."""
KEYBOARD_KEYS = (
    ['None'] + list('ABCDEFGHIJKLMNOPQRSTUVWXYZ') + ['Alpha' + str(i) for i in range(10)]
    + ['F' + str(i) for i in range(1, 25)]
    + ['Space', 'Return', 'Tab', 'Backspace', 'Escape', 'Insert', 'Delete', 'Home', 'End', 'PageUp', 'PageDown',
       'LeftArrow', 'RightArrow', 'UpArrow', 'DownArrow', 'LeftShift', 'RightShift', 'LeftControl', 'RightControl',
       'LeftAlt', 'RightAlt', 'BackQuote', 'Minus', 'Equals', 'LeftBracket', 'RightBracket', 'Backslash',
       'Semicolon', 'Quote', 'Comma', 'Period', 'Slash', 'CapsLock', 'Numlock', 'ScrollLock', 'Pause', 'Print']
    + ['Keypad' + str(i) for i in range(10)]
    + ['KeypadPeriod', 'KeypadDivide', 'KeypadMultiply', 'KeypadMinus', 'KeypadPlus', 'KeypadEnter', 'KeypadEquals']
    + ['Mouse' + str(i) for i in range(7)]
)
CONTROLLER_BUTTONS = [''] + ['Gamepad: ' + button for button in (
    'A / Cross', 'B / Circle', 'X / Square', 'Y / Triangle', 'LB / L1', 'RB / R1', 'LT / L2', 'RT / R2',
    'Back / Share', 'Start / Options', 'Left stick press', 'Right stick press',
    'D-pad left', 'D-pad right', 'D-pad up', 'D-pad down')]
CONTROLLER_BUTTONS += ['Raw button ' + str(i) for i in range(32)]
# Preserve useful game-action choices and existing saved bindings.
CONTROLLER_BUTTONS += ['MenuTriangle', 'InGamePlayersList', 'Jump', 'Boost', 'UseWeapon', 'Drift_left_right']


def add_shortcut_choices(features):
    for feature in features:
        for i, setting in enumerate(feature['settings']):
            key = setting[0]
            if setting[2] != 'text':
                continue
            choices = KEYBOARD_KEYS if key.endswith('Key') else CONTROLLER_BUTTONS if key in (
                'ControllerAction', 'SaveAction', 'LoadAction', 'LeftAction', 'RightAction') else None
            if choices is None:
                continue
            row = list(setting) + [None] * max(0, 10 - len(setting))
            row[7] = list(dict.fromkeys(choices + [row[3]]))
            row[9] = choices is CONTROLLER_BUTTONS  # Preserve old custom actions; keyboard choices are valid KeyCode names.
            if choices is CONTROLLER_BUTTONS:
                row[6] = 'Choose a physical gamepad button. Templates normalize Xbox/PlayStation layouts; raw buttons support other controllers. Existing game actions remain selectable.'
            feature['settings'][i] = tuple(row)
