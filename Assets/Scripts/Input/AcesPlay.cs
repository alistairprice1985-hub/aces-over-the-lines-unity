//------------------------------------------------------------------------------
// Auto-generated-style wrapper around AcesPlay.inputactions.
//
// Hand-written to match the format Unity's "Generate C# Class" produces. When
// the .inputactions asset is edited via the Input Actions Editor, this file
// can be regenerated (right-click AcesPlay.inputactions → "Generate C# Class")
// and the embedded JSON will be refreshed from the asset. The asset is the
// source of truth for the bindings; this wrapper exposes them as strongly
// typed members for runtime use.
//------------------------------------------------------------------------------

using System;
using UnityEngine.InputSystem;

namespace AcesOverTheLines.Input
{
    public class AcesPlay : IDisposable
    {
        public InputActionAsset Asset { get; }
        public AcesPlayActions AcesPlayMap { get; }

        public AcesPlay()
        {
            Asset = InputActionAsset.FromJson(JSON);
            AcesPlayMap = new AcesPlayActions(Asset.FindActionMap("AcesPlay", throwIfNotFound: true));
        }

        public void Enable()  => AcesPlayMap.Enable();
        public void Disable() => AcesPlayMap.Disable();
        public void Dispose()
        {
            if (Asset != null) UnityEngine.Object.Destroy(Asset);
        }

        public readonly struct AcesPlayActions
        {
            readonly InputActionMap _map;
            public AcesPlayActions(InputActionMap map)
            {
                _map = map;
                Pitch        = map.FindAction("Pitch",        throwIfNotFound: true);
                Roll         = map.FindAction("Roll",         throwIfNotFound: true);
                AD           = map.FindAction("AD",           throwIfNotFound: true);
                Rudder       = map.FindAction("Rudder",       throwIfNotFound: true);
                ThrottleAxis = map.FindAction("ThrottleAxis", throwIfNotFound: true);
                ThrottleUp   = map.FindAction("ThrottleUp",   throwIfNotFound: true);
                ThrottleDown = map.FindAction("ThrottleDown", throwIfNotFound: true);
                Fire         = map.FindAction("Fire",         throwIfNotFound: true);
                MouseMode    = map.FindAction("MouseMode",    throwIfNotFound: true);
                MouseDelta   = map.FindAction("MouseDelta",   throwIfNotFound: true);
            }

            public InputAction Pitch        { get; }
            public InputAction Roll         { get; }
            public InputAction AD           { get; }
            public InputAction Rudder       { get; }
            public InputAction ThrottleAxis { get; }
            public InputAction ThrottleUp   { get; }
            public InputAction ThrottleDown { get; }
            public InputAction Fire         { get; }
            public InputAction MouseMode    { get; }
            public InputAction MouseDelta   { get; }

            public void Enable()  => _map.Enable();
            public void Disable() => _map.Disable();
        }

        // Embedded copy of AcesPlay.inputactions. Kept in sync by regenerating
        // when the asset changes.
        const string JSON = @"{
    ""name"": ""AcesPlay"",
    ""maps"": [
        {
            ""name"": ""AcesPlay"",
            ""id"": ""a1b2c3d4-e5f6-4789-9abc-def012345678"",
            ""actions"": [
                { ""name"": ""Pitch"",        ""type"": ""Value"",  ""id"": ""11111111-aaaa-4bbb-bccc-000000000001"", ""expectedControlType"": ""Axis"",    ""processors"": """", ""interactions"": """",      ""initialStateCheck"": true  },
                { ""name"": ""Roll"",         ""type"": ""Value"",  ""id"": ""11111111-aaaa-4bbb-bccc-000000000002"", ""expectedControlType"": ""Axis"",    ""processors"": """", ""interactions"": """",      ""initialStateCheck"": true  },
                { ""name"": ""AD"",           ""type"": ""Value"",  ""id"": ""11111111-aaaa-4bbb-bccc-000000000003"", ""expectedControlType"": ""Axis"",    ""processors"": """", ""interactions"": """",      ""initialStateCheck"": true  },
                { ""name"": ""Rudder"",       ""type"": ""Value"",  ""id"": ""11111111-aaaa-4bbb-bccc-000000000004"", ""expectedControlType"": ""Axis"",    ""processors"": """", ""interactions"": """",      ""initialStateCheck"": true  },
                { ""name"": ""ThrottleAxis"", ""type"": ""Value"",  ""id"": ""11111111-aaaa-4bbb-bccc-000000000005"", ""expectedControlType"": ""Axis"",    ""processors"": """", ""interactions"": """",      ""initialStateCheck"": true  },
                { ""name"": ""ThrottleUp"",   ""type"": ""Button"", ""id"": ""11111111-aaaa-4bbb-bccc-000000000006"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """",      ""initialStateCheck"": false },
                { ""name"": ""ThrottleDown"", ""type"": ""Button"", ""id"": ""11111111-aaaa-4bbb-bccc-000000000007"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """",      ""initialStateCheck"": false },
                { ""name"": ""Fire"",         ""type"": ""Button"", ""id"": ""11111111-aaaa-4bbb-bccc-000000000008"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """",      ""initialStateCheck"": false },
                { ""name"": ""MouseMode"",    ""type"": ""Button"", ""id"": ""11111111-aaaa-4bbb-bccc-000000000009"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": ""Press"", ""initialStateCheck"": false },
                { ""name"": ""MouseDelta"",   ""type"": ""Value"",  ""id"": ""11111111-aaaa-4bbb-bccc-00000000000a"", ""expectedControlType"": ""Vector2"", ""processors"": """", ""interactions"": """",      ""initialStateCheck"": false }
            ],
            ""bindings"": [
                { ""name"": ""Pitch1D"",  ""id"": ""22222222-aaaa-4bbb-bccc-000000000001"", ""path"": ""1DAxis"",                ""interactions"": """", ""processors"": """", ""groups"": """",         ""action"": ""Pitch"",        ""isComposite"": true,  ""isPartOfComposite"": false },
                { ""name"": ""positive"", ""id"": ""22222222-aaaa-4bbb-bccc-000000000002"", ""path"": ""<Keyboard>/downArrow"",  ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""Pitch"",        ""isComposite"": false, ""isPartOfComposite"": true  },
                { ""name"": ""negative"", ""id"": ""22222222-aaaa-4bbb-bccc-000000000003"", ""path"": ""<Keyboard>/upArrow"",    ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""Pitch"",        ""isComposite"": false, ""isPartOfComposite"": true  },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-000000000004"", ""path"": ""<Joystick>/stick/y"",    ""interactions"": """", ""processors"": ""invert"", ""groups"": ""Joystick"", ""action"": ""Pitch"",        ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-000000000005"", ""path"": ""<Joystick>/stick/x"",    ""interactions"": """", ""processors"": ""invert"", ""groups"": ""Joystick"", ""action"": ""Roll"",         ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": ""AD1D"",     ""id"": ""22222222-aaaa-4bbb-bccc-000000000006"", ""path"": ""1DAxis"",                ""interactions"": """", ""processors"": """", ""groups"": """",         ""action"": ""AD"",           ""isComposite"": true,  ""isPartOfComposite"": false },
                { ""name"": ""positive"", ""id"": ""22222222-aaaa-4bbb-bccc-000000000007"", ""path"": ""<Keyboard>/d"",          ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""AD"",           ""isComposite"": false, ""isPartOfComposite"": true  },
                { ""name"": ""negative"", ""id"": ""22222222-aaaa-4bbb-bccc-000000000008"", ""path"": ""<Keyboard>/a"",          ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""AD"",           ""isComposite"": false, ""isPartOfComposite"": true  },
                { ""name"": ""Rudder1D"", ""id"": ""22222222-aaaa-4bbb-bccc-000000000009"", ""path"": ""1DAxis"",                ""interactions"": """", ""processors"": """", ""groups"": """",         ""action"": ""Rudder"",       ""isComposite"": true,  ""isPartOfComposite"": false },
                { ""name"": ""positive"", ""id"": ""22222222-aaaa-4bbb-bccc-00000000000a"", ""path"": ""<Keyboard>/rightArrow"", ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""Rudder"",       ""isComposite"": false, ""isPartOfComposite"": true  },
                { ""name"": ""negative"", ""id"": ""22222222-aaaa-4bbb-bccc-00000000000b"", ""path"": ""<Keyboard>/leftArrow"",  ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""Rudder"",       ""isComposite"": false, ""isPartOfComposite"": true  },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-00000000000c"", ""path"": ""<Joystick>/rz"",      ""interactions"": """", ""processors"": """", ""groups"": ""Joystick"", ""action"": ""Rudder"",       ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-00000000000d"", ""path"": ""<Joystick>/slider"",     ""interactions"": """", ""processors"": """", ""groups"": ""Joystick"", ""action"": ""ThrottleAxis"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-00000000000e"", ""path"": ""<Keyboard>/leftShift"",  ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""ThrottleUp"",   ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-00000000000f"", ""path"": ""<Keyboard>/rightShift"", ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""ThrottleUp"",   ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-000000000010"", ""path"": ""<Keyboard>/leftCtrl"",   ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""ThrottleDown"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-000000000011"", ""path"": ""<Keyboard>/rightCtrl"",  ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""ThrottleDown"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-000000000012"", ""path"": ""<Keyboard>/space"",      ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""Fire"",         ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-000000000013"", ""path"": ""<Joystick>/trigger"",    ""interactions"": """", ""processors"": """", ""groups"": ""Joystick"", ""action"": ""Fire"",         ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-000000000014"", ""path"": ""<Keyboard>/m"",          ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""MouseMode"",    ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """",         ""id"": ""22222222-aaaa-4bbb-bccc-000000000015"", ""path"": ""<Mouse>/delta"",         ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard"", ""action"": ""MouseDelta"",   ""isComposite"": false, ""isPartOfComposite"": false }
            ]
        }
    ],
    ""controlSchemes"": [
        { ""name"": ""Keyboard"", ""bindingGroup"": ""Keyboard"", ""devices"": [ { ""devicePath"": ""<Keyboard>"", ""isOptional"": false, ""isOR"": false }, { ""devicePath"": ""<Mouse>"", ""isOptional"": true, ""isOR"": false } ] },
        { ""name"": ""Joystick"", ""bindingGroup"": ""Joystick"", ""devices"": [ { ""devicePath"": ""<Joystick>"", ""isOptional"": false, ""isOR"": false } ] }
    ]
}";
    }
}
