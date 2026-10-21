using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 第三人称角色移动：WASD 相对相机方向移动、Shift 奔跑、空格跳跃、重力。
    /// 基于 CharacterController，不依赖 Rigidbody。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动")]
        public float walkSpeed = 4f;
        public float runSpeed = 7f;
        public float acceleration = 14f;
        public float turnSpeed = 720f;      // 度/秒

        [Header("跳跃 / 重力")]
        public bool allowJump = true;
        public float jumpHeight = 1.1f;
        public float gravity = -22f;

        /// <summary>false 时忽略所有输入（对话/暂停中），但重力仍然生效。</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>相机参考，用于把输入转成世界方向。空则用 Camera.main。</summary>
        public Transform CameraReference { get; set; }

        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public Vector3 Velocity => _velocity;
        public float CurrentSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;

        private CharacterController _controller;
        private Vector3 _velocity;          // 实际速度（含竖直分量）
        private Vector3 _planarTarget;      // 期望的水平速度

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            Vector3 input = Vector3.zero;
            bool run = false;
            bool jump = false;

            if (InputEnabled)
            {
                input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
                if (input.sqrMagnitude > 1f) input.Normalize();
                run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                jump = allowJump && Input.GetKeyDown(KeyCode.Space);
            }

            // 输入 -> 相机相对方向
            Transform cam = CameraReference != null ? CameraReference : (Camera.main != null ? Camera.main.transform : null);
            Vector3 forward = cam != null ? cam.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.right : Vector3.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();
            Vector3 moveDir = forward * input.z + right * input.x;

            float targetSpeed = (run ? runSpeed : walkSpeed) * moveDir.magnitude;
            _planarTarget = moveDir.normalized * targetSpeed;

            Vector3 planar = new Vector3(_velocity.x, 0f, _velocity.z);
            planar = Vector3.MoveTowards(planar, _planarTarget, acceleration * Time.deltaTime);

            // 竖直
            bool grounded = _controller.isGrounded;
            if (grounded && _velocity.y < 0f) _velocity.y = -2f;    // 贴地
            if (grounded && jump) _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _velocity.y += gravity * Time.deltaTime;

            _velocity = new Vector3(planar.x, _velocity.y, planar.z);
            _controller.Move(_velocity * Time.deltaTime);

            // 朝向移动方向
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }
        }

        /// <summary>瞬移到某个位置（会先关掉 CharacterController 避免被卡住）。</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            _controller.enabled = false;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _velocity = Vector3.zero;
            _controller.enabled = true;
        }

        /// <summary>停住角色（对话开始时调用，避免滑步）。</summary>
        public void Halt()
        {
            _velocity = new Vector3(0f, _velocity.y, 0f);
            _planarTarget = Vector3.zero;
        }
    }
}
