using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionStudy
{
    /// <summary>
    /// 入力の読み取り。アセットに依存しないよう InputAction をコードで定義している。
    /// キーボード + マウス / ゲームパッドの両対応。
    /// 検証用の「入力遅延」トグルが ON の時は、移動と攻撃の入力をわざと遅らせて渡す。
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class PlayerInputReader : MonoBehaviour
    {
        InputAction m_Move;
        InputAction m_LookMouse;
        InputAction m_LookStick;
        InputAction m_AttackMouse;
        InputAction m_Attack;
        InputAction m_TogglePanel;
        InputAction m_LockOn;
        InputAction m_LockSwitch;

        [Tooltip("検証用トグル「入力遅延」で遅らせる時間(秒)。60fps で 0.1 秒 = 6 フレーム")]
        public float simulatedDelay = 0.1f;

        // 入力遅延の再現用: 押した時刻と移動入力の履歴
        readonly Queue<float> m_PendingAttackPresses = new Queue<float>();
        readonly List<KeyValuePair<float, Vector2>> m_MoveHistory = new List<KeyValuePair<float, Vector2>>();
        bool m_AttackPressedThisFrame;
        Vector2 m_Move2D;

        public Vector2 Move => m_Move2D;
        public Vector2 LookMouseDelta => m_LookMouse.ReadValue<Vector2>();
        public Vector2 LookStick => m_LookStick.ReadValue<Vector2>();

        /// <summary>このフレームに攻撃ボタンが押されたか。左クリックはデバッグパネルの上で押された時だけ攻撃にしない。</summary>
        public bool AttackPressed => m_AttackPressedThisFrame;

        static bool IsMouseOverPanel()
        {
            Mouse mouse = Mouse.current;
            return mouse != null && HitFeelDebugPanel.IsPointerOverPanel(mouse.position.ReadValue());
        }

        public bool TogglePanelPressed => m_TogglePanel.WasPressedThisFrame();

        /// <summary>ロックオン / 解除のボタン (Q / マウスホイールクリック / 右スティック押し込み)。</summary>
        public bool LockOnPressed => m_LockOn.WasPressedThisFrame();

        /// <summary>ロックオン対象の切り替え用のホイール量(上が正)。</summary>
        public float LockSwitchScroll => m_LockSwitch.ReadValue<float>();

        void Awake()
        {
            m_Move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            m_Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            m_Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            m_Move.AddBinding("<Gamepad>/leftStick");

            m_LookMouse = new InputAction("LookMouse", InputActionType.Value, "<Mouse>/delta");
            m_LookStick = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            m_AttackMouse = new InputAction("AttackMouse", InputActionType.Button, "<Mouse>/leftButton");
            m_Attack = new InputAction("Attack", InputActionType.Button);
            m_Attack.AddBinding("<Keyboard>/j");
            m_Attack.AddBinding("<Gamepad>/buttonWest");

            m_TogglePanel = new InputAction("TogglePanel", InputActionType.Button);
            m_TogglePanel.AddBinding("<Keyboard>/tab");
            m_TogglePanel.AddBinding("<Gamepad>/select");

            m_LockOn = new InputAction("LockOn", InputActionType.Button);
            m_LockOn.AddBinding("<Keyboard>/q");
            m_LockOn.AddBinding("<Mouse>/middleButton");
            m_LockOn.AddBinding("<Gamepad>/rightStickPress");
            m_LockSwitch = new InputAction("LockSwitch", InputActionType.Value, "<Mouse>/scroll/y");
        }

        void Update()
        {
            bool rawPress = m_Attack.WasPressedThisFrame() || (m_AttackMouse.WasPressedThisFrame() && !IsMouseOverPanel());
            Vector2 rawMove = Vector2.ClampMagnitude(m_Move.ReadValue<Vector2>(), 1f);

            if (!HitFeelSettings.T.inputDelay)
            {
                m_PendingAttackPresses.Clear();
                m_MoveHistory.Clear();
                m_AttackPressedThisFrame = rawPress;
                m_Move2D = rawMove;
                return;
            }

            // 【悪い例】入力遅延: 押した入力を simulatedDelay 秒経ってから渡す
            float now = Time.unscaledTime;
            if (rawPress)
                m_PendingAttackPresses.Enqueue(now);
            m_AttackPressedThisFrame = false;
            while (m_PendingAttackPresses.Count > 0 && now - m_PendingAttackPresses.Peek() >= simulatedDelay)
            {
                m_PendingAttackPresses.Dequeue();
                m_AttackPressedThisFrame = true;
            }

            m_MoveHistory.Add(new KeyValuePair<float, Vector2>(now, rawMove));
            while (m_MoveHistory.Count >= 2 && now - m_MoveHistory[1].Key >= simulatedDelay)
                m_MoveHistory.RemoveAt(0);
            if (now - m_MoveHistory[0].Key >= simulatedDelay)
                m_Move2D = m_MoveHistory[0].Value;
        }

        void OnEnable()
        {
            m_Move.Enable();
            m_LookMouse.Enable();
            m_LookStick.Enable();
            m_AttackMouse.Enable();
            m_Attack.Enable();
            m_TogglePanel.Enable();
            m_LockOn.Enable();
            m_LockSwitch.Enable();
        }

        void OnDisable()
        {
            m_Move.Disable();
            m_LookMouse.Disable();
            m_LookStick.Disable();
            m_AttackMouse.Disable();
            m_Attack.Disable();
            m_TogglePanel.Disable();
            m_LockOn.Disable();
            m_LockSwitch.Disable();
        }

        void OnDestroy()
        {
            m_Move.Dispose();
            m_LookMouse.Dispose();
            m_LookStick.Dispose();
            m_AttackMouse.Dispose();
            m_Attack.Dispose();
            m_TogglePanel.Dispose();
            m_LockOn.Dispose();
            m_LockSwitch.Dispose();
        }
    }
}
