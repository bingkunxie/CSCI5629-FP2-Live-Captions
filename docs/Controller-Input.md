# Thumbstick menu input

The reaction palette belongs to the **right thumbstick click**, beside A/B. Left thumbstick click opens the agenda editor. Touching/tilting the stick and pressing A/B are separate controls.

## Fixed binding

The installed Oculus Touch and Quest Touch Plus OpenXR layouts expose a control named thumbstickClicked, with the usage Primary2DAxisClick. The old path used primary2DAxisClick as a control name, so it did not resolve on those layouts.

Version 0.3.2 uses the handed usage paths:
- Right: <XRController>{RightHand}/{Primary2DAxisClick}
- Left: <XRController>{LeftHand}/{Primary2DAxisClick}

Canonical thumbstickClicked bindings and the existing Unity XR/Meta controller state provide compatibility with runtime-created action sets. All input sources are merged before detecting a rising edge. Holding does not repeat; after focus loss or disabling, release before another press. F2/F3 editor shortcuts remain available.

## Verification

Offline tests inject state events into the actual installed Oculus Touch and Touch Plus input layouts. They reproduce the old unresolved binding, verify right-stick open/hold/release/close, ensure A/touch do not activate the menu, keep left/right functions separate, and check startup/resume suppression.

These are simulated input events using real SDK layouts, not a claim that a person physically pressed the installed headset controller. Runtime logs use fixed input/menu markers without recording meeting content.
