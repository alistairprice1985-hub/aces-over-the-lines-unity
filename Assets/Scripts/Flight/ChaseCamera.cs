using UnityEngine;

namespace AcesOverTheLines.Flight
{
    // External chase camera — trails the player aircraft at a fixed
    // body-frame offset (12 m behind, 3 m above) and looks 5 m ahead.
    // Ports src/core/chaseCamera.js (external mode only).
    //
    // BOTH position and look-at are rigid (no temporal smoothing) by
    // intentional design — see DEFECTS S2-014 in the JS source: smoothing
    // the look-at while position was rigid produced a moving mismatch
    // where the aircraft drifted out of frame during attitude changes.
    // Both rigid keeps the aircraft within ~4° of frame centre at all
    // times. Cockpit mode is deferred until we have proper cockpit
    // geometry.
    [RequireComponent(typeof(Camera))]
    public class ChaseCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float fov = 70f;
        // Body-frame: +x forward, +y up, +z right (JS sim convention).
        // 12 m behind = body -x; 3 m up = body +y.
        [SerializeField] Vector3 externalOffsetBody = new Vector3(-12f, 3f, 0f);
        // Look 5 m ahead of the target along body +x so the camera frames
        // the aircraft slightly low in view.
        [SerializeField] Vector3 lookAheadBody = new Vector3(5f, 0f, 0f);

        Camera _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null) _cam.fieldOfView = fov;
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 tPos = target.position;
            Quaternion tRot = target.rotation;

            // World camera position = target + (body offset rotated to world).
            transform.position = tPos + tRot * externalOffsetBody;

            // Look at a point ahead of the aircraft along body forward.
            Vector3 lookWorld = tPos + tRot * lookAheadBody;
            transform.LookAt(lookWorld);

            if (_cam != null && _cam.fieldOfView != fov) _cam.fieldOfView = fov;
        }
    }
}
