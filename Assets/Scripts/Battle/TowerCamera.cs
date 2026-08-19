using UnityEngine;

namespace Skyscraper.Battle
{
    /// Keeps the whole tower and the ground in frame at once.
    ///
    /// This is not decoration. The legal release band rides on top of the stack,
    /// so once the tower passes a few metres the drop zone leaves a fixed view
    /// entirely and the player is aiming at something off-screen. Panning up
    /// would solve that but hide the base and the enemies attacking it, which
    /// are the other half of the decision -- so the view zooms out instead,
    /// exactly as the reference art frames it: ground, base, full tower, and the
    /// drop band all visible together.
    ///
    /// Attached to the main camera at runtime by BattleRuntime, so the scene
    /// asset needs no extra wiring.
    public class TowerCamera : MonoBehaviour
    {
        [Tooltip("Vertical room kept above the release band.")]
        public float Headroom = 1.5f;
        [Tooltip("World units kept below the ground line.")]
        public float Underfoot = 1f;
        public float Smoothing = 3f;

        BattleRuntime _runtime;
        BrickDropper _dropper;
        Camera _cam;
        float _baseSize, _baseY, _aspect;

        public void Bind(BattleRuntime runtime)
        {
            _runtime = runtime;
            _dropper = runtime != null ? runtime.GetComponent<BrickDropper>() : null;
            _cam = GetComponent<Camera>();
            if (_cam != null) ApplyBaseFraming(true);
        }

        /// The opening framing is derived, not authored. How large a brick looks
        /// is decided entirely by orthographicSize, so leaving that to whatever
        /// the scene happened to be saved with makes the reference match
        /// unreproducible -- RefScale pins it instead: one cell always covers
        /// CellPx of RefWidth. Recomputed on aspect change, because a resized
        /// Game view would otherwise quietly rescale every brick.
        void ApplyBaseFraming(bool snap)
        {
            _aspect = _cam.aspect;
            _baseSize = RefScale.OrthoSize(_aspect);
            float groundY = _runtime != null ? _runtime.GroundY : 0f;
            _baseY = groundY - RefScale.GroundFromBottom + _baseSize;
            if (!snap) return;
            _cam.orthographicSize = _baseSize;
            var p = transform.position;
            transform.position = new Vector3(p.x, _baseY, p.z);
        }

        void LateUpdate()
        {
            if (_cam == null || _runtime == null || !_cam.orthographic) return;
            if (!Mathf.Approximately(_cam.aspect, _aspect)) ApplyBaseFraming(false);

            float bandTop = _dropper != null ? _dropper.DropHeight : _runtime.DropLineY;
            float wantTop = bandTop + Headroom;

            // The bottom edge stays where the reference puts it -- the pedestal
            // sits GroundFromBottom above it, with the terrain band and the HUD
            // filling the rest. Underfoot only matters if a caller shrinks the
            // base framing below that.
            float bottom = Mathf.Min(_runtime.GroundY - Underfoot, _baseY - _baseSize);

            float size = Mathf.Max(_baseSize, (wantTop - bottom) * 0.5f);
            float y = Mathf.Max(_baseY, bottom + size);

            float k = 1f - Mathf.Exp(-Smoothing * Time.deltaTime);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, size, k);
            var p = transform.position;
            transform.position = new Vector3(p.x, Mathf.Lerp(p.y, y, k), p.z);
        }
    }
}
