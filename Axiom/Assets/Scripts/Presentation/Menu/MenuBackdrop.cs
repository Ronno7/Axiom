using UnityEngine;
using UnityEngine.Serialization;

namespace GeometricFlight.Presentation
{
    /// <summary>
    /// The menu glides along the grid floor for real: the view and the constructs around it (this object's children)
    /// fly forward together over the world grid, which never moves. Each time they have covered one repeat of the grid
    /// (its major spacing), they and the dust step back by exactly that distance. The grid is identical there and
    /// everything else moved with them, so the frame doesn't change, and the menu never wanders off its place. PLAY
    /// stops the glide; the entry departs from wherever the view has got to.
    /// </summary>
    [DefaultExecutionOrder(-10)] // Moves the view before the look's dust and the ground follow it.
    [DisallowMultipleComponent]
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("The ground the menu glides over. Its grid's repeat sets the step back.")]
        [SerializeField] private InfiniteGround ground;
        [Tooltip("Its dust stays put in the world, so it steps back with the view.")]
        [SerializeField] private MenuLook look;
        [FormerlySerializedAs("scrollSpeed")]
        [SerializeField, Min(0f)] private float glideSpeed = 5f;

        private bool gliding = true;

        /// <summary>How far the view has glided past where it was placed, less whole repeats of the grid.</summary>
        public float Travelled { get; private set; }

        /// <summary>PLAY: the camera takes the motion over from here.</summary>
        public void StopGlide() => gliding = false;

        /// <summary>Distance travelled after <paramref name="distance"/> more metres, kept within one repeat.</summary>
        public static float Glide(float travelled, float distance, float repeat) =>
            repeat > 0f ? Mathf.Repeat(travelled + distance, repeat) : travelled + distance;

        private void Update()
        {
            if (!gliding || viewCamera == null) return;
            float repeat = ground != null ? ground.RepeatLength : 0f;
            float next = Glide(Travelled, glideSpeed * Time.deltaTime, repeat);
            bool steppedBack = next < Travelled;
            Vector3 move = Vector3.forward * (next - Travelled);
            Travelled = next;
            transform.position += move;
            viewCamera.transform.position += move;
            if (steppedBack && look != null) look.MoveDust(Vector3.back * repeat);
        }
    }
}
