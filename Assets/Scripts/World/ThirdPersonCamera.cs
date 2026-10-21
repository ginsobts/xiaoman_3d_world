using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 第三人称环绕相机：鼠标旋转、滚轮缩放、简单的墙体避让。
    /// 挂在 Camera 上，由 WorldController 设置 Target。
    ///
    /// 防晕设计：
    /// - 旋转用鼠标的原始增量（不做惯性/加速），只加很短的一段平滑去掉低帧率抖动；
    /// - 鼠标灵敏度按「每像素多少度」计，默认约 0.09°/px，一屏宽转 180° 左右；
    /// - 墙体避让的拉近/拉远用不同速度平滑，不会瞬间跳变；
    /// - 执行顺序晚于所有玩家 / 相机目标脚本，避免相机落后目标一帧造成抖动。
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 pivotOffset = new Vector3(0f, 1.35f, 0f);

        [Header("旋转")]
        [Tooltip("每单位鼠标增量对应的角度。Unity 的 Mouse X/Y 轴默认已乘以 0.1，所以 1.2 ≈ 0.12°/像素。")]
        public float mouseSensitivity = 1.2f;
        [Tooltip("旋转平滑时间（秒）。0 = 完全跟手；0.03–0.06 能去掉抖动但几乎感觉不到延迟。")]
        [Range(0f, 0.15f)] public float rotationSmoothTime = 0.04f;
        public float minPitch = -25f;
        public float maxPitch = 65f;
        public bool invertY = false;

        [Header("距离")]
        public float distance = 3.8f;
        public float minDistance = 1.25f;
        public float maxDistance = 7f;
        public float zoomSpeed = 2f;

        [Header("平滑 / 碰撞")]
        public float followSmooth = 18f;
        public float collisionRadius = 0.25f;
        public LayerMask collisionMask = ~0;
        [Tooltip("撞墙时拉近的速度（米/秒）；拉远回位用更慢的速度，避免来回弹。")]
        public float collisionPullInSpeed = 30f;
        public float collisionReleaseSpeed = 4f;

        /// <summary>false 时不响应鼠标，但仍跟随目标。</summary>
        public bool InputEnabled { get; set; } = true;

        public float Yaw => _yaw;
        public float Pitch => _pitch;

        private float _yaw;
        private float _pitch = 18f;
        private float _targetYaw;
        private float _targetPitch = 18f;
        private float _yawVelocity, _pitchVelocity;
        private float _currentDistance;
        private Vector3 _smoothedPivot;
        private bool _initialized;

        /// <summary>把相机立刻摆到目标身后（切场景/读档后用）。</summary>
        public void SnapBehind(float yaw)
        {
            _yaw = _targetYaw = yaw;
            _pitch = _targetPitch = 18f;
            _yawVelocity = _pitchVelocity = 0f;
            _currentDistance = distance;
            if (Target != null) _smoothedPivot = Target.position + pivotOffset;
            _initialized = true;
            ApplyTransform(true);
        }

        private void LateUpdate()
        {
            if (Target == null) return;

            if (!_initialized)
            {
                SnapBehind(Target.eulerAngles.y);
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (InputEnabled)
            {
                float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
                float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity * (invertY ? 1f : -1f);
                _targetYaw += mx;
                _targetPitch = Mathf.Clamp(_targetPitch + my, minPitch, maxPitch);

                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.0001f)
                    distance = Mathf.Clamp(distance - scroll * zoomSpeed * 5f, minDistance, maxDistance);
            }

            if (rotationSmoothTime <= 0.0001f)
            {
                _yaw = _targetYaw;
                _pitch = _targetPitch;
            }
            else
            {
                _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVelocity, rotationSmoothTime, Mathf.Infinity, dt);
                _pitch = Mathf.SmoothDamp(_pitch, _targetPitch, ref _pitchVelocity, rotationSmoothTime, Mathf.Infinity, dt);
            }

            Vector3 pivot = Target.position + pivotOffset;
            // 用不受 timeScale 影响的时间，暂停时也能平滑过去
            _smoothedPivot = Vector3.Lerp(_smoothedPivot, pivot, 1f - Mathf.Exp(-followSmooth * dt));

            ApplyTransform(false);
        }

        private void ApplyTransform(bool snap)
        {
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 dir = rot * Vector3.back;
            float desired = distance;

            // 墙体避让：从 pivot 往相机方向球形射线，碰到就拉近
            if (Physics.SphereCast(_smoothedPivot, collisionRadius, dir, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (Target == null || !hit.transform.IsChildOf(Target))
                    desired = Mathf.Max(minDistance * 0.5f, hit.distance - 0.05f);
            }

            if (snap) _currentDistance = desired;
            else
            {
                float speed = desired < _currentDistance ? collisionPullInSpeed : collisionReleaseSpeed;
                _currentDistance = Mathf.MoveTowards(_currentDistance, desired, speed * Time.unscaledDeltaTime);
            }

            transform.position = _smoothedPivot + dir * _currentDistance;
            transform.rotation = rot;
        }
    }
}
