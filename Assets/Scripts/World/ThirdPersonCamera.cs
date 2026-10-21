using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 第三人称环绕相机：鼠标旋转、滚轮缩放、简单的墙体避让。
    /// 挂在 Camera 上，由 WorldController 设置 Target。
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);

        [Header("旋转")]
        public float mouseSensitivity = 3f;
        public float minPitch = -25f;
        public float maxPitch = 70f;
        public bool invertY = false;

        [Header("距离")]
        public float distance = 5f;
        public float minDistance = 1.5f;
        public float maxDistance = 9f;
        public float zoomSpeed = 2f;

        [Header("平滑 / 碰撞")]
        public float followSmooth = 18f;
        public float collisionRadius = 0.25f;
        public LayerMask collisionMask = ~0;

        /// <summary>false 时不响应鼠标，但仍跟随目标。</summary>
        public bool InputEnabled { get; set; } = true;

        public float Yaw => _yaw;
        public float Pitch => _pitch;

        private float _yaw;
        private float _pitch = 18f;
        private Vector3 _smoothedPivot;
        private bool _initialized;

        /// <summary>把相机立刻摆到目标身后（切场景/读档后用）。</summary>
        public void SnapBehind(float yaw)
        {
            _yaw = yaw;
            _pitch = 18f;
            if (Target != null) _smoothedPivot = Target.position + pivotOffset;
            _initialized = true;
            ApplyTransform();
        }

        private void LateUpdate()
        {
            if (Target == null) return;

            if (!_initialized)
            {
                SnapBehind(Target.eulerAngles.y);
                return;
            }

            if (InputEnabled)
            {
                float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
                float my = Input.GetAxis("Mouse Y") * mouseSensitivity * (invertY ? 1f : -1f);
                _yaw += mx;
                _pitch = Mathf.Clamp(_pitch + my, minPitch, maxPitch);

                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.0001f)
                    distance = Mathf.Clamp(distance - scroll * zoomSpeed * 5f, minDistance, maxDistance);
            }

            Vector3 pivot = Target.position + pivotOffset;
            // 用不受 timeScale 影响的时间，暂停时也能平滑过去
            _smoothedPivot = Vector3.Lerp(_smoothedPivot, pivot, 1f - Mathf.Exp(-followSmooth * Time.unscaledDeltaTime));

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 dir = rot * Vector3.back;
            float dist = distance;

            // 墙体避让：从 pivot 往相机方向球形射线，碰到就拉近
            if (Physics.SphereCast(_smoothedPivot, collisionRadius, dir, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (Target == null || !hit.transform.IsChildOf(Target))
                    dist = Mathf.Max(minDistance * 0.5f, hit.distance - 0.05f);
            }

            transform.position = _smoothedPivot + dir * dist;
            transform.rotation = rot;
        }
    }
}
